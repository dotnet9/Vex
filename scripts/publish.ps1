# 统一发布入口：scripts/publish.ps1
# 用法：pwsh scripts/publish.ps1 [-RuntimeIdentifier win-x64] [-Version 0.0.0]
# 输出：artifacts/publish/<rid>/<AppName>/
[CmdletBinding()]
param(
    [string] $RuntimeIdentifier = "win-x64",
    [string] $Version = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = Join-Path $repositoryRoot "Directory.Build.props"
    if (Test-Path -LiteralPath $props) {
        $match = Select-String -LiteralPath $props -Pattern '<Version>([^<]+)</Version>' |
            Select-Object -First 1
        if ($match) { $Version = $match.Matches[0].Groups[1].Value }
    }
}
Write-Host "发布 $RuntimeIdentifier (Version=$Version)"

$tfm = if ($RuntimeIdentifier.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { "net11.0-windows" } else { "net11.0" }
# 全平台 NativeAOT：完整反射元数据保全（Prism/DryIoc），单线程 ILC 更稳；win-x86 保持自包含单文件
$ilcArgs = @("-p:PublishAot=true", "-p:PublishTrimmed=true", "-p:PublishSingleFile=false",
    "-p:IlcGenerateCompleteTypeMetadata=true", "-p:IlcTrimMetadata=false", "-p:IlcSingleThreaded=true")
if ($RuntimeIdentifier -eq "win-x86") { $ilcArgs = @() }
if ($RuntimeIdentifier.StartsWith("osx-", [StringComparison]::OrdinalIgnoreCase)) { $ilcArgs += "-p:StripSymbols=false" }
dotnet publish (Join-Path $repositoryRoot "src/Vex/Vex.csproj") -c Release -f $tfm -r $RuntimeIdentifier @ilcArgs /p:PublishProfile=FolderProfile__$RuntimeIdentifier -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }
