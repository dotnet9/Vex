# Vex 1.6.3 验证记录

日期：2026-10-17。Windows 本机验证；共享库使用 nuget.org 正式版 12.1.4.29，不引用相邻仓库项目。所有临时产物位于仓外 `D:/github/temp_files/vex-native`。

## 安装范围迁移

统一当前用户安装，默认 `%LOCALAPPDATA%\Programs\Vex`。准备安装前重新读取自身精确 AppId 的 HKLM 32/64 位注册，校验路径与注册卸载器；旧全用户安装需先确认迁移，再请求管理员权限运行其卸载器。退出码、注册清除和重新探测均成功后才能复制新应用；静默安装遇到旧范围时停止，要求先交互迁移。禁止目标与旧目录重叠，或位于 Windows / Program Files。

迁移前备份 `Vex.exe.config` 和 `Vex.dll.config` 到 `%LOCALAPPDATA%\Vex\MigrationBackup`。已有用户配置优先；没有时在旧安装卸载完成后立即恢复，后续即使取消载荷复制也能重试。新包不覆盖已有配置。不递归清空旧目录，不操作文档和草稿位置。

- 独立中文、空格路径的生产入口真编译通过；没有在开发机执行产品安装或卸载。
- `verify_vex_migration.py`：49 项断言通过。编译期测试后端只模拟注册和卸载结果，预处理检查不存在注册 API、ShellExec、Exec、外部 DLL 或安装段，且不创建向导。
- 原生安全夹具的中英、浅深主流程与维护、未知位置、多范围共十组通过；另补中英“升级并迁移”两组，共十二组。保留四边描边、只读路径、取消与真实窗口更新交接检查。
- CI 的安装器契约与正式发布预检均包含迁移断言。

证据：`migration-163-final/receipt.json`、`migration-163-final/assertions.txt`、`visual-163/*/verification.json`。

## 图片和表格

图片通过公开工厂继承原有异步加载、SVG / 动图和点击预览能力，只调整显示几何：按正文可用宽度等比例显示，移除固定的 900×520 显示上限。表格通过共享控件的 `MarkdownTableLayout.FillAvailableWidth` 铺满正文，按内容比例分配列宽，窄窗换行。阅读和即时编辑共用表格样式类，使用浅灰表头、中性边框和隔行底色，并提供深色对应色。

- 正式依赖下 `media-layout`：145 项断言、49 张截图。覆盖 1380 / 980 窗口宽度，浅深主题，阅读 / 源码并排 / 即时编辑，管道与 HTML 表格，PNG / SVG，以及正文演示。
- RELAP 长文档五张 HTML 表格在阅读和即时编辑中均实际进入视口；演示可见真实单元格，原文内容和源文件 SHA256 不变。
- 编辑工具回归通过；共享库表格布局、HTML 表格与表格编辑专项 13 项通过。
- 七套主应用原型审计、三个安装器原型审计及单文件生成一致性检查通过；五平台安装资产门禁继续拒绝便携 ZIP。

证据：`media-163-final.log`、`media-163-final/*.png`、`html-tables-163/*.png`、`editing-tools-163`。截图为真实 Avalonia 控件的离屏渲染，不代表 Linux/macOS 实机或多 DPI 验收。

## 发布边界

版本与三个安装器原型同步到 1.6.3。GitHub Actions 按标签构建 Windows x64 EXE、Linux x64 / arm64 DEB、macOS x64 / arm64 DMG 及 SHA256。真实全用户卸载、UAC 及跨范围升级尚未在开发机执行；采用安全夹具验证编排，真实安装器只编译。五平台最终结果以对应 Actions 和 Release 资产为准。
