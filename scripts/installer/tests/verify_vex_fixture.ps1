[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [ValidateSet('english','chinesesimplified')][string]$Language='chinesesimplified',
    [string]$Output='',
    [ValidateSet('system','light','dark')][string]$Theme='system',
    [switch]$Cancel,
    [switch]$Maintenance,
    [switch]$CurrentVersion,
    [switch]$UnknownLocation,
    [switch]$Multiple,
    [switch]$Migration,
    [switch]$ReduceMotion
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
# 取样点贴按钮左上角内侧：既避开居中文字，也落在 8px 圆角之内。
function FillOf([Drawing.Bitmap]$Shot,[IntPtr]$Control) {
    $controlBounds=Bounds $Control; $windowBounds=Bounds $window
    $Shot.GetPixel($controlBounds.left-$windowBounds.left+9,$controlBounds.top-$windowBounds.top+4).ToArgb()
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
    if ($ReduceMotion) { [void]$command.Append(' /VEXMOTION=off') }
    Check (-not $updateReady.WaitOne(0)) '安装向导未显示前不会确认就绪'
    if (-not [CodeWFSetupFixtureDesktop]::CreateProcess($installerPath,$command,[IntPtr]::Zero,[IntPtr]::Zero,$false,0,[IntPtr]::Zero,(Split-Path -Parent $installerPath),[ref]$startup,[ref]$processInfo)) { throw '无法启动独立夹具。' }
    $ownedHandles.Add([uint32]$processInfo.processId,$processInfo.process)
    $window=Wait-For { @([CodeWFSetupFixtureDesktop]::Windows($desktop) | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TWizardForm' -and [CodeWFSetupFixtureDesktop]::IsWindowVisible($_) }) | Select-Object -First 1 } '极简安装窗口'
    Register-Processes
    Check ($updateReady.WaitOne(1000)) '真实 Inno 安装窗口显示后确认更新交接'
    $initial=Bounds $window
    Check (($initial.right-$initial.left) -eq 640 -and ($initial.bottom-$initial.top) -eq 360) '默认窗口与统一原型一致为 640×360'
    Check ([CodeWFSetupFixtureDesktop]::HasShapedRegion($window)) '原生窗口应用圆角区域'
    Check ([CodeWFSetupFixtureDesktop]::IsWindowEnabled((Button '^×$'))) '安装前关闭按钮可用'
    Check (@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -match '^(TEdit|TNewPathEdit)$' }).Count -eq 0) '首次打开设置默认折叠'
    Capture '01-welcome'
    $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output '01-welcome.png'))
    try {
        $surface=$shot.GetPixel(260,10).ToArgb()
        foreach($point in @(@(260,0),@(610,0),@(0,100),@(($shot.Width-1),16),@(($shot.Width-1),100),@(100,($shot.Height-1)))) {
            Check ($shot.GetPixel($point[0],$point[1]).ToArgb() -ne $surface) '窗口四边均有独立于背景的可见描边'
        }
    } finally { $shot.Dispose() }
    # 抗锯齿检查只针对真正填色的主按钮：维护页按版本关系是「升级」或「修复安装」。
    $primary=if ($Maintenance) { Button '^Upgrade$|^升级$|^Repair installation$|^修复安装$' } else { Button '^Install now$|^立即安装$' }
    if ($primary -and [CodeWFSetupFixtureDesktop]::IsWindowEnabled($primary)) {
        $primaryArea=Bounds $primary
        $windowArea=Bounds $window
        $left=$primaryArea.left-$windowArea.left; $top=$primaryArea.top-$windowArea.top
        $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output '01-welcome.png'))
        try {
            $surface=$shot.GetPixel($left-1,$top+10).ToArgb()
            $fill=$shot.GetPixel($left+20,$top+2).ToArgb()
            $edgeColors=[Collections.Generic.HashSet[int]]::new()
            for ($edgeY=0; $edgeY -lt 8; $edgeY++) {
                for ($edgeX=0; $edgeX -lt 8; $edgeX++) {
                    $color=$shot.GetPixel($left+$edgeX,$top+$edgeY).ToArgb()
                    if ($color -ne $surface -and $color -ne $fill) { [void]$edgeColors.Add($color) }
                }
            }
            Check ($edgeColors.Count -ge 3) '主按钮圆角具有连续抗锯齿过渡'
        } finally { $shot.Dispose() }
    }
    [void][CodeWFSetupFixtureDesktop]::Send((Button '^×$'),243,1)
    Capture 'close-pressed'
    [void][CodeWFSetupFixtureDesktop]::Send((Button '^×$'),243,0)
    $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output 'close-pressed.png'))
    try {
        Check ($shot.GetPixel(610,0).ToArgb() -ne $shot.GetPixel(600,10).ToArgb() -and
            $shot.GetPixel(639,16).ToArgb() -ne $shot.GetPixel(630,16).ToArgb()) '关闭按钮按下时仍保留右上方完整边框'
    } finally { $shot.Dispose() }
    if ($Theme -eq 'dark') {
        Check ([CodeWFSetupFixtureDesktop]::PixelLightness((Join-Path $Output '01-welcome.png'),.5,.04) -lt 80) '深色界面无需修改系统主题'
    }
    if ($Maintenance) {
        $versionLog=Get-Content -LiteralPath (Join-Path $Output 'setup.log') -Raw
        $installedLine=@($versionLog -split "`r?`n" | Where-Object { $_ -match 'VEX FIXTURE installed=' })[0]
        $canUpgrade=[bool]((-not $UnknownLocation) -and (-not $CurrentVersion))
        if ($UnknownLocation) {
            Check ($installedLine -match 'VEX FIXTURE installed=(已安装 · 版本未知|Installed · Version unavailable)') '无法读取已安装版本时明确显示未知，不冒用安装包版本'
        } else {
            $localVersion=if ($CurrentVersion) {'1\.0\.0'} else {'0\.9\.0'}
            Check ($installedLine -match "VEX FIXTURE installed=(已安装 · $localVersion · 安装于 \d{4}-\d{2}-\d{2}|Installed · $localVersion · installed on \d{4}-\d{2}-\d{2})") '已安装行只报本机版本与这台机器上的安装日期'
            Check ($installedLine -notmatch '→') '已安装行不再复述安装包版本或升级箭头'
        }
        Check ($null -eq (Button '^Open$|^打开$')) '维护页彻底移除「打开」，执行入口只留一个'
        Check ($null -ne (Button '^Repair installation$|^修复安装$|^Install for the current user$|^为当前用户安装$|^Upgrade and migrate$|^升级并迁移$|^Upgrade$|^升级$')) '已有安装直接呈现原型维护入口'
        $nextPattern=if ($Migration) { '^Upgrade and migrate$|^升级并迁移$' }
        elseif ($UnknownLocation) { '^Install for the current user$|^为当前用户安装$' }
        elseif ($canUpgrade) { '^Upgrade$|^升级$' }
        else { '^Repair installation$|^修复安装$' }
        Check ($null -ne (Button $nextPattern)) "维护页主按钮按版本关系给出动作：$nextPattern"
        # 标签控件没有可读文本，标题按既有约定通过夹具日志核对。
        $headingLine=@($versionLog -split "`r?`n" | Where-Object { $_ -match 'VEX FIXTURE heading=' })[0]
        Check ($headingLine -match 'VEX FIXTURE heading=(安装包 1\.0\.0|Setup package 1\.0\.0$)') '维护页标题陈述安装包版本，不再写成祈使句'
        Capture '02-maintenance'
        if (-not $UnknownLocation -and -not $Migration) {
            $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output '02-maintenance.png'))
            try {
                $background=$shot.GetPixel(400,260).ToArgb()
                Check ((FillOf $shot (Button $nextPattern)) -ne $background) '整页唯一强调色落在右下角执行按钮上'
            } finally { $shot.Dispose() }
        }
        # 更新日志栏：胶囊点击后窗口加宽一栏，读随包日志，更早版本默认折叠。
        $pill=Wait-For { Button '^View changelog$|^查看更新日志$' } '安装包行的查看更新日志胶囊'
        Click $pill
        [void](Wait-For { $area=Bounds $window; (($area.right-$area.left) -eq 960) } '日志栏展开后窗口加宽到 960')
        $logMemo=Wait-For { @(Controls | Where-Object { ([CodeWFSetupFixtureDesktop]::Class($_) -eq 'TRichEditViewer') -and
            ([CodeWFSetupFixtureDesktop]::Text($_) -match 'V1\.10\.0') }) | Select-Object -First 1 } '右侧日志栏渲染随包的更新日志'
        $changelogLog=Get-Content -LiteralPath (Join-Path $Output 'setup.log') -Raw
        $changelogMatch=[regex]::Matches($changelogLog,'VEX FIXTURE changelog releases=(\d+) earlier=(\d) meta=([^\r\n]+)')
        $changelog=$changelogMatch[$changelogMatch.Count-1]
        Check ([int]$changelog.Groups[1].Value -ge 2) '跨版本升级时日志栏列出不止一个小节'
        Check ($changelog.Groups[2].Value -eq '0') '更早的版本默认折叠，只展开最新一节'
        $metaPattern=if ($UnknownLocation) { '^(安装包 1\.0\.0|Package 1\.0\.0)$' }
        elseif ($CurrentVersion) { '^(安装包|Package) 1\.0\.0 · (1\.0\.0 之后|since 1\.0\.0)$' }
        else { '^(安装包|Package) 1\.0\.0 · (0\.9\.0 之后|since 0\.9\.0)$' }
        Check ($changelog.Groups[3].Value -match $metaPattern) "日志栏说明本安装包与本机版本的来源：$metaPattern"
        $more=Wait-For { Button '^View [0-9]+ earlier release|^查看更早' } '折叠中的更早版本入口'
        Click $more
        [void](Wait-For { (Get-Content -LiteralPath (Join-Path $Output 'setup.log') -Raw) -match 'changelog releases=\d+ earlier=1' } '展开更早版本')
        Check ($null -ne (Button '^Hide earlier releases$|^收起更早版本$')) '展开后入口改为收起'
        Check ([CodeWFSetupFixtureDesktop]::Text($logMemo) -match 'V1\.9\.0') '展开后渲染出更早小节'
        Capture 'maintenance-changelog'
        Click (Button '^Hide changelog$|^收起更新日志$')
        [void](Wait-For { $area=Bounds $window; (($area.right-$area.left) -eq 640) } '收起日志栏后窗口回到 640')
        Click (Button '^Installation details$|^安装详情$')
        $list=Wait-For { @(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TNewCheckListBox' }) | Select-Object -First 1 } '可展开已有安装详情'
        $memo=@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TMemo' }) | Select-Object -First 1
        if ($UnknownLocation) {
            Check ([CodeWFSetupFixtureDesktop]::Text($memo) -match '无法确定|could not be determined') '未知位置明确保留真实安全回退'
        } else { Check ([CodeWFSetupFixtureDesktop]::Text($memo) -match '维刻 maintenance with spaces') '维护入口完整显示原安装路径' }
        if ($Multiple) {
            Check ([CodeWFSetupFixtureDesktop]::Send($list,395) -eq 3) '多作用域安装保留全部维护选项'
            [void][CodeWFSetupFixtureDesktop]::Send($list,390,2)
            [void][CodeWFSetupFixtureDesktop]::Send($list,256,32)
            [void][CodeWFSetupFixtureDesktop]::Send($list,257,32)
            [void](Wait-For { (Get-Content -LiteralPath (Join-Path $Output 'setup.log') -Raw) -match 'VEX FIXTURE installed=(已安装|Installed) · 0\.8\.0' } '切换安装记录后刷新已安装版本')
            Check ([CodeWFSetupFixtureDesktop]::Text($memo) -match 'C:\\Program Files\\Vex') '所选安装记录的版本和路径保持一致'
        }
        Check (([CodeWFSetupFixtureDesktop]::GetWindowStyle($memo,-16) -band 2048) -ne 0) '维护安装路径为只读'
        $area=[CodeWFSetupFixtureDesktop]::VisibleBounds($memo,$window)
        Check (($area.bottom-$area.top) -gt 60 -and ($area.right-$area.left) -gt 350) '紧凑维护窗口内路径区域完整可见'
        Capture '03-maintenance-details'
        # 单选切到卸载时，执行按钮只改文案与配色，不整页重绘。
        if (-not $UnknownLocation -and -not $Migration -and -not $Multiple) {
            [void][CodeWFSetupFixtureDesktop]::Send($list,390,1)
            [void][CodeWFSetupFixtureDesktop]::Send($list,256,32)
            [void][CodeWFSetupFixtureDesktop]::Send($list,257,32)
            $uninstall=Wait-For { Button '^Uninstall$|^卸载$' } '选中卸载行后执行按钮改为卸载'
            Capture 'maintenance-uninstall'
            $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output 'maintenance-uninstall.png'))
            try {
                $buttonArea=Bounds $uninstall; $windowArea=Bounds $window
                $left=$buttonArea.left-$windowArea.left; $top=$buttonArea.top-$windowArea.top
                Check ($shot.GetPixel($left+9,$top+4).ToArgb() -eq $shot.GetPixel($left-4,$top+10).ToArgb()) '卸载时执行按钮降为文字按钮，强调色不留在它身上'
            } finally { $shot.Dispose() }
        }
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
    Click (Button '^Open source under MIT$|^基于 MIT 协议开源$')
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
    Check (($expanded.bottom-$expanded.top) -eq ($initial.bottom-$initial.top)) '展开自定义安装仍保持原型的 640×360 窗口'
    $checkboxes=@(Controls | Where-Object { [CodeWFSetupFixtureDesktop]::Class($_) -eq 'TNewCheckBox' })
    Check ($checkboxes.Count -eq 2) '提供桌面快捷方式与 Markdown 候选选项'
    $association=@($checkboxes | Where-Object { [CodeWFSetupFixtureDesktop]::Text($_) -match 'Markdown' })[0]
    Check ([CodeWFSetupFixtureDesktop]::Send($association,240) -eq 1) 'Markdown 打开方式候选默认选中'
    Capture '02-custom'
    $primary=Bounds (Button '^Install now$|^立即安装$')
    Check (($primary.right-$primary.left) -eq 104 -and ($primary.bottom-$primary.top) -eq 36) '主按钮按原型使用 104×36 像素布局'
    $shot=[Drawing.Bitmap]::FromFile((Join-Path $Output '02-custom.png'))
    try {
        $windowArea=Bounds $window
        $left=$primary.left-$windowArea.left; $top=$primary.top-$windowArea.top
        $surface=$shot.GetPixel($left-2,$top+12).ToArgb()
        Check ($shot.GetPixel($left+16,$top+12).ToArgb() -ne $surface -and
            $shot.GetPixel($left+88,$top+12).ToArgb() -ne $surface) '展开设置后主按钮完整绘制，没有被背景覆盖'
    } finally { $shot.Dispose() }
    Click (Button '^Open source under MIT$|^基于 MIT 协议开源$')
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
    Check ($null -ne (Button '^Cancel installation$|^取消安装$')) '真实安装进度页面提供取消操作'
    Capture '05-progress'
    $frameDirectory=Join-Path $Output 'progress-frames'
    New-Item -ItemType Directory -Path $frameDirectory -Force | Out-Null
    $frameClock=[Diagnostics.Stopwatch]::StartNew()
    $frameNumber=0
    while ($frameClock.ElapsedMilliseconds -lt 850) {
        [CodeWFSetupFixtureDesktop]::Capture($window,(Join-Path $frameDirectory ('frame-{0:D3}.png' -f $frameNumber)))
        $frameNumber++
        Start-Sleep -Milliseconds 16
    }
    $trackVisible=$true
    foreach ($frameFile in Get-ChildItem -LiteralPath $frameDirectory -Filter '*.png') {
        $shot=[Drawing.Bitmap]::FromFile($frameFile.FullName)
        try {
            if ($shot.GetPixel(410,148).ToArgb() -eq $shot.GetPixel(410,141).ToArgb()) { $trackVisible=$false }
        } finally { $shot.Dispose() }
    }
    Check ($frameNumber -ge 8 -and $trackVisible) '连续进度帧始终保留轨道，无白色空帧'
    $finish=Wait-For { Button '^Start writing$|^写点什么$|^Close$|^关闭$' } '一键安装直接进入完成页'
    Check (Test-Path -LiteralPath (Join-Path $destination 'LICENSE')) '一键安装把载荷复制到选择的目录'
    Check (@(Get-ChildItem -LiteralPath $destination -Recurse -File).Count -eq 1) '夹具只复制许可，不生成卸载器或其他文件'
    $log=Get-Content -LiteralPath (Join-Path $Output 'setup.log') -Raw
    $animatedValues=@([regex]::Matches($log,'VEX FIXTURE progress=(\d+) target=7000') | ForEach-Object { [int]$_.Groups[1].Value })
    $intermediateFrames=@($animatedValues | Where-Object { $_ -gt 0 -and $_ -lt 7000 }).Count
    if ($ReduceMotion) { Check ($intermediateFrames -eq 0 -and $animatedValues -contains 7000) '关闭动画后直接显示真实进度，不生成过渡帧' }
    else { Check ($intermediateFrames -ge 3) '稀疏安装进度回调会产生多个平滑过渡帧' }
    $monotonic=$true
    for ($frameIndex=1; $frameIndex -lt $animatedValues.Count; $frameIndex++) {
        if ($animatedValues[$frameIndex] -lt $animatedValues[$frameIndex-1]) { $monotonic=$false }
    }
    Check $monotonic '安装进度平滑前进，不回退'
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
