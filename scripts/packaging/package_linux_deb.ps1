[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("linux-x64", "linux-arm64")]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $SourceDirectory = "",
    [string] $OutputDirectory = "",
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$scriptRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repositoryRoot "artifacts/publish/$RuntimeIdentifier/Vex"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/release"
}

$sourcePath = (Resolve-Path -LiteralPath $SourceDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$cleanVersion = $Version.Trim().TrimStart('v', 'V').Split('+')[0]
if ($cleanVersion -notmatch '^\d+(\.\d+){1,3}$') {
    throw "Version must be numeric (for example 1.1.2): $Version"
}

$architecture = if ($RuntimeIdentifier -eq "linux-arm64") { "arm64" } else { "amd64" }
$packageName = "Vex-v$cleanVersion-$RuntimeIdentifier.deb"
$packagePath = Join-Path $outputPath $packageName
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Vex-deb-" + [Guid]::NewGuid().ToString("N"))
$appPath = Join-Path $stageRoot "usr/lib/vex"
$binPath = Join-Path $stageRoot "usr/bin"
$desktopPath = Join-Path $stageRoot "usr/share/applications"
$iconPath = Join-Path $stageRoot "usr/share/icons/hicolor/256x256/apps"
$controlPath = Join-Path $stageRoot "DEBIAN"

if ((Test-Path -LiteralPath $packagePath) -and -not $Force) {
    throw "Artifact already exists: $packagePath (use -Force to overwrite)."
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $appPath, $binPath, $desktopPath, $iconPath, $controlPath -Force | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "logo.png") -Destination (Join-Path $iconPath "vex.png")
    # 调试符号不进入安装器，保留宿主语言资源和运行依赖。
    Get-ChildItem -LiteralPath $sourcePath -Recurse -File -Force | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($sourcePath, $_.FullName)
        if ($_.Extension -in '.pdb', '.dbg' -or $relative -match '(^|[\\/])[^\\/]+\.dSYM([\\/]|$)') { return }
        $destination = Join-Path $appPath $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }

    $executable = Join-Path $appPath "Vex"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Published executable was not produced: $executable"
    }
    if ($IsLinux) {
        & chmod 755 $executable
        if ($LASTEXITCODE -ne 0) { throw '无法设置程序启动权限。' }
    }

    [IO.File]::WriteAllText((Join-Path $binPath 'vex'), "#!/bin/sh`n" + 'exec /usr/lib/vex/Vex "$@"' + "`n")
    if ($IsLinux) {
        & chmod 755 (Join-Path $binPath 'vex')
        if ($LASTEXITCODE -ne 0) { throw '无法设置启动入口权限。' }
    }

    @(
        "[Desktop Entry]",
        "Type=Application",
        "Name=Vex",
        "Comment=Vex Markdown editor",
        "Exec=vex %f",
        "Icon=vex",
        "MimeType=text/markdown;text/x-markdown;",
        "Terminal=false",
        "Categories=Office;TextEditor;"
    ) | Set-Content -LiteralPath (Join-Path $desktopPath "vex.desktop") -Encoding ascii

    $installedSize = [Math]::Ceiling(((Get-ChildItem -LiteralPath (Join-Path $stageRoot 'usr') -Recurse -File | Measure-Object Length -Sum).Sum) / 1KB)
    @(
        "Package: vex",
        "Version: $cleanVersion",
        "Section: text",
        "Priority: optional",
        "Architecture: $architecture",
        "Installed-Size: $installedSize",
        "Maintainer: Dotnet9 <1012434131@qq.com>",
        "Depends: libc6, libx11-6, libxrandr2, libxrender1, libxi6, libfontconfig1, libfreetype6, libglib2.0-0",
        "Description: Vex Markdown editor",
        " A cross-platform Markdown editor desktop application built with Avalonia."
    ) | Set-Content -LiteralPath (Join-Path $controlPath "control") -Encoding ascii

    if ($IsLinux) {
        & chmod 644 (Join-Path $controlPath 'control')
        if ($LASTEXITCODE -ne 0) { throw '无法设置 DEB 元数据权限。' }
    }
    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }
    # 补齐独立安装包的图标、品牌和软件管理器元数据。
    & (Join-Path $scriptRoot 'installer/linux/Write-LinuxPackageMetadata.ps1') -StagingDirectory $stageRoot -RepositoryRoot $repositoryRoot -Version $cleanVersion
    & dpkg-deb --build --root-owner-group $stageRoot $packagePath
    if ($LASTEXITCODE -ne 0) {
        throw "dpkg-deb failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "安装包未生成：$packagePath" }
    $sha = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$sha  $packageName" | Set-Content -LiteralPath "$packagePath.sha256" -Encoding ascii
    Write-Host "Package: $packagePath"
}
finally {
    if (Test-Path -LiteralPath $stageRoot) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
