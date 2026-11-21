# Vex 2.2.0.0：让 .NET 11 NativeAOT 跑在 Windows 7 上

## 背景

Vex 主分支用 .NET 11 NativeAOT 发布 win-x64。Windows 7 x64 用户启动即报「丢失 api-ms-win-core-synch-l1-2-0.dll」——静态导入由加载器在进程第一行代码运行前解析，找不到就直接终止，连主界面都没见过。

缺失的这个 DLL 是 Windows 8 引入的 API Set，Win7 的加载器 schema 里没有这个契约；KB2999226 只补 UCRT（api-ms-win-crt-*），不覆盖它。

此前 Win7 用户唯一的选择是冻结的 win7-archive 分支（.NET 10 + 旧 SkiaSharp），停在了 1.20.1.0，与主线的功能差距只增不减。V2.2.0.0 把兼容做回主线：CRT 换轨、新系统 API 走 thunk、预编译库加垫片，三层各解决一段。

## 根因定位：PE 导入表

报错只说缺哪个模块，「谁引入了它」要靠证据。做法是逐模块扫描 exe 和全部 DLL 的 PE 导入表，结果三个模块静态引入了 Win8+ 的 WaitOnAddress / WakeByAddressSingle：

1. **Vex.exe 本体**——.NET 11 AOT 运行时 PAL（平台抽象层）的线程等待原语；
2. **libSkiaSharp.dll**——SkiaSharp 官方预编译；
3. **av_libglesv2.dll**——GLES 加载器，Avalonia 渲染链路依赖。

再对比 v2.1.0.1 与 v2.1.0.2 的导入表，逐项一致——这不是某次升级引入的回归，而是 .NET AOT 运行时的固有关联。微软官方对 .NET 的 Windows 门槛已抬到 Windows 10（.NET 8 起），AOT 运行时不再为老系统保留兼容路径，PAL 直接静态链接新系统的同步原语。只要用这个运行时，导入表里就有它，链接期之外无从回避。

## 三层方案

三层各管一段：主程序的 CRT、主程序的新系统 API、预编译库的新系统 API。

### 第一层：VC-LTL 5.3.1——主程序 CRT

主程序对 UCRT 的依赖以 api-ms-win-crt-* 形式出现在导入表里，Win7 上这套 api-set 同样不存在。VC-LTL 把 CRT 链接目标从 UCRT api-set 换成 msvcrt.dll——Win7 自带，无需任何 KB 或运行库安装包。

效果在导入表上直接可见：主程序的 api-ms-win-crt-* 全部消失。

### 第二层：YY-Thunks 1.2.4-beta.2——主程序的新系统 API

CRT 之外，.NET 11 AOT 运行时 PAL 还静态引入了 Win8+ 的 WaitOnAddress 等同步原语。YY-Thunks 在链接期把这类 API 的引用替换为自带的 thunk 代码：Win7 上走模拟实现，新系统上直接转发原生 API。

Vex.csproj 里就是两个 PackageReference（版本定在 build/Versions.props）：

```xml
<PackageReference Include="VC-LTL" />
<PackageReference Include="YY-Thunks" />
```

两层叠加后，主程序导入表 api-ms 全消（实测）。

### 第三层：垫片 DLL——预编译库管不到的部分

VC-LTL 和 YY-Thunks 只作用于自己参与链接的模块。libSkiaSharp.dll 与 av_libglesv2.dll 是官方预编译的二进制，既不能重链 CRT，也不能注入 thunk——它们导入表里的 api-ms-win-core-synch-l1-2-0 原样保留。

办法是自己写一个同名垫片（native/win7-shim/synch_shim.c，105,472 字节），导出三个函数名与 OS 一致，shim.def 做导出映射：

```c
LIBRARY api-ms-win-core-synch-l1-2-0
EXPORTS
    WaitOnAddress       = shim_wait
    WakeByAddressSingle = shim_wake_one
    WakeByAddressAll    = shim_wake_all
```

WaitOnAddress 的语义是「值变化才醒」：目标地址当前值与调用方给的比较值不同即视为已变化，立即返回；相同则阻塞。Win7 就有 SRWLOCK 和 CONDITION_VARIABLE，足够模拟这套 futex 语义——全局一把 SRWLOCK 保护 64 个等待块，等待方锁内比对，没变就带剩余超时睡在条件变量上，被唤醒后回循环重新比对：

```c
BOOL woke = SleepConditionVariableSRW(&blk->cv, &gate, remaining, 0);
blk->waiters--;
if (blk->waiters == 0) blk->used = FALSE;
ReleaseSRWLockExclusive(&gate);
if (!woke) return FALSE;
```

唤醒方按地址找到等待块，WakeConditionVariable / WakeAllConditionVariable 分别对应 WakeByAddressSingle / WakeByAddressAll。垫片自身只依赖 kernel32，导入表 77 个函数全是 Win7 原生导出——垫片不会引入新的兼容问题。

## 放置策略

垫片放 exe 同目录，靠两个加载器行为分叉：

- **Windows 8+**：导入名先经 API Set schema 解析再落到物理 DLL，应用目录里的同名文件根本不参与解析——本机 Win11 带垫片实测运行正常；
- **Windows 7**：schema 里没有这个契约，加载器走标准搜索顺序，exe 目录最先命中垫片。

复制不需要手动。Vex.csproj 的一个 Content 项限定 Windows RID，构建/发布输出自动带上，publish.ps1、publish-win-x64.bat 与 CI/CD 全部自动包含：

```xml
<Content Include="..\..\native\win7-shim\api-ms-win-core-synch-l1-2-0.dll"
         CopyToOutputDirectory="PreserveNewest"
         Link="api-ms-win-core-synch-l1-2-0.dll"
         Condition="'$(RuntimeIdentifier)' == 'win-x64' or '$(RuntimeIdentifier)' == 'win-x86' or '$(RuntimeIdentifier)' == ''" />
```

Condition 排除了 linux 与 macOS 的 RID，非 Windows 发布不含此 DLL。只有改 synch_shim.c 才需要重跑 native/win7-shim/build.cmd 重新预构建。

## 验证

- **PE 导入表扫描**：Vex.exe 与垫片合计 0 个 api-ms-win-core-synch-l1-2-0 导入——主程序的两处由 VC-LTL 与 YY-Thunks 消除，libSkiaSharp / av_libglesv2 残留的两条由垫片覆盖。
- **本机 Win11 冒烟**：AOT 发布产物含垫片，`--verify-ui` 64 次渲染零操作错误——兼容改动没有破坏 Win8+ 路径。
- **Win7 x64 真机**：启动进入主界面（用户实测）。

本地门禁 scripts/verify-release.ps1 与 CI 同源，此后每次 tag 推送前都会重复整套 Win11 侧验证；Win7 真机验收随版本发布由用户实测覆盖。

## 附带改动

- **VC-LTL + YY-Thunks 接线入库**：src/Vex/Vex.csproj 两个 PackageReference，版本（VC-LTL 5.3.1、YY-Thunks 1.2.4-beta.2）定在 build/Versions.props，Directory.Packages.props 中央声明。
- **垫片预构建 DLL 入库**：105KB 的 api-ms-win-core-synch-l1-2-0.dll 与源码、shim.def、build.cmd 一起放在 native/win7-shim/，MSBuild Content 自动复制。
- **build.cmd 改 vswhere 动态定位 VS**：不再写死 VS 安装路径，换电脑或 CI 上重新编译垫片不再受环境约束。
- **pre-push 门禁**：.githooks/pre-push 拦截 v*（应用发布）与 lib-v*（库包发布）tag，推送前强制本地验证——verify-release.ps1 覆盖构建、三套 xunit、UiVerification 十二场景、win-x64 AOT 发布与三大 AOT 验证器，约 15 分钟；库包 tag 走 verify-libraries.ps1。失败阻止推送，--no-verify 是逃生口。
- **外观菜单密度切换修复**：此前的重建实现让已打开弹层不变宽窄，验证器捕获的旧控件引用也失联。宽度改为绑定 VexUiAppearanceMenuWidth 资源，密度切换时已打开的弹层即时缩放。
- **CodeWF.Avalonia.Controls 12.1.6.9**：面板回归修复随四库统一版本线进入。

## 收尾

win7-archive 分支正式退役删除，文档四处口径同步更新——主线已兼容，冻结分支没有存在理由。版本 V2.2.0.0。

一条主线，Win7 到 Win11，维护成本回到一份代码。
