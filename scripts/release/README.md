# Native AOT 发布脚本

`publish_aot.bat` 是 Native AOT 发布入口，默认只列出支持的平台，不执行构建或创建目录。脚本复用 `publish_native.ps1` 的 AOT 编译参数和主机/RID 限制，并在成功发布后创建版本化 ZIP。

## 用法

```powershell
# 列出平台别名及 RID
scripts\release\publish_aot.bat

# 列出所有完整平台代码
scripts\release\publish_aot.bat list all

# 发布当前主机对应的 AOT RID
scripts\release\publish_aot.bat all

# 发布单个平台（必须在匹配的操作系统和架构上）
scripts\release\publish_aot.bat win-x64
scripts\release\publish_aot.bat linux-x64
scripts\release\publish_aot.bat osx-arm64

# 指定发布版本
scripts\release\publish_aot.bat all -Version 1.10.4
```

支持的 Native AOT RID：`win-x64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`。AOT 发布需在与 RID 匹配的操作系统/架构上执行；`all` 仅发布当前主机匹配的 RID，不会伪跨平台编译。

## 输出

- 可执行载荷：`publish/exe/<RID>/Vex/`
- 版本化归档：`publish/zip/Vex-<版本>-<RID>.zip`

ZIP 内保留 `Vex/` 目录及 Native AOT 运行所需的所有附属原生库。未指定 `-Version` 时读取 `Directory.Build.props` 中的版本，不自动修改项目版本。
