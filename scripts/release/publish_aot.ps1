# Publish Native AOT payloads and versioned ZIP archives into publish/exe and publish/zip.
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [Alias('RuntimeIdentifier')]
    [string] $Platform = '',
    [Parameter(Position = 1)]
    [string] $Scope = '',
    [string] $Version = '',
    [string] $PublishRoot = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$nativePublisher = Join-Path $PSScriptRoot 'publish_native.ps1'
$releaseRoot = if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
    Join-Path $repositoryRoot 'publish'
}
else {
    [IO.Path]::GetFullPath($PublishRoot)
}

$releasePlatforms = [ordered]@{
    win = @('win-x64')
    linux = @('linux-x64', 'linux-arm64')
    osx = @('osx-x64', 'osx-arm64')
}

function Get-HostRuntimeIdentifier {
    $platformName = if ($env:OS -eq 'Windows_NT') { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
    $architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    return "$platformName-$architecture"
}

function Get-ReleaseVersion {
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        return $Version.Trim().TrimStart('v', 'V')
    }

    [xml] $props = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'Directory.Build.props'))
    return [string] $props.Project.PropertyGroup.Version
}

function Show-Platforms {
    param([switch] $ShowAll)

    $releaseVersion = Get-ReleaseVersion
    $hostRuntime = Get-HostRuntimeIdentifier
    Write-Output "Vex $releaseVersion Native AOT release"
    Write-Output "Current host: $hostRuntime"
    Write-Output ''
    if ($ShowAll) {
        Write-Output 'Supported platform codes:'
        foreach ($name in $releasePlatforms.Keys) {
            foreach ($rid in $releasePlatforms[$name]) {
                Write-Output ('  {0,-12} {1}' -f $rid, $name)
            }
        }
    }
    else {
        Write-Output 'Supported platforms:'
        foreach ($name in $releasePlatforms.Keys) {
            Write-Output ('  {0,-7} {1}' -f $name, ($releasePlatforms[$name] -join ', '))
        }
        Write-Output '  all     Publish the RID matching this host.'
    }

    Write-Output ''
    Write-Output 'Commands:'
    Write-Output '  publish_aot.bat                         Show supported platforms. No files are created.'
    Write-Output '  publish_aot.bat list                     Show supported platforms.'
    Write-Output '  publish_aot.bat list all                 Show every supported RID code.'
    Write-Output '  publish_aot.bat <win|linux|osx|RID|all>  Publish Native AOT payload and ZIP.'
    Write-Output ''
    Write-Output 'Options: -Version x.y.z  -PublishRoot <dir>'
    Write-Output 'Output:  <PublishRoot>/exe/<RID>/Vex and <PublishRoot>/zip/Vex-<version>-<RID>.zip'
    Write-Output 'Native AOT can only be compiled on a matching OS and architecture.'
}

function Resolve-RequestedRuntimes {
    param([Parameter(Mandatory)] [string] $Requested)

    $target = $Requested.Trim().ToLowerInvariant()
    $hostRuntime = Get-HostRuntimeIdentifier
    $rids = @($releasePlatforms.Values | ForEach-Object { $_ })

    if ($target -eq 'all') {
        $matches = @($rids | Where-Object { $_ -eq $hostRuntime })
        if ($matches.Count -eq 0) {
            throw "This host has no supported Native AOT RID: $hostRuntime. Run 'publish_aot.bat list all' to see supported codes."
        }
        return $matches
    }

    if ($releasePlatforms.Contains($target)) {
        $architecture = $hostRuntime.Split('-')[-1]
        $runtime = "$target-$architecture"
        if ($runtime -notin $releasePlatforms[$target]) {
            throw "Unsupported architecture: $runtime. Run 'publish_aot.bat list all' to see supported codes."
        }
        $target = $runtime
    }

    if ($target -notin $rids) {
        throw "Unsupported platform: $Requested. Run 'publish_aot.bat list all' to see supported codes."
    }
    if ($target -ne $hostRuntime) {
        throw "Native AOT RID $target must be published on a matching host. Current host: $hostRuntime."
    }
    return @($target)
}

function Publish-AotRuntime {
    param(
        [Parameter(Mandatory)] [string] $Rid,
        [Parameter(Mandatory)] [string] $ReleaseVersion
    )

    $exeRoot = Join-Path $releaseRoot 'exe'
    $zipRoot = Join-Path $releaseRoot 'zip'
    $ridRoot = Join-Path $exeRoot $Rid
    $payloadRoot = Join-Path $ridRoot 'Vex'
    $zipPath = Join-Path $zipRoot "Vex-$ReleaseVersion-$Rid.zip"
    $nativeParameters = @{
        Platform = $Rid
        Version = $ReleaseVersion
        PublishRoot = $payloadRoot
    }

    New-Item -ItemType Directory -Path $exeRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $zipRoot -Force | Out-Null

    Write-Host "Publishing Native AOT: $Rid (Version=$ReleaseVersion)"
    & $nativePublisher @nativeParameters
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Native AOT publish failed: $Rid" }

    if (-not (Test-Path -LiteralPath $payloadRoot -PathType Container)) {
        throw "Native payload directory is missing: $payloadRoot"
    }
    $executable = Join-Path $payloadRoot $(if ($Rid.StartsWith('win-')) { 'Vex.exe' } else { 'Vex' })
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Native executable is missing: $executable"
    }

    if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    [IO.Compression.ZipFile]::CreateFromDirectory($ridRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host "Executable payload: $payloadRoot"
    Write-Host "Versioned archive: $zipPath"
}

$requested = $Platform.Trim().ToLowerInvariant()
$scopeValue = $Scope.Trim().ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($requested) -or $requested -eq 'list') {
    Show-Platforms -ShowAll:($scopeValue -eq 'all')
    return
}
if (-not [string]::IsNullOrWhiteSpace($scopeValue)) {
    throw "Unexpected second argument: $Scope. Use 'list all' to list all platform codes."
}

$releaseVersion = Get-ReleaseVersion
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "Version must be three or four numeric parts: $releaseVersion"
}
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'Native AOT release requires the .NET 10 SDK.'
}

foreach ($rid in (Resolve-RequestedRuntimes -Requested $requested)) {
    Publish-AotRuntime -Rid $rid -ReleaseVersion $releaseVersion
}
