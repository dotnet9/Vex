param([Parameter(Mandatory)][string]$OutputRoot)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
[xml]$packages = Get-Content (Join-Path $repository 'Directory.Packages.props') -Raw
$candidates = @($packages.Project.ItemGroup.PackageVersion | Where-Object {
    $_.Include -like 'CodeWF.Avalonia.*' -and $_.Version -like '*-*'
})
if ($candidates.Count -eq 0) {
    Write-Output '正式库版本：使用公开 NuGet 源。'
    return
}
$versions = @($candidates.Version | Select-Object -Unique)
if ($versions.Count -ne 1) { throw 'Library candidates must share one version.' }
$revision = [string]$packages.Project.PropertyGroup.CodeWFAvaloniaSourceRevision
if ($revision -notmatch '^[0-9a-f]{40}$') { throw 'Candidate library requires an exact source commit.' }

# 在全新目录重建，不能借用开发机缓存或浮动 main。
$root = (New-Item -ItemType Directory -Path $OutputRoot).FullName
$source = Join-Path $root 'source'
$feed = (New-Item -ItemType Directory -Path (Join-Path $root 'feed')).FullName
git init $source
if ($LASTEXITCODE) { throw 'Library checkout initialization failed.' }
git -C $source remote add origin https://github.com/dotnet9/CodeWF.Avalonia.git
if ($LASTEXITCODE) { throw 'Library remote setup failed.' }
git -C $source fetch --depth 1 origin $revision
if ($LASTEXITCODE) { throw 'Pinned library fetch failed.' }
git -C $source checkout --detach FETCH_HEAD
if ($LASTEXITCODE) { throw 'Pinned library checkout failed.' }
if ((git -C $source rev-parse HEAD) -ne $revision) { throw 'Library revision mismatch.' }

foreach ($package in $candidates) {
    $project = @(Get-ChildItem (Join-Path $source 'src') -Filter ($package.Include + '.csproj') -Recurse)
    if ($project.Count -ne 1) { throw "Cannot resolve library project $($package.Include)." }
    dotnet pack $project[0].FullName -c Release "-p:PackageVersion=$($versions[0])" `
        "-p:ArtifactsPath=$(Join-Path $root 'artifacts')" --output $feed
    if ($LASTEXITCODE) { throw "Candidate pack failed: $($package.Include)." }
}
$config = Join-Path $root 'NuGet.Config'
$feedXml = [Security.SecurityElement]::Escape($feed)
@"
<configuration><packageSources><clear />
<add key="candidate" value="$feedXml" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources></configuration>
"@ | Set-Content $config -Encoding utf8
dotnet restore (Join-Path $repository 'scripts/ui-verification/UiVerification.csproj') --configfile $config
if ($LASTEXITCODE) { throw 'Vex restore from pinned library candidates failed.' }
Write-Output "候选库已还原：$($versions[0]) @ $revision"
