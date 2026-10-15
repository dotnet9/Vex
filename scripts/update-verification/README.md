# 更新检查验证

验证「关于」浮层的 16 个更新界面状态场景，使用受控的 `IUpdateService`，不访问网络或启动真实安装器。覆盖当前版本、失败重试、缺少校验文件 / 平台资产时跳转发布页、下载成功、校验失败、安装退出条件、重复检查拦截、取消、动态切语和释放订阅。

```powershell
dotnet run --project scripts/update-verification/UpdateVerification.csproj

```

失败返回非零退出码。网络发现、选包和下载校验由 Toolkit 的更新组件及其测试负责；本脚本验证应用界面状态与外部操作边界。
