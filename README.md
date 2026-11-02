# Vex · 维刻

写下想法，看见脉络。

Vex 是基于 .NET 10 与 Avalonia 12 的桌面 Markdown 编辑器，支持 **Windows、Linux、macOS**，围绕本地文件提供即时编辑、源码、阅读、章节导图、导出与本机 MCP 协作。

官网：[vex.codewf.com](https://vex.codewf.com/) · 出品：[码坊 CodeWF](https://codewf.com) · 当前版本：**1.20.1.0**

![Vex 桌面工作区](docs/图片/主界面.png)

## 下载

从 [GitHub Releases](https://github.com/dotnet9/Vex/releases/latest) 下载安装包及对应 SHA256：Windows x64 EXE、Linux x64/arm64 DEB、macOS x64/arm64 DMG。

## 功能

- 即时编辑、源码、阅读与章节导图，八种固定布局及独立视图窗口，共享当前文档。
- 本地文件与文件夹、最近记录、大纲、查找替换、自动保存和异常退出草稿恢复。
- Markdown、HTML、PDF、PNG、Word 导出，微信公众号、知乎、稀土掘金复制。
- 九套界面配色、十三套正文排版，支持浅色、深色、跟随系统及四种界面语言。
- 文件夹与大纲折叠、布局、导航和编辑偏好持久化；保存失败、外部变更与恢复草稿有独立保护流程。
- 本机 MCP 服务提供 29 个工具，支持访问范围、修改确认、审计和 stdio 桥接。

源码与即时编辑提供选区字号、前景色和背景色，样式以 Markdown 中的行内标记保存。Mermaid 支持 24 类图表，单击即可在图表上方编辑源码，点击正文后提交并收起。单阅读正文会随窗口扩展，最大可达当前显示器宽度的一半。

应用能力与验证边界见 [快速开始](docs/快速开始.md) 和 [验收记录](docs/验收与已知限制.md)。

## 界面

以下界面由 Vex 1.20.0.0 与正式自研库 12.2.48 的真实 Avalonia 控件渲染生成。更多操作见[快速开始](docs/快速开始.md)，采集方式见[截图说明](docs/图片/截图说明.md)。

| 源码与阅读对照 | 文档导图 |
| --- | --- |
| ![源码与阅读共用同一份 Markdown](docs/图片/源码对照.png) | ![根据文档标题生成的章节导图](docs/图片/思维导图.png) |

| 配色与外观 | 正文排版 |
| --- | --- |
| ![浅深色模式与九套界面配色](docs/图片/外观与配色.png) | ![十三套正文排版与渲染密度](docs/图片/正文排版.png) |

## 原型与文档

桌面及三平台安装原型在 [design](design/index.html)，应用已接入 9 套配色、13 套正文排版及对应工作区交互。运行 `node design/scripts/preview.cjs` 后打开 http://127.0.0.1:4317/design/ 。视觉对照和跨平台验证范围见验收文档。

- [仓库学习入口](docs/仓库学习文档.md)
- [需求文档](docs/需求文档.md) · [概要设计](docs/概要设计.md) · [详细设计](docs/详细设计.md)
- [原型与资源](docs/原型与资源.md) · [Markdown 示例](docs/Markdown功能示例.md)
- [开发与验证](docs/开发与验证.md) · [发布流程](docs/安装与发布.md)
- [MCP 使用与接口](docs/MCP接口.md) · [更新日志](docs/更新日志.md) · [鸣谢](docs/鸣谢.md)

## 运行

~~~powershell
# 重产物默认落在仓库内 artifacts/；想放仓库外时用相对或任意路径即可（正斜杠跨平台）。
$vexWorkRoot = '../vex-temp'
dotnet restore Vex.slnx "-p:ArtifactsPath=$vexWorkRoot/artifacts"
dotnet build Vex.slnx --no-restore -v:minimal "-p:ArtifactsPath=$vexWorkRoot/artifacts"
dotnet run --project src/Vex/Vex.csproj -f net10.0 --no-restore "-p:ArtifactsPath=$vexWorkRoot/artifacts"
~~~

版本与编译属性在 Directory.Build.props，NuGet 中央包版本在 Directory.Packages.props，SDK 由 global.json 选择。应用图标以根目录 logo.svg、logo.png、logo.ico 为源；共享库经 NuGet 消费，本地联调和五 RID NativeAOT 发布见开发文档。

## 许可证

项目使用 [MIT](LICENSE)。依赖与维护者见 [鸣谢](docs/鸣谢.md)，浏览器原型本地依赖见 [许可说明](design/Third-Party-Licenses.html)。
