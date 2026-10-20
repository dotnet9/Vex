# Vex 离屏与桌面验证

验证器加载实际 App、主窗口、XAML 模板与 ViewModel，通过 Avalonia.Headless + Skia 绘制。设置、草稿及最近记录使用内存服务，MCP 关闭，文件关联注册跳过；测试文件复制到外部输出目录后再编辑和保存。

需要 .NET 10 SDK。正式版本直接从 nuget.org 还原共享库，不需要克隆库仓库：

    dotnet build scripts/ui-verification/UiVerification.csproj -c Release -p:ArtifactsPath=D:/github/temp_files/vex-native/artifacts
    dotnet D:/github/temp_files/vex-native/artifacts/bin/UiVerification/release/UiVerification.dll vex D:/github/temp_files/vex-native/refined/vex

修改共享库时，用 `scripts/prepare_local_libraries.ps1 -PackageVersion <新版本>` 生成本地 NuGet 包及仓外配置，再构建验证。默认输出到工作区上两级的 `temp_nugets` 与 `temp_files/vex-libraries`，可通过 `PackageRoot` / `WorkRoot` 指定位置；构建时沿用 `WorkRoot/vex-artifacts`。Lite 是 Markdown 的传递依赖，禁止跨仓 ProjectReference。已有包可加 `-SkipPack`，脚本拒绝覆盖同版本包，避免旧缓存干扰。开发与本地验证无需等待线上包；全部本地验证通过后再打共享库 Tag，正式 Vex 发布 CI 使用同版本线上包。

验证模式：

| 模式 | 范围 |
| --- | --- |
| vex | 完整应用回归，含四语、明暗主题、布局、编辑、浮层与六步引导 |
| workspace | 单文档八布局、章节操作、真实指针拖动、共享撤销、多窗口同步、引导恢复及 RELAP 样本 |
| layout | 窗口尺寸、指针连续拖动、正文与代码几何 |
| editing-tools | 格式命令、表格、演示与编辑偏好 |
| settings-ui | 七类偏好、四语窄窗、MCP 三页签、语法、水印、图片路径、编码、自动保存与外部冲突 |
| preferences / locale / path-identity / file-tree | 偏好持久化、语言、路径身份与文件树 |
| buttons / danger-buttons | 八色浅深、普通 / 悬停 / 按下共 96 种按钮状态 |
| parity | 同一份文档、1280×820 与 980×640、明暗模式、八布局和浮层截图 |
| performance | 每次独立进程加载指定布局；记录首帧、所有视图就绪与内存 |
| pointer-regressions | 首次拖选、跨段正反向选择与替换撤销、长代码选择 / 横向滑块、更新日志宽滚动条往返拖动 |
| presentation | 正文 / 文档大纲 / 导图演示，标题分页、只读导图、键盘、缩放、阅读位置与原窗口恢复 |
| html-tables | 最大化 RELAP 五张 HTML 表格，阅读 / 即时编辑跳转后实际单元格进入视口，正文演示与原文哈希 |
| media-layout | 1380 / 980 宽度、浅深主题、阅读 / 即时 / 源码并排的管道表格与 HTML 表格铺满、表头和隔行底色、PNG / SVG 等比例铺满、正文演示 |
| stress | 五份丰富样本与 RELAP，明暗两轮八布局、导图平移缩放、三窗口换文档、快速并发读取与原文哈希 |

工作区核心约定：每个进程只有一份当前文档；文件在左侧、大纲在独立右侧栏；八种布局可组合编辑、阅读和导图。源码与即时编辑全局互斥，独立窗口共用文档。布局与排版选择后保持展开。

即时编辑检查真实点击前后文字起点和高度、富文本选区、输入与撤销、上下左右跨块与边界合并。代码块默认无顶部工具栏或复制按钮；编辑时在下方显示语言输入框，验证其可见、可点击、改类型及 Esc 恢复。原生中文文字注入不等于完整中文输入法组合验证。

源码和即时编辑的正文快捷键使用共享历史；筛选、查找、章节表单及代码类型输入框保留各自的撤销。工作区回归通过真实焦点与键盘事件检查筛选撤销不会改变正文，并验证返回源码后仍可撤销正文。

章节检查包含 CRLF、Setext、YAML、围栏代码、H6 限制、循环移动保护，以及防抖期间可靠的源码区间映射。删除需确认，并可保留内容。引导完成 / 跳过恢复原文、修改状态、共享历史、布局、偏好、导图视口及原独立窗口实例。

对照参考只来自 [Vex-Flutter/design](https://github.com/dotnet9/Vex-Flutter/tree/main/design)，截图和样本文档存到仓外验收目录。本仓库的 `desktop-parity` 模式验证真实 Avalonia 布局，不生成或维护 HTML 原型。

长文档性能，最后一个参数可替换为其他只读样本：

    dotnet D:/github/temp_files/vex-native/artifacts/bin/UiVerification/release/UiVerification.dll performance D:/github/temp_files/vex-native/final/performance/instant instant "D:\github\company\shxskj\S-FlowC\docs\S-FlowC建模调试客户端_RELAP技术实现方案v1.3.md"

分别对 instant、source、read、map、source-render、source-map、instant-map、three 串行运行，避免其他构建与性能进程争用。校验原文件 SHA256 不变；Headless 数据不能代替实体桌面和 NativeAOT 性能。

丰富样本与持续滚动切换：

    python scripts/generate_stress_documents.py --output-root D:/github/md/mds
    dotnet D:/github/temp_files/vex-native/artifacts/bin/UiVerification/release/UiVerification.dll stress D:/github/temp_files/vex-native/stress-repeat D:/github/md/mds 2
    dotnet D:/github/temp_files/vex-native/artifacts/bin/UiVerification/release/UiVerification.dll pointer-regressions D:/github/temp_files/vex-native/pointer-repeat

末尾参数是轮数；每轮反转文件和布局顺序。性能及压力模式先设置为当前屏幕工作区并最大化；Headless 本机为 1920×1280、Scaling 1，结果 JSON 记录实际尺寸。六文档 × 八布局 × 两轮共 96 组合、3,240 个滑块移动步骤。每文档还覆盖正文分页、大纲、六轮导图切换与退出恢复。另验证导图平移、三窗口替换、源码 / 即时编辑互斥与 10 轮每轮六个并发读取请求。JSON 逐组合落到外部输出目录，出现失败时保留已完成记录。结果和边界见 docs/性能与稳定性验证-20261017.md。

演示专项验证：

    dotnet D:/github/temp_files/vex-native/artifacts/bin/UiVerification/release/UiVerification.dll presentation D:/github/temp_files/vex-native/presentation-tests

压力模式可加 `--trace-presentation` 记录恢复全文前后的物化数及内存，或 `--sample=Vex-02` 缩小诊断范围。演示进入 / 翻页的采样包含固定约 400ms 的异步渲染等待，不能当成纯翻页延迟或桌面 FPS。

vex.25 的演示专项还检查 RELAP 的五张 HTML 表格及即时编辑源码往返，并在最大化窗口连续三次深度滚动、切换正文 / 导图演示和退出，比较块序号与块内比例。虚拟布局估高会改变全文滚动比例，因此不能只比较像素偏移或滚动条比例。

vex.27 补充大纲定位回归：阅读订阅导航事件；即时编辑先物化并完成布局，再按目标实际位置校正滚动，超高表格定位到顶部。`html-tables` 不能只检查控件存在，还要确认目标单元格与视口相交，并生成十张导航截图和一张演示截图。共享库另覆盖 480 余块文档的远距离往返跳转、超高表格顶部可见、不进入编辑和物化数量上限。

    dotnet D:/github/temp_files/vex-native/artifacts-vex27/bin/UiVerification/release/UiVerification.dll html-tables D:/github/temp_files/vex-native/html-tables-vex27

正式发布使用 scripts/publish.ps1，可通过 ArtifactsRoot 与 PublishRoot 将产物定向至上述外部目录。正式 EXE 的 --verify-runtime <独立目录> 检查 NativeAOT、四语资源和隔离偏好；verify_native_publish.ps1 检查 PE CLR 目录为零与窗口响应。编译失败不能用 JIT 结果代替。

本机 .NET 10.0.10 / 10.0.12 的 ILC 使用服务器 GC 时在生成类型信息期间触发 0xc0000409；发布脚本现显式设置 IlcUseServerGc=false，仅调整编译器进程。10.0.12 通过此配置完成编译，正式包使用以下命令复现（编译器包需已安装在对应缓存目录）：

    $vexIlcTargets = Join-Path $env:USERPROFILE '.nuget/packages/microsoft.dotnet.ilcompiler/10.0.12/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets'
    ./scripts/publish.ps1 -RuntimeIdentifier win-x64 -ArtifactsRoot D:/github/temp_files/vex-native/aot-vex27 -PublishRoot D:/github/temp_files/vex-native/publish-vex27/win-x64 -RuntimeFrameworkVersion 10.0.12 -NativeCompilerTargetsPath $vexIlcTargets
    ./scripts/verify_native_publish.ps1 -ExecutablePath D:/github/temp_files/vex-native/publish-vex27/win-x64/Vex.exe -OutputPath D:/github/temp_files/vex-native/native-aot-vex27-evidence.json -DocumentPath D:/github/md/mds/Vex-01-综合语法与交互.md

历史版本验证见 docs/verification-v1.5.9.md；当前实现与限制见 docs/历史原生验收.md。
