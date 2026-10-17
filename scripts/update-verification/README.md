# 更新检查验证

验证独立更新浮层和 Windows 安装交接的 40 个场景，使用受控的 `IUpdateService` 和启动替身，不访问网络或运行产品安装器。覆盖当前版本、失败重试、缺少校验文件 / 平台资产时跳转发布页、下载成功、校验失败、关闭后恢复下载、安装重试复用下载、重复检查、取消、动态切语和释放订阅。

Windows 使用随机命名事件，只有安装窗口显示后确认就绪才允许退出。额外验证启动失败、无就绪信号、启动异常、取消，以及中文和空格路径。真实 Inno 的窗口信号由 `scripts/installer/tests/verify_vex_fixture.ps1` 验证，夹具只复制 LICENSE，不注册或安装 Vex。`scripts/ui-verification` 的 `updates` 模式验证菜单与关于共用同一会话、关闭继续下载、重复入口去重、四语浅深主题及安装失败后保留界面。

```powershell
dotnet run --project scripts/update-verification/UpdateVerification.csproj

```

失败返回非零退出码。网络发现、选包和下载校验由 Toolkit 的更新组件及其测试负责；本脚本验证应用界面状态与外部操作边界。
