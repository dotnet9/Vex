# 更新检查验证

验证「关于」窗口的版本检查：新版本、当前版本、重定向、HTTP 错误、网络超时、取消和无效版本均覆盖。每次检查最多请求一次网页，不请求 GitHub API 或安装包列表。

```powershell
dotnet run --project scripts/update-verification/UpdateVerification.csproj

# 加上真实 GitHub 验证，不修改本机设置或打开发布页
dotnet run --project scripts/update-verification/UpdateVerification.csproj -- --live
```

English: checks release detection and failure handling with a single web request, without API or asset-list fallback. The optional `--live` mode verifies GitHub without changing settings or opening the release page.
