#!/bin/bash
# 激进裁剪发布测试脚本（不使用 Native AOT）

echo "=== 激进裁剪发布测试（无 Native AOT）==="
echo "目标: win-x64"
echo "配置: Release + TrimMode=link"
echo ""

# 清理旧的发布产物
echo "清理旧产物..."
rm -rf artifacts/publish/win-x64-trimmed

# 发布激进裁剪版本
echo "开始发布..."
dotnet publish src/Vex/Vex.csproj \
  -c Release \
  -f net10.0-windows \
  -r win-x64 \
  -p:PublishAot=false \
  -p:PublishSingleFile=true \
  -p:VexExcludePublishDocuments=false \
  -o artifacts/publish/win-x64-trimmed

if [ $? -eq 0 ]; then
    echo ""
    echo "=== 发布成功 ==="
    echo "输出目录: artifacts/publish/win-x64-trimmed"
    echo ""
    echo "=== 体积对比 ==="
    echo "优化前（原版本）:"
    ls -lh artifacts/publish/win-x64/Vex/Vex.exe 2>/dev/null || echo "  未找到原版本"
    du -sh artifacts/publish/win-x64/Vex 2>/dev/null || echo "  未找到原版本目录"
    echo ""
    echo "优化后（激进裁剪）:"
    ls -lh artifacts/publish/win-x64-trimmed/Vex.exe 2>/dev/null || echo "  未找到 Vex.exe"
    du -sh artifacts/publish/win-x64-trimmed
    echo ""
    echo "最大的 10 个文件:"
    find artifacts/publish/win-x64-trimmed -type f -printf '%s %p\n' | sort -rn | head -10 | awk '{size=$1/1024/1024; $1=""; printf "%.2f MB\t%s\n", size, $0}'
else
    echo ""
    echo "=== 发布失败 ==="
    echo "请检查错误信息"
fi
