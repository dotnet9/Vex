# publish_native.ps1
# 作用：发布干净的本机 Native AOT 单文件。默认只打印支持的参数和平台。
# 区别：publish.ps1 会制作安装包、zip 和清单，并允许非本机 RID 退回自包含裁剪。
#       本脚本只接受本机可 Native AOT 编译的 RID，不制作安装包，也不复制文档和调试文件。
# 用法：publish_native.bat
#       publish_native.bat list
#       publish_native.bat win
#       publish_native.bat all -Version 1.10.1
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [Alias('RuntimeIdentifier')]
    [string] $Platform = '',
    [string] $Version = '',
    [Alias('WorkRoot')]
    [string] $ArtifactsRoot = '',
    [string] $PublishRoot = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$releasePlatforms = [ordered]@{
    win = @('win-x64')
    linux = @('linux-x64', 'linux-arm64')
    osx = @('osx-x64', 'osx-arm64')
}

function Get-HostRuntimeIdentifier {
    $isMacHost = $PSVersionTable.PSVersion.Major -ge 6 -and $IsMacOS
    $name = if ($env:OS -eq 'Windows_NT') { 'win' } elseif ($isMacHost) { 'osx' } else { 'linux' }
    $architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    return "$name-$architecture"
}

function Get-ProjectVersion {
    if (-not [string]::IsNullOrWhiteSpace($Version)) { return $Version.Trim().TrimStart('v', 'V') }
    $propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
    [xml] $properties = [IO.File]::ReadAllText($propsPath)
    return [string] $properties.Project.PropertyGroup.Version
}

function Show-NativePublishUsage {
    $hostRuntime = Get-HostRuntimeIdentifier
    $versionText = Get-ProjectVersion
    Write-Output "Vex $versionText native publish"
    Write-Output "Current host: $hostRuntime"
    Write-Output ''
    Write-Output 'Supported platforms:'
    foreach ($name in $releasePlatforms.Keys) {
        $marker = if ($hostRuntime.StartsWith("$name-")) { 'current' } else { 'requires matching OS' }
        Write-Output ('  {0,-7} {1,-24} {2}' -f $name, ($releasePlatforms[$name] -join ', '), $marker)
    }
    Write-Output ''
    Write-Output 'Commands:'
    Write-Output '  publish_native.bat                 Show this help. Nothing is published.'
    Write-Output '  publish_native.bat list            Show this help.'
    Write-Output '  publish_native.bat <platform|RID>  Publish one matching Native AOT payload.'
    Write-Output '  publish_native.bat all             Publish every RID that matches this host.'
    Write-Output ''
    Write-Output 'Options: -Version x.y.z  -ArtifactsRoot <dir>  -PublishRoot <dir>'
    Write-Output 'Output:  artifacts/publish/<RID>/Vex'
    Write-Output 'Payload: Native AOT single file. Unchainable native libraries stay beside the executable.'
    Write-Output 'Excluded: documents, scripts, pdb, xml, and app config.'
}

function Resolve-NativeRuntime {
    param([Parameter(Mandatory)] [string] $Requested)

    $target = $Requested.Trim().ToLowerInvariant()
    $hostRuntime = Get-HostRuntimeIdentifier
    if ($releasePlatforms.Contains($target)) {
        $architecture = $hostRuntime.Split('-')[-1]
        $runtime = "$target-$architecture"
        if ($runtime -notin $releasePlatforms[$target]) {
            throw "Unsupported architecture: $Requested ($architecture). Run without arguments to see supported platforms."
        }
        return $runtime
    }
    foreach ($group in $releasePlatforms.Values) {
        if ($target -in $group) { return $target }
    }
    throw "Unsupported platform: $Requested. Run without arguments to see supported platforms."
}

function Clear-NativePayload {
    param([Parameter(Mandatory)] [string] $Payload)

    if (-not (Test-Path -LiteralPath $Payload -PathType Container)) { return }
    $executableNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$executableNames.Add('Vex.exe')
    [void]$executableNames.Add('Vex')
    $removed = 0
    Get-ChildItem -LiteralPath $Payload -Force | ForEach-Object {
        $remove = $executableNames.Contains($_.Name) -or $_.Extension -in @('.pdb', '.xml', '.ps1', '.py', '.config', '.md', '.txt') -or $_.Name -in @('docs', 'I18n')
        if (-not $remove) { return }
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
        $removed++
    }
    if ($removed -gt 0) { Write-Host "Removed $removed non-runtime item(s) from $Payload" }
}

function Publish-NativeRuntime {
    param(
        [Parameter(Mandatory)] [string] $Rid,
        [Parameter(Mandatory)] [string] $ReleaseVersion
    )

    $hostRuntime = Get-HostRuntimeIdentifier
    if ($Rid -ne $hostRuntime) {
        throw "Native AOT payload $Rid must be published on a matching OS/architecture. This host is $hostRuntime."
    }
    if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
        throw 'Native publish requires the .NET 10 SDK.'
    }

    $payload = if ($PublishRoot) { [IO.Path]::GetFullPath($PublishRoot) } else { Join-Path $repositoryRoot "artifacts/publish/$Rid/Vex" }
    $arguments = @(
        'publish', (Join-Path $repositoryRoot 'src/Vex/Vex.csproj'),
        '-c', 'Release', '-f', $(if ($Rid.StartsWith('win-')) { 'net10.0-windows' } else { 'net10.0' }),
        '-r', $Rid, '-m:1',
        '-p:PublishAot=true', '-p:PublishTrimmed=true', '-p:PublishSingleFile=true',
        '-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:IncludeAllContentForSelfExtract=true', '-p:DebugSymbols=false', '-p:DebugType=none',
        '-p:CopyOutputSymbolsToPublishDirectory=false', '-p:VexExcludePublishDocuments=true',
        '-p:IlcGenerateCompleteTypeMetadata=true', '-p:IlcTrimMetadata=false',
        '-p:IlcSingleThreaded=true', '-p:IlcUseServerGc=false',
        "-p:Version=$ReleaseVersion", "-p:PublishDir=$payload/"
    )
    if ($ArtifactsRoot) { $arguments += "-p:ArtifactsPath=$([IO.Path]::GetFullPath($ArtifactsRoot))" }
    if ($Rid.StartsWith('osx-')) { $arguments += '-p:StripSymbols=false' }

    Write-Host "Native AOT publish: $Rid (Version=$ReleaseVersion)"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Native AOT publish failed: $Rid" }
    Clear-NativePayload -Payload $payload
    $executable = Join-Path $payload $(if ($Rid.StartsWith('win-')) { 'Vex.exe' } else { 'Vex' })
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Native executable is missing: $executable" }
    Write-Host "Native payload: $payload"
}

$requested = $Platform.Trim().ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($requested) -or $requested -eq 'list') {
    Show-NativePublishUsage
    return
}

$releaseVersion = Get-ProjectVersion
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw "Version must be three or four numeric parts: $releaseVersion" }
if ($requested -eq 'all') {
    if ($PublishRoot) { throw '-PublishRoot selects one output directory and cannot be combined with all.' }
    $hostRuntime = Get-HostRuntimeIdentifier
    $published = 0
    foreach ($group in $releasePlatforms.Values) {
        foreach ($rid in $group) {
            if ($rid -ne $hostRuntime) { continue }
            Publish-NativeRuntime -Rid $rid -ReleaseVersion $releaseVersion
            $published++
        }
    }
    if ($published -eq 0) { throw "This host has no supported Native AOT RID: $hostRuntime" }
    Write-Host "Published $published Native AOT payload(s)."
    return
}

Publish-NativeRuntime -Rid (Resolve-NativeRuntime -Requested $Platform) -ReleaseVersion $releaseVersion
