# 发布脚本

本目录包含 Vex 的普通桌面发布与 Native AOT 发布入口。查询平台列表不会构建程序，也不会创建发布目录。

## Native AOT 发布

使用 `publish_aot.bat` 生成 Native AOT 可执行载荷和带版本号的 ZIP。脚本通过 `publish_native.ps1` 执行 AOT 编译，保留该流程现有的单文件、裁剪、原生库和发布文档排除选项。

### 平台列表

```powershell
# 默认行为：列出平台别名及其 RID
scripts\release\publish_aot.bat

# 与默认行为相同
scripts\release\publish_aot.bat list

# 展开完整平台代码，便于复制 RID
scripts\release\publish_aot.bat list all
```

支持的平台代码：

| 平台别名 | RID |
| --- | --- |
| `win` | `win-x64` |
| `linux` | `linux-x64`、`linux-arm64` |
| `osx` | `osx-x64`、`osx-arm64` |

平台列表只显示支持范围，不会尝试构建所有操作系统的产物。

### 执行发布

```powershell
# 发布与当前主机系统和架构匹配的 RID
scripts\release\publish_aot.bat all

# 使用当前主机架构解析平台别名
scripts\release\publish_aot.bat win
scripts\release\publish_aot.bat linux
scripts\release\publish_aot.bat osx

# 指定一个完整 RID；当前主机必须与该 RID 匹配
scripts\release\publish_aot.bat win-x64
scripts\release\publish_aot.bat linux-arm64
scripts\release\publish_aot.bat osx-arm64

# 指定归档版本和发布根目录
scripts\release\publish_aot.bat all -Version 1.10.4 -PublishRoot E:\VexRelease
```

Native AOT 需要匹配的操作系统、架构、.NET 10 SDK、Native AOT 工具链，以及脚本使用的 PowerShell 7。跨操作系统或跨架构 RID 会被拒绝。`all` 只选择当前主机对应的 RID，例如 Windows x64 主机只发布 `win-x64`，不会在本机代编 Linux/macOS AOT。

未指定 `-Version` 时，脚本读取仓库根目录 `Directory.Build.props` 中的 `<Version>`；指定 `-Version` 可覆盖归档版本。该脚本不会自动递增或修改项目版本。

### 输出目录

默认发布根目录为仓库根目录下的 `publish`：

```text
publish/
├── exe/
│   └── <RID>/
│       └── Vex/
│           ├── Vex.exe 或 Vex
│           └── AOT 运行所需的原生附属文件（如有）
└── zip/
    └── Vex-<版本>-<RID>.zip
```

ZIP 内保留 `<RID>/Vex/` 目录结构及 AOT 运行需要的原生附属库。`-PublishRoot` 可将整个 `exe` 与 `zip` 输出树改到指定目录。仅实际发布时才创建目录；列表命令不会创建 `publish/exe` 或 `publish/zip`。

## 现有发布入口

| 脚本 | 用途 |
| --- | --- |
| `publish.ps1` / `publish_all.bat` | 现有桌面发布流程，负责更新版本并衔接 Windows EXE 安装包、Linux DEB、macOS DMG 等打包流程。 |
| `publish_native.ps1` / `publish_native.bat` | 只生成单个或当前主机可构建的 Native AOT 可执行载荷，不创建版本化 ZIP。 |
| `publish_aot.ps1` / `publish_aot.bat` | AOT 发布入口；复用 `publish_native.ps1`，并额外将每个 RID 的载荷归档到 `publish/zip`。 |
| `prepare_release_notes.bat` | 整理发布说明，不执行编译或打包。 |

## 验证列表模式

```powershell
scripts\release\publish_aot.bat
scripts\release\publish_aot.bat list all
```

两条命令都应只输出平台与用法说明；发布目录只会由实际发布命令创建。
