#!/bin/bash
# docs-shots 崩溃 bisect 探针：EXIT 0=good（60 帧+无崩溃）；1=bad（崩溃）；125=skip（构建失败）
cd /e/github/Apps/Vex || exit 125
dotnet build scripts/ui-verification -c Release --nologo -v q 2>&1 | grep -qE "error CS|MSB30" && exit 125
rm -rf artifacts/docs-shots
dotnet run --project scripts/ui-verification -c Release --no-build -- docs-shots artifacts/docs-shots > /dev/null 2>&1
code=$?
frames=$(ls "artifacts/docs-shots/大纲导航与阅读跟随/" 2>/dev/null | wc -l)
echo "PROBE code=$code frames=$frames commit=$(git rev-parse --short HEAD)"
if [ "$code" -eq 0 ] && [ "$frames" -ge 60 ]; then exit 0; fi
exit 1
