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
# NativeAOT 的 ILC 在 preview SDK 上并行编译可能崩溃，单线程更稳
$ilcArgs = @()
if ($RuntimeIdentifier.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { $ilcArgs += "-p:IlcSingleThreaded=true" }
dotnet publish (Join-Path $repositoryRoot "src/Vex/Vex.csproj") -c Release -f $tfm -r $RuntimeIdentifier @ilcArgs /p:PublishProfile=FolderProfile__$RuntimeIdentifier -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }
