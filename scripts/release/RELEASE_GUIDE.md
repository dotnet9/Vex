# GitHub 发布脚本使用指南

## 功能说明

`release_to_github.ps1` / `release_to_github.bat` 是完整的 GitHub 发布自动化脚本，执行以下操作：

1. ✅ 构建 Native AOT 发布包（win-x64）
2. ✅ 创建 Git 标签
3. ✅ 推送标签到 GitHub
4. ✅ 创建 GitHub Release 并上传发布包

## 前置要求

1. **PowerShell 7+** - 执行脚本需要
2. **GitHub CLI (gh)** - 创建 Release 需要
   - 安装: https://cli.github.com/
   - 配置: `gh auth login`
3. **.NET 10 SDK** - 构建 Native AOT 需要
4. **Native AOT 工具链** - Windows 需要 Visual Studio 构建工具

## 基本用法

### 1. 发布当前版本（从 Directory.Build.props 读取）

```powershell
scripts\release\release_to_github.bat
```

这会：
- 读取 `Directory.Build.props` 中的 `<Version>` (当前 1.10.4)
- 构建 `win-x64` 平台的 Native AOT 发布包
- 创建标签 `v1.10.4`
- 推送到 GitHub
- 创建 Release 并上传 `Vex-1.10.4-win-x64.zip`

### 2. 指定版本号发布

```powershell
scripts\release\release_to_github.bat -Version 1.11.0
```

### 3. 跳过构建（已经构建好了）

```powershell
scripts\release\release_to_github.bat -SkipBuild
```

### 4. 创建草稿 Release

```powershell
scripts\release\release_to_github.bat -Draft
```

Release 将不会立即发布，需要在 GitHub 上手动发布。

### 5. 创建预发布版本

```powershell
scripts\release\release_to_github.bat -Prerelease
```

标记为预发布版本（Beta/RC 等）。

### 6. 自定义发布说明

```powershell
scripts\release\release_to_github.bat -ReleaseNotes "## 更新内容`n- 优化 AOT 体积，减少 50%`n- 修复已知问题"
```

不指定时会自动生成发布说明。

## 完整示例

```powershell
# 示例 1: 完整发布流程（推荐）
scripts\release\release_to_github.bat

# 示例 2: 发布新版本 1.11.0
scripts\release\release_to_github.bat -Version 1.11.0

# 示例 3: 创建草稿，手动检查后再发布
scripts\release\release_to_github.bat -Draft

# 示例 4: 预发布版本（Beta）
scripts\release\release_to_github.bat -Version 1.11.0-beta.1 -Prerelease

# 示例 5: 已经构建好，只创建 Release
scripts\release\release_to_github.bat -SkipBuild

# 示例 6: 完整的自定义发布
scripts\release\release_to_github.bat -Version 1.11.0 -Draft -ReleaseNotes "测试版本"
```

## 执行流程

```
┌─────────────────────────────────┐
│ 1. 检查 Git 状态                │
│    - 检查未提交的更改           │
│    - 检查标签是否已存在         │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 2. 构建 Native AOT 发布包       │
│    - 执行 publish_aot.ps1       │
│    - 生成 win-x64 发布包        │
│    - 输出到 publish/packages/   │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 3. 创建 Git 标签                │
│    - git tag -a v1.10.4         │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 4. 推送标签到 GitHub            │
│    - git push origin v1.10.4    │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 5. 创建 GitHub Release          │
│    - gh release create          │
│    - 上传 ZIP 文件              │
└─────────────────────────────────┘
```

## 输出位置

- **构建产物**: `publish/artifacts/win-x64/Vex/`
- **发布包**: `publish/packages/Vex-<版本>-win-x64.zip`
- **GitHub Release**: https://github.com/sunyonghuan/Vex/releases

## 故障排除

### 问题 1: "标签已存在"

```powershell
# 删除本地标签
git tag -d v1.10.4

# 删除远程标签
git push origin --delete v1.10.4

# 然后重新运行发布脚本
```

### 问题 2: "GitHub CLI 未登录"

```powershell
# 登录 GitHub
gh auth login

# 选择 GitHub.com
# 选择 HTTPS 或 SSH
# 按提示完成认证
```

### 问题 3: "构建失败"

检查前置要求：
- .NET 10 SDK: `dotnet --version`
- Native AOT 工具链: Visual Studio 构建工具
- PowerShell 7: `pwsh --version`

### 问题 4: "工作区有未提交的更改"

脚本会提示是否继续，可以选择：
- 提交更改: `git add . && git commit -m "message"`
- 忽略并继续: 在提示时输入 `y`

## 注意事项

⚠️ **重要提醒**:
1. 发布前确保代码已经提交到 master 分支
2. 版本号必须唯一，不能重复发布相同版本
3. 标签一旦推送到 GitHub 就很难撤销，请谨慎操作
4. 建议先使用 `-Draft` 创建草稿，检查无误后再发布

## 手动发布流程（供参考）

如果不想使用自动化脚本，可以手动执行：

```powershell
# 1. 构建
scripts\release\publish_aot.bat win-x64

# 2. 创建标签
git tag -a v1.10.4 -m "Release 1.10.4"

# 3. 推送标签
git push origin v1.10.4

# 4. 创建 Release
gh release create v1.10.4 publish/packages/Vex-1.10.4-win-x64.zip --title "Vex 1.10.4" --generate-notes
```
