[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [Alias('RuntimeIdentifier')]
    [string] $Platform = 'list',
    [string] $Version = '',
    [string] $WorkRoot = '',
    [string] $OutputDirectory = '',
    [string] $InnoSetupCompiler = ''
)

$ErrorActionPreference = 'Stop'

function Update-ProjectVersion {
    param(
        [Parameter(Mandatory)] [string] $PropsPath,
        [string] $RequestedVersion
    )

    $bytes = [IO.File]::ReadAllBytes($PropsPath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $text = [Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    $match = [regex]::Match($text, '<Version>(?<version>\d+\.\d+\.\d+(?:\.\d+)?)</Version>')
    if (-not $match.Success) { throw "Directory.Build.props does not contain a numeric Version." }
    $current = $match.Groups['version'].Value
    $requested = $RequestedVersion.Trim().TrimStart('v', 'V')
    if ([string]::IsNullOrWhiteSpace($requested)) {
        $parts = $current.Split('.')
        $parts[-1] = [string]([int] $parts[-1] + 1)
        $next = $parts -join '.'
    }
    else {
        if ($requested -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
            throw "Version must be three or four numeric parts: $RequestedVersion"
        }
        $next = $requested
    }
    if ($next -eq $current) { throw "Version $current is already current. Omit -Version to bump it, or pass a different version." }

    $updated = [regex]::Replace($text, '<Version>[^<]+</Version>', "<Version>$next</Version>", 1)
    $encoding = [Text.UTF8Encoding]::new($hasBom)
    [IO.File]::WriteAllText($PropsPath, $updated, $encoding)
    Write-Host "Version updated: $current -> $next"
    return $next
}

function Find-PowerShell7 {
    $command = Get-Command pwsh -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    $candidates = @(
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'PowerShell\7\pwsh.exe' }),
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'PowerShell\7\pwsh.exe' }),
        $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe' })
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }
    return @($candidates)[0]
}

function Assert-PowerShell7 {
    if ($PSVersionTable.PSVersion.Major -ge 7) { return }
    $pwsh = Find-PowerShell7
    if (-not $pwsh) { throw 'Publishing requires PowerShell 7 (pwsh). Listing platforms works in Windows PowerShell.' }
    # Windows 应用执行别名不在 cmd 的 PATH 里，where 找不到，但文件可以直接启动。
    $relaunch = @('-NoLogo', '-NoProfile', '-File', $PSCommandPath, '-Platform', $Platform)
    if ($Version) { $relaunch += @('-Version', $Version) }
    if ($WorkRoot) { $relaunch += @('-WorkRoot', $WorkRoot) }
    if ($OutputDirectory) { $relaunch += @('-OutputDirectory', $OutputDirectory) }
    if ($InnoSetupCompiler) { $relaunch += @('-InnoSetupCompiler', $InnoSetupCompiler) }
    & $pwsh @relaunch
    exit $(if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE })
}

$platforms = [ordered]@{
    win = @('win-x64')
    linux = @('linux-x64', 'linux-arm64')
    osx = @('osx-x64', 'osx-arm64')
}
[xml] $props = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Directory.Build.props'))
$displayVersion = if ($Version) { $Version } else { [string] $props.Project.PropertyGroup.Version }
$target = $Platform.Trim().ToLowerInvariant()
if ($target -eq 'list') {
    Write-Output "Vex $displayVersion - available release platforms:"
    foreach ($name in $platforms.Keys) {
        Write-Output ('  {0,-7} {1}' -f $name, ($platforms[$name] -join ', '))
    }
    Write-Output ''
    Write-Output 'Usage: publish_all.bat [list|all|Win|Linux|osx|RID] [options]'
    Write-Output '       pwsh ./publish.ps1 [list|all|Win|Linux|osx|RID] [-Version x.y.z] [options]'
    Write-Output 'Platform names select the current OS architecture; an exact RID selects one release.'
    Write-Output 'all publishes every release RID, then writes one zip and checksum for each payload.'
    Write-Output 'A release updates Directory.Build.props before packaging. Omit -Version to bump the last number.'
    Write-Output 'NativeAOT releases require the matching OS/architecture and PowerShell 7.'
    Write-Output 'Windows: EXE installer; Linux: DEB; macOS: DMG. No arguments only lists platforms.'
    return
}

if ($target -eq 'all') {
    Assert-PowerShell7
    if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) { throw 'Publishing requires the .NET 10 SDK.' }
    $propsPath = Join-Path $PSScriptRoot 'Directory.Build.props'
    $Version = Update-ProjectVersion -PropsPath $propsPath -RequestedVersion $Version
    $publishParameters = @{ RuntimeIdentifier = 'all'; Version = $Version }
    if ($WorkRoot) { $publishParameters.ArtifactsRoot = [IO.Path]::GetFullPath($WorkRoot) }
    if ($OutputDirectory) { $publishParameters.OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory) }
    & (Join-Path $PSScriptRoot 'scripts/publish.ps1') @publishParameters
    return
}

$hostPlatform = if ($env:OS -eq 'Windows_NT') { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
$architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$runtimeIdentifier = if ($platforms.Contains($target)) { "$target-$architecture" } else { $target }
$targetPlatform = @($platforms.Keys | Where-Object { $runtimeIdentifier -in $platforms[$_] })
if ($targetPlatform.Count -ne 1) {
    throw "Unsupported platform or architecture: $Platform ($architecture). Run with 'list' for supported RIDs."
}
if ($targetPlatform[0] -ne $hostPlatform -or $runtimeIdentifier -ne "$hostPlatform-$architecture") {
    throw "NativeAOT release $runtimeIdentifier must run on a matching OS/architecture. This host is $hostPlatform-$architecture."
}
Assert-PowerShell7
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) { throw 'Publishing requires the .NET 10 SDK.' }
if ($hostPlatform -ne 'win' -and $InnoSetupCompiler) { throw '-InnoSetupCompiler is only valid for Windows.' }

$propsPath = Join-Path $PSScriptRoot 'Directory.Build.props'
$Version = Update-ProjectVersion -PropsPath $propsPath -RequestedVersion $Version
$packageParameters = @{ Version = $Version }
if ($WorkRoot) { $packageParameters.WorkRoot = [IO.Path]::GetFullPath($WorkRoot) }
if ($OutputDirectory) { $packageParameters.OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory) }
if ($hostPlatform -eq 'win') {
    if ($InnoSetupCompiler) { $packageParameters.InnoSetupCompiler = $InnoSetupCompiler }
    & (Join-Path $PSScriptRoot 'scripts/package_windows.ps1') @packageParameters
    return
}

# Publish and package the same RID; retain the existing native packaging recipes.
if (-not $WorkRoot) { $WorkRoot = Join-Path $PSScriptRoot '../../temp_files/vex-release' }
$artifactsRoot = Join-Path ([IO.Path]::GetFullPath($WorkRoot)) $runtimeIdentifier
& (Join-Path $PSScriptRoot 'scripts/publish.ps1') -RuntimeIdentifier $runtimeIdentifier -Version $Version -ArtifactsRoot $artifactsRoot
if ($hostPlatform -eq 'linux') {
    $packageParameters.Remove('WorkRoot')
    & (Join-Path $PSScriptRoot 'scripts/package_linux_deb.ps1') -RuntimeIdentifier $runtimeIdentifier @packageParameters -Force
}
else {
    $previousArtifactsRoot = $env:ARTIFACTS_ROOT
    $previousPublishRoot = $env:PUBLISH_ROOT
    try {
        $env:ARTIFACTS_ROOT = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'artifacts/release' }
        $env:PUBLISH_ROOT = Join-Path $PSScriptRoot 'artifacts/publish'
        & bash (Join-Path $PSScriptRoot 'scripts/package_macos.sh') $runtimeIdentifier
        if ($LASTEXITCODE -ne 0) { throw "macOS packaging failed: $LASTEXITCODE" }
    }
    finally {
        $env:ARTIFACTS_ROOT = $previousArtifactsRoot
        $env:PUBLISH_ROOT = $previousPublishRoot
    }
}
