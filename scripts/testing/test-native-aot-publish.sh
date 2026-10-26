#!/bin/bash
# Native AOT 发布测试脚本

echo "=== Native AOT 发布测试 ==="
echo "目标: win-x64"
echo "配置: Release"
echo ""

# 清理旧的发布产物
echo "清理旧产物..."
rm -rf artifacts/publish/win-x64-aot

# 发布 Native AOT 版本
echo "开始 Native AOT 发布..."
dotnet publish src/Vex/Vex.csproj \
  -c Release \
  -f net10.0-windows \
  -r win-x64 \
  -p:PublishAot=true \
  -p:VexExcludePublishDocuments=false \
  -o artifacts/publish/win-x64-aot

if [ $? -eq 0 ]; then
    echo ""
    echo "=== 发布成功 ==="
    echo "输出目录: artifacts/publish/win-x64-aot"
    echo ""
    echo "文件大小对比:"
    ls -lh artifacts/publish/win-x64-aot/Vex.exe 2>/dev/null || echo "未找到 Vex.exe"
    echo ""
    echo "目录总大小:"
    du -sh artifacts/publish/win-x64-aot
    echo ""
    echo "最大的 10 个文件:"
    find artifacts/publish/win-x64-aot -type f -printf '%s %p\n' | sort -rn | head -10 | awk '{size=$1/1024/1024; $1=""; printf "%.2f MB\t%s\n", size, $0}'
else
    echo ""
    echo "=== 发布失败 ==="
    echo "请检查错误信息"
fi
