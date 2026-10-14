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

$tfm = if ($RuntimeIdentifier.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { "net10.0-windows" } else { "net10.0" }
# 全平台 NativeAOT：完整反射元数据保全（Prism/DryIoc），单线程 ILC 更稳
$ilcArgs = @("-p:PublishAot=true", "-p:PublishTrimmed=true", "-p:PublishSingleFile=false",
    "-p:DebugSymbols=true", "-p:DebugType=portable",
    "-p:IlcGenerateCompleteTypeMetadata=true", "-p:IlcTrimMetadata=false", "-p:IlcSingleThreaded=true")
# macOS 保留符号（ld_classic 不支持压缩调试段）
if ($RuntimeIdentifier.StartsWith("osx-", [StringComparison]::OrdinalIgnoreCase)) { $ilcArgs += "-p:StripSymbols=false" }
# 限制工程级并发，避免本地多仓联调时 MSBuild 子进程争用内存。
dotnet publish (Join-Path $repositoryRoot "src/Vex/Vex.csproj") -c Release -f $tfm -r $RuntimeIdentifier -m:1 @ilcArgs /p:PublishProfile=FolderProfile__$RuntimeIdentifier -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }
