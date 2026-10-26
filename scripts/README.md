# Scripts 目录结构说明

本目录包含 Vex 项目的所有脚本和工具。为便于维护，已按功能分类整理。

## 📁 目录结构

### 🔨 build/ - 构建相关脚本
构建、清理和发布相关的脚本。

- **publish.ps1** - 主发布脚本，用于发布各平台版本
- **prepare_local_libraries.ps1** - 准备本地依赖库
- **verify_native_publish.ps1** - 验证 Native AOT 发布结果
- **clean-development-artifacts.ps1** - 清理开发过程中的临时产物

**常用命令：**
```powershell
# 发布所有平台版本
.\build\publish.ps1

# 清理开发产物
.\build\clean-development-artifacts.ps1
```

---

### 📦 packaging/ - 打包相关脚本
生成各平台安装包和验证工具。

- **package_windows.ps1** - 打包 Windows 版本
- **package_linux_deb.ps1** - 打包 Linux DEB 安装包
- **package_macos.sh** - 打包 macOS 应用
- **package_vex_msix.ps1** - 打包 Windows MSIX 格式
- **Vex.iss** - Inno Setup 安装脚本
- **verify_release_installers.ps1** - 验证安装包完整性

**常用命令：**
```powershell
# Windows 打包
.\packaging\package_windows.ps1

# Linux 打包
.\packaging\package_linux_deb.ps1

# macOS 打包
bash ./packaging/package_macos.sh
```

---

### 🛠️ tools/ - 开发工具脚本
用于开发过程的辅助工具和生成脚本。

- **generate-menu-icon-geometry.cjs** - 生成菜单图标几何路径
- **generate-menu-icon-geometry.test.cjs** - 图标生成测试
- **render-product-icons.cjs** - 渲染产品图标
- **sync_native_design.cjs** - 同步原生设计资源
- **sync_native_locales.cjs** - 同步本地化文件
- **mcp_stdio_bridge.py** - MCP stdio 协议桥接工具
- **generate_stress_documents.py** - 生成压力测试 Markdown 文档

**常用命令：**
```bash
# 生成菜单图标
node tools/generate-menu-icon-geometry.cjs

# 同步设计资源
node tools/sync_native_design.cjs

# 同步语言文件
node tools/sync_native_locales.cjs
```

---

### 🧪 testing/ - 测试相关脚本
性能和功能测试工具。

- **stress_vex_markdown_services.ps1** - Markdown 服务压力测试

**常用命令：**
```powershell
# 运行压力测试
.\testing\stress_vex_markdown_services.ps1
```

---

### ⚙️ system/ - 系统集成脚本
操作系统级别的集成功能。

- **register_vex_context_menu.ps1** - 注册右键菜单（"用 Vex 打开"）
- **unregister_vex_context_menu.ps1** - 注销右键菜单

**常用命令：**
```powershell
# 注册右键菜单（需管理员权限）
.\system\register_vex_context_menu.ps1

# 注销右键菜单
.\system\unregister_vex_context_menu.ps1
```

---

### 📋 release/ - 发布相关脚本
准备发布说明和版本信息。

- **prepare_release_notes.ps1** - 生成发布说明（PowerShell）
- **prepare_release_notes.bat** - 生成发布说明（批处理）

**常用命令：**
```powershell
# 准备发布说明
.\release\prepare_release_notes.ps1
```

---

## 📂 其他子目录

### Languages/
语言和本地化资源文件。

### installer/
安装器相关文件和资源。

### native-design/
原生设计规范和资源。

### tests/
测试项目和测试代码。

### 验证项目
以下目录包含各类功能的可视化验证工具：
- **about-visual-verification/** - 关于页面可视化验证
- **performance-verification/** - 性能验证工具
- **settings-verification/** - 设置界面验证
- **ui-verification/** - UI 组件验证
- **update-verification/** - 更新功能验证

---

## 🚀 快速开始

### 开发构建流程
```powershell
# 1. 准备本地库
.\build\prepare_local_libraries.ps1

# 2. 发布版本
.\build\publish.ps1

# 3. 打包
.\packaging\package_windows.ps1
```

### 工具使用流程
```bash
# 同步设计资源
node tools/sync_native_design.cjs

# 同步语言文件
node tools/sync_native_locales.cjs

# 生成图标
node tools/generate-menu-icon-geometry.cjs
```

---

## 📝 脚本依赖

### PowerShell 脚本
- Windows PowerShell 5.1+ 或 PowerShell Core 7+
- .NET SDK 10.0+

### Node.js 脚本
- Node.js 18+
- npm 依赖（根据各脚本要求）

### Python 脚本
- Python 3.8+
- 依赖包见各脚本开头注释

---

## 🔗 相关文档

- [构建文档](../docs/构建指南.md)
- [发布流程](../docs/发布流程.md)
- [Native AOT 优化说明](../docs/Native-AOT-优化说明.md)

---

**最后更新：** 2026/10/27
