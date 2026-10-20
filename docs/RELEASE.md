# 发布流程规范（RELEASE）

> 本文档是各仓库统一的标准发布流程。**每次发版都按本文档执行**，避免出现
> 「Release 页只有 Full Changelog 链接」「安装包缺失」「版本号不一致」这类不规范情况。

## 1. 版本号约定

- 版本号唯一来源：根目录 `Directory.Build.props` 的 `<Version>`（个别仓库在主 csproj，如 IEditor/QuickApp）。
- 发布 tag 必须与该版本一致：`<Version>1.2.3</Version>` ↔ tag `v1.2.3`。
- README 的「当前版本」行（如有）同步更新。
- 更新日志：有 `UpdateLog.md` 的仓库，发布前新增对应版本条目（`## 版本 (YYYY-MM-DD)`）。

## 2. 标准发布流程

开发联调使用本地 NuGet 包，不等待 NuGet 平台发布，不用 Tag 验证尚未完成的功能。
可用 `scripts/prepare_local_libraries.ps1 -PackageVersion <新版本>` 在仓外打包并还原；
临时源配置与产物不提交。本地恢复使用该脚本输出的 `NuGet.local.config`，避免添加跨仓项目引用。

```text
1. 改版本号（Directory.Build.props / csproj）+ 更新 UpdateLog.md + 同步 README 版本行
2. 使用本地共享库包完成编译、功能回归、Windows NativeAOT 运行和安装器门禁；记录其他平台尚待真实验收的边界
3. 全部本地验证通过后，按修改范围分别用中文提交，最终版本提交使用“发布：升级 X.Y.Z 并更新发布说明”
4. 将完整发布说明保存为仓外 UTF-8 文本，再打附注 tag（见下节）：
       git tag -a vX.Y.Z -F <发布说明文件路径>
5. 推送分支与 tag：git push origin <branch> && git push origin vX.Y.Z
6. CI（release.yml）自动：测试 → 全平台 NativeAOT 发布 → 制作安装包 → 创建 GitHub Release
7. 到 Release 页确认：说明可读、各平台安装包齐全（win exe / linux deb / mac dmg）
```

共享库有新版本时，在上述本地验证完成后才推送共享库 Tag。正式 Vex 发布 CI 使用线上包，
因此发布阶段先确认对应包已可还原，再推送 Vex Tag；这一步不阻塞开发和本地验证。

## 3. 发布说明规范（重要）

**Release 页的说明来自附注 tag 的消息**。打 tag 时必须写清楚：

发布说明文件使用真实换行，例如：

```text
应用名 X.Y.Z

本版本做了什么（第一段概览）

- 变更要点 1
- 变更要点 2
```

- 工作流的「准备发布说明」步骤会按以下优先级取内容：
  1. 精选说明 `.github/release-notes/vX.Y.Z.md`（存在则优先）；
  2. **附注 tag 消息**（`git tag -l vX.Y.Z --format='%(contents)'`）；
  3. 兜底：仅版本标题 + 自动生成的 Full Changelog（**不允许停留在这一档**）。
- 不确定时，发布后打开 Release 页检查说明是否可读。

## 4. 平台与安装包矩阵

| 平台 | 产物 | 说明 |
| --- | --- | --- |
| win-x64 | `.setup.exe` | NativeAOT |
| linux-x64 / linux-arm64 | `.deb`（amd64/arm64） | NativeAOT；arm64 在 arm runner 上原生编译 |
| osx-x64 / osx-arm64 | `.dmg` | NativeAOT；保留符号（Apple ld_classic 不支持压缩调试段） |

仅发布以上五个 RID 的安装包及对应 `.sha256`，不发布 ZIP 或 win-x86 产物。

- 统一配方：`-p:PublishAot=true -p:PublishTrimmed=true -p:PublishSingleFile=false
  -p:IlcGenerateCompleteTypeMetadata=true -p:IlcTrimMetadata=false -p:IlcSingleThreaded=true -p:IlcUseServerGc=false`。
- Linux runner 需 `clang zlib1g-dev`；不要在 x64 runner 上交叉编译其他架构的 AOT。
- 平台相关功能未适配时，应用内以「当前平台功能正在开发中」友好提示，不阻塞发布与启动。

## 5. 发布后检查

- Release 页：说明可读、资产齐全（对照矩阵）、带 `.sha256` 的照旧。
- 安装包抽查：至少在 Windows 本机跑一次发布产物。
