[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackageVersion,
    [string] $LibraryRoot = "",
    [string] $WorkRoot = "",
    [string] $PackageRoot = "",
    [switch] $SkipPack
)

$ErrorActionPreference = 'Stop'
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?$') {
    throw 'PackageVersion 必须是 NuGet 版本号。'
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (!$LibraryRoot) { $LibraryRoot = Join-Path $repositoryRoot '../../libs/CodeWF.Avalonia' }
if (!$WorkRoot) { $WorkRoot = Join-Path $repositoryRoot '../../temp_files/vex-libraries' }
if (!$PackageRoot) { $PackageRoot = Join-Path $repositoryRoot '../../temp_nugets' }
$LibraryRoot = (Resolve-Path -LiteralPath $LibraryRoot).Path
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
$packages = [ordered]@{
    'CodeWF.Avalonia.Lang' = 'src/Lang/CodeWF.Avalonia.Lang/CodeWF.Avalonia.Lang.csproj'
    'CodeWF.Avalonia.Controls' = 'src/Controls/CodeWF.Avalonia.Controls/CodeWF.Avalonia.Controls.csproj'
    'CodeWF.Avalonia.DataGrid' = 'src/DataGrid/CodeWF.Avalonia.DataGrid/CodeWF.Avalonia.DataGrid.csproj'
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
$propsPath = Join-Path $repositoryRoot 'build/Versions.props'
$propsText = [IO.File]::ReadAllText($propsPath)
$propsText = [regex]::Replace($propsText, '(<CodeWFAvaloniaVersion>)[^<]+(</CodeWFAvaloniaVersion>)',
    { param($match) $match.Groups[1].Value + $PackageVersion + $match.Groups[2].Value })
# 未提交源码只能在本机验证，不能给 CI 留下与实际包内容不符的源码提交。
$libraryRevision = if (!(git -C $LibraryRoot status --porcelain)) { git -C $LibraryRoot rev-parse HEAD } else { '' }
$propsText = [regex]::Replace($propsText, '(<CodeWFAvaloniaSourceRevision>)[^<]*(</CodeWFAvaloniaSourceRevision>)',
    { param($match) $match.Groups[1].Value + $libraryRevision + $match.Groups[2].Value })
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
# 全局缓存按「包 id + 版本」命中即复用，不回头校验内容。同版本曾打过一次包（尤其 -PackageVersion
# 只改包版本、不改库 Directory.Build.props 的 Version，程序集仍是旧版本号），还原就会链到上一轮的
# 二进制，验证器据此得出的通过/失败都是过时的。刚产出的包才是本轮事实源，先把同名缓存条目清掉。
$globalPackages = ((& dotnet nuget locals global-packages --list) |
    Where-Object { $_ -match 'global-packages:' } |
    ForEach-Object { ($_ -replace '^.*global-packages:\s*', '').Trim() })
if (-not $globalPackages) { throw '无法定位 NuGet 全局缓存目录。' }
foreach ($name in $packages.Keys) {
    $cached = [IO.Path]::GetFullPath((Join-Path $globalPackages ($name.ToLowerInvariant() + '/' + $PackageVersion)))
    $allowedCacheRoot = [IO.Path]::GetFullPath($globalPackages).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$cached.StartsWith($allowedCacheRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '包缓存路径越界。' }
    if (Test-Path -LiteralPath $cached) { Remove-Item -LiteralPath $cached -Recurse -Force }
}
dotnet restore (Join-Path $repositoryRoot 'scripts/ui-verification/UiVerification.csproj') `
    --configfile $configPath "-p:ArtifactsPath=$WorkRoot/vex-artifacts"
if ($LASTEXITCODE -ne 0) { throw 'Vex 本地 NuGet 还原失败。' }
Write-Host "已通过 NuGet 接入 $PackageVersion；未添加跨仓项目引用。"
