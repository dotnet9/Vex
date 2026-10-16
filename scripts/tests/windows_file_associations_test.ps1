[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ExecutablePath,
    [Parameter(Mandatory)] [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw '文件关联回归仅支持 Windows。' }
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null

function Read-RegistryTree([string] $Path) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path, $false)
    if ($null -eq $key) { return $null }
    try {
        $values = [ordered]@{}
        foreach ($name in @($key.GetValueNames() | Sort-Object)) {
            $values[$name] = @($key.GetValueKind($name).ToString(), $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames))
        }
        $children = [ordered]@{}
        foreach ($name in @($key.GetSubKeyNames() | Sort-Object)) { $children[$name] = Read-RegistryTree ($Path + '\' + $name) }
        return [ordered]@{ Values = $values; Children = $children }
    }
    finally { $key.Dispose() }
}

function Get-AssociationSnapshot {
    # 只读 Vex 自身候选及三种扩展名的现有选择，不创建或修改任何注册项。
    $snapshot = [ordered]@{}
    foreach ($path in @('Software\Classes\Vex.Markdown', 'Software\Classes\Applications\Vex.exe')) {
        $snapshot[$path] = Read-RegistryTree $path
    }
    foreach ($extension in @('.md', '.markdown', '.mdown')) {
        foreach ($path in @("Software\Classes\$extension", "Software\Classes\SystemFileAssociations\$extension\shell\OpenWithVex", "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension\UserChoice")) {
            $snapshot[$path] = Read-RegistryTree $path
        }
    }
    return ConvertTo-Json -InputObject $snapshot -Depth 30 -Compress
}

$before = Get-AssociationSnapshot
$sourceHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
# 正式原生自检在隔离配置下调用实际 Windows 启动集成入口，不创建窗口。
$verificationProcess = Start-Process -FilePath $executable -ArgumentList @('--verify-runtime', ('"' + $output + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'runtime.stdout.log') -RedirectStandardError (Join-Path $output 'runtime.stderr.log')
try {
    if (-not $verificationProcess.WaitForExit(30000)) {
        $verificationProcess.Kill()
        throw '原生运行自检超过 30 秒，已停止本次自检进程。'
    }
    if ($verificationProcess.ExitCode -ne 0) { throw "原生运行自检失败：$($verificationProcess.ExitCode)" }
}
finally { $verificationProcess.Dispose() }
$after = Get-AssociationSnapshot
if ($before -cne $after) { throw '原生启动集成改变了现有 Markdown 文件关联或默认应用选择。' }
$resultPath = Join-Path $output 'runtime-verification.txt'
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf) -or (Get-Content -LiteralPath $resultPath -Raw) -notmatch 'NativeAOT runtime verification passed') { throw '正式 NativeAOT 自检收据缺失。' }
if ((Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sourceHash) { throw '验证前后正式程序哈希发生变化。' }
$snapshotHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($after))).ToLowerInvariant()
[ordered]@{ Executable = $executable; ExecutableSha256 = $sourceHash; RegistrySnapshotSha256 = $snapshotHash; RegistryUnchanged = $true; DefaultApplicationUnchanged = $true; NativeRuntimePassed = $true; DesktopOpened = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'file-associations-verification.json') -Encoding utf8
Write-Host 'PASS: 正式原生运行、四语与隔离偏好通过，Windows 候选和默认应用完全未变。'
