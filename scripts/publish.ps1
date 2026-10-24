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
        [xml] $properties = [IO.File]::ReadAllText($props)
        $Version = [string] $properties.Project.PropertyGroup.Version
    }
}
Write-Host "发布 $RuntimeIdentifier (Version=$Version)"

$tfm = if ($RuntimeIdentifier.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { "net10.0-windows" } else { "net10.0" }
# 全平台 NativeAOT：完整反射元数据保全（Prism/DryIoc）。
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
dotnet publish (Join-Path $repositoryRoot "src/Vex/Vex.csproj") -c Release -f $tfm -r $RuntimeIdentifier -m:1 @ilcArgs /p:PublishProfile=FolderProfile__$RuntimeIdentifier -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }
