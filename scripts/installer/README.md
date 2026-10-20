# Vex 安装器

Vex 的实际安装包由本目录及 `scripts/Vex.iss` 独立维护，只消费发布目录载荷，不读取相邻仓库或应用源码。应用发布完成后，安装器只消费指定发布目录中的载荷，无需为修改安装界面重新构建应用。

Windows 产品入口使用 `Vex.Setup.iss`：默认约 520 × 366，右下角单切口，系统浅深主题，一键安装，目录与两个安装选项折叠在“自定义安装”。MIT 原文通过可选窗口查看，原生安装引擎继续负责进度、文件占用、取消、回滚与启动应用。维护沿用精确 AppId 探测与已注册卸载器校验；卸载保留系统原生确认与进度窗口。静默安装继续使用 Inno 的命令行参数。

Vex 品牌固定为 **维刻 / Vex**，简体中文 Slogan 为 **维刻 Vex · 写下想法，看见脉络。**。macOS 使用精简的 Finder 拖放背景，Linux 使用标准软件管理器读取的产品元数据。二者的系统授权与复制、安装、移除交互仍由操作系统处理。

独立验证入口：`tests/Vex.Fixture.iss` 与 `tests/verify_vex_fixture.ps1`。夹具只将 LICENSE 复制到指定临时目录，不注册、不建快捷方式、不启动应用；在独立桌面验证，不切换用户桌面。生产脚本可用 `scripts/tests/windows_setup_contract_test.ps1 -Compile -InnoSetupCompiler <ISCC.exe>` 在独立副本中编译受控载荷。

## 共享基础快照

源目录为 CodeWF.Avalonia 的 `scripts/installer`。所有 App 与两个 Demo 在各自仓库保留完整快照，打包不读取相邻仓库。`source-sha256.json` 记录共享源摘要；文本统一 LF，位图保持二进制。

## Windows

保留的 `CodeWF.Setup.iss` 是共享基础快照，以下说明用于其独立夹具与来源校验；Vex 产品入口已切换到上面的专用外观。共享层使用 Inno Setup 6 的原生页面与 Windows GDI，主色为 `#512BD4`，无需 WebView、联网下载或第三方皮肤 DLL。

- 实际窗口区域为凹缺、突出左舱、右切角和阶梯底边。标题栏支持拖动与最小化，按钮保留原生键盘和焦点行为。
- 深色、浅色、跟随系统循环切换；默认深色。系统外观只读，不修改用户个性化设置。
- 八角 CodeWF 品牌核心、轨道与粒子响应真实安装进度，主色继续采用 .NET 紫。核心使用可聚焦的原生按钮，点击通过默认浏览器访问 `https://codewf.com`，不在安装时自动打开网页。
- 品牌动画和进度先在可复用离屏位图完成，再一次提交整帧；品牌按钮禁止背景擦除，整数百分比未变化时不重复更新文本和进度。减少动态效果时停止粒子及呼吸动画；隐藏和最小化时停止绘制。生产流程不注入测试等待时间。
- MIT 许可保留各仓库原文，界面支持简体中文和英文。许可、目录、安装任务和完成启动项由 Inno 正常处理。
- 维护入口只匹配自身精确 AppId。修复复用正常安装，卸载打开已验证的注册卸载器；成功后关闭安装向导，禁止继续安装。
- 独立卸载窗口使用相同品牌资源，百分比读取原生进度条。资源放在 `{app}\.codewf-installer` 并由 Inno 卸载日志精确清理。保留未登记文件，不递归清空安装目录。

使用 32 位 Inno Setup 引擎编译；产品载荷仍为 Windows x64 NativeAOT。当前 GDI 回调按该引擎 ABI 定义，升级编译器时必须保留引擎架构并运行原生夹具。

```powershell
ISCC.exe scripts/<产品名>.iss /DAppVersion=<版本> /DSourceDir=<NativeAOT发布目录> /DOutputDir=<安装包目录>
```

测试目录中的安装夹具不创建卸载注册、快捷方式或文件关联，也不启动产品。原生 GUI 验证器在独立桌面运行，不切换用户桌面；正式产品安装器只编译，不在开发机替用户执行安装。维护逻辑检查预处理结果，确认夹具排除了真实注册探测和卸载启动。

Windows、Linux、macOS 的安装器原型只维护在 [Vex-Flutter/design](https://github.com/dotnet9/Vex-Flutter/tree/main/design)。本仓库保留真实安装器实现和原生验证；发布及 CI 不要求克隆 Vex-Flutter。

## macOS 与 Linux

`macos/` 提供统一 DMG 背景与 Finder 拖放布局，生成 x64 / arm64 DMG；系统复制、覆盖、Gatekeeper 和授权仍由 macOS 处理。默认使用中英双语静态背景。可选 PKG 仍是原型提案，不新增第二种正式交付格式。

`linux/` 提供统一品牌与 AppStream 元数据，生成 x64 / arm64 DEB。软件管理器外观及授权受发行版控制，不能由包文件统一为 Windows 异形窗口。

所有平台交付真实 EXE / DEB / DMG 与 SHA-256，不用 ZIP 代替安装包。Windows 夹具或受控 shell 测试不等同于真实 Linux / macOS 安装验收；签名、公证、多 DPI、多显示器及读屏仍需要相应环境检查。
