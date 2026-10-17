# Vex 1.6.0 发布前验证

2026-10-17，Windows x64，.NET 10。正式依赖为 nuget.org 的 CodeWF.Avalonia 12.1.4.28；五个依赖包均已公开可下载，NuGet 缓存元数据确认来源为官方源。仓库 NuGet.Config 仅配置 nuget.org，不使用本地预览包或跨仓项目引用。

- 共享库正式版本：338 项测试通过，云端构建、测试、原型及安装包契约检查通过。
- Vex 解决方案和离屏验证器在独立输出目录重新构建，均为零错误；仍有现有可空和裁剪分析警告。
- 正式依赖下应用回归 656 项、HTML 表格定位专项 23 项通过；定位专项确认五张真实表格在阅读和即时编辑中进入视口，演示显示单元格且原文不变。
- 七项浏览器原型审计通过，覆盖八种布局、即时编辑、引导、四语及演示的 320 次翻页。
- 五平台安装包门禁、Linux 双架构安装入口、macOS 资源布局及失败传播、单文件原型和换行一致性检查通过。本机的 Linux/macOS 检查使用受控替身。

日志和截图位于 `D:/github/temp_files/vex-native/`：`library-release-12.1.4.28.log`、`release160-build.log`、`release160-verifier-build.log`、`release160-vex.log`、`release160-html-tables.log`、`prototype-release-1.6.0.log` 及 `release160-html-tables/`。

正式安装包由 `v1.6.0` 触发 GitHub Actions，分别制作 Windows x64 EXE、Linux x64 / arm64 DEB、macOS x64 / arm64 DMG 及 SHA256。发布门禁拒绝额外 ZIP。上述离屏及结构检查不代替正式 NativeAOT 构建、真实安装和各平台桌面验收；此前本地原生调试验证的范围见[原型对照验收](../design/原型对照验收.md)。
