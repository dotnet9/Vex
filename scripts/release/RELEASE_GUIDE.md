# GitHub 发布脚本使用指南

## 功能说明

`release_to_github.ps1` / `release_to_github.bat` 是完整的 GitHub 发布自动化脚本，执行以下操作：

1. ✅ **自动更新版本号**（递增补丁版本）
2. ✅ 构建 Native AOT 发布包（win-x64）
3. ✅ 创建 Git 标签
4. ✅ 推送标签到 GitHub
5. ✅ 创建 GitHub Release 并上传发布包

## 前置要求

1. **PowerShell 7+** - 执行脚本需要
2. **GitHub CLI (gh)** - 创建 Release 需要
   - 安装: https://cli.github.com/
   - 配置: `gh auth login`
3. **.NET 10 SDK** - 构建 Native AOT 需要
4. **Native AOT 工具链** - Windows 需要 Visual Studio 构建工具

## 基本用法

### 1. 自动发布（推荐）

```powershell
scripts\release\release_to_github.bat
```

这会：
- 读取当前版本（如 1.10.4）
- 提示使用建议版本（1.10.5，自动递增）
- 更新 `Directory.Build.props` 中的版本号
- 提交版本号更改
- 构建 `win-x64` 平台的 Native AOT 发布包
- 创建标签 `v1.10.5`
- 推送到 GitHub
- 创建 Release 并上传 `Vex-1.10.5-win-x64.zip`

### 2. 指定版本号发布

```powershell
scripts\release\release_to_github.bat -Version 1.11.0
```

跳过交互式版本选择，直接使用指定版本。

### 3. 保持当前版本号（不递增）

```powershell
scripts\release\release_to_github.bat -SkipVersionUpdate
```

使用 `Directory.Build.props` 中的当前版本，不更新版本号。

### 4. 跳过构建（已经构建好了）

```powershell
scripts\release\release_to_github.bat -SkipBuild
```

### 5. 创建草稿 Release

```powershell
scripts\release\release_to_github.bat -Draft
```

Release 将不会立即发布，需要在 GitHub 上手动发布。

### 6. 创建预发布版本

```powershell
scripts\release\release_to_github.bat -Prerelease -Version 1.11.0-beta.1
```

标记为预发布版本（Beta/RC 等）。

### 7. 自定义发布说明

```powershell
scripts\release\release_to_github.bat -ReleaseNotes "## 更新内容`n- 优化 AOT 体积，减少 50%`n- 修复已知问题"
```

不指定时会自动生成发布说明。

## 完整示例

```powershell
# 示例 1: 完整发布流程（推荐）
# 自动递增版本号 1.10.4 -> 1.10.5
scripts\release\release_to_github.bat

# 示例 2: 发布新的大版本
scripts\release\release_to_github.bat -Version 2.0.0

# 示例 3: 创建草稿，手动检查后再发布
scripts\release\release_to_github.bat -Draft

# 示例 4: 预发布版本（Beta）
scripts\release\release_to_github.bat -Version 1.11.0-beta.1 -Prerelease

# 示例 5: 已经构建好，只创建 Release
scripts\release\release_to_github.bat -SkipBuild -SkipVersionUpdate

# 示例 6: 完整的自定义发布
scripts\release\release_to_github.bat -Version 1.11.0 -Draft -ReleaseNotes "测试版本"
```

## 版本号管理

### 自动递增规则

脚本默认自动递增**补丁版本号**：
- 当前版本 `1.10.4` → 建议版本 `1.10.5`
- 当前版本 `2.3.9` → 建议版本 `2.3.10`

### 交互式版本选择

运行脚本时，会提示：

```
当前版本: 1.10.4
建议版本: 1.10.5 (自动递增)

使用建议版本? (Y/n/输入自定义版本号):
```

- 回车或输入 `y` → 使用建议版本（1.10.5）
- 输入 `n` → 使用当前版本（1.10.4），不更新
- 输入自定义版本号 → 使用自定义版本（如 `2.0.0`）

### 版本号格式

支持的版本号格式：
- ✅ `1.10.4` (主版本.次版本.补丁版本)
- ✅ `1.10.4.1` (四位版本号)
- ✅ `2.0.0-beta.1` (预发布版本)

### 版本更改提交

脚本会自动：
1. 更新 `Directory.Build.props` 中的 `<Version>` 标签
2. 提交更改：`chore: bump version to x.x.x`
3. 继续执行发布流程

## 执行流程

```
┌─────────────────────────────────┐
│ 1. 版本号管理                  │
│    - 读取当前版本               │
│    - 自动递增建议版本           │
│    - 用户确认或自定义           │
│    - 更新并提交版本号           │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 2. 检查 Git 状态                │
│    - 检查未提交的更改           │
│    - 检查标签是否已存在         │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 3. 构建 Native AOT 发布包       │
│    - 执行 publish_aot.ps1       │
│    - 生成 win-x64 发布包        │
│    - 输出到 publish/packages/   │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 4. 创建 Git 标签                │
│    - git tag -a v1.10.5         │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 5. 推送标签到 GitHub            │
│    - git push origin v1.10.5    │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 6. 创建 GitHub Release          │
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
