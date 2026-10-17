[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [ValidateSet('english','chinesesimplified')][string]$Language='chinesesimplified',
    [string]$Output='',
    [ValidateSet('system','light','dark')][string]$Theme='system',
    [switch]$Cancel,
    [switch]$Maintenance,
    [switch]$UnknownLocation,
    [switch]$Multiple,
    [switch]$Migration
)
$ErrorActionPreference='Stop'
if (-not $IsWindows) { throw '原生安装夹具仅支持 Windows。' }
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Vex.Fixture.iss') -Raw
if ($source -notmatch '(?m)^Uninstallable=no$' -or $source -notmatch '(?m)^CreateUninstallRegKey=no$' -or
    $source -notmatch '(?m)^#define CodeWFSetupFixture$' -or $source -match '(?m)^\[(Registry|Icons|Run)\]') { throw '不安全的安装测试夹具。' }
$installerPath=(Resolve-Path -LiteralPath $Installer).Path
if ([IO.Path]::GetFileName($installerPath) -ne 'Vex.Installer.Fixture.exe') { throw '只运行专用界面测试夹具。' }
if (-not $Output) { $Output=Join-Path ([IO.Path]::GetTempPath()) ('vex-native-install-'+[guid]::NewGuid().ToString('N')) }
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $Output -Force | Out-Null
. (Join-Path $PSScriptRoot 'Fixture.Desktop.ps1')
$desktop=[IntPtr]::Zero
$window=[IntPtr]::Zero
$processInfo=[CodeWFSetupFixtureDesktop+ProcessInfo]::new()
$ownedHandles=[Collections.Generic.Dictionary[uint32,IntPtr]]::new()
$assertions=[Collections.Generic.List[string]]::new()
$updateEventName='Local\Vex.Update.'+[guid]::NewGuid().ToString('N')
$updateReady=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,$updateEventName)
function Check([bool]$Condition,[string]$Message) {
    if (-not $Condition) { throw $Message }; $assertions.Add($Message); Write-Host "PASS: $Message"
}
function Wait-For([scriptblock]$Predicate,[string]$Message) {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 25) { $value=& $Predicate; if ($value) { return $value }; Start-Sleep -Milliseconds 60 }
    throw "等待超时：$Message"
}
function Controls([IntPtr]$Parent=$window) {
    @([CodeWFSetupFixtureDesktop]::Children($Parent) | Where-Object { [CodeWFSetupFixtureDesktop]::IsWindowVisible($_) })
}
function Button([string]$Pattern,[IntPtr]$Parent=$window) {
    @(Controls $Parent | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -match '^(TNewButton|Button)$' -and
        ([CodeWFSetupFixtureDesktop]::Text($_) -replace '&','') -match $Pattern }) | Select-Object -First 1
}
function Click([IntPtr]$Control) {
    if ($Control -eq [IntPtr]::Zero) { throw '目标控件不存在。' }
    [void][CodeWFSetupFixtureDesktop]::PostMessage($Control,245,[IntPtr]::Zero,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 100
}
function Bounds([IntPtr]$Control) {
    $bounds=[CodeWFSetupFixtureDesktop+Rect]::new()
    [void][CodeWFSetupFixtureDesktop]::GetWindowRect($Control,[ref]$bounds)
    $bounds
}
function Register-Processes {
    if ($processInfo.process -eq [IntPtr]::Zero) { return }
    $processes=@(Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId)
    $known=[Collections.Generic.HashSet[uint32]]::new()
    foreach ($id in $ownedHandles.Keys) { [void]$known.Add($id) }
    [void]$known.Add([uint32]$processInfo.processId)
    do {
        $added=$false
        foreach ($item in $processes) {
            if ($known.Contains([uint32]$item.ParentProcessId) -and $known.Add([uint32]$item.ProcessId)) { $added=$true }
        }
    } while ($added)
    foreach ($id in $known) {
        if (-not $ownedHandles.ContainsKey($id)) {
            $handle=[CodeWFSetupFixtureDesktop]::OpenProcess(0x00100001,$false,$id)
            if ($handle -ne [IntPtr]::Zero) { $ownedHandles.Add($id,$handle) }
        }
    }
}
function Capture([string]$Name) { [CodeWFSetupFixtureDesktop]::Capture($window,(Join-Path $Output ($Name+'.png'))) }
try {
    $desktopName='Vex.Installer.Verify.'+[guid]::NewGuid().ToString('N')
    $desktop=[CodeWFSetupFixtureDesktop]::CreateDesktop($desktopName,[IntPtr]::Zero,[IntPtr]::Zero,0,0x01FF,[IntPtr]::Zero)
    if ($desktop -eq [IntPtr]::Zero) { throw '无法创建独立桌面。' }
    $startup=[CodeWFSetupFixtureDesktop+StartupInfo]::new()
    $startup.cb=[Runtime.InteropServices.Marshal]::SizeOf($startup)
    $startup.desktop='winsta0\'+$desktopName
    $destination=Join-Path $Output 'fixture payload'
    $command=[Text.StringBuilder]::new('"'+$installerPath+'" /SP- /LANG='+$Language+' /VEXTHEME='+$Theme+' /DIR="'+$destination+'" /LOG="'+(Join-Path $Output 'setup.log')+'"')
    [void]$command.Append(' /VEXUPDATEREADY='+$updateEventName)
    Check (-not $updateReady.WaitOne(0)) '安装向导未显示前不会确认就绪'
    if (-not [CodeWFSetupFixtureDesktop]::CreateProcess($installerPath,$command,[IntPtr]::Zero,[IntPtr]::Zero,$false,0,[IntPtr]::Zero,(Split-Path -Parent $installerPath),[ref]$startup,[ref]$processInfo)) { throw '无法启动独立夹具。' }
    $ownedHandles.Add([uint32]$processInfo.processId,$processInfo.process)
    $window=Wait-For { @([CodeWFSetupFixtureDesktop]::Windows($desktop) | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TWizardForm' -and [CodeWFSetupFixtureDesktop]::IsWindowVisible($_) }) | Select-Object -First 1 } '极简安装窗口'
    Register-Processes
    Check ($updateReady.WaitOne(1000)) '真实 Inno 安装窗口显示后确认更新交接'
    $initial=Bounds $window
    Check ([CodeWFSetupFixtureDesktop]::HasShapedRegion($window)) '原生窗口应用单角切口区域'
    Check ([CodeWFSetupFixtureDesktop]::IsWindowEnabled((Button '^×$'))) '安装前关闭按钮可用'
    Check (@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -match '^(TEdit|TNewPathEdit)$' }).Count -eq 0) '首次打开设置默认折叠'
    Capture '01-welcome'
    $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output '01-welcome.png'))
    try {
        $surface=$shot.GetPixel(260,40).ToArgb()
        foreach($point in @(@(260,0),@(0,100),@(($shot.Width-1),100),@(100,($shot.Height-1)))) {
            Check ($shot.GetPixel($point[0],$point[1]).ToArgb() -ne $surface) '窗口四边均有独立于背景的可见描边'
        }
    } finally { $shot.Dispose() }
    if ($Theme -eq 'dark') {
        Check ([CodeWFSetupFixtureDesktop]::PixelLightness((Join-Path $Output '01-welcome.png'),.5,.04) -lt 80) '深色界面无需修改系统主题'
    }
    if ($Maintenance) {
        Check ($null -ne (Button '^Repair installation$|^修复安装$|^Install for the current user$|^为当前用户安装$|^Upgrade and migrate$|^升级并迁移$')) '已有安装直接呈现原型维护入口'
        if ($Migration) { Check ($null -ne (Button '^Upgrade and migrate$|^升级并迁移$')) '旧全用户安装明确提示升级并迁移' }
        Capture '02-maintenance'
        Click (Button '^Installation details$|^安装详情$')
        $list=Wait-For { @(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TNewCheckListBox' }) | Select-Object -First 1 } '可展开已有安装详情'
        $memo=@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TMemo' }) | Select-Object -First 1
        if ($UnknownLocation) {
            Check ([CodeWFSetupFixtureDesktop]::Text($memo) -match '无法确定|could not be determined') '未知位置明确保留真实安全回退'
            Check (-not [CodeWFSetupFixtureDesktop]::IsWindowEnabled((Button '^Uninstall$|^卸载$'))) '未知卸载路径不提供可执行的卸载按钮'
        } else { Check ([CodeWFSetupFixtureDesktop]::Text($memo) -match '维刻 maintenance with spaces') '维护入口完整显示原安装路径' }
        if ($Multiple) { Check ([CodeWFSetupFixtureDesktop]::Send($list,395) -eq 3) '多作用域安装保留全部维护选项' }
        Check (([CodeWFSetupFixtureDesktop]::GetWindowStyle($memo,-16) -band 2048) -ne 0) '维护安装路径为只读'
        $area=[CodeWFSetupFixtureDesktop]::VisibleBounds($memo,$window)
        Check (($area.bottom-$area.top) -gt 60 -and ($area.right-$area.left) -gt 350) '紧凑维护窗口内路径区域完整可见'
        Capture '03-maintenance-details'
    }
    if ($Cancel -or $Maintenance) {
        Click (Button '^×$')
        $confirmation=Wait-For {
            if (-not [CodeWFSetupFixtureDesktop]::IsWindow($window)) { return 'closed' }
            @([CodeWFSetupFixtureDesktop]::Windows($desktop) | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -match 'MsgBox|#32770|TaskDialog' -and [CodeWFSetupFixtureDesktop]::IsWindowVisible($_) }) | Select-Object -First 1
        } '取消安装或系统确认'
        if ($confirmation -ne 'closed') { Click (Button '^Yes|^是' $confirmation) }
        [void](Wait-For { -not [CodeWFSetupFixtureDesktop]::IsWindow($window) } '确认后关闭安装器')
        Check (-not (Test-Path -LiteralPath (Join-Path $destination 'LICENSE'))) '取消操作不写入安装载荷'
        [ordered]@{Language=$Language;Cancelled=$true;Maintenance=[bool]$Maintenance;Assertions=$assertions;DesktopWasSwitched=$false;ProductWasInstalled=$false} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Output 'verification.json') -Encoding utf8
        Write-Host "PASS: 取消与维护夹具验证通过；证据：$Output"
        return
    }
    Click (Button '^Open source license$|^开源许可$')
    $license=Wait-For { @(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TRichEditViewer' -and [CodeWFSetupFixtureDesktop]::Text($_) -match 'Permission is hereby granted' }) | Select-Object -First 1 } '原窗口内 MIT 许可浮层'
    Check ($null -ne $license) '许可浮层保留 MIT 原文'
    Capture '04-license'
    $before=[CodeWFSetupFixtureDesktop]::Send($license,206)
    [void][CodeWFSetupFixtureDesktop]::Send($license,522,-7864320)
    [void][CodeWFSetupFixtureDesktop]::Send($license,522,-7864320)
    Check ([CodeWFSetupFixtureDesktop]::Send($license,206) -gt $before) '许可浮层可用滚轮阅读完整原文'
    Click (Button '^Back$|^返回$')
    Click (Button '^Customize installation|^自定义安装')
    $edit=Wait-For { @(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -match '^(TEdit|TNewPathEdit)$' }) | Select-Object -First 1 } '安装目录输入'
    $expanded=Bounds $window
    Check (($expanded.bottom-$expanded.top) -gt ($initial.bottom-$initial.top)) '展开自定义安装后窗口随内容增高'
    $checkboxes=@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TNewCheckBox' })
    Check ($checkboxes.Count -eq 2) '提供桌面快捷方式与 Markdown 候选选项'
    $association=@($checkboxes | Where-Object { [CodeWFSetupFixtureDesktop]::Text($_) -match 'Markdown' })[0]
    Check ([CodeWFSetupFixtureDesktop]::Send($association,240) -eq 1) 'Markdown 打开方式候选默认选中'
    Capture '02-custom'
    $primary=Bounds (Button '^Install now$|^立即安装$')
    Check (($primary.right-$primary.left) -eq 176 -and ($primary.bottom-$primary.top) -eq 42) '主按钮按原型使用 176×42 像素布局'
    Click (Button '^Open source license$|^开源许可$')
    Check (@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Text($_) -match 'Permission is hereby granted' }).Count -gt 0) '展开设置后也能查看许可'
    Click (Button '^Back$|^返回$')
    [CodeWFSetupFixtureDesktop]::SetText($edit,'relative\path')
    Click (Button '^Install now$|^立即安装$')
    $errorDialog=Wait-For { @([CodeWFSetupFixtureDesktop]::Windows($desktop) | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -match 'MsgBox|#32770|TaskDialog' -and [CodeWFSetupFixtureDesktop]::IsWindowVisible($_) }) | Select-Object -First 1 } '无效目录提示'
    Check (-not (Test-Path -LiteralPath (Join-Path $destination 'LICENSE'))) '无效目录不会开始复制'
    Click (Button '^OK$|^确定' $errorDialog)
    [CodeWFSetupFixtureDesktop]::SetText($edit,$destination)
    foreach ($checkbox in $checkboxes) {
        $checked=if ([CodeWFSetupFixtureDesktop]::Text($checkbox) -match 'Markdown') { 0 } else { 1 }
        if ([CodeWFSetupFixtureDesktop]::Send($checkbox,240) -ne $checked) { Click $checkbox }
        Check ([CodeWFSetupFixtureDesktop]::Send($checkbox,240) -eq $checked) '通过真实点击更改安装选项'
    }
    Click (Button '^Customize installation|^自定义安装')
    Click (Button '^Install now$|^立即安装$')
    Check ($null -ne (Button '^Cancel$|^取消$')) '真实安装进度页面提供取消操作'
    Capture '05-progress'
    $finish=Wait-For { Button '^Close$|^关闭$' } '一键安装直接进入完成页'
    Check (Test-Path -LiteralPath (Join-Path $destination 'LICENSE')) '一键安装把载荷复制到选择的目录'
    Check (@(Get-ChildItem -LiteralPath $destination -Recurse -File).Count -eq 1) '夹具只复制许可，不生成卸载器或其他文件'
    $log=Get-Content -LiteralPath (Join-Path $Output 'setup.log') -Raw
    Check ($log.Contains('VEX FIXTURE desktop=1') -and $log.Contains('VEX FIXTURE association=0')) '折叠设置中的选项准确传给安装引擎'
    Capture '03-complete'
    Click $finish
    [void](Wait-For { -not [CodeWFSetupFixtureDesktop]::IsWindow($window) } '正常关闭完成页')
    [ordered]@{Language=$Language;Assertions=$assertions;DesktopWasSwitched=$false;ProductWasInstalled=$false;Output=$Output} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Output 'verification.json') -Encoding utf8
    Write-Host "PASS: 极简安装器原生交互通过；证据：$Output"
} catch {
    if ($window -ne [IntPtr]::Zero -and [CodeWFSetupFixtureDesktop]::IsWindow($window)) { Capture 'failure' }
    @([CodeWFSetupFixtureDesktop]::Windows($desktop) | Where-Object { [CodeWFSetupFixtureDesktop]::IsWindowVisible($_) } | ForEach-Object {
        [ordered]@{Class=[CodeWFSetupFixtureDesktop]::Class($_);Text=[CodeWFSetupFixtureDesktop]::Text($_);Children=@(Controls $_ | ForEach-Object {
            [ordered]@{Class=[CodeWFSetupFixtureDesktop]::Class($_);Text=[CodeWFSetupFixtureDesktop]::Text($_)}
        })}
    }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'failure-controls.json') -Encoding utf8
    throw
} finally {
    try { Register-Processes } catch { Write-Warning '无法补充夹具后代进程，将只清理已登记的进程。' }
    foreach ($entry in $ownedHandles.GetEnumerator()) {
        [void][CodeWFSetupFixtureDesktop]::TerminateProcess($entry.Value,0)
        [void][CodeWFSetupFixtureDesktop]::CloseHandle($entry.Value)
    }
    if ($processInfo.thread -ne [IntPtr]::Zero) { [void][CodeWFSetupFixtureDesktop]::CloseHandle($processInfo.thread) }
    if ($desktop -ne [IntPtr]::Zero) { [void][CodeWFSetupFixtureDesktop]::CloseDesktop($desktop) }
    $updateReady.Dispose()
}
