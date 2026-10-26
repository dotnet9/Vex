# Vex · 维刻

一款面向本地文件的跨平台 Markdown 编辑器。

Vex 基于 [.NET 10](https://dotnet.microsoft.com/) 与 [Avalonia](https://avaloniaui.net/) 构建，支持 Windows、Linux 和 macOS。它将即时编辑、Markdown 源码、阅读预览、章节导图和文档导出集中在一个工作区中，并可通过本机 MCP 服务与 AI 客户端协作。

> 本仓库是基于 [dotnet9/Vex](https://github.com/dotnet9/Vex) 的二次开发版本。上游项目采用 MIT License；本仓库在遵守原许可证和保留版权、许可声明的前提下进行功能、界面和工程实现的持续调整。具体来源和依赖许可见[鸣谢](docs/项目管理/鸣谢.md)。

![Vex 主界面](docs/图片/主界面.png)

## 项目定位

Vex 适合需要在本地管理 Markdown 文档、持续写作并将内容发布到不同渠道的用户。项目关注以下体验：

- **本地优先**：文档和附件保存在用户自己的目录，不依赖在线账户或云端服务。
- **多视图协作**：即时编辑、源码、阅读和章节导图共享同一份当前文档，可组合布局或拆分到独立窗口。
- **从写作到发布**：支持 Markdown、HTML、PDF、PNG、Word 导出，以及面向公众号、知乎和稀土掘金的富文本复制。
- **可恢复、可追溯**：提供自动保存、异常草稿恢复、外部文件变更检测、查找替换和本地操作审计等能力。
- **可扩展协作**：内置可控权限的本机 MCP 服务，便于 AI 客户端读取、修改和审计文档。

## 下载使用

从 [Releases](https://github.com/sunyonghuan/Vex/releases/latest) 下载与你的系统和 CPU 架构匹配的安装包，并优先使用同版本的 SHA256 校验文件验证下载内容。

| 平台 | 安装包 | 说明 |
| --- | --- | --- |
| Windows x64 | `Vex-v<版本>-win-x64-setup.exe` | 当前用户安装，可选文件关联和桌面快捷方式 |
| Linux x64 / arm64 | `Vex-v<版本>-linux-x64.deb` / `Vex-v<版本>-linux-arm64.deb` | 适用于 Debian 系发行版 |
| macOS Intel / Apple Silicon | `Vex-<版本>-osx-x64.dmg` / `Vex-<版本>-osx-arm64.dmg` | 将 `Vex.app` 拖入 `Applications` |

安装、升级、文件关联、数据位置和备份方法请阅读[快速开始](docs/开发文档/快速开始.md)。

## 主要功能

### 写作与阅读

- 即时编辑、Markdown 源码、阅读预览和章节导图。
- 八种固定工作区布局，支持将视图分离到独立窗口。
- Markdown、HTML、PDF、PNG 和 Word（`.docx`）导出。
- 查找与替换，支持大小写、整词、正则表达式和循环查找。
- 图片附件复制、相对路径管理，以及文档移动时的附件跟随。

### 文档管理

- 打开文件或文件夹，支持最近文件、目录浏览、文件筛选和大纲导航。
- 支持 `.md`、`.markdown`、`.mdown` 和 `.txt` 文档。
- 自动保存、恢复草稿、未保存内容保护和外部文件变更对比。
- 文件树、大纲折叠、排序、宽度、布局和编辑偏好持久化。

### 外观与协作

- 浅色、深色、跟随系统等界面模式，多套界面配色和正文排版。
- 简体中文、繁体中文、英文和日文界面。
- 一键复制适用于公众号、知乎和稀土掘金的富 HTML 内容。
- 本机 MCP 服务支持访问范围、修改确认、审计日志和 stdio 桥接。

功能细节、已验证范围和已知限制以[快速开始](docs/开发文档/快速开始.md)及[验收与已知限制](docs/项目管理/验收与已知限制.md)为准。

## 从源码运行

### 环境要求

- .NET SDK `10.0.100` 或兼容的后续 SDK；仓库通过 `global.json` 固定 SDK 选择策略。
- Windows、Linux 或 macOS 开发环境。
- Native AOT 发布必须在目标操作系统和目标架构上执行。

### 快速启动

Windows 可以直接运行根目录的 `start.bat`；它会调用 `scripts/start.bat`，完成还原、构建并启动应用。也可以传入一个文档路径：

```powershell
./start.bat "docs\开发文档\快速开始.md"
```

跨平台环境使用 PowerShell：

```powershell
pwsh ./scripts/start.ps1
```

### 手动构建与运行

```powershell
$artifacts = 'E:/temp/vex-build'
dotnet restore Vex.slnx "-p:ArtifactsPath=$artifacts"
dotnet build Vex.slnx --no-restore -v:minimal "-p:ArtifactsPath=$artifacts"
dotnet run --project src/Vex/Vex.csproj -f net10.0 --no-restore "-p:ArtifactsPath=$artifacts"
```

默认构建产物写入仓库外的临时目录，避免与其他构建结果混用；也可以通过 `ArtifactsPath` 指定自己的目录。版本号、仓库地址和基础构建属性集中在 `Directory.Build.props`，NuGet 版本集中在 `Directory.Packages.props`。

## 项目结构

```text
Vex/
├── assets/                  # 应用图标和品牌资源
├── design/                  # 可离线预览的界面原型及其资源
├── docs/                    # 需求、设计、开发、发布和验收文档
├── scripts/                 # 启动、验证、打包和发布脚本
├── src/
│   ├── Vex/                 # 桌面应用、编辑器、文件服务、国际化和 MCP
│   └── Vex.Controls/        # 可复用控件和主题资源
├── Vex.slnx                 # 生产项目解决方案
├── Directory.Build.props    # 版本、构建和发布属性
└── Directory.Packages.props # NuGet 中央包版本管理
```

从源码学习建议先阅读[文档入口](docs/README.md)，再按“需求 → 概要设计 → 详细设计 → 开发与验证”的顺序了解项目。

## 发布

常规发布入口：

```powershell
./publish_all.bat list
./publish_all.bat win
./publish_all.bat linux
./publish_all.bat osx
./publish_all.bat all
```

也可以使用 PowerShell 发布指定平台：

```powershell
pwsh ./scripts/release/publish.ps1 Linux
pwsh ./scripts/release/publish.ps1 osx
```

本机 Native AOT 的干净单文件发布使用 `scripts/release/publish_native.bat`。完整的 RID、安装包、校验文件和发布前检查说明见[安装与发布](docs/运维文档/安装与发布.md)。

## 开发与贡献

欢迎提交 Issue、改进建议和 Pull Request。提交前请：

1. 先阅读[贡献与安全](docs/项目管理/贡献与安全.md)，确认问题报告和敏感信息处理方式。
2. 说明复现环境、操作步骤、预期行为和实际结果；界面问题请附截图或录屏。
3. 将改动限制在清晰的职责范围内，并同步更新受影响的文档或验收说明。
4. 至少完成与改动相关的构建、测试或验证脚本，并在 PR 中写明验证结果。

项目文档、演示文件和验证入口集中在 [`docs/README.md`](docs/README.md)。

## 上游、许可证与第三方声明

### 上游项目

- 上游项目：[dotnet9/Vex](https://github.com/dotnet9/Vex)
- 本仓库：[sunyonghuan/Vex](https://github.com/sunyonghuan/Vex)
- 上游许可证：[MIT License](https://github.com/dotnet9/Vex/blob/main/LICENSE)

本项目及其自行维护的代码使用 MIT License。MIT 允许在保留版权声明和许可证文本的前提下使用、复制、修改、合并、发布、再许可和销售软件。软件按“现状”提供，作者不对使用软件产生的损失承担保证或责任。完整条款请见 [`LICENSE`](LICENSE)。

二次开发时请注意：

- 不要删除或隐藏上游项目及其他第三方组件要求保留的版权和许可证声明。
- 对新增代码、资源和依赖分别确认其来源及许可证，不能因为主项目采用 MIT 就推断所有内容都采用 MIT。
- 发布安装包或源码时，随包保留本项目的 `LICENSE` 以及适用的第三方许可文件。

第三方依赖、资源来源和对应许可证见[鸣谢](docs/项目管理/鸣谢.md)；设计原型使用的浏览器资源见 [`design/Third-Party-Licenses.html`](design/Third-Party-Licenses.html)。

## 相关链接

- [项目官网](https://vex.codewf.com/)
- [更新日志](docs/项目管理/更新日志.md)
- [快速开始](docs/开发文档/快速开始.md)
- [MCP 接口说明](docs/技术文档/MCP接口.md)
- [安装与发布](docs/运维文档/安装与发布.md)
- [鸣谢与依赖许可](docs/项目管理/鸣谢.md)
