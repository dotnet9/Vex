[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackageVersion,
    [string] $LibraryRoot = "",
    [string] $WorkRoot = "D:\github\temp_files\vex-libraries",
    [string] $PackageRoot = "D:\github\temp_nugets",
    [switch] $SkipPack
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (!$LibraryRoot) { $LibraryRoot = Join-Path $repositoryRoot '../../libs/CodeWF.Avalonia' }
$LibraryRoot = (Resolve-Path -LiteralPath $LibraryRoot).Path
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
$packages = [ordered]@{
    'CodeWF.Avalonia.Lang' = 'src/Lang/CodeWF.Avalonia.Lang/CodeWF.Avalonia.Lang.csproj'
    'CodeWF.Avalonia.Controls' = 'src/Controls/CodeWF.Avalonia.Controls/CodeWF.Avalonia.Controls.csproj'
    'CodeWF.Avalonia.Markdown.Lite' = 'src/Markdown/CodeWF.Avalonia.Markdown.Lite/CodeWF.Avalonia.Markdown.Lite.csproj'
    'CodeWF.Avalonia.Markdown' = 'src/Markdown/CodeWF.Avalonia.Markdown/CodeWF.Avalonia.Markdown.csproj'
    'CodeWF.Avalonia.MindView' = 'src/MindView/CodeWF.Avalonia.MindView/CodeWF.Avalonia.MindView.csproj'
}
foreach ($name in $packages.Keys) {
    $packageFile = Join-Path $PackageRoot "$name.$PackageVersion.nupkg"
    if ($SkipPack -and !(Test-Path -LiteralPath $packageFile)) { throw "本地包不存在：$packageFile" }
    if (!$SkipPack -and (Test-Path -LiteralPath $packageFile)) { throw "请使用新的包版本，避免覆盖 NuGet 缓存：$packageFile" }
}
if (!$SkipPack) {
    [IO.Directory]::CreateDirectory($PackageRoot) | Out-Null
    foreach ($project in $packages.Values) {
        dotnet pack (Join-Path $LibraryRoot $project) -c Release --nologo `
            "-p:PackageVersion=$PackageVersion" "-p:ArtifactsPath=$WorkRoot/artifacts" "-p:PackageOutputPath=$PackageRoot"
        if ($LASTEXITCODE -ne 0) { throw "打包失败：$project" }
    }
}
$propsPath = Join-Path $repositoryRoot 'Directory.Packages.props'
$propsText = [IO.File]::ReadAllText($propsPath)
foreach ($name in $packages.Keys | Where-Object { $_ -ne 'CodeWF.Avalonia.Markdown.Lite' }) {
    $pattern = '(<PackageVersion Include="' + [regex]::Escape($name) + '" Version=")[^"]+("\s*/>)'
    $propsText = [regex]::Replace($propsText, $pattern, { param($match) $match.Groups[1].Value + $PackageVersion + $match.Groups[2].Value })
}
[IO.File]::WriteAllText($propsPath, $propsText)
[IO.Directory]::CreateDirectory($WorkRoot) | Out-Null
$configPath = Join-Path $WorkRoot 'NuGet.local.config'
$escapedPackageRoot = [Security.SecurityElement]::Escape($PackageRoot)
[IO.File]::WriteAllText($configPath, @"
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear />
<add key="VexLocalLibraries" value="$escapedPackageRoot" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
</packageSources></configuration>
"@)
dotnet restore (Join-Path $repositoryRoot 'scripts/ui-verification/UiVerification.csproj') `
    --configfile $configPath "-p:ArtifactsPath=D:/github/temp_files/vex-native/artifacts"
if ($LASTEXITCODE -ne 0) { throw 'Vex 本地 NuGet 还原失败。' }
Write-Host "已通过 NuGet 接入 $PackageVersion；未添加跨仓项目引用。"
