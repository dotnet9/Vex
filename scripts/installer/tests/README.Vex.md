# Vex 安装器外观回归

生产入口 `scripts/Vex.iss` 使用 `Vex.Setup.iss`。外观层不读取 HTML，不依赖 WebView，不分发字体；共享维护快照及其来源摘要保持完整。

Windows 首屏为 520×366，展开设置为 520×572，维护为 520×448。单像素描边沿整个圆角和右下切口绘制，浅深色均来自原型配色；字体使用系统 Segoe UI / Microsoft YaHei UI。修复和卸载作为直接操作显示，完整路径及多作用域选择放入“安装详情”；未知位置不虚构路径，未验证卸载器时禁用直接卸载。

许可在原窗口中显示，保留 UTF-8 中文、只读复制、滚轮与原生键盘滚动。文件占用、路径错误、取消确认、回滚及真正卸载仍使用 Inno/Windows 的真实对话框，不模拟业务进度。更新就绪事件仍在真实主窗口显示后触发。

## 运行

需要 PowerShell 7、Inno Setup 6；截图参考另外需要 Node、`design/tests` 的 npm 依赖、Chromium/Edge 和 Pillow。

```powershell
$env:VEX_BROWSER_EXECUTABLE='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
node scripts/installer/tests/capture_vex_prototype.cjs D:/temp/vex-reference
./scripts/installer/tests/verify_vex_visual.ps1 `
  -Compiler 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe' `
  -Output D:/temp/vex-native -ReferenceDirectory D:/temp/vex-reference
```

使用新的输出目录。夹具只复制 LICENSE，不写真实产品注册，不建快捷方式，不执行应用或卸载器。完成页的启动选项没有对应执行项。独立桌面不会切换用户当前桌面，测试结束按持有句柄清理夹具后代进程。

矩阵包含中英文×浅深色的首页、折叠设置、许可、真实安装进度、完成、维护，以及未知路径、多安装作用域。交互检查包括更新就绪握手、四边描边、按钮尺寸、许可滚动、安装选项传递、只读路径、取消和复制载荷。Actions 运行原生矩阵并上传截图/JSON；固定字体环境中的参考图对照使用 `-ReferenceDirectory`。

## 视觉分数的含义

`compare_vex_visual.py` 输出未平滑 RGB 分数和两图各做 1px 高斯平滑后的 RGB 分数，公式为 `100 × (1 − RGB 平均绝对误差 / 255)`。后者阈值 98%，用于容许 GDI 与 Chromium 的字体栅格化差异。仅排除窗口区域以外的角落及机器相关路径文字，按钮、维护详情入口和许可正文都参与比较。尺寸最多容许 1px 浏览器取整差异，窗口边缘另有真实像素检查。

该分数不能直接解释为主观视觉或体验的“98%”，尤其不能推及系统确认框、真实产品覆盖安装、不同 DPI 和 Linux/macOS 桌面。它和完整截图、控件几何、交互证据一起评审。Linux 软件管理器和 macOS Finder 的系统外观由操作系统控制。
