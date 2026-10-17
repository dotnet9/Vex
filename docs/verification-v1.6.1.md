# Vex 1.6.1 发布前验证

2026-10-17，Windows x64，.NET 10，Inno Setup 6.7.2。共享库沿用正式 NuGet 依赖，无跨仓项目引用。

- Windows 安装器：修复许可证弹窗缺参，中文和空格独立目录真编译通过。发布流水线在五平台 NativeAOT 构建前先编译受控载荷。
- 更新状态与安装交接：40 项检查通过，覆盖更新失败、SHA256 失败、缺失资产、重复检查、取消、恢复下载、安装失败后复用文件和 Windows 启动异常。只在收到随机命名事件后允许退出；单纯创建进程不代表就绪。
- 真实 Inno：独立桌面的安全夹具收到窗口显示后的就绪信号；许可证、选项、安装及完成交互通过。夹具仅复制 LICENSE，不写注册、不建快捷方式、不启动或安装 Vex。
- 应用浮层：33 项检查通过，菜单和关于复用同一个模型，关闭后下载继续；重复入口不叠加界面，安装失败保持运行；四语、浅深主题、980 × 640 的布局与按钮可达性通过。
- 解决方案 Release 构建零错误，保留既有警告；应用回归 656 项通过，关于元数据与四语布局通过。
- 离线原型：同步统一更新窗口，覆盖七种更新场景、重试、关闭继续下载、入口复用与四语。

本机证据位于 `D:/github/temp_files/vex-native/`：`update-verification.log`、`update-build.log`、`update-ui.log`、`update-ui/`、`update-prototype.log`、`update-fixture/zh-CN-light2/verification.json`。后续发布复验以 `release161-` 开头。

正式五平台安装器由新标签 `v1.6.1` 触发 Actions，不移动已有 `v1.6.0` 标签。只发布 EXE / DEB / DMG 与 SHA256，拒绝便携 ZIP。上述检查不代替真实产品覆盖安装及 Linux/macOS 桌面验收。
