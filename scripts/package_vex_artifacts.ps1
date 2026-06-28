[CmdletBinding()]
param(
    [string[]]$RuntimeIdentifier = @(
        "win-x64",
        "win-x86",
        "linux-x64",
        "linux-arm64",
        "osx-x64",
        "osx-arm64"
    ),
    [string]$Version = "",
    [string]$PublishRoot = "",
    [string]$ArtifactsRoot = "",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ReleaseVersion([string]$InputVersion) {
    if ($InputVersion -match "^[vV]") {
        return $InputVersion
    }

    return "v$InputVersion"
}

function Test-ReleaseFile([System.IO.FileInfo]$File) {
    return $File.Extension -ine ".pdb"
}

function Get-Sha256Hex([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha256.ComputeHash($stream)
            return -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function New-ReleaseArchive(
    [System.IO.FileInfo[]]$Entries,
    [string]$SourceRoot,
    [string]$DestinationPath
) {
    if (Test-Path -LiteralPath $DestinationPath -PathType Leaf) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $resolvedRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
    $rootPrefix = $resolvedRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    ) + [System.IO.Path]::DirectorySeparatorChar

    $archive = [System.IO.Compression.ZipFile]::Open(
        $DestinationPath,
        [System.IO.Compression.ZipArchiveMode]::Create
    )

    try {
        foreach ($entry in $Entries) {
            $entryPath = $entry.FullName
            if (-not $entryPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to package '$entryPath' because it is outside '$resolvedRoot'."
            }

            $relativePath = $entryPath.Substring($rootPrefix.Length)
            $relativePath = $relativePath.Replace([System.IO.Path]::DirectorySeparatorChar, "/")
            $relativePath = $relativePath.Replace([System.IO.Path]::AltDirectorySeparatorChar, "/")

            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $entryPath,
                $relativePath,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

$RuntimeIdentifier = @(
    foreach ($rid in $RuntimeIdentifier) {
        foreach ($part in ($rid -split ",")) {
            $normalized = $part.Trim()
            if ($normalized.Length -gt 0) {
                $normalized
            }
        }
    }
)

if ($RuntimeIdentifier.Count -eq 0) {
    throw "At least one runtime identifier is required."
}

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $buildPropsPath = Join-Path $repoRoot "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $buildPropsPath -PathType Leaf)) {
        throw "Directory.Build.props was not found. Pass -Version explicitly."
    }

    [xml]$buildProps = Get-Content -Raw -Encoding UTF8 -LiteralPath $buildPropsPath
    $versionNode = $buildProps.Project.PropertyGroup |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.Version) } |
        Select-Object -First 1

    if ($null -eq $versionNode) {
        throw "Directory.Build.props does not define Version. Pass -Version explicitly."
    }

    $Version = [string]$versionNode.Version
}

$releaseVersion = Get-ReleaseVersion $Version

if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
    $PublishRoot = Join-Path $repoRoot "publish"
}

if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $repoRoot "artifacts\release"
}

New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null

$manifestPath = Join-Path $ArtifactsRoot "Vex-$releaseVersion-release-manifest.json"
if ((Test-Path -LiteralPath $manifestPath -PathType Leaf) -and -not $Force) {
    throw "Release manifest already exists: '$manifestPath'. Pass -Force to replace it."
}

$plans = New-Object System.Collections.Generic.List[object]
foreach ($rid in $RuntimeIdentifier) {
    if ([string]::IsNullOrWhiteSpace($rid)) {
        throw "Runtime identifier cannot be empty."
    }

    $publishDir = Join-Path $PublishRoot $rid
    if (-not (Test-Path -LiteralPath $publishDir -PathType Container)) {
        throw "Publish directory '$publishDir' was not found. Run publish_all.bat first or pass -PublishRoot."
    }

    $allEntries = @(Get-ChildItem -LiteralPath $publishDir -Recurse -File)
    $entries = @($allEntries | Where-Object { Test-ReleaseFile $_ })
    $excludedEntries = @($allEntries | Where-Object { -not (Test-ReleaseFile $_) })
    if ($entries.Count -eq 0) {
        throw "Publish directory '$publishDir' does not contain release files after exclusions."
    }

    $archiveName = "Vex-$releaseVersion-$rid.zip"
    $archivePath = Join-Path $ArtifactsRoot $archiveName
    $checksumPath = "$archivePath.sha256"

    $existingOutputs = @(
        @($archivePath, $checksumPath) |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )

    if ($existingOutputs.Count -gt 0 -and -not $Force) {
        $existingList = $existingOutputs -join "', '"
        throw "Artifact output already exists: '$existingList'. Pass -Force to replace it."
    }

    $uncompressedBytes = ($entries | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $uncompressedBytes) {
        $uncompressedBytes = 0
    }

    $plans.Add([pscustomobject]@{
        RuntimeIdentifier = $rid
        PublishDir = $publishDir
        Entries = $entries
        ArchiveName = $archiveName
        ArchivePath = $archivePath
        ChecksumPath = $checksumPath
        UncompressedBytes = [int64]$uncompressedBytes
        ExcludedFileCount = $excludedEntries.Count
    }) | Out-Null
}

$packagedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$packages = New-Object System.Collections.Generic.List[object]

foreach ($plan in $plans) {
    New-ReleaseArchive `
        -Entries $plan.Entries `
        -SourceRoot $plan.PublishDir `
        -DestinationPath $plan.ArchivePath

    $sha256 = Get-Sha256Hex $plan.ArchivePath
    Set-Content -Encoding ASCII -LiteralPath $plan.ChecksumPath -Value "$sha256  $($plan.ArchiveName)"

    $packages.Add([ordered]@{
        product = "Vex"
        version = $Version
        releaseVersion = $releaseVersion
        runtimeIdentifier = $plan.RuntimeIdentifier
        archive = $plan.ArchiveName
        sha256 = $sha256
        fileCount = $plan.Entries.Count
        excludedFileCount = $plan.ExcludedFileCount
        uncompressedBytes = $plan.UncompressedBytes
    }) | Out-Null

    Write-Host "Packaged $($plan.ArchiveName) (excluded $($plan.ExcludedFileCount) debug symbol files)"
}

$manifest = [ordered]@{
    product = "Vex"
    version = $Version
    releaseVersion = $releaseVersion
    packagedAt = $packagedAt
    packages = $packages
}

$manifest |
    ConvertTo-Json -Depth 6 |
    Set-Content -Encoding UTF8 -LiteralPath $manifestPath

Write-Host "Wrote release manifest $manifestPath"
