# 统一发布入口：scripts/publish.ps1
# 用法：pwsh scripts/publish.ps1 [-RuntimeIdentifier win-x64] [-Version 0.0.0]
# 输出：artifacts/publish/<rid>/<AppName>/
[CmdletBinding()]
param(
    [string] $RuntimeIdentifier = "win-x64",
    [string] $Version = "",
    [string] $ArtifactsRoot = "",
    [string] $PublishRoot = "",
    [string] $RuntimeFrameworkVersion = "",
    [string] $NativeCompilerTargetsPath = ""
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
# 全平台 NativeAOT：完整反射元数据保全（MS DI 反射激活）。
# ILC 使用单线程与工作站 GC，避免本机 .NET 10 编译器在生成类型信息时崩溃；不改变应用 GC。
$ilcArgs = @("-p:PublishAot=true", "-p:PublishTrimmed=true", "-p:PublishSingleFile=false",
    "-p:DebugSymbols=true", "-p:DebugType=portable",
    "-p:IlcGenerateCompleteTypeMetadata=true", "-p:IlcTrimMetadata=false", "-p:IlcSingleThreaded=true", "-p:IlcUseServerGc=false")
# macOS 保留符号（ld_classic 不支持压缩调试段）
if ($RuntimeIdentifier.StartsWith("osx-", [StringComparison]::OrdinalIgnoreCase)) { $ilcArgs += "-p:StripSymbols=false" }
if ($ArtifactsRoot) { $ilcArgs += "-p:ArtifactsPath=$([IO.Path]::GetFullPath($ArtifactsRoot))" }
if ($PublishRoot) { $ilcArgs += "-p:PublishDir=$([IO.Path]::GetFullPath($PublishRoot))/" }
if ($RuntimeFrameworkVersion) { $ilcArgs += "-p:RuntimeFrameworkVersion=$RuntimeFrameworkVersion" }
if ($NativeCompilerTargetsPath) {
    $compilerTargets = (Resolve-Path -LiteralPath $NativeCompilerTargetsPath).Path
    $ilcArgs += "-p:ILCompilerTargetsPath=$compilerTargets"
}
# 限制工程级并发，避免本地多仓联调时 MSBuild 子进程争用内存。
$publishLog = Join-Path ([IO.Path]::GetTempPath()) ("vex-publish-" + $RuntimeIdentifier + ".log")
dotnet publish (Join-Path $repositoryRoot "src/Vex/Vex.csproj") -c Release -f $tfm -r $RuntimeIdentifier -m:1 @ilcArgs /p:PublishProfile=FolderProfile__$RuntimeIdentifier -p:Version=$Version 2>&1 | Tee-Object -FilePath $publishLog
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }

# AOT/裁剪零回归门禁（借鉴 StreamMark 的"零裁剪警告是 CI 门禁"实践）：
# IL2xxx 警告意味着有代码路径在裁剪下不可见。基线记录在
# scripts/aot-trim-warning-baseline.txt（每 RID 一行 rid=数量）；基线内放行，
# 超过基线即发布失败——收紧裁剪后应主动下调基线，让门禁跟着目标走。
$trimWarnings = @(Select-String -LiteralPath $publishLog -Pattern 'warning IL\d+' -AllMatches)
$baselineFile = Join-Path $PSScriptRoot "aot-trim-warning-baseline.txt"
$baseline = $null
if (Test-Path -LiteralPath $baselineFile) {
    foreach ($line in Get-Content -LiteralPath $baselineFile -Encoding utf8) {
        if ($line -match "^\s*$([Regex]::Escape($RuntimeIdentifier))\s*=\s*(\d+)\s*$") { $baseline = [int]$Matches[1] }
    }
}
if ($null -eq $baseline) {
    if ($trimWarnings.Count -gt 0) {
        Write-Host "AOT 门禁提示：$RuntimeIdentifier 有 $($trimWarnings.Count) 条 IL 裁剪警告，基线文件未收录该 RID，暂不拦截。" -ForegroundColor Yellow
    }
} elseif ($trimWarnings.Count -gt $baseline) {
    $trimWarnings | Select-Object -First 10 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    throw "AOT 零回归门禁失败：$RuntimeIdentifier 出现 $($trimWarnings.Count) 条 IL 裁剪警告，超过基线 $baseline（详见 $publishLog）"
} elseif ($trimWarnings.Count -lt $baseline) {
    Write-Host "AOT 门禁提示：$RuntimeIdentifier 裁剪警告降至 $($trimWarnings.Count) 条（基线 $baseline），可下调 scripts/aot-trim-warning-baseline.txt 收紧门禁。" -ForegroundColor Green
}
