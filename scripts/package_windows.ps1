[CmdletBinding()]
param(
    [string] $Version = '',
    [string] $WorkRoot = '',
    [string] $OutputDirectory = '',
    [string] $InnoSetupCompiler = ''
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'win-x64 安装包必须在 Windows 上制作。' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $Version) {
    [xml] $props = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
    $Version = [string] $props.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw '安装包版本必须是三段或四段数字。' }
if (-not $WorkRoot) { $WorkRoot = Join-Path $repositoryRoot '../../temp_files/vex-windows' }
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'artifacts/release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

# 先检查打包工具，避免完成耗时 AOT 编译后才发现无法生成安装包。
if (-not $InnoSetupCompiler) {
    $command = Get-Command ISCC.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { $InnoSetupCompiler = $command.Source }
    else {
        $candidates = @(
            if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe' }
            if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe' }
        )
        $registryRoots = @(
            'HKLM:/SOFTWARE/Microsoft/Windows/CurrentVersion/Uninstall',
            'HKLM:/SOFTWARE/WOW6432Node/Microsoft/Windows/CurrentVersion/Uninstall',
            'HKCU:/SOFTWARE/Microsoft/Windows/CurrentVersion/Uninstall'
        )
        $installations = Get-ChildItem -LiteralPath $registryRoots -ErrorAction SilentlyContinue |
            Get-ItemProperty -ErrorAction SilentlyContinue | Where-Object DisplayName -Like 'Inno Setup*'
        foreach ($installation in $installations) {
            if ($installation.InstallLocation) { $candidates += Join-Path $installation.InstallLocation 'ISCC.exe' }
        }
        $InnoSetupCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}
if (-not $InnoSetupCompiler -or -not (Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) {
    throw '需要 Inno Setup 6。请安装后重试，或传入 -InnoSetupCompiler <ISCC.exe 路径>。'
}
$InnoSetupCompiler = (Resolve-Path -LiteralPath $InnoSetupCompiler).Path
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) { throw '需要 .NET 10 SDK。' }

# 每次使用独立构建目录，手工改代码后重做不会混入上次发布遗留的文件。
$runRoot = Join-Path $WorkRoot ('builds/' + [guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $repositoryRoot 'artifacts/publish/win-x64/Vex'
$artifactsRoot = Join-Path $runRoot 'artifacts'
# 只清理固定发布目录，确保手工重新制作时不会混入旧版本载荷。
if (Test-Path -LiteralPath $publishRoot) {
    $resolvedPublishRoot = (Resolve-Path -LiteralPath $publishRoot).Path
    $expectedPublishRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/publish/win-x64/Vex'))
    if ($resolvedPublishRoot -ne $expectedPublishRoot) { throw '发布目录解析结果与仓库固定路径不一致。' }
    Remove-Item -LiteralPath $resolvedPublishRoot -Recurse -Force
}
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$previousProcessorCount = $env:DOTNET_PROCESSOR_COUNT
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
try {
    $env:DOTNET_PROCESSOR_COUNT = '4'
    $temporaryDirectory = Join-Path $runRoot 'tmp'
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $env:TEMP = $temporaryDirectory
    $env:TMP = $temporaryDirectory
    & (Join-Path $PSScriptRoot 'publish.ps1') -RuntimeIdentifier win-x64 -Version $Version -ArtifactsRoot $artifactsRoot -PublishRoot $publishRoot
    $executable = Join-Path $publishRoot 'Vex.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw '发布目录缺少 Vex.exe。' }

    # 正式载荷自检使用隔离配置，不安装产品，也不修改用户现有设置。
    $verificationRoot = Join-Path $runRoot 'runtime-verification'
    $start = [Diagnostics.ProcessStartInfo]::new($executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $publishRoot
    $start.ArgumentList.Add('--verify-runtime')
    $start.ArgumentList.Add($verificationRoot)
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(60000)) {
            $process.Kill()
            throw 'NativeAOT 运行时自检超时。'
        }
        if ($process.ExitCode -ne 0) { throw "NativeAOT 运行时自检失败：$($process.ExitCode)" }
    }
    finally { $process.Dispose() }
    $evidence = Get-Content -LiteralPath (Join-Path $verificationRoot 'runtime-verification.txt') -Raw
    if ($evidence -notmatch '^NativeAOT runtime verification passed;') { throw '缺少 NativeAOT 自检成功记录。' }
    Write-Host $evidence

    & $InnoSetupCompiler '/Q' (Join-Path $PSScriptRoot 'Vex.iss') "/DAppVersion=$Version" "/DSourceDir=$publishRoot" "/DOutputDir=$OutputDirectory"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup 编译失败：$LASTEXITCODE" }
    $installerName = "Vex-v$Version-win-x64-setup.exe"
    $installer = Join-Path $OutputDirectory $installerName
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf) -or (Get-Item -LiteralPath $installer).Length -eq 0) { throw '未生成非空 Windows 安装包。' }
    $sha = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$sha  $installerName" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii
    [ordered]@{
        version = $Version; executable = $executable; installer = $installer; sha256 = $sha
        runtimeVerification = $evidence.Trim(); builtAt = [DateTimeOffset]::Now.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'build-result.json') -Encoding utf8
    Write-Host "Windows 安装包制作成功：$installer"
    Write-Host "SHA-256：$sha"
    Write-Host "构建记录：$runRoot"
}
finally {
    $env:DOTNET_PROCESSOR_COUNT = $previousProcessorCount
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
