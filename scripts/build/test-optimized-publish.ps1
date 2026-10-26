# 测试优化后的发布脚本
# 用法：pwsh scripts/build/test-optimized-publish.ps1

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptRoot

Write-Host "=== 测试激进裁剪优化发布 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "优化内容：" -ForegroundColor Yellow
Write-Host "  ✓ TrimMode=link（激进裁剪）"
Write-Host "  ✓ 移除 EventSource 支持"
Write-Host "  ✓ 移除 UTF7 编码支持"
Write-Host "  ✓ 移除 HTTP 活动传播"
Write-Host "  ✓ 移除元数据更新支持"
Write-Host "  ✓ 排除 PDB 符号文件"
Write-Host ""
Write-Host "预期效果：" -ForegroundColor Yellow
Write-Host "  • 体积：185 MB → 37 MB（减少 80%）"
Write-Host "  • 内存：减少 20-35%"
Write-Host "  • 编译时间：比 Native AOT 快 5-10 倍"
Write-Host ""

# 测试单平台发布
$testRid = 'win-x64'
Write-Host "开始测试发布：$testRid" -ForegroundColor Green
Write-Host ""

$publishScript = Join-Path $PSScriptRoot 'publish.ps1'
& $publishScript -RuntimeIdentifier $testRid

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "=== 发布失败 ===" -ForegroundColor Red
    exit 1
}

# 检查输出
$outputPath = Join-Path $repositoryRoot "artifacts/publish/$testRid/Vex"
if (-not (Test-Path $outputPath)) {
    Write-Host ""
    Write-Host "错误：输出目录不存在 $outputPath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== 发布成功 ===" -ForegroundColor Green
Write-Host ""

# 分析体积
$exePath = Join-Path $outputPath 'Vex.exe'
if (Test-Path $exePath) {
    $exeSize = (Get-Item $exePath).Length / 1MB
    Write-Host "主程序大小：" -ForegroundColor Cyan
    Write-Host "  Vex.exe: $([math]::Round($exeSize, 2)) MB"
}

$totalSize = (Get-ChildItem $outputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ""
Write-Host "总大小：" -ForegroundColor Cyan
Write-Host "  $([math]::Round($totalSize, 2)) MB"

# 列出最大的文件
Write-Host ""
Write-Host "最大的 10 个文件：" -ForegroundColor Cyan
Get-ChildItem $outputPath -Recurse -File |
    Sort-Object Length -Descending |
    Select-Object -First 10 |
    ForEach-Object {
        $size = $_.Length / 1MB
        $relativePath = $_.FullName.Substring($outputPath.Length + 1)
        Write-Host ("  {0,-50} {1,8:N2} MB" -f $relativePath, $size)
    }

Write-Host ""
Write-Host "=== 测试完成 ===" -ForegroundColor Green
Write-Host ""
Write-Host "输出目录：$outputPath" -ForegroundColor Yellow
