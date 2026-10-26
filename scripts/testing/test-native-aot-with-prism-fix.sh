#!/bin/bash
echo "=== 测试 Native AOT 编译（Prism 修复方案 A）==="
echo ""
echo "配置说明："
echo "  - TrimmerRootAssembly: 保留 Prism 完整元数据"
echo "  - 绕过 DialogWindow 的反射绑定检查"
echo ""

dotnet publish src/Vex/Vex.csproj \
  -c Release \
  -f net10.0-windows \
  -r win-x64 \
  -p:PublishAot=true \
  -p:PublishTrimmed=true \
  -p:PublishSingleFile=true \
  --self-contained

if [ $? -eq 0 ]; then
    echo ""
    echo "=== 编译成功！==="
    
    OUTPUT_PATH="artifacts/publish/win-x64/Vex"
    if [ -f "$OUTPUT_PATH/Vex.exe" ]; then
        SIZE=$(du -sh "$OUTPUT_PATH/Vex.exe" | cut -f1)
        TOTAL=$(du -sh "$OUTPUT_PATH" | cut -f1)
        echo ""
        echo "发布产物："
        echo "  Vex.exe: $SIZE"
        echo "  总大小: $TOTAL"
    fi
else
    echo ""
    echo "=== 编译失败 ==="
    exit 1
fi
