# Vex 1.6.5 验证记录

日期：2026-10-20。Windows 本机开发与验证使用 CodeWF.Avalonia 12.1.4.33 本地 NuGet 包，五个包位于 `E:/github/temp_nugets`，通过仓外临时 NuGet 配置还原。无跨仓项目引用。本记录保留当时的功能、原生和发布证据；当前原型入口见 [设计说明](design/原型设计.md)。

另将被 Git 忽略的 `artifacts/parity/prototype` 旧截图与样本文档、四份原型生成日志移至仓外验收目录，仓内不再保留第二处原型产物。

## 功能与界面

- 共享库全解决方案 Release 编译通过，Markdown 439 项测试全部通过；全量测试通过 600 项（Content 505、Controls 95）。覆盖行内格式、独立语法管道、字体字号、代码折行、PDF 纸张/页边距/水印、富 HTML 复制及编辑历史。
- `settings-ui` 58 项断言通过：七类真实设置绑定、四语和深浅主题、800×480 窄窗、MCP 服务/客户端/审计、插入撤销重做、水印、HTML 语法、图片目录/URI/同名保留/目录越界、新文档编码换行。
- 自动保存使用真实打开流程识别 BOM，验证写回后保留 BOM；外部内容修改及文件锁不会被覆盖。真实两秒空闲计时器写回具名文件并清除修改状态。
- 全应用 `vex`、编辑工具 `editing-tools`、八布局及独立窗口 `workspace`、四语 `locale`、桌面布局 `desktop-parity` 均通过。修正编码识别与原生下拉选项后，相关设置、应用、工作区及编辑回归通过。
- 更新状态与安装器交接 40 项、更新浮层四语布局 33 项、图片与表格自适应排版 145 项本地回归通过。
- 设置存储回归通过，类型化偏好、外部配置键及其他 XML 节保留；补充无关更新后默认值、旧空选项归一化及用户主动清空文本回归。`settings-real-ui` 以真实仓外 XML 配置重复全部 58 项设置界面断言，通过。
- 性能验证覆盖 12/100 章节文档与即时/源码/阅读切换、隐藏预览同步和图片基址；本轮与原生编译并行，不据此宣称性能提升。
- 已查看日文窄窗、浅色导出、深色 MCP 服务/客户端/审计与水印截图；不代表像素级完全一致或多 DPI 验收。

## 安装器与打包

- Windows 安装器独立真编译通过，包含中文和空格路径；正式安装器未在开发机执行。
- 独立桌面原生夹具验证中英、浅深、新装、维护、未知位置、多范围和迁移十二组场景，默认 640×360，设置和维护按内容展开。真实许可、选项、取消及窗口就绪交接保留。
- 迁移夹具 49 项断言通过：生产迁移编排使用惰性注册/进程后端，原文件保持不变，不执行真实卸载及 UAC。
- Linux 双架构元数据及打包合同通过；Windows 使用受控 `dpkg-deb` 边界，不声称实际生成或安装 Debian 包。
- macOS 资源布局、三语言品牌背景、稳定设备卸载、有限重试、清理及错误传播合同通过；检查 Finder 配置与已提交背景一致。不在 Windows 上挂载镜像或操作 Finder。
- 五平台发布资产合同通过，拒绝错误版本、产品、额外 ZIP、篡改、缺失及空文件。正式矩阵为 Windows x64 EXE、Linux x64/arm64 DEB、macOS x64/arm64 DMG，各附 SHA256。

## NativeAOT 与发布顺序

Windows NativeAOT 最终构建、四语和隔离偏好自检、文件关联不变检查通过。PE 的 CLR 目录为零，EXE 为 154,020,864 字节，SHA256 为 `18d09ccea0c556b2f73046d8fa345e691cd77c4de33d0876f6d585c5f961f98e`。

原生界面检查发现真实配置保存将未设置字符串变为空值，导致编码与换行等默认选项丢失；已在配置加载时归一化有效默认选项，保留可清空文本，并使用显式选项映射。新增真实 XML 设置界面回归及原生默认值自检。最终 EXE 在独立桌面通过真实菜单打开偏好设置，默认编码/换行选择可见，选择 CRLF 和代码换行写回真实测试配置。主窗口响应及截图正常，验证前后用户草稿、最近文件和审计文件哈希不变。

最终原生载荷的 Windows 安装包真编译通过，产物 42,896,880 字节并生成 SHA256；未在开发机执行正式安装。临时设置文件已清理，未打入安装包。本地门禁全部通过后再进入共享库及 Vex Tag 发布。

开发不等待线上 NuGet。只有本地功能、原生运行及安装器门禁全部通过后，才推送共享库 Tag；正式包可还原后再推送 Vex Tag，触发五平台 CI/CD。

发布阶段五个正式 `12.1.4.33` 包已从 nuget.org 成功还原到独立缓存，包内源码提交均为 `31246a1`。Vex 使用这些正式包重新还原、Release 编译，并通过真实 XML 设置界面 58 项及更新浮层 33 项复核；其他依赖复用既有缓存，未混用本地 `.33` 包。证据位于 `official33/restore.log`、`official33/vex-restore.log`、`official33/vex-build.log`、`official33/settings-real-ui.log` 与 `official33/updates.log`。

## CI 与正式发布

- 共享库 [主分支 CI](https://github.com/dotnet9/CodeWF.Avalonia/actions/runs/39403935849) 通过，包含构建测试、生成物审计、打包合同和 AOT 冒烟；[12.1.4.33 五平台 Demo 发布](https://github.com/dotnet9/CodeWF.Avalonia/actions/runs/39403052560) 成功，Release 包含五个安装包及五个 SHA256 文件。
- Vex [NuGet 发布](https://github.com/dotnet9/Vex/actions/runs/39405017999) 与 [主分支更新回归](https://github.com/dotnet9/Vex/actions/runs/39405008910) 通过；更新回归包含状态、四语浮层、媒体布局和真实 Inno 夹具交接。
- Vex [1.6.5 五平台发布](https://github.com/dotnet9/Vex/actions/runs/39405018132) 成功，包含 Windows 正式 NativeAOT 产物运行自检、安装器预检及五平台资产门禁；[正式 Release](https://github.com/dotnet9/Vex/releases/tag/v1.6.5) 已生成。本机未下载正式安装包复核 SHA256。
- 发布后发现 CI 将本地轻量引用的提交标题误当成附注 Tag 说明；改为读取远端标签对象并检查类型，补充精选说明和仅同步 Release 正文的流程。六组本地回归通过，覆盖附注标签、轻量标签兜底、远端对象恢复、精选优先、空说明和缺失说明；夹具验证远端及既有本地标签未被改写。

## 证据与边界

仓外证据根目录：`E:/github/temp_files/vex-parity`。共享库：`library-final-tests33.log`、`library-all-tests33.log`、`library-build33.log`、`local33-pack.log`；界面与设置：`final33-*.log`、`final33/settings-ui/*.png`、`settings-real-ui.log`、`settings-final33.log`；原生：`native165-publish-verified.log`、`native165-ui-verified.log`、`native165/desktop/receipt.json`、`native165/runtime-verified`、`native165/pe-receipt.json`；安装器：`native165-installer-verified.log`、`native165/installer`、`installer-matrix`、`migration/receipt.json`、`final33-setup-contract.log`；平台门禁：`final33-linux-contract.log`、`final33-macos-contract.log`、`final33-dmg-contract.log`、`final33-release-contract.log`。

构建仍报告现有依赖的裁剪/AOT 警告及 SourceLink 构建依赖 NU1902 提示。真实 Linux/macOS 安装、Finder/Gatekeeper、公证、多 DPI、真实全用户迁移及卸载尚未在对应系统验收；最终五平台构建与资产结果以发布 Actions 和 Release 为准。
