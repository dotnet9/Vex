[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "",
    [string]$PublishRoot = "",
    [string]$ArtifactsRoot = "",
    [string]$IdentityName = "CodeWF.Vex",
    [string]$Publisher = "CN=CodeWF",
    [string]$PublisherDisplayName = "CodeWF",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [switch]$PrepareOnly,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Get-BuildVersion([string]$RepoRoot) {
    $buildPropsPath = Join-Path $RepoRoot "Directory.Build.props"
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

    return [string]$versionNode.Version
}

function ConvertTo-MsixVersion([string]$InputVersion) {
    $coreVersion = ($InputVersion -split "[-+]")[0]
    $parts = @($coreVersion -split "\.")
    if ($parts.Count -gt 4) {
        throw "MSIX version '$InputVersion' has more than four numeric parts."
    }

    while ($parts.Count -lt 4) {
        $parts += "0"
    }

    foreach ($part in $parts) {
        $number = 0
        if (-not [int]::TryParse($part, [ref]$number) -or $number -lt 0 -or $number -gt 65535) {
            throw "MSIX version '$InputVersion' contains an invalid part '$part'."
        }
    }

    return ($parts -join ".")
}

function Get-MsixArchitecture([string]$Rid) {
    switch -Regex ($Rid) {
        "win-x64$" { return "x64" }
        "win-x86$" { return "x86" }
        "win-arm64$" { return "arm64" }
        default { throw "MSIX packaging supports Windows runtime identifiers only. Actual: '$Rid'." }
    }
}

function Find-Tool([string]$CommandName, [string]$WindowsKitPattern) {
    $command = Get-Command $CommandName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $candidates = @(Get-ChildItem -LiteralPath (Join-Path $programFilesX86 "Windows Kits\10\bin") -Filter $CommandName -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like $WindowsKitPattern } |
            Sort-Object FullName -Descending)

        if ($candidates.Count -gt 0) {
            return $candidates[0].FullName
        }
    }

    return $null
}

function Remove-DirectoryUnderRoot([string]$Path, [string]$Root) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $rootPrefix = $resolvedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove '$resolvedPath' because it is outside '$resolvedRoot'."
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function ConvertTo-XmlText([string]$Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}

function Copy-PublishContents([string]$Source, [string]$Destination) {
    $items = @(Get-ChildItem -LiteralPath $Source -Force)
    if ($items.Count -eq 0) {
        throw "Publish directory '$Source' does not contain files."
    }

    foreach ($item in $items) {
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function Ensure-MsixAssets([string]$LayoutRoot, [string]$RepoRoot) {
    $assetsDir = Join-Path $LayoutRoot "Assets"
    $logoPath = Join-Path $assetsDir "logo.png"
    if (Test-Path -LiteralPath $logoPath -PathType Leaf) {
        return
    }

    $sourceLogoPath = Join-Path $RepoRoot "src\Vex\Assets\logo.png"
    if (-not (Test-Path -LiteralPath $sourceLogoPath -PathType Leaf)) {
        throw "MSIX logo asset was not found in the publish output or source tree."
    }

    New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
    Copy-Item -LiteralPath $sourceLogoPath -Destination $logoPath -Force
}

$repoRoot = Get-RepoRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-BuildVersion $repoRoot
}

$msixVersion = ConvertTo-MsixVersion $Version
$architecture = Get-MsixArchitecture $RuntimeIdentifier

if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
    $PublishRoot = Join-Path $repoRoot "publish"
}

if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $repoRoot "artifacts\installer"
}

New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null

$publishDir = Join-Path $PublishRoot $RuntimeIdentifier
if (-not (Test-Path -LiteralPath $publishDir -PathType Container)) {
    throw "Publish directory '$publishDir' was not found. Run publish_all.bat first or pass -PublishRoot."
}

$executablePath = Join-Path $publishDir "Vex.exe"
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Vex.exe was not found in '$publishDir'. MSIX packaging requires a Windows publish output."
}

$layoutRoot = Join-Path $ArtifactsRoot "msix-layout\$RuntimeIdentifier"
$packagePath = Join-Path $ArtifactsRoot "Vex-$Version-$RuntimeIdentifier.msix"
if (-not $PrepareOnly -and (Test-Path -LiteralPath $packagePath -PathType Leaf) -and -not $Force) {
    throw "MSIX output already exists: '$packagePath'. Pass -Force to replace it."
}

if ((Test-Path -LiteralPath $layoutRoot -PathType Container) -and -not $Force) {
    throw "MSIX layout already exists: '$layoutRoot'. Pass -Force to replace it."
}

if ($Force) {
    Remove-DirectoryUnderRoot $layoutRoot $ArtifactsRoot
}

New-Item -ItemType Directory -Force -Path $layoutRoot | Out-Null
Copy-PublishContents $publishDir $layoutRoot
Ensure-MsixAssets $layoutRoot $repoRoot

$manifestPath = Join-Path $layoutRoot "AppxManifest.xml"
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity
    Name="$(ConvertTo-XmlText $IdentityName)"
    Publisher="$(ConvertTo-XmlText $Publisher)"
    Version="$msixVersion"
    ProcessorArchitecture="$architecture" />
  <Properties>
    <DisplayName>Vex</DisplayName>
    <PublisherDisplayName>$(ConvertTo-XmlText $PublisherDisplayName)</PublisherDisplayName>
    <Logo>Assets\logo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Applications>
    <Application Id="Vex" Executable="Vex.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="Vex"
        Description="Vex Markdown editor"
        Square150x150Logo="Assets\logo.png"
        Square44x44Logo="Assets\logo.png"
        BackgroundColor="transparent">
        <uap:DefaultTile Square150x150Logo="Assets\logo.png" Square44x44Logo="Assets\logo.png" />
      </uap:VisualElements>
      <Extensions>
        <uap:Extension Category="windows.fileTypeAssociation">
          <uap:FileTypeAssociation Name="markdown">
            <uap:DisplayName>Markdown Document</uap:DisplayName>
            <uap:InfoTip>Markdown document</uap:InfoTip>
            <uap:SupportedFileTypes>
              <uap:FileType>.md</uap:FileType>
              <uap:FileType>.markdown</uap:FileType>
              <uap:FileType>.mdown</uap:FileType>
            </uap:SupportedFileTypes>
          </uap:FileTypeAssociation>
        </uap:Extension>
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

Set-Content -Encoding UTF8 -LiteralPath $manifestPath -Value $manifest
Write-Host "Prepared MSIX layout $layoutRoot"

if ($PrepareOnly) {
    Write-Host "PrepareOnly was set; skipping makeappx packaging."
    return
}

$makeAppx = Find-Tool "makeappx.exe" "*\x64\makeappx.exe"
if ([string]::IsNullOrWhiteSpace($makeAppx)) {
    throw "makeappx.exe was not found. Install the Windows SDK or pass -PrepareOnly to generate only the MSIX layout."
}

& $makeAppx pack /d $layoutRoot /p $packagePath /overwrite
if ($LASTEXITCODE -ne 0) {
    throw "makeappx.exe failed with exit code $LASTEXITCODE."
}

if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
    if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
        throw "Certificate file was not found: '$CertificatePath'."
    }

    $signTool = Find-Tool "signtool.exe" "*\x64\signtool.exe"
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw "signtool.exe was not found. Install the Windows SDK or omit -CertificatePath."
    }

    $signArgs = @("sign", "/fd", "SHA256", "/f", $CertificatePath)
    if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) {
        $signArgs += @("/p", $CertificatePassword)
    }

    $signArgs += $packagePath
    & $signTool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Packaged MSIX $packagePath"
