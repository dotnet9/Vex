; Vex 专用安装外观；不读取软件原型、应用源码或其他仓库。



; 安装、注册、覆盖、取消及回滚继续交给 Inno Setup。



#ifndef VexLicensePath



#define VexLicensePath "..\LICENSE"



#endif



#define VexBrandDirectory ExtractFilePath(__PATHFILENAME__)



#define VexChangelogPath ExtractFilePath(__PATHFILENAME__) + "..\..\docs\更新日志.md"



#define VexWebsite "https://vex.codewf.com/"



[Files]



Source: "{#VexLicensePath}"; DestName: "Vex-License.txt"; Flags: dontcopy



; 维护页右侧的更新日志栏读这份随包文档，与安装包内嵌版本永远同源。



; （正文现已由 ISPP 在编译期内嵌，此随包副本保留作存证与对照。）



Source: "{#VexChangelogPath}"; DestName: "Vex-Changelog.txt"; Flags: dontcopy



Source: "{#VexBrandDirectory}vex-brand-light.bmp"; Flags: dontcopy



Source: "{#VexBrandDirectory}vex-brand-dark.bmp"; Flags: dontcopy







[Setup]



AppPublisherURL={#VexWebsite}



#if VER >= EncodeVer(7,0,0)



SetupArchitecture=x86



#endif



DisableWelcomePage=no



DisableReadyPage=yes



DisableFinishedPage=no



WizardResizable=no



WizardSizePercent=100



ShowLanguageDialog=no



LanguageDetectionMethod=uilanguage







[CustomMessages]



english.VTitle=Vex Setup



chinesesimplified.VTitle=维刻 Vex 安装



english.VSlogan=Write your thoughts. See the connections.



chinesesimplified.VSlogan=写下想法，看见脉络。



english.VWelcome=Let your ideas take shape.



chinesesimplified.VWelcome=让想法，慢慢成形。



english.VMeta=Version {#AppVersion} · Current user



chinesesimplified.VMeta=版本 {#AppVersion} · 仅为当前用户安装



english.VInstall=Install now



chinesesimplified.VInstall=立即安装



english.VCustom=Customize installation



chinesesimplified.VCustom=自定义安装选项



english.VLocation=Installation folder



chinesesimplified.VLocation=安装位置



english.VBrowse=Browse…



chinesesimplified.VBrowse=浏览…



english.VDesktop=Create a desktop shortcut



chinesesimplified.VDesktop=创建桌面快捷方式



english.VScope=Install for the current user



chinesesimplified.VScope=仅为当前用户安装



english.VAssociation=Register a Markdown opening option



chinesesimplified.VAssociation=注册 Markdown 打开方式候选



english.VLicense=Open source under MIT



chinesesimplified.VLicense=基于 MIT 协议开源



english.VLicenseTitle=MIT open source license



chinesesimplified.VLicenseTitle=MIT 开源许可



english.VAssociationHint=Keeps your default app unchanged



chinesesimplified.VAssociationHint=不改变系统默认应用



english.VInstalled=Installed · %1



chinesesimplified.VInstalled=已安装 · %1



english.VInstalledOn=Installed · %1 · installed on %2



chinesesimplified.VInstalledOn=已安装 · %1 · 安装于 %2



english.VInstalledUnknown=Installed · Version unavailable



chinesesimplified.VInstalledUnknown=已安装 · 版本未知



english.VPackage=Setup package %1



chinesesimplified.VPackage=安装包 %1



english.VReleased=%1 release



chinesesimplified.VReleased=%1 发布



english.VChangelog=View changelog



chinesesimplified.VChangelog=查看更新日志



english.VChangelogHide=Hide changelog



chinesesimplified.VChangelogHide=收起更新日志



english.VChangelogHeading=Changelog



chinesesimplified.VChangelogHeading=更新日志



english.VChangelogSince=Package %1



chinesesimplified.VChangelogSince=安装包 %1



english.VChangelogSinceInstalled=Package %1 · since %2



chinesesimplified.VChangelogSinceInstalled=安装包 %1 · %2 之后



english.VChangelogEarlier=View %1 earlier release(s)



chinesesimplified.VChangelogEarlier=查看更早 %1 个版本



english.VChangelogCollapse=Hide earlier releases



chinesesimplified.VChangelogCollapse=收起更早版本



english.VChangelogEmpty=This package carries no changelog.



chinesesimplified.VChangelogEmpty=这个安装包没有附带更新日志。



english.VRepair=Repair installation



chinesesimplified.VRepair=修复安装



english.VUpgrade=Upgrade



chinesesimplified.VUpgrade=升级



english.VContinueInstall=Install for the current user



chinesesimplified.VContinueInstall=为当前用户安装



english.VRegistrationIntro=An installation record exists. You can install for the current user.



chinesesimplified.VRegistrationIntro=检测到已有安装记录，可继续为当前用户安装。



english.VDetails=Installation details



chinesesimplified.VDetails=安装详情



english.VRemove=Uninstall



chinesesimplified.VRemove=卸载



english.VProgress=Installation progress



chinesesimplified.VProgress=安装进度



english.VCancelInstall=Cancel installation



chinesesimplified.VCancelInstall=取消安装



english.VBack=Back



chinesesimplified.VBack=返回



english.VClose=Close



chinesesimplified.VClose=关闭



english.VOpen=Open



chinesesimplified.VOpen=打开



english.VStartWriting=Start writing



chinesesimplified.VStartWriting=写点什么



english.VInstalling=Installing



chinesesimplified.VInstalling=正在安装



english.VInstallingIntro=Just a moment, then you can start writing.



chinesesimplified.VInstallingIntro=稍等片刻，即可开始写作。



english.VFinished=All set,%nlet your ideas grow.



chinesesimplified.VFinished=一切就绪，%n让想法开始生长。



english.VFinishedIntro=Your own Markdown space is ready.



chinesesimplified.VFinishedIntro=你的专属 Markdown 空间已准备完毕。



english.VRepairing=Repairing



chinesesimplified.VRepairing=正在修复



english.VUpgrading=Upgrading



chinesesimplified.VUpgrading=正在升级



english.VRepaired=All set,%nlet your ideas grow.



chinesesimplified.VRepaired=一切就绪，%n让想法开始生长。



english.VPathError=Choose an absolute local folder outside Windows and the drive root.



chinesesimplified.VPathError=请选择有效的本地绝对路径，不能使用系统目录或磁盘根目录。



english.VUninstalling=Uninstalling Vex…



chinesesimplified.VUninstalling=正在卸载 Vex…







; 复用已验证的精确 AppId 探测和卸载启动逻辑，不加载旧的视觉引擎。



#include "CodeWF.Setup.Maintenance.iss"



#include "Vex.Migration.iss"



#include "Vex.Update.iss"



#include "Vex.Drawing.iss"







[Code]



type



  TVPoint = record X, Y: Integer; end;



  TVRect = record Left, Top, Right, Bottom: Integer; end;



  TVCharFormat = array[0..22] of LongWord;



  TVPaint = record



    DC: LongWord; Erase: LongBool; Bounds: TVRect;



    Restore, Update: LongBool; Reserved: array[0..31] of Byte;



  end;



  TVDrawItem = record



    CtlType, CtlID, ItemID, ItemAction, ItemState: LongWord;



    ItemWindow, DC: LongWord;



    Bounds: TVRect;



    ItemData: LongWord;



  end;



function VPolygon(var Points: TVPoint; Count, Mode: Integer): LongWord;



  external 'CreatePolygonRgn@gdi32.dll stdcall';



function VRoundedRegion(Left, Top, Right, Bottom, Width, Height: Integer): LongWord;



  external 'CreateRoundRectRgn@gdi32.dll stdcall';



function VRectRegion(Left, Top, Right, Bottom: Integer): LongWord;



  external 'CreateRectRgn@gdi32.dll stdcall';



function VSystemMetric(Index: Integer): Integer;



  external 'GetSystemMetrics@user32.dll stdcall';



function VCombineRegion(Target, First, Second: LongWord; Mode: Integer): Integer;



  external 'CombineRgn@gdi32.dll stdcall';



function VSetRegion(Window, Region: LongWord; Redraw: Boolean): Integer;



  external 'SetWindowRgn@user32.dll stdcall';



function VDeleteObject(Handle: LongWord): Boolean;



  external 'DeleteObject@gdi32.dll stdcall';



function VGetLong(Window: LongWord; Index: Integer): Longint;



  external 'GetWindowLongW@user32.dll stdcall';



function VSetLong(Window: LongWord; Index: Integer; Value: Longint): Longint;



  external 'SetWindowLongW@user32.dll stdcall';



function VGetClassLong(Window: LongWord; Index: Integer): LongWord;



  external 'GetClassLongW@user32.dll stdcall';



function VProbeClassName(Window: LongWord; Buffer: String; Max: Integer): Integer;



  external 'GetClassNameW@user32.dll stdcall';



function VProbeModule(Name: String): LongWord;



  external 'GetModuleHandleW@kernel32.dll stdcall';



function VSetClassLong(Window: LongWord; Index: Integer; Value: LongWord): LongWord;



  external 'SetClassLongW@user32.dll stdcall';



function VCallProc(Previous, Window, Message, WParam, LParam: LongWord): Longint;



  external 'CallWindowProcW@user32.dll stdcall';



function VGetRect(Window: LongWord; var Bounds: TVRect): Boolean;



  external 'GetWindowRect@user32.dll stdcall';



procedure VCopyItem(var Target: TVDrawItem; Source, Size: LongWord);



  external 'RtlMoveMemory@kernel32.dll stdcall';



function VBrush(Color: LongWord): LongWord;



  external 'CreateSolidBrush@gdi32.dll stdcall';



function VFill(DC: LongWord; var Bounds: TVRect; Brush: LongWord): Integer;



  external 'FillRect@user32.dll stdcall';



function VTextColor(DC, Color: LongWord): LongWord;



  external 'SetTextColor@gdi32.dll stdcall';



function VBackgroundColor(DC, Color: LongWord): LongWord;



  external 'SetBkColor@gdi32.dll stdcall';



function VTextMode(DC: LongWord; Mode: Integer): Integer;



  external 'SetBkMode@gdi32.dll stdcall';



function VDrawText(DC: LongWord; Text: String; Length: Integer; var Bounds: TVRect; Format: LongWord): Integer;



  external 'DrawTextW@user32.dll stdcall';



function VFillRegion(DC, Region, Brush: LongWord): Boolean;



  external 'FillRgn@gdi32.dll stdcall';



function VInvalidate(Window, Rect: LongWord; Erase: Boolean): Boolean;



  external 'InvalidateRect@user32.dll stdcall';



function VRedraw(Window, Rect, Region, Flags: LongWord): Boolean;



  external 'RedrawWindow@user32.dll stdcall';



function VGetFocus: LongWord;



  external 'GetFocus@user32.dll stdcall';



function VSelectObject(DC, Obj: LongWord): LongWord;



  external 'SelectObject@gdi32.dll stdcall';



function VSaveDC(DC: LongWord): Integer;



  external 'SaveDC@gdi32.dll stdcall';



function VRestoreDC(DC: LongWord; Saved: Integer): Boolean;



  external 'RestoreDC@gdi32.dll stdcall';



function VClipRect(DC: LongWord; Left, Top, Right, Bottom: Integer): Integer;



  external 'IntersectClipRect@gdi32.dll stdcall';



function VCreateFont(Height, Width, Escapement, Orientation, Weight: Integer;



  Italic, Underline, StrikeOut, CharSet, OutPrecision, ClipPrecision, Quality, Pitch: LongWord; Face: String): LongWord;



  external 'CreateFontW@gdi32.dll stdcall';



function VCreatePen(Style, Width: Integer; Color: LongWord): LongWord;



  external 'CreatePen@gdi32.dll stdcall';



function VMove(DC: LongWord; X,Y: Integer; Previous: LongWord): Boolean;



  external 'MoveToEx@gdi32.dll stdcall';



function VLineTo(DC: LongWord; X,Y: Integer): Boolean;



  external 'LineTo@gdi32.dll stdcall';



function VSend(Window, Message, WParam, LParam: LongWord): LongWord;



  external 'SendMessageW@user32.dll stdcall';



function VParameters(Action, Parameter: LongWord; var Value: Cardinal; Update: LongWord): Boolean;



  external 'SystemParametersInfoW@user32.dll stdcall';



function VSetTimer(Window, ID, Interval, Callback: LongWord): LongWord;



  external 'SetTimer@user32.dll stdcall';



function VKillTimer(Window, ID: LongWord): Boolean;



  external 'KillTimer@user32.dll stdcall';



function VCursor(var Point: TVPoint): Boolean;



  external 'GetCursorPos@user32.dll stdcall';



function VIsWindow(Window: LongWord): Boolean;



  external 'IsWindow@user32.dll stdcall';



function VSetWindowTheme(Window: LongWord; Application, IDs: String): Integer;



  external 'SetWindowTheme@uxtheme.dll stdcall';



function VBeginPaint(Window: LongWord; var Paint: TVPaint): LongWord;



  external 'BeginPaint@user32.dll stdcall';



function VEndPaint(Window: LongWord; var Paint: TVPaint): Boolean;



  external 'EndPaint@user32.dll stdcall';



function VSendCharFormat(Window, Message, Flags: LongWord; var Format: TVCharFormat): Longint;



  external 'SendMessageW@user32.dll stdcall';



function VSendRect(Window, Message, WParam: LongWord; var Bounds: TVRect): Longint;



  external 'SendMessageW@user32.dll stdcall';



function VTickCount: LongWord;



  external 'GetTickCount@kernel32.dll stdcall';



function VProgressRatio(Value, Scale, Maximum: Integer): Integer;



  external 'MulDiv@kernel32.dll stdcall';







var



  VTitle, VName, VSlogan, VPathLabel, VScope, VPercent, VStatus, VDescription,



    VAssociationHint, VProgressLabel, VModalTitle, VInstalled, VLocation,



    VReleasedLabel, VLogTitle, VLogMeta: TLabel;



  VBack, VMark, VProgressBar: TBitmapImage;



  VProgressFrame: TBitmap;



  VCustom, VLicense, VClose, VWebsite, VLog, VLogMore, VModalBack: TNewButton;



  VLicenseMemo, VLogMemo: TRichEditViewer;



  VDesktop, VAssociation: TNewCheckBox;



  VButtons: array[0..9] of TNewButton;



  VButtonProcs: array[0..9] of LongWord;



  VButtonFonts: array[0..9] of LongWord;



  VCheckProcs: array[0..1] of LongWord;



  VCheckFont: LongWord;



  VLicenseProc, VLogProc: LongWord;



  VWheelDelta: Integer;



  VPreviousProc, VSurfaceBrush, VSoftBrush: LongWord;



  VExpanded, VInitialized, VDetailsOpen, VLicenseOpen, VRepairing: Boolean;



  VChangelogOpen, VChangelogEarlier, VChangelogLoaded: Boolean;



  VUpgradeAvailable: Boolean;



  VHover, VModalY, VWindowWidth: Integer;



  VProgressShown, VProgressTarget, VProgressFrom, VProgressPixel, VProgressPercent: Integer;



  VProgressStarted: LongWord;



  VProgressAnimated: Boolean;



  VReleaseCount, VShownCount: Integer;



  VReleaseVersions, VReleaseDates, VReleaseBodies: array[0..23] of String;



  VShownReleases: array[0..23] of Integer;



  VLogRenderedFor, VInstalledVersion: String;



  VSurface, VText, VMuted, VField, VSoft, VLine, VAccent, VBrand, VBrandText, VOnAccent, VAccentHover, VAccentSoft, VDanger: TColor;



#ifdef CodeWFSetupFixture



  VFixtureVersions: array[0..3] of String;



#endif







procedure VBounds(Control: TControl; X, Y, Width, Height: Integer);



begin



  Control.SetBounds(ScaleX(X), ScaleY(Y), ScaleX(Width), ScaleY(Height));



end;







function VRegion(Width, Height: Integer): LongWord;



begin



  Result:=VRoundedRegion(0,0,Width+1,Height+1,ScaleX(16),ScaleY(16));



end;







procedure VRound(DC: LongWord; X,Y,Width,Height,Radius: Integer; Color: TColor; Outline: Boolean);



begin



  VDrawingRound(DC,X,Y,Width,Height,Radius,Color,Outline);



end;







function VFontFace(Strong: Boolean): String;



begin



  Result:='Segoe UI';



  if Strong then Result:='Segoe UI Semibold';



  if ActiveLanguage='chinesesimplified' then Result:='Microsoft YaHei UI';



end;







procedure VFont(Font: TFont; Pixels: Integer; Strong: Boolean);



begin



  Font.Name:=VFontFace(Strong);



  if ActiveLanguage='chinesesimplified' then begin



    if Strong then Font.Style:=[fsBold] else Font.Style:=[];



  end;



  Font.Height:=-ScaleY(Pixels);



end;







function VLabel(X, Y, Width, Height, Size: Integer; Text: String): TLabel;



begin



  Result := TLabel.Create(WizardForm);



  Result.Parent := WizardForm; Result.AutoSize := False;



  Result.Transparent := True;



  Result.Caption := Text; VFont(Result.Font,Size,False);



  VBounds(Result, X, Y, Width, Height);



end;







function VButton(X, Y, Width, Height: Integer; Text: String; Handler: TNotifyEvent): TNewButton;



begin



  Result := TNewButton.Create(WizardForm); Result.Parent := WizardForm;



  Result.Caption := Text; Result.OnClick := Handler;



  VBounds(Result, X, Y, Width, Height);



end;







function VIsMaintenance: Boolean;



begin



  Result:=False;



  if CWMPage<>nil then Result:=WizardForm.CurPageID=CWMPage.ID;



end;







function VSelectedRegistration: Integer;



var I: Integer;



begin



  Result:=-1;



  if (CWMPage<>nil) and (CWMPage.SelectedValueIndex>0) then begin



    if CWMPage.SelectedValueIndex<=CWMRegistrationCount then Result:=CWMPage.SelectedValueIndex-1;



    exit;



  end;



  for I:=0 to CWMRegistrationCount-1 do



    if (CWMRepairPath<>'') and (CompareText(CWMRegistrations[I].AppPath,CWMRepairPath)=0) then begin



      Result:=I; exit;



    end;



  if CWMRegistrationCount=1 then Result:=0;



end;







function VRegisteredVersion(Index: Integer): String;



var Path: String;



begin



  Result:='';



  if (Index<0) or (Index>=CWMRegistrationCount) then exit;



#ifdef CodeWFSetupFixture



  Result:=VFixtureVersions[Index];



#else



  RegQueryStringValue(CWMRegistrations[Index].RootKey,CWMUninstallKey,'DisplayVersion',Result);



  Result:=Trim(Result);



  Path:=CWMRegistrations[Index].AppPath;



  if (Result='') and (Path<>'') then GetVersionNumbersString(AddBackslash(Path)+'Vex.exe',Result);



#endif



end;







{ 逐段取版本号数字，跳过 . - _ + 等分隔符；串尾之后恒为 0，便于 1.7 与 1.7.0 比较。 }



function VVersionPart(const Source: String; var Position: Integer): Integer;



var Digits: String;



begin



  Digits:='';



  while (Position<=Length(Source)) and (Source[Position]>='0') and (Source[Position]<='9') do begin



    Digits:=Digits+Source[Position]; Position:=Position+1;



  end;



  Result:=StrToIntDef(Digits,0);



  while (Position<=Length(Source)) and



    not ((Source[Position]>='0') and (Source[Position]<='9')) do Position:=Position+1;



end;







{ 只有读得到数字版本且确实落后于本安装包时才算升级；版本未知或更新时保持修复入口。 }



function VIsOlder(const Left, Right: String): Boolean;



var I,J,LeftPart,RightPart: Integer;



begin



  Result:=False;



  { Inno 的 or 不保证短路，空串取 [1] 会越界，因此先行退出。 }



  if Left='' then exit;



  if not ((Left[1]>='0') and (Left[1]<='9')) then exit;



  I:=1; J:=1;



  while (I<=Length(Left)) or (J<=Length(Right)) do begin



    LeftPart:=VVersionPart(Left,I); RightPart:=VVersionPart(Right,J);



    if LeftPart<>RightPart then begin Result:=LeftPart<RightPart; exit; end;



  end;



end;







function VIsOlderVersion(const Installed: String): Boolean;



begin



  Result:=VIsOlder(Installed,'{#AppVersion}');



end;







{ 本机安装日期取卸载注册写入的 InstallDate；读不到就不编造日期。 }



function VPad2(Value: Integer): String;



begin



  Result:=IntToStr(Value);



  if Length(Result)<2 then Result:='0'+Result;



end;







function VInstalledOn(RootKey: Integer): String;



var Stamp: String; Y,M,D: Integer;



begin



  Result:='';



  #ifdef CodeWFSetupFixture



    Result:=ExpandConstant('{param:VEXINSTALLEDON|2026-10-20}');



    exit;



  #endif



  if not RegQueryStringValue(RootKey,CWMUninstallKey,'InstallDate',Stamp) then exit;



  Stamp:=Trim(Stamp);



  if Length(Stamp)<>8 then exit;



  Y:=StrToIntDef(Copy(Stamp,1,4),0); M:=StrToIntDef(Copy(Stamp,5,2),0); D:=StrToIntDef(Copy(Stamp,7,2),0);



  if (Y<1990) or (M<1) or (M>12) or (D<1) or (D>31) then exit;



  Result:=IntToStr(Y)+'-'+VPad2(M)+'-'+VPad2(D);



end;







{ Inno 的 Pos 把非 ASCII 字符按两个单元计数，而 Copy/Length 按一个字符计，



  所以搜中文或全角符号会给出越界下标；逐字符比较才与 Copy 同一套坐标。 }



function VCharPos(const S: String; const Need: String): Integer;



var I: Integer;



begin



  Result:=0;



  for I:=1 to Length(S) do



    if Copy(S,I,Length(Need))=Need then begin Result:=I; exit; end;



end;







{ 随包的 docs/更新日志.md 按「## V版本（日期）」切成小节：标题行的发布日期与右侧日志栏同源。 }



procedure VAddRelease(const Header: String);



var Body, Date: String; Open, Close: Integer;



begin



  if VReleaseCount>High(VReleaseVersions) then exit;



  Body:=Trim(Copy(Header,5,Length(Header)));



  Open:=VCharPos(Body,'（'); Close:=VCharPos(Body,'）');



  Date:='';



  if (Open>0) and (Close>Open) then begin



    Date:=Trim(Copy(Body,Open+1,Close-Open-1));



    Body:=Trim(Copy(Body,1,Open-1));



  end;



  VReleaseVersions[VReleaseCount]:=Body;



  VReleaseDates[VReleaseCount]:=Date;



  VReleaseBodies[VReleaseCount]:='';



  VReleaseCount:=VReleaseCount+1;



end;







{ 随包更新日志的文本在编译期内嵌为本文件里的纯字面量函数（由 docs/更新日志.md 生成：



  ' 双写为 ''，每个非 ASCII 字符出独立 #N 转义、ASCII 连续段出字面量，行间裸 #10，分块尾部补



  分块尾部也补转义；段落保持纯 ASCII）。之所以内嵌：运行时所有 UTF-8 解码路径在 CI 的 Inno 6.7.1 上已全数



  判死（UTF8Decode 返回值再赋 String 按 1252 盲宽化、LoadStringsFromFile 实测吐 1252 最佳适配



  指纹、外部 String 参数编组 1252 往返、Chr 截断字节域），唯有 #NNNN 转义实证支持宽码点；且



  单条语句的转义 token 一多编译产物即退问号（6.7.1 脚本编译器的长转义链缺陷），故拆成多条



  中等长度赋值。改完 docs/更新日志.md 后按同规则重生成此段；漂移会被 windows-installer-contract



  的更新日志逐字断言当场抓住（夹具期望值取自 md 原文）。 }



function VChangelogEmbedded: String;



begin



  Result:='# '#26356#26032#26085#24535#65288#32500#21051' Vex'#65289#10#10'## V1.11.0'#65288'2026-10-25'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#23433#35013#21253#32500#25252#39029#25353#21407#22411#37325#25490#65306#24050#23433#35013#34892#21482#25253#26412#26426#29256#26412#19982#23433#35013#26085#26399#65292#31532#20108#34892#25913#20026#20107#23454#38472#36848#12300#23433#35013#21253' <'#29256#26412#21495'>'#12301#24182#22312#21491#20391#26631#27880#21457#24067#26085#26399#12289#38468#19978#12300#26597#30475#26356#26032#26085#24535#12301#24378#35843#20837#21475#8212#8212#28857#20987#21518#31383#21475#21152#23485#65292#21491#26639#23637#24320#38543#21253#30340#36825#20221#26356#26032#26085#24535#65292#40664#35748#21482#23637#24320#26368#26032#19968#33410#65292#26356#26089#29256#26412#21487#25240#21472#26597#30475#65307#12300#25171#24320#12301#24443#24213#31227#38500#65292#21491#19979#35282#21807#19968#19968#39063#25191#34892#25353#38062#36319#38543#23433#35013#35814#24773#37324#30340#21333#36873#22312#12300#21319#32423' / '#20462#22797#23433#35013' / '#21368#36733#12301#20043#38388#20999#25442#65288#21368#36733#38477#20026#25991#23383#25353#38062#65289#65292#25972#39029#24378#35843#33394#38543#20043#33853#22312#25152#36873#25805#20316#19978#12290#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#32500#25252#39029#30340#26356#26032#26085#24535#26639#20999#22351#20013#25991#26465#30446#8212#8212'Inno '#30340' Pos '#25226#38750' ASCII '#23383#31526#25353#20004#20010#21333#20803#35745#25968#65292#32780#21462#23376#20018#25353#19968#20010#23383#31526#35745#65292#26085#26399#25324#21495#19982#26631#31614#21518#30340#36830#23383#31526#22240#27492#33853#22312#38169#20301#19978#65307#25913#29992#36880#23383#31526#23450#20301#65292#21457#24067#26085#26399#19982#12300'['#26032#22686'] '#27491#25991#12301#30340#20999#20998#24674#22797#24178#20928#12290#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#30452#25509#25171#24320#31243#24207#26102#19981#20877#33258#21160#36733#20837#38543#21253#30340#20845#20221#28436#31034#25991#26723#8212#8212#26410#24102#21442#25968#30340#21551#21160#21482#30041#19968#20221#31354#30333#25991#26723#65292#25991#20214#21015#34920#30001#29992#25143#33258#24049#25171#24320#20915#23450#65307#28436#31034#25991#26723#20173#21487#20174#12300#25991#20214' '#8594' '#28436#31034#25991#26723#12301#25171#24320#65292#24037#20316#21306#24674#22797#24320#20851#30340#34892#20026#19981#21464#12290#10#10'## V1.10.0'#65288'2026-10-25'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#31383#21475#25913#25104#22359#29366#24067#23616#65306#26631#39064#26639#30333#24213#12289#27491#25991#21306#28784#30011#24067#65292#25991#20214#26639#12289#32534#36753#35270#22270#12289#22823#32434#21508#33258#25104#20026#22278#35282#25551#36793#22359#65292#36793#32536#30041' 10px'#12289#22359#38388#30041' 4px'#65292#30028#38754#23618#32423#19968#30524#21487#36776#12290#10'- '#10024'['#26032#22686']-'#24213#37096#29366#24577#26639#36864#24441#65292#25913#20026#24748#28014#22312#27491#25991#19979#26041#30340#22278#35282#33014#22218#65306#20013#38388#26159#12300#28304#30721' / '#38405#35835' / '#23548#22270' / '#21363#26102#12301#20998#27573#30452#36873#65292#21491#31471#26174#31034#23383#25968#65307#24067#23616#30011#24266#22312#33014#22218#27491#19978#26041#25171#24320#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#25991#20214#19982#22823#32434#30340#25240#21472#25353#38062#31227#21040#22359#35282#65288#23637#24320#26102#36148#22359#20869#19979#35282#12289#25240#21472#26102#36148#31383#36793#65289#65292#20301#32622#38543#31383#21475#23485#24230#23454#26102#36319#38543#12290#10'- '#10024'['#26032#22686']-'#35270#22270#20998#26639#21487#25302#25341#65306#28304#30721#65295#39044#35272#65295#23548#22270#20043#38388#19982#19978#19979#20998#26639#37117#33021#25302#21160#65292#27604#20363#25353#24067#23616#20998#21035#35760#20303#65292#38190#30424#21487#29992#26041#21521#38190' 5% '#24494#35843#12289'Home/End '#25758#36793#30028#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#12300#24037#20855#12301#33756#21333#24182#20837#12300#25991#20214#12301#65306#20559#22909#35774#32622#12289#23383#25968#32479#35745#12289#24179#21488#22797#21046#12289'MCP '#30452#25509#22312#25991#20214#33756#21333#23637#24320#65292#39030#32423#33756#21333#30001#19971#20010#20943#20026#20845#20010#65307#35270#22270#33756#21333#21435#25481#19982#24213#37096#33014#22218#37325#22797#30340#24320#20851#12290#10'- '#10024'['#26032#22686']-'#27491#25991#25490#29256#24377#23618#25913#20026' 4 '#21015' 3 '#34892#21345#29255#65292#39044#35272#21435#25481#22810#20313#26631#35760#65292#35828#26126#19982#24341#29992#21508#21344#22266#23450#34892#25968#65292#19977#31181#30028#38754#23494#24230#19979#37117#26080#38656#28378#21160#21363#21487#30475#21040#12300#26032#22686#25490#29256#12301#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#26597#25214#26465#29420#31435#25104#22359#65292#19982#35270#22270#22359#21516#23485#65307#25991#20214#21015#34920#30340#25490#24207#12289#21047#26032#12289#25240#21472#24555#25463#38062#31227#21040#25628#32034#26694#27491#19979#26041#12290#10'- '#10024'['#26032#22686']-'#20803#32032#33756#21333#21452#20837#21475#19982#34920#26684#32467#26500#24341#25806#65306'20 '#39033#34920#26684#32467#26500#25805#20316#12289#27573#33853#36716#25442#12289#22359#30340#25554#20837#21024#38500#12289#20219#21153#21246#36873#20999#25442#19982#31896#36148#20026#32431#25991#26412#65292#25805#20316#21518#20809#26631#22238#21040#21407#21333#20803#26684#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#26631#39064#26639#31383#21475#25511#21046#25913#29992#38543#24102#30340' Lucide '#30690#37327#22270#26631#65292#22823#32434#25910#36215#38062#34917#24748#20572#21453#39304#19982#26080#38556#30861#21517#31216#12290#10'- '#9889'['#20248#21270']-'#20381#36182#21319#32423' CodeWF.Avalonia 12.2.9'#65306#25490#29256#30446#24405#31561#27604#32553#25918#12289#23548#22270#21345#29255#21407#22411#21270#12289#25972#31383#28014#23618#27133#65307'Mermaid '#22270#34920#37197#33394#19982#23494#24230#32852#21160#23545#40784#21407#22411#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#30028#38754#23494#24230#19977#26723#65288#32039#20945' / '#27491#24120' / '#23485#26494#65289#36827#20837#20559#22909#19982#22806#35266#33756#21333#65307#37197#33394#21345#34917#22806#25193#24378#35843#29615#65292#24377#20986#23618#31661#22836#32479#19968#24182#35302#21457#25991#23383#22402#30452#23621#20013#12290#10'- '#9889'['#20248#21270']-'#21491#20391#36741#21161#26639#25910#25947#20026#21333#19968#22823#32434#38754#26495#65292#25991#31456#20449#24687#25913#30001#23646#24615#23545#35805#26694#25215#36733#65292#31456#33410#35814#24773#24748#28014#22238#21040#23548#22270#21491#19978#35282#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#38405#35835#31383#26684#39064#27880#25353#21407#22411#37325#25490#65288#39064#27880#23621#24038#12289#25490#29256#20837#21475#23621#20013#12289#38025#20303#38062#23621#21491#65289#65292#34920#26684#24037#20855#19982#39044#35272#33410#22863#23545#40784#21407#22411#23454#27979#23610#23544#12290#10'- '#9889'['#20248#21270']-'#25490#29256#30011#24266#37325#20889#65306#27599#24352#21345#29255#29420#31435#39044#35272#12289#26041#21521#38190#27983#35272#65292#36873#25321#21518#19981#20877#20851#38381#38754#26495#65307#23548#22270#26679#24335#24377#23618#25913' 3 '#21015#32593#26684#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#36864#20986#19982#20999#25442#25991#26723#30340#26410#20445#23384#25552#31034#25353#31995#32479#20445#23384#26694#37325#32472#65288#20445#23384' + '#20002#24323' / '#21462#28040#65289#65292#20854#20313#24377#23618#23485#24230#32479#19968#25910#25947#21040' 480'#12289'620 '#20004#26723#65292#24377#23618#32972#26223#32479#19968#20026#30333#33394#12290#10'- '#9889'['#20248#21270']-'#20559#22909#35774#32622#25353#21407#22411#20998#20845#31867#65306#32534#36753#22120#39029#31934#31616#12289#36164#28304#30446#24405#19982#21069#26223#33394#24102#36873#25321#22120#12289#23548#20986#39029#25913#39029#36793#36317#25302#25341#12289'MCP '#23458#25143#31471#19982#23457#35745#25913#20998#27573#39029#31614#24182#34917#27979#35797#36830#25509#19982' stdio '#26725#25509#65292#23457#35745#25913#19977#21015#20498#24207#34920#26684#12290#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#36864#20986#28436#31034#21518#38405#35835#35270#22270#21482#21097#24403#21069#31456#33410#27491#25991#8212#8212#29616#22312#36864#20986#30636#38388#26080#26465#20214#22238#33853#20840#25991#65292#21518#32493#20219#20309#26174#38706#37117#28210#26579#25972#31687#12290#10'- '#128027'['#20462#22797']-Native AOT '#29256#26412#21551#21160#21363#22240' libSkiaSharp '#23849#28291#8212#8212#23548#22270#28857#38453#25913#29992#20301#22270#36148#29255#12290#10'- '#128027'['#20462#22797']-'#26597#25214#26367#25442#26465#21491#20391#34987#35009#25481#65292#35745#25968#24509#31456#12289'Aa'#12289#20840#23383#21305#37197#19982#20851#38381#38062#24674#22797#23436#25972#21487#35265#65307#23548#22270#26679#24335#24377#23618#37325#26032#21487#25171#24320#12290#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#26356#26032#26085#24535#19982#40483#35874#31383#20307#22312#21333#23454#20363#36716#21457#26102#36328#32447#31243#23849#28291#12290#10'- '#128027'['#20462#22797']-'#20999#25442#25991#26723#21518#27491#25991#28378#21160#20301#32622#19981#22797#20301#12289#30028#38754#23494#24230#19982#25490#29256#25913#21160#21482#22312#21551#21160#26102#29983#25928#31561#38382#39064#12290#10#10'## V1.9.0'#65288'2026-10-22'#65289#10#10'- '#10024'['#26032#22686']-'#29366#24577#26639#20013#22830#26032#22686#12300#28304#30721' / '#38405#35835' / '#23548#22270#12301#19977#27573#30452#36873#65292#12300#26356#22810#12301#25171#24320#23436#25972#24067#23616#30011#24266#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#28304#30721#12289#38405#35835#12289#23548#22270#19977#22788#24037#20855#26465#21487#12300#38025#20303#12301#65306#40664#35748#38025#20303#24120#39547#65292#21462#28040#21518#24748#28014#33258#21160#38544#34255#65292#29366#24577#20250#34987#35760#20303#65307#26032#25163#24341#23548#26399#38388#33258#21160#20840#31243#38025#20303#12290#10'- '#10024'['#26032#22686']-'#28436#31034#23383#21495#25913#20026' A'#8722' / '#25302#21160#26465' / A+ '#21152#30334#20998#27604#35835#25968#65292'1.0'#8211'2.5 '#23454#26102#32553#25918#27491#25991#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#26032#25163#24341#23548#65306#31532' 3 '#24149#30495#23454#23637#24320#12300#22806#35266#12301#38754#26495#25552#31034#23494#24230#19982#37197#33394#65307#31532' 4 '#24149#31561#22270#34920#30495#27491#20986#22270#20877#32487#32493#65307#31532' 5 '#24149#25913#29992#36731#25552#31034#65307#24341#23548#21345#25442#25104#28857#29366#36827#24230#19982#19978#19968#27493'/'#32487#32493'/'#19979#19968#27493#12290#10'- '#10024'['#26032#22686']-'#25991#26723#27700#21360#25903#25345#23383#21495#12289#21069#26223#33394#12289#31895#32454#19982#23454#26102#26679#20363#65292#38405#35835' / '#21363#26102' / '#28436#31034' / '#23548#20986#19968#33268#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#30028#38754#37197#33394#25903#25345#26032#22686' / '#32534#36753' / '#21024#38500#29992#25143#37197#33394#65288#21452#26126#26263' 4 '#33394#65292#20854#20313#35821#20041#38190#33258#21160#27966#29983#65289#12290#10'- '#10024'['#26032#22686']-'#23548#22270#25903#25345#20845#31181#24067#23616#65288#21333#20391#26641#24418' / '#21452#20391#24179#34913' / '#20013#24515#25918#23556' / '#21521#19979#26641#24418' / '#30446#24405#26641' / '#40060#39592#22270#65289#19982#20845#31181#26679#24335#65288#28165#38597' / '#32548#32439' / '#32447#26694' / '#33014#22218' / '#33033#32476' / '#40657#30333#65289#65292#38543#35774#32622#25345#20037#21270#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#38405#35835#35270#22270#34920#26684#24748#20572#20986#29616#12300#22797#21046' Markdown / '#19979#36733' CSV / '#39044#35272#34920#26684#12301#65307#22270#34920#25903#25345#21452#20987#36827#20837#28304#30721#32534#36753#19982#23454#26102#39044#35272#65292#20445#23384#21487#25764#38144#12290#10'- '#10024'['#26032#22686']-'#25490#29256#30011#24266#25903#25345#26032#22686' / '#21024#38500#33258#23450#20041#25490#29256#65288#22522#20110#31616#32422#27966#29983#65292#20379#28210#26579#19982#23548#20986#20351#29992#65289#12290#10'- '#10024'['#26032#22686']-'#25991#20214#33756#21333#25903#25345#20174' HTML / Word / PDF '#23548#20837#20026#26410#20445#23384#25991#26723#65292#24182#26032#22686#12300#20445#23384#20840#37096#12301#20837#21475#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#21333#23454#20363#36816#34892#65306#20877#27425#25171#24320#25991#20214#20250#36716#21040#24050#24320#31383#21475#65292#19981#20877#37325#22797#24320#36827#31243#65307#25991#26723#20445#23384#36880#36335#24452#20889#20837#38145#12290#10'- '#128296'['#20248#21270']-'#20559#22909#35774#32622#21435#25481#12300#35774#32622#24050#20445#23384' / '#23436#25104#12301#39029#33050#65292#25913#20540#21363#29983#25928#65307#22806#35266#39029#38598#20013#21160#25928#19982#27700#21360#21442#25968#12290#10'- '#128296'['#20248#21270']-'#29366#24577#26639#23383#25968#20837#21475#25913#20026#25171#24320#25991#31456#32479#35745#28014#23618#65307#38405#35835#19982#21363#26102#34920#26684#38138#28385#27491#25991#23485#24230#12290#10#10'## V1.8.0'#65288'2026-10-22'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#26631#39064#26639#26032#22686#12300#22806#35266#12301#33756#21333#65306#27973#33394#12289#28145#33394#12289#36319#38543#31995#32479#19977#26723#26126#26263#19982#31934#36873#37197#33394#21363#28857#21363#25442#65292#36873#25321#20250#34987#35760#20303#12290#10'- '#9889'['#20248#21270']-'#30028#38754#37197#33394#25509#20837#20849#20139#37197#33394#27880#20876#34920#65292#19982' CodeWF '#32452#20214#24211#20849#29992#21516#19968#22871#21697#29260#37197#33394#65292#26126#26263#27169#24335#19979#30340#25991#23383#23545#27604#24230#26356#31283#23450#12290#10'## V1.7.3'#65288'2026-10-21'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#20174#8220#25991#20214' / '#28436#31034#25991#26723#8221#25171#24320#38543#31243#24207#38468#24102#30340#20889#20316#12289#22270#25991#12289#20844#24335#21644#22270#34920#26679#20363#65292#31163#32447#20063#33021#38405#35835#21644#32451#20064#12290#65288'v1.7.2'#65289#10'- '#10024'['#26032#22686']-'#25552#20379#29420#31435#30340' Windows '#23433#35013#21253#21046#20316#33050#26412#65292#26041#20415#20462#25913#20195#30721#21518#33258#34892#21046#20316#23433#35013#21253#12290#65288'v1.7.1'#65289#10'- '#9889'['#20248#21270']-'#23433#35013#36827#24230#26465#24179#28369#21069#36827#65292#20943#23569#38378#30333#21644#21345#39039#65307#20851#38381#31995#32479#21160#30011#21518#30452#25509#26174#31034#23454#38469#36827#24230#12290#65288'v1.7.2'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#37325#26032#25972#29702#26356#26032#26085#24535#65292#25353#26085#26399#38598#20013#23637#31034#65292#29992#26356#26131#25026#30340#25991#23383#35828#26126#21464#21270#12290#65288'v1.7.2'#65289#10'- '#9889'['#20248#21270']-'#23448#32593#20837#21475#32479#19968#20351#29992' vex.codewf.com'#65292#24110#21161#33756#21333#12289#20851#20110#31383#21475#21644#23433#35013#39029#38754#37117#21487#35775#38382#12290#65288'v1.7.2'#65289#10'- '#9889'['#20248#21270']-'#26356#26032#26085#24535#12289#40483#35874#22312#29420#31435#31383#21475#25171#24320#65292#25903#25345#20687#26222#36890#25991#26723#19968#26679#32534#36753#65292#19981#25171#26029#24403#21069#20889#20316#12290#65288'v1.7.2'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#21152#24555#22270#34920#36739#22810#30340#25991#26723#20999#25442#65292#20248#20808#26174#31034#27491#22312#38405#35835#30340#22270#34920#65307#22270#34920#38543#39029#38754#23485#24230#23436#25972#32553#25918#65292#19981#20877#20986#29616#20869#37096#28378#21160#26465#12290#65288'v1.7.2'#65289#10'- '#9889'['#20248#21270']-'#22270#34920#37319#29992#27973#32043#33410#28857#19982#26580#21644#30340#22810#33394#37197#33394#65292#39292#22270#12289#26102#38388#32447#21644#26705#22522#22270#26356#26131#21306#20998#65292#28145#33394#27169#24335#20063#33021#28165#26970#38405#35835#12290#65288'v1.7.2'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#35843#25972#39030#37096#33756#21333#12289#22806#35266#21644#35821#35328#36873#25321#12289#25490#29256#19982#35270#22270#38754#26495#65292#20197#21450#20559#22909#35774#32622#12289#20851#20110#21644#26356#26032#31383#21475#30340#24067#23616#12290#65288'v1.7.1'#65289#10'- '#9889'['#20248#21270']-'#33756#21333#21644#24377#20986#38754#26495#30340#20986#29616#26356#33258#28982#65292#20851#38381#21160#25928#21518#31435#21363#21709#24212#25805#20316#12290#65288'v1.7.1'#65289#10'- '#9889'['#20248#21270']-'#32479#19968#25991#20214#21306#12289#25991#26723#32467#26500#21644#36741#21161#38754#26495#65292#25552#20379' 16 '#22871#37197#33394#19982' 27 '#22871#27491#25991#25490#29256#12290#65288'v1.7.0'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#35760#20303#25991#20214#22841#21644#25991#26723#32467#26500#30340#25240#21472#29366#24577#12289#38754#26495#23485#24230#12289#25490#24207#19982#39029#31614#65292#25628#32034#32467#26463#21518#24674#22797#21407#26469#30340#23637#24320#29366#24577#12290#65288'v1.7.0'#65289#10'- '#9889'['#20248#21270']-'#20445#23384#12289#23548#20986#21644#33609#31295#24674#22797#22833#36133#26102#20445#30041#25552#31034#65292#25903#25345#37325#35797#65307#22806#37096#20462#25913#21644#36864#20986#21069#26356#28165#26970#22320#20445#25252#26410#20445#23384#20869#23481#12290#65288'v1.7.0'#65289#10'- '#9889'['#20248#21270']-'#32479#19968#21508#24179#21488#30340#32511#33394#21697#29260#22270#26631#21644#23433#35013#30028#38754#65292#25552#20379' Windows'#12289'Linux '#21644' macOS '#23433#35013#21253#12290#65288'v1.7.0'#65289#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#26705#22522#22270#20004#20391#30340#25991#23383#21644#25968#23383#23436#25972#26174#31034#65292#36739#38271#30340#26631#31614#20063#19981#20250#34987#36793#32536#35009#25481#12290#10'- '#128027'['#20462#22797']-'#23433#35013#21253#27491#30830#26174#31034#30005#33041#19978#24050#23433#35013#30340#29256#26412#65292#19981#20877#25226#23427#26174#31034#25104#23433#35013#21253#30340#26032#29256#26412#12290#65288'v1.7.2'#65289#10'- '#128027'['#20462#22797']-'#34917#40784#23433#35013#31383#21475#21491#19978#35282#30340#36793#26694#65292#25913#21892#8220#25171#24320#8221#31561#25353#38062#22278#35282#30340#38191#40831#12290#65288'v1.7.2'#65289#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#28436#31034#20013#26469#22238#20999#25442#31456#33410#33021#21450#26102#26356#26032#20869#23481#65292#36864#20986#28436#31034#21518#20934#30830#22238#21040#38271#25991#26723#21407#26469#30340#38405#35835#20301#32622#12290#65288'v1.7.2'#65289#10'- '#128027'['#20462#22797']-'#20462#27491#20999#25442#35821#35328#26102#30340#24322#24120#65292#20197#21450#38271#25991#26723#36864#20986#28436#31034#21518#38405#35835#20301#32622#20559#31227#30340#38382#39064#12290#65288'v1.7.0'#65289#10#10'## V1.6.5'#65288'2026-10-20'#65289#10#10'- '#10024'['#26032#22686']-'#34917#40784#19971#32452#26700#38754#33756#21333#65292#20197#21450#33050#27880#12289#19978#19979#26631#12289#39640#20142#21644#22810#31181#22270#34920#27169#26495#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#20559#22909#35774#32622#38598#20013#31649#29702#24037#20316#21306#24674#22797#12289#33258#21160#20445#23384#12289#33609#31295#12289#23383#20307#12289#22270#29255#12289#27700#21360#21644#23548#20986#36873#39033#12290#10'- '#9889'['#20248#21270']-'#33258#21160#20445#23384#20445#30041#25991#20214#21407#26377#32534#30721#65292#24182#26816#26597#22806#37096#20462#25913#65307#21516#21517#22270#29255#20869#23481#19981#21516#26102#20998#21035#20445#23384#12290#10'- '#9889'['#20248#21270']-PDF '#23548#20986#25903#25345#36873#25321#32440#24352#12289#39029#36793#36317#21644#25991#23383#27700#21360#65292#23548#20986#21518#21487#23450#20301#25991#20214#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-MCP '#35774#32622#20998#20026#26381#21153#12289#23458#25143#31471#37197#32622#21644#25805#20316#23457#35745#19977#20010#39029#31614#65292#36830#25509#20449#24687#26356#28165#26970#12290#10'- '#9889'['#20248#21270']-Windows '#23433#35013#31383#21475#40664#35748#20445#25345#31616#27905#65292#21487#25353#38656#23637#24320#23433#35013#36873#39033#21644#24050#26377#23433#35013#35814#24773#12290#10'- '#128027'['#20462#22797']-'#20462#27491#20445#23384#20559#22909#21518#37096#20998#32534#30721#12289#25442#34892#36873#39033#21464#31354#30340#38382#39064#12290#10#10'## V1.6.4'#65288'2026-10-19'#65289#10#10'- '#10024'['#26032#22686']-'#25991#20214#23548#33322#19982#25991#26723#32467#26500#20998#21035#25918#22312#24038#21491#20004#20391#65292#35760#20303#25991#26723#32467#26500#30340#26174#31034#21644#24067#23616#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#34917#40784#25991#20214#25490#24207#12289#25240#21472#19982#25991#20214#22841#21491#38190#25805#20316#65292#25913#21892#25991#26723#26174#31034#21644#24037#20316#21306#20351#29992#20307#39564#12290#10#10'## V1.6.3'#65288'2026-10-17'#65289#10#10'- '#10024'['#26032#22686']-'#27491#25991#21644#23548#22270#25903#25345#28436#31034#65292#25353' F6 '#36827#20837#65307#36864#20986#21518#24674#22797#21407#24067#23616#21644#38405#35835#20301#32622#12290#65288'v1.6.0'#65289#10'- '#9889'['#20248#21270']-Windows '#40664#35748#23433#35013#21040#24403#21069#29992#25143#30446#24405#65307#21319#32423#26087#29256#26102#20445#30041#35774#32622#65292#36801#31227#21462#28040#25110#22833#36133#21518#20572#27490#23433#35013#65292#36991#20813#37325#22797#23433#35013#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#38405#35835#12289#21363#26102#32534#36753#21644#28436#31034#20013#30340#22270#29255#38543#27491#25991#23485#24230#32553#25918#65292#34920#26684#33258#21160#20998#37197#21015#23485#65292#27973#28145#33394#19979#26356#26131#38405#35835#12290#10'- '#9889'['#20248#21270']-'#24110#21161#33756#21333#21644#20851#20110#31383#21475#20849#29992#26816#26597#26356#26032#20837#21475#65292#20851#38381#21518#20877#27425#25171#24320#21487#32487#32493#19979#36733#25110#23433#35013#12290#65288'v1.6.1'#65289#10'- '#9889'['#20248#21270']-'#32593#39029#34920#26684#21487#30452#25509#38405#35835#65292#20445#30041#21333#20803#26684#20869#30340#25442#34892#12289#21015#34920#21644#25991#23383#26684#24335#12290#65288'v1.6.0'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#32479#19968#20013#25991#23383#20307#12289#26631#39064#34892#36317#12289#27491#25991#30041#30333#21644#24037#20855#26639#22270#26631#65292#32431#38405#35835#26102#38544#34255#32534#36753#25805#20316#12290#65288'v1.6.0'#65289#10'- '#128027'['#20462#22797']-'#25913#21892#23433#35013#31383#21475#22312#27973#33394#26700#38754#19978#30340#36793#26694#12289#38452#24433#21644#25353#38062#26174#31034#12290#65288'v1.6.2'#65289#10'- '#128027'['#20462#22797']-'#20462#27491#37096#20998' Windows '#23433#35013#21253#26080#27861#25171#24320#30340#38382#39064#12290#65288'v1.6.1'#65289#10'- '#128027'['#20462#22797']-'#26356#26032#23433#35013#22833#36133#12289#36229#26102#25110#21462#28040#26102#20445#30041#27491#22312#36816#34892#30340' Vex'#65292#25903#25345#37325#35797#21644#26597#30475#23433#35013#25991#20214#12290#65288'v1.6.1'#65289#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#20462#27491#38271#25991#26723#36864#20986#28436#31034#21518#36339#21040#20854#20182#31456#33410#65292#20197#21450#28857#20987#25991#26723#32467#26500#21518#30446#26631#26410#36827#20837#21487#35265#21306#22495#30340#38382#39064#12290#65288'v1.6.0'#65289#10'- '#128027'['#20462#22797']-'#25913#21892#21363#26102#32534#36753#30340#36328#27573#36873#25321#12289#22797#21046#19982#26367#25442#65292#20197#21450#25302#21160#26356#26032#26085#24535#28378#21160#26465#26102#30340#21345#39039#12290#65288'v1.6.0'#65289#10'- '#10060'['#21024#38500']-'#21457#24067#19979#36733#20013#31227#38500#20415#25658' ZIP'#65292#32479#19968#25552#20379#21508#24179#21488#23433#35013#21253#12290#65288'v1.6.0'#65289#10#10'## V1.5.9'#65288'2026-10-16'#65289#10#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#25554#20837#33756#21333#30452#25509#21015#20986#24120#29992#21629#20196#65292#36739#23567#23631#24149#20063#33021#26597#30475#23436#25972#33756#21333#12290#10'- '#9889'['#20248#21270']-'#21363#26102#32534#36753#21482#22312#24403#21069#32534#36753#27573#33853#26174#31034#28304#30721#65292#20854#20182#27573#33853#20445#25345#25490#29256#65292#20943#23569#37325#22797#20869#23481#12290#10'- '#9889'['#20248#21270']-'#25193#22823#23485#23631#27491#25991#21306#22495#65292#35843#25972#27491#25991#12289#20195#30721#21644#29366#24577#26639#23383#21495#65292#25991#20214#21015#34920#26356#32039#20945#12290#10'- '#9889'['#20248#21270']-'#23433#35013#26102#21487#36873#25321#26159#21542#25226' Vex '#21152#20837' Markdown '#25171#24320#26041#24335#65292#20043#21518#21551#21160#19981#20250#25797#33258#37325#26032#27880#20876#12290#65288'v1.5.8'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#35760#20303#35270#22270#12289#23548#22270#12289#33258#21160#37197#23545#12289#21160#25928#12289#19987#27880#21644#25171#23383#26426#27169#24335#30340#35774#32622#65307#20999#25442#25991#26723#26102#20943#23569#37325#22797#21152#36733#12290#65288'v1.5.8'#65289#10'- '#128027'['#20462#22797']-'#20462#27491#25302#21160#20391#26639#20998#38548#32447#21518#20986#29616#31354#30333#12289#27491#25991#38169#20301#65292#20197#21450' MCP '#35774#32622#26080#27861#20851#38381#30340#38382#39064#12290#10'- '#128027'['#20462#22797']-'#20462#27491#27973#33394#32972#26223#19979#20195#30721#25991#23383#19981#28165#26970#30340#38382#39064#12290#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#20462#27491#37096#20998#31995#32479#19979#25991#20214#36335#24452#35782#21035#38169#35823#65292#20197#21450#25353#38062#24748#20572#21518#25991#23383#19981#28165#26970#30340#38382#39064#12290#65288'v1.5.8'#65289#10'- '#128027'['#20462#22797']-'#20999#25442#35821#35328#21518#65292#32534#36753#22120#21491#38190#33756#21333#21644#20869#32622#31383#21475#20063#21450#26102#26356#26032#12290#65288'v1.5.6'#65289#10'- '#10060'['#21024#38500']-'#31227#38500#24403#26102#37325#22797#30340#28304#30721#24182#25490#39044#35272#20837#21475#65292#28304#30721#19982#38405#35835#20998#21035#25552#20379#29420#31435#35270#22270#12290#10#10'## V1.5.5'#65288'2026-10-15'#65289#10#10'- '#10024'['#26032#22686']-'#23548#22270#25903#25345#20840#25991#12289#25240#21472#12289#32553#25918#21644#23450#20301#65292#21487#35843#25972#27573#33853#25688#35201#38271#24230#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#25552#20379#21487#20132#20114#30340#26032#25163#24341#23548#65292#28436#32451#32467#26463#21518#24674#22797#21407#26469#30340#25991#26723#21644#35774#32622#12290#10'- '#9889'['#20248#21270']-'#24037#20316#21306#25353#32534#36753#12289#38405#35835#21644#23548#22270#32452#32455#65292#32534#36753#21306#21487#36873#25321#21363#26102#32534#36753#25110#28304#30721#12290#10'- '#9889'['#20248#21270']-'#26631#39064#26639#38598#20013#36873#25321#37197#33394#12289#27973#28145#27169#24335#21644#35821#35328#65292#24182#20998#21035#35760#20303#36873#25321#12290#10'- '#9889'['#20248#21270']-'#34920#26684#26681#25454#20869#23481#20998#37197#21015#23485#65292#20943#23569#21491#20391#22810#20313#31354#30333#65307#21363#26102#32534#36753#25903#25345#36328#27573#25764#38144#21644#37325#20570#12290#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#25913#21892' macOS '#19979#35821#35328#21644#20869#32622#24110#21161#25991#26723#30340#21152#36733#12290#10#10'## V1.5.4'#65288'2026-10-14'#65289#10#10'- '#10024'['#26032#22686']-'#21457#29616#26032#29256#21518#21487#30452#25509#19979#36733#23433#35013#21253#65292#25903#25345#21462#28040#12289#37325#35797#21644#19979#36733#23436#25972#24615#26816#26597#12290#10'- '#9889'['#20248#21270']-'#31616#20307#20013#25991#12289#32321#20307#20013#25991#12289#33521#25991#21644#26085#25991#21487#30452#25509#20999#25442#65292#30028#38754#27169#24335#19982#37197#33394#20998#21035#20445#23384#12290#10'- '#9889'['#20248#21270']-'#34920#26684#25353#20869#23481#20998#37197#21015#23485#65292#31364#31383#21475#21644#38271#35789#20063#33021#27491#24120#26174#31034#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#23433#35013#21253#25552#20379' Markdown '#25991#20214#22270#26631#21644#25171#24320#26041#24335#20505#36873#65292#20445#30041#29992#25143#21407#26377#40664#35748#24212#29992#12290#10'- '#128027'['#20462#22797']-'#20462#27491#20027#31383#21475#39030#37096#22810#20313#31354#30333#65292#20197#21450#20851#20110#21644#26410#20445#23384#25552#31034#26080#27861#27491#24120#25171#24320#30340#38382#39064#12290#10#10'## V1.5.3'#65288'2026-10-08'#65289#10#10'- '#9889'['#20248#21270']-'#25991#20214#26641#25913#20026#32039#20945#21333#34892#26174#31034#65292#32479#19968#23618#32423#32553#36827#21644#22270#25991#23545#40784#65292#38271#25991#20214#21517#30465#30053#65292#23436#25972#20449#24687#21487#24748#20572#26597#30475#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#35843#25972#26631#39064#26639#12289#20391#26639#39029#31614#12289#25991#20214#34892#21644#29366#24577#26639#30340#38388#36317#21450#37197#33394#12290#65288'v1.5.2'#65289#10'- '#128027'['#20462#22797']-'#20462#27491#36830#32493#20999#25442#25991#26723#26102#21487#33021#20986#29616#30340#26174#31034#24322#24120#12290#10'- '#128027'['#20462#22797']-'#20462#27491#20391#26639#26174#31034#21364#27809#26377#23485#24230#12289#25991#20214#34892#37197#33394#24322#24120#65292#20197#21450#26410#20462#25913#25991#26723#34987#35823#21028#20026#24050#20462#25913#30340#38382#39064#12290#65288'v1.5.2'#65289#10#10'## V1.5.1'#65288'2026-10-07'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#36755#20837#25104#23545#31526#21495#12289#24555#36895#25171#24320#25991#26723#12289#25302#20837#22270#29255#12289#22797#21046#20026#32431#25991#26412#65292#20197#21450#28857#20987#20219#21153#26694#20462#25913#23436#25104#29366#24577#12290#65288'v1.5.0'#65289#10'- '#10024'['#26032#22686']-'#19987#27880#27169#24335#38544#34255#24178#25200#21306#22495#65292#25171#23383#26426#27169#24335#35753#24403#21069#36755#20837#34892#20445#25345#22312#35270#37326#20013#37096#12290#65288'v1.5.0'#65289#10'- '#9889'['#20248#21270']-'#26597#25214#12289#25991#20214#37325#21629#21517#12289#25991#26723#23646#24615#31561#25805#20316#38598#20013#22312#24212#29992#20869#24377#31383#23436#25104#65292#20943#23569#29420#31435#31383#21475#12290#65288'v1.5.0'#65289#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#37325#21629#21517#26102#33258#21160#36873#20013#25991#20214#21517#65292#25903#25345' Enter '#30830#35748#21644' Esc '#21462#28040#12290#65288'v1.5.0'#65289#10'- '#9889'['#20248#21270']-'#20027#39064#12289#33756#21333#12289#20391#26639#21644#29366#24577#26639#26356#21327#35843#65292#21160#25928#21487#32479#19968#20851#38381#12290#65288'v1.5.0'#65289#10'- '#128027'['#20462#22797']-'#20462#22797#19978#19968#29256#26412#23433#35013#21253#26410#33021#27491#24120#29983#25104#30340#38382#39064#12290#10#10'## V1.4.0'#65288'2026-10-06'#65289#10#10'- '#10024'['#26032#22686']-'#27491#25991#21487#26174#31034' Mermaid '#27969#31243#22270#12289#26102#24207#22270#31561#22270#34920#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#26410#20445#23384#30830#35748#12289#38169#35823#25552#31034#21644#25991#20214#37325#21629#21517#32479#19968#22312#24212#29992#20869#26174#31034#12290#10'- '#9889'['#20248#21270']-'#20445#23384#12289#23548#20986#12289#22797#21046#21644#37325#21629#21517#23436#25104#21518#32473#20986#31616#30701#25552#31034#65292#19981#25171#26029#32487#32493#20889#20316#12290#10'- '#128027'['#20462#22797']-'#25913#21892#28145#33394#27169#24335#21551#21160#26102#38378#30333#65292#20197#21450#37096#20998#20027#39064#39068#33394#19981#19968#33268#30340#38382#39064#12290#10#10'## V1.3.4'#65288'2026-10-04'#65289#10#10'- '#10024'['#26032#22686']-'#25552#20379' Windows x64'#12289'Linux x64/ARM64'#12289'macOS Intel/Apple Silicon '#20116#31181#23433#35013#21253#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#20851#20110#31383#21475#21487#26816#26597#26032#29256#24182#25171#24320#19979#36733#39029#38754#12290#65288'v1.3.1'#65289#10'- '#9889'['#20248#21270']-'#20248#21270#21508#24179#21488#21551#21160#19982#20869#23384#20351#29992#65292#23578#26410#25903#25345#30340#25805#20316#20250#32473#20986#26126#30830#25552#31034#12290#65288'v1.3.3'#65289#10'- '#9889'['#20248#21270']-'#35774#32622#21644#24212#29992#25968#25454#20445#23384#21040#21508#31995#32479#30340#26631#20934#30446#24405#65292#39318#27425#21551#21160#26102#36801#31227#26087#25968#25454#24182#20445#30041#22791#20221#12290#65288'v1.3.2'#65289#10#10'## V1.3.0'#65288'2026-10-02'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-MCP '#25903#25345#26597#25214#26367#25442#12289#25764#38144#37325#20570#12289#21015#20986#25991#20214#21644#26032#24314#25991#26723#65292'AI '#25805#20316#21487#22312#24212#29992#20013#32487#32493#25764#38144#12290#10'- '#10024'['#26032#22686']-MCP '#21487#35835#21462#25480#26435#33539#22260#20869#30340#25991#26723#65292#25552#20379#24120#35265' AI '#23458#25143#31471#30340#36830#25509#35828#26126#12290#10'- '#10024'['#26032#22686']-MCP '#25805#20316#23457#35745#20250#20445#30041#21040#37325#21551#21518#65292#29366#24577#26639#23454#26102#26174#31034#26381#21153#26159#21542#36816#34892#12290#10'- '#10024'['#26032#22686']-MCP '#26412#22320#26381#21153#21487#36830#25509' AI '#23458#25143#31471#65292#35835#21462#21644#32534#36753#25480#26435#25991#26723#65292#37325#35201#25805#20316#20808#30830#35748#12290#65288'v1.2.0'#65289#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#21487#25171#24320#25972#20010#25991#20214#22841#65292#33258#21160#21047#26032#26032#22686#12289#21024#38500#21644#37325#21629#21517#30340#25991#20214#65292#24182#24674#22797#19978#27425#24037#20316#21306#12290#65288'v1.2.0'#65289#10'- '#128027'['#20462#22797']-'#25913#21892' Windows '#23433#35013#21518#30340#35774#32622#20445#23384#65292#36991#20813#37325#21551#21518#20027#39064#31561#20559#22909#20002#22833#12290#10'- '#128027'['#20462#22797']-'#20462#27491#27491#24335#29256#26412#20013#37096#20998#33756#21333#26080#27861#28857#20987#65292#20197#21450#22270#29255#35828#26126#12289#22823#32434#35282#26631#21644#20195#30721#25991#23383#19981#28165#26970#30340#38382#39064#12290#65288'v1.2.0'#65289#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#22810#20010#31383#21475#21516#26102#20351#29992#26102#65292#20445#23384#33258#24049#30340#35774#32622#19981#20250#35206#30422#20854#20182#31383#21475#21018#20570#30340#20462#25913#12290#65288'v1.2.0'#65289#10'- '#128027'['#20462#22797']-MCP '#25171#24320#20854#20182#25991#26723#21069#20808#30830#35748#26410#20445#23384#20869#23481#65292#20462#27491#26356#26032#26085#24535#26080#27861#25171#24320#30340#38382#39064#12290#65288'v1.2.0'#65289#10#10'## V1.1.2.3'#65288'2026-06-08'#65289#10#10'- '#9889'['#20248#21270']-'#31616#21270#24182#25918#22823' Vex '#22270#26631#65292#20219#21153#26639#23567#23610#23544#19979#26356#23481#26131#36776#35748#12290#10'- '#9889'['#20248#21270']-'#32479#19968#20869#32622#20013#25991#24110#21161#21644#25991#26723#20837#21475#65292#20943#23569#37325#22797#20869#23481#12290#65288'v1.1.2.2'#65289#10;



  Result:=Result+'- '#128027'['#20462#22797']-'#25991#20214#22841#20013#30340#25991#26723#21464#21270#20250#21450#26102#21047#26032#65292'MCP '#35774#32622#21644#25805#20316#25552#31034#38543#30028#38754#35821#35328#20999#25442#12290#65288'v1.1.2.2'#65289#10#10'## V1.1.2.1'#65288'2026-06-02'#65289#10#10'- '#10024'['#26032#22686']-'#26356#26032#24212#29992#22270#26631#65292#25913#21892#20219#21153#26639#21644#31383#21475#20013#30340#26174#31034#12290#10'- '#9889'['#20248#21270']-'#35843#25972#33756#21333#12289#39029#31614#12289#28378#21160#26465#21644#25552#31034#26694#65292#27973#28145#33394#19979#26356#21327#35843#12290#10'- '#9889'['#20248#21270']-'#26368#36817#25991#20214#21487#24748#20572#26597#30475#23436#25972#36335#24452#65292#21516#21517#25991#20214#26356#23481#26131#21306#20998#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#20851#20110#21644#40483#35874#31383#21475#26356#32039#20945#65292#32593#31449#19982#25991#26723#38142#25509#21487#30452#25509#25171#24320#12290#10#10'## V1.1.2'#65288'2026-05-27'#65289#10#10'- '#10024'['#26032#22686']-'#20174#32593#39029#31896#36148#20869#23481#26102#65292#21487#20248#20808#36716#25442#20026' Markdown'#12290#10'- '#10024'['#26032#22686']-'#25903#25345#23548#20986' Word '#25991#26723#65292#23548#20986#21518#21487#25171#24320#25991#20214#25152#22312#20301#32622#12290#65288'v1.1.1'#65289#10'- '#9889'['#20248#21270']-PDF '#27491#25991#21487#36873#25321#21644#22797#21046#65292'PDF '#19982' Word '#20445#30041#22270#29255#65292#26041#20415#31163#32447#38405#35835#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#22797#21046#21040#24494#20449#20844#20247#21495#12289#30693#20046#21644#31232#22303#25496#37329#26102#20445#30041#24403#21069#25490#29256#12289#22270#29255#21644#34920#26684#26679#24335#12290#65288'v1.1.1'#65289#10'- '#128027'['#20462#22797']-'#20462#27491#21516#19968#34892#20013#21152#31895#12289#26012#20307#21644#21024#38500#32447#26174#31034#19981#27491#30830#65292#20197#21450#20013#25991#21152#31895#22833#25928#30340#38382#39064#12290#10'- '#128027'['#20462#22797']-'#38480#21046#26597#25214#26639#30340#36229#38271#36755#20837#65292#36991#20813#25745#20081#30028#38754#65307#20462#27491#32593#39029#31896#36148#26102#20986#29616#21407#22987' HTML '#30340#38382#39064#12290#65288'v1.1.1'#65289#10#10'## V0.1.0'#65288'2026-05-22'#65289#10#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#25552#20379' Markdown '#32534#36753#12289#39044#35272#12289#25991#20214#21015#34920#21644#25991#26723#22823#32434#12290#10'- '#10024'['#26032#22686']-'#25903#25345#26032#24314#12289#25171#24320#12289#20445#23384#12289#21478#23384#20026#12289#37325#21629#21517#19982#21024#38500#25991#26723#12290#10'- '#10024'['#26032#22686']-'#25903#25345#26597#25214#26367#25442#12289#21306#20998#22823#23567#20889#12289#25972#35789#21644#27491#21017#21305#37197#12290#10'- '#10024'['#26032#22686']-'#25903#25345#33258#21160#24310#32493#21015#34920#19982#32553#36827#65292#20197#21450#20219#21153#21015#34920#12289#20195#30721#39640#20142#21644#26412#22320#22270#29255#12290#10;



  Result:=Result+'- '#10024'['#26032#22686']-'#25903#25345' HTML'#12289#25171#21360#39044#35272#12289'PNG'#12289'PDF '#21644' Word '#23548#20986#65292#20197#21450#22797#21046#21040#24120#35265#20889#20316#24179#21488#12290#10'- '#10024'['#26032#22686']-'#25552#20379#37197#33394#12289#27491#25991#25490#29256#12289#34892#21495#12289#29366#24577#26639#21644#31383#21475#32622#39030#36873#39033#12290#10'- '#10024'['#26032#22686']-'#25552#20379#26032#25163#24341#23548#12289#24555#36895#24320#22987#12289#40483#35874#65292#20197#21450#31616#20307#20013#25991#12289#32321#20307#20013#25991#12289#33521#25991#21644#26085#25991#30028#38754#12290#10'- '#9889'['#20248#21270']-'#22806#37096#20462#25913#20250#33258#21160#21047#26032#65292#23384#22312#26410#20445#23384#20869#23481#26102#20808#20445#25252#24403#21069#32534#36753#12290#10;



  Result:=Result+'- '#9889'['#20248#21270']-'#25913#21892#38271#25991#26723#30340#32479#35745#12289#25991#26723#32467#26500#25195#25551#12289#26597#25214#21644#39044#35272#21709#24212#12290#10;



end;







{ 只列比本机版本新的小节；本机版本读不到时全列，没有可列的则退回本版本自己。 }



function VVisibleReleases(const Installed: String): Integer;



var I: Integer;



begin



  Result:=0;



  if not VChangelogLoad then exit;



  for I:=0 to VReleaseCount-1 do



    if (Installed='') or VIsOlder(Installed,VReleaseVersions[I]) then begin



      if Result>High(VShownReleases) then break;



      VShownReleases[Result]:=I; Result:=Result+1;



    end;



  if Result=0 then



    for I:=0 to VReleaseCount-1 do



      if VReleaseVersions[I]='{#AppVersion}' then begin VShownReleases[0]:=I; Result:=1; break; end;



end;







function VPackageReleaseDate: String;



var I: Integer;



begin



  Result:='';



  if not VChangelogLoad then exit;



  for I:=0 to VReleaseCount-1 do



    if VReleaseVersions[I]='{#AppVersion}' then begin Result:=VReleaseDates[I]; exit; end;



end;







function VRTFColor(Color: TColor): String;



begin



  Result:='\red'+IntToStr(Color and $FF)+'\green'+IntToStr((Color shr 8) and $FF)+'\blue'+IntToStr((Color shr 16) and $FF);



end;







function VRTFText(const Text: String): String;



var I, Code: Integer;



begin



  Result:='';



  for I:=1 to Length(Text) do



    if (Text[I]='\') or (Text[I]='{') or (Text[I]='}') then Result:=Result+'\'+Text[I]



    else begin



      Code:=Ord(Text[I]);



      if Code>127 then begin



        if Code>32767 then Code:=Code-65536;



        Result:=Result+'\u'+IntToStr(Code)+'?';



      end else Result:=Result+Text[I];



    end;



end;







{ 「- ✨[新增]-正文」渲染成带色标签加正文；跨版本升级时更早的小节默认收起。 }



function VRTFBullet(const Line: String): String;



var Tag, Text, Rest: String; Open, Close, Color: Integer;



begin



  Text:=Trim(Copy(Line,3,Length(Line)));



  Result:='\pard\sa70\fs18';



  Open:=VCharPos(Text,'['); Close:=VCharPos(Text,']');



  if (Open>0) and (Open<=3) and (Close>Open) then begin



    Tag:=Trim(Copy(Text,Open+1,Close-Open-1));



    Rest:=Trim(Copy(Text,Close+1,Length(Text)));



    { 源文档写作「✨[新增]-正文」，标签后的连字符只是分隔符，留着会和标签连读成正文开头。 }



    if Copy(Rest,1,1)='-' then Rest:=Trim(Copy(Rest,2,Length(Rest)));



    Color:=2;



    if Tag='新增' then Color:=3;



    if Tag='修复' then Color:=4;



    { 控制字后的第一个空格会被 RTF 当分隔符吃掉，标签与正文之间要留两个才剩一个。 }



    Result:=Result+'\cf'+IntToStr(Color)+' ['+VRTFText(Tag)+']\cf1  '+VRTFText(Rest);



  end else Result:=Result+'\cf1 '+VRTFText(Text);



  Result:=Result+'\par ';



end;

function VChangelogLoad: Boolean;
var Text, Line: String; I: Integer;
begin
  if not VChangelogLoaded then begin
    VChangelogLoaded:=True;
    Text:=VChangelogEmbedded+#10;
    for I:=1 to Length(Text) do
      if Text[I]=#10 then begin
        if Copy(Line,1,4)='## V' then VAddRelease(Line)
        else if (VReleaseCount>0) and (Copy(Trim(Line),1,2)='- ') then
          VReleaseBodies[VReleaseCount-1]:=VReleaseBodies[VReleaseCount-1]+Trim(Line)+#10;
        Line:='';
      end else if Text[I]<>#13 then Line:=Line+Text[I];
  end;
  Result:=VReleaseCount>0;
end;

procedure VRenderChangelog
procedure VRenderChangelog(const Installed: String);



var RTF, Body, Line, Key: String; I, J, Index, Shown: Integer;



begin



  VShownCount:=VVisibleReleases(Installed);



  Shown:=VShownCount;



  if (not VChangelogEarlier) and (Shown>1) then Shown:=1;



  Key:=Installed+'|'+IntToStr(Shown);



  if VLogRenderedFor=Key then exit;



  VLogRenderedFor:=Key;



  RTF:='{\rtf1\ansi\deff0\uc1{\colortbl;'+VRTFColor(VText)+';'+VRTFColor(VMuted)+';'+



    VRTFColor(VAccent)+';'+VRTFColor(VDanger)+'}{\fonttbl{\f0 '+VFontFace(False)+';}}\f0\fs18 ';



  if VShownCount=0 then RTF:=RTF+'\pard\sa70\cf2 '+VRTFText(CustomMessage('VChangelogEmpty'))+'\par '



  else for I:=0 to Shown-1 do begin



    Index:=VShownReleases[I];



    RTF:=RTF+'\pard\sb180\sa80\fs20\cf3\b '+VRTFText('V'+VReleaseVersions[Index])+



      '\b0\cf2  '+VRTFText(VReleaseDates[Index])+'\par ';



    Body:=VReleaseBodies[Index]+#10; Line:='';



    for J:=1 to Length(Body) do



      if Body[J]=#10 then begin



        if Line<>'' then RTF:=RTF+VRTFBullet(Line);



        Line:='';



      end else if Body[J]<>#13 then Line:=Line+Body[J];



  end;



  VLogMemo.RTFText:=RTF+'}';



end;







procedure VPaintBackground;



var Canvas: TCanvas; Saved, I: Integer;



begin



  VBack.Bitmap.Width:=WizardForm.ClientWidth; VBack.Bitmap.Height:=WizardForm.ClientHeight;



  Canvas:=VBack.Bitmap.Canvas;



  Canvas.Brush.Color:=VSurface; Canvas.Pen.Color:=VSurface;



  Canvas.Rectangle(0,0,VBack.Bitmap.Width,VBack.Bitmap.Height);



  Canvas.Brush.Color:=VBrand; Canvas.Pen.Color:=VBrand;



  Canvas.Rectangle(ScaleX(1),ScaleY(1),ScaleX(180),VBack.Bitmap.Height-ScaleY(1));



  Saved:=VSaveDC(Canvas.Handle);



  VClipRect(Canvas.Handle,ScaleX(1),ScaleY(1),ScaleX(180),VBack.Bitmap.Height-ScaleY(1));



  Canvas.Brush.Style:=bsClear;



  Canvas.Pen.Color:=$DAE4D7;



  if VSurface<>clWhite then Canvas.Pen.Color:=$32402A;



  Canvas.Ellipse(ScaleX(-179),VBack.Bitmap.Height-ScaleY(210),ScaleX(261),VBack.Bitmap.Height+ScaleY(230));



  VRestoreDC(Canvas.Handle,Saved); Canvas.Brush.Style:=bsSolid;



  if (WizardForm.CurPageID=wpWelcome) and VExpanded and not VLicenseOpen then begin



    VRound(Canvas.Handle,ScaleX(209),ScaleY(82),ScaleX(402),ScaleY(177),7,VLine,True);



    Canvas.Pen.Color:=VLine; Canvas.MoveTo(ScaleX(210),ScaleY(125)); Canvas.LineTo(ScaleX(610),ScaleY(125));



    VRound(Canvas.Handle,ScaleX(222),ScaleY(138),ScaleX(310),ScaleY(38),8,VField,False);



    VRound(Canvas.Handle,ScaleX(222),ScaleY(138),ScaleX(310),ScaleY(38),8,VLine,True);



  end;



  if VIsMaintenance and not VLicenseOpen then begin



    VRound(Canvas.Handle,ScaleX(209),ScaleY(118),ScaleX(402),ScaleY(72),8,VSoft,False);



    VRound(Canvas.Handle,ScaleX(209),ScaleY(118),ScaleX(402),ScaleY(72),8,VLine,True);



  end;



  if VIsMaintenance and VChangelogOpen and not VLicenseOpen then begin



    { 右侧日志栏是一整块浅底，与正文列之间只留一条分隔线。 }



    Canvas.Brush.Color:=VSoft; Canvas.Pen.Color:=VSoft;



    Canvas.Rectangle(ScaleX(640),ScaleY(1),VBack.Bitmap.Width,VBack.Bitmap.Height-ScaleY(1));



    Canvas.Pen.Color:=VLine; Canvas.MoveTo(ScaleX(640),ScaleY(1));



    Canvas.LineTo(ScaleX(640),VBack.Bitmap.Height-ScaleY(1));



    if VLogMore.Visible then begin



      Canvas.MoveTo(ScaleX(656),VBack.Bitmap.Height-ScaleY(46));



      Canvas.LineTo(VBack.Bitmap.Width-ScaleX(16),VBack.Bitmap.Height-ScaleY(46));



    end;



  end;



  if (WizardForm.CurPageID=wpFinished) and not VLicenseOpen then begin



    VRound(Canvas.Handle,ScaleX(392),ScaleY(74),ScaleX(36),ScaleY(36),18,VAccentSoft,False);



    VFont(Canvas.Font,20,False); Canvas.Font.Color:=VAccent; Canvas.Brush.Color:=VAccentSoft;



    Canvas.TextOut(ScaleX(401),ScaleY(78),'✓');



  end;



  if VLicenseOpen then



    VRound(Canvas.Handle,ScaleX(208),ScaleY(88),ScaleX(404),ScaleY(202),7,VSoft,False);



  VRound(Canvas.Handle,0,0,VBack.Bitmap.Width,VBack.Bitmap.Height,8,VLine,True);



  { 布局完成后统一请求下一帧，避免同步重画背景覆盖尚未失效的自绘按钮。 }



  VInvalidate(WizardForm.Handle,0,False);



  for I:=0 to 9 do



    if (VButtons[I]<>nil) and VButtons[I].Visible then VInvalidate(VButtons[I].Handle,0,False);



end;







procedure VDrawProgress;



var Position, Pixel, Percent: Integer;



begin



  Pixel:=VProgressShown*ScaleX(402) div 10000;



  Percent:=VProgressShown div 100;



  if (Pixel=VProgressPixel) and (Percent=VProgressPercent) then exit;



  VProgressPixel:=Pixel; VProgressPercent:=Percent;



  VPercent.Caption:=IntToStr(Percent)+'%';



  Position:=VProgressShown*402 div 10000;



  if Position<18 then Position:=18; if Position>384 then Position:=384;



  VBounds(VPercent,209+Position-18,120,36,18);



  if VProgressFrame.Width<>ScaleX(402) then VProgressFrame.Width:=ScaleX(402);



  if VProgressFrame.Height<>ScaleY(5) then VProgressFrame.Height:=ScaleY(5);



  VProgressFrame.Canvas.Brush.Color:=VSurface;



  VProgressFrame.Canvas.Pen.Color:=VSurface;



  VProgressFrame.Canvas.Rectangle(0,0,VProgressFrame.Width,VProgressFrame.Height);



  VRound(VProgressFrame.Canvas.Handle,0,0,VProgressFrame.Width,VProgressFrame.Height,3,VSoft,False);



  if Pixel>0 then VRound(VProgressFrame.Canvas.Handle,0,0,Pixel,VProgressFrame.Height,3,VAccent,False);



  { 在离屏位图完成整帧后只提交一次，避免清空、画轨道、画进度分别暴露到屏幕。 }



  VProgressBar.Bitmap.Assign(VProgressFrame);



#ifdef CodeWFSetupFixture



  Log('VEX FIXTURE progress='+IntToStr(VProgressShown)+' target='+IntToStr(VProgressTarget));



#endif



end;







procedure VAdvanceProgress;



var Elapsed: LongWord; T, Remaining: Extended;



begin



  Elapsed:=VTickCount-VProgressStarted;



  if (Elapsed>=160) or not VProgressAnimated then begin



    VProgressShown:=VProgressTarget;



    VKillTimer(WizardForm.Handle,42);



  end else begin



    T:=Elapsed/160.0; Remaining:=1-T;



    VProgressShown:=VProgressFrom+Round((VProgressTarget-VProgressFrom)*(1-Remaining*Remaining*Remaining));



  end;



  VDrawProgress;



end;







procedure VLayout;



var Page, Height, Width, I, ButtonY, BrandY, NextWidth, PillWidth, PillX, TextWidth: Integer;



  Home, Finished, Installing, Maintenance, Main, Panel: Boolean;



  Region: LongWord; TextBounds: TVRect; Selected: Integer;



  InstalledVersion, InstalledPath, InstalledOn, PackageDate, VFixLog, VFixClass: String;



begin



  if not VInitialized then exit;



  Page:=WizardForm.CurPageID;



  Home:=Page=wpWelcome; Finished:=Page=wpFinished; Installing:=Page=wpInstalling;



  Maintenance:=VIsMaintenance; Main:=not VLicenseOpen;



  Height:=360;



  if Maintenance and VDetailsOpen then Height:=620;



  if not Home and not Finished and not Installing and not Maintenance then Height:=560;



  { 更新日志栏在正文列右侧展开：正文列恒为 640，窗口按整栏宽度加长。 }



  Width:=640;



  Panel:=Main and Maintenance and VChangelogOpen;



  if Panel then Width:=960;



  VWindowWidth:=Width;



  WizardForm.ClientHeight:=ScaleY(Height);



  WizardForm.ClientWidth:=ScaleX(Width);



  Region:=VRegion(WizardForm.ClientWidth,WizardForm.ClientHeight);



  if VSetRegion(WizardForm.Handle,Region,True)=0 then VDeleteObject(Region);



  VBounds(VBack,0,0,Width,Height);



  VTitle.Visible:=False;



  BrandY:=(Height-135) div 2;



  VMark.Visible:=True; VName.Visible:=True;



  VBounds(VMark,63,BrandY,56,56); VBounds(VName,17,BrandY+74,148,30);



  VName.Caption:='维刻 Vex'; VName.Alignment:=taCenter; VFont(VName.Font,20,True);



  VBounds(VClose,Width-46,0,46,32);



  VSlogan.Visible:=True; VSlogan.Alignment:=taCenter; VSlogan.WordWrap:=True;



  VBounds(VSlogan,17,BrandY+119,148,34); VFont(VSlogan.Font,12,False);



  VStatus.Visible:=Main; VStatus.Alignment:=taLeftJustify; VFont(VStatus.Font,22,True);



  VBounds(VStatus,209,30,402,34);



  VDescription.Visible:=Main and (Installing or Finished);



  VDescription.Alignment:=taLeftJustify; VFont(VDescription.Font,14,False);



  VBounds(VDescription,209,71,402,26);



  InstalledPath:=CWMRepairPath;



  { 所选注册项要用独立变量：I 随后会被内层页面的遍历复用。 }



  Selected:=VSelectedRegistration;



  if Selected>=0 then InstalledPath:=CWMRegistrations[Selected].AppPath;



  InstalledVersion:=VRegisteredVersion(Selected);



  VUpgradeAvailable:=VIsOlderVersion(InstalledVersion);



  if Home then VStatus.Caption:=CustomMessage('VWelcome');



  if Installing then begin VStatus.Caption:=CustomMessage('VInstalling'); VDescription.Caption:=CustomMessage('VInstallingIntro'); end;



  if Finished then begin VStatus.Caption:=CustomMessage('VFinished'); VDescription.Caption:=CustomMessage('VFinishedIntro'); end;



  if Installing and VRepairing then begin



    if VUpgradeAvailable then VStatus.Caption:=CustomMessage('VUpgrading')



    else VStatus.Caption:=CustomMessage('VRepairing');



  end;



  if Finished and VRepairing then VStatus.Caption:=CustomMessage('VRepaired');



  WizardForm.OuterNotebook.Visible:=Main and not Home and not Finished and not Installing and (not Maintenance or VDetailsOpen);



  VBounds(WizardForm.OuterNotebook,208,242,404,280);



  VBounds(WizardForm.InnerPage,0,0,404,280); VBounds(WizardForm.InnerNotebook,0,0,404,280);



  for I:=0 to WizardForm.InnerNotebook.PageCount-1 do VBounds(WizardForm.InnerNotebook.Pages[I],0,0,404,280);



  if Maintenance then begin



    CWMPage.SubCaptionLabel.Visible:=False;



    VBounds(CWMPage.CheckListBox,0,0,404,85); VFont(CWMPage.CheckListBox.Font,12,False);



    VBounds(CWMLocationLabel,0,94,404,20); VFont(CWMLocationLabel.Font,12,False);



    VBounds(CWMLocation,0,120,404,136); VFont(CWMLocation.Font,12,False);



    VStatus.Caption:=FmtMessage(CustomMessage('VPackage'),['{#AppVersion}']);



    VBounds(VStatus,209,56,240,34);



#ifdef CodeWFSetupFixture



    if Maintenance then Log('VEX FIXTURE heading='+VStatus.Caption);



#endif



    if (CWMRepairPath='') or VMigrationNeeded then begin



      VDescription.Visible:=Main and not VDetailsOpen;



      VDescription.Caption:=CustomMessage('VRegistrationIntro');



      if VMigrationNeeded then VDescription.Caption:=CustomMessage('VMigrationIntro');



      VBounds(VDescription,209,228,402,54); VFont(VDescription.Font,12,False);



    end;



  end;



  VInstalled.Visible:=Main and Maintenance; VLocation.Visible:=Main and Maintenance;



  VBounds(VInstalled,209,29,402,18); VBounds(VLocation,222,130,376,48);



  InstalledOn:='';



  if Selected>=0 then InstalledOn:=VInstalledOn(CWMRegistrations[Selected].RootKey);



  VInstalledVersion:=InstalledVersion;



  if InstalledVersion='' then VInstalled.Caption:=CustomMessage('VInstalledUnknown')



  else if InstalledOn='' then VInstalled.Caption:=FmtMessage(CustomMessage('VInstalled'),[InstalledVersion])



  else VInstalled.Caption:=FmtMessage(CustomMessage('VInstalledOn'),[InstalledVersion,InstalledOn]);



#ifdef CodeWFSetupFixture



  if Maintenance then Log('VEX FIXTURE installed='+VInstalled.Caption);



#endif



  VLocation.Caption:=CustomMessage('VLocation')+#13#10+MinimizePathName(CWMDisplayPath(InstalledPath),VLocation.Font,ScaleX(376));



  VLocation.Hint:=CWMDisplayPath(InstalledPath); VLocation.ShowHint:=True;



  { 第二行右侧：安装包发布日期 + 查看更新日志胶囊，胶囊贴正文列右缘。 }



  PackageDate:=VPackageReleaseDate;



  VReleasedLabel.Visible:=Main and Maintenance and (PackageDate<>'');



  VLog.Visible:=Main and Maintenance;



  VReleasedLabel.Caption:=FmtMessage(CustomMessage('VReleased'),[PackageDate]);



  VReleasedLabel.Alignment:=taRightJustify; VFont(VReleasedLabel.Font,12,False);



  if VLog.Visible then begin



    if VChangelogOpen then VLog.Caption:=CustomMessage('VChangelogHide')



    else VLog.Caption:=CustomMessage('VChangelog');



    VFont(VBack.Bitmap.Canvas.Font,12,False);



    PillWidth:=VBack.Bitmap.Canvas.TextWidth(VLog.Caption)+ScaleX(26);



    PillX:=ScaleX(640)-ScaleX(16)-PillWidth;



    VLog.SetBounds(PillX,ScaleY(58),PillWidth,ScaleY(28));



    if VReleasedLabel.Visible then begin



      TextWidth:=VBack.Bitmap.Canvas.TextWidth(VReleasedLabel.Caption)+ScaleX(4);



      VReleasedLabel.SetBounds(PillX-ScaleX(10)-TextWidth,ScaleY(63),TextWidth,ScaleY(18));



    end;



  end;



  VRenderChangelog(InstalledVersion);



  VLogTitle.Visible:=Panel; VLogMeta.Visible:=Panel; VLogMemo.Visible:=Panel;



  VLogMore.Visible:=Panel and (VShownCount>1);



  if Panel then begin



    VLogTitle.Caption:=CustomMessage('VChangelogHeading');



    if InstalledVersion='' then VLogMeta.Caption:=FmtMessage(CustomMessage('VChangelogSince'),['{#AppVersion}'])



    else VLogMeta.Caption:=FmtMessage(CustomMessage('VChangelogSinceInstalled'),['{#AppVersion}',InstalledVersion]);



    VBounds(VLogTitle,656,14,90,18); VBounds(VLogMeta,748,17,Width-748-16,18);



    if VLogMore.Visible then begin



      if VChangelogEarlier then VLogMore.Caption:=CustomMessage('VChangelogCollapse')



      else VLogMore.Caption:=FmtMessage(CustomMessage('VChangelogEarlier'),[IntToStr(VShownCount-1)]);



      VBounds(VLogMore,656,Height-38,Width-656-16,24);



    end;



    VBounds(VLogMemo,656,54,Width-656-16,Height-54-46);



    VLogMemo.Width:=ScaleX(Width-656-16)+VSystemMetric(2);



    Region:=VRectRegion(0,0,ScaleX(Width-656-16),VLogMemo.Height);



    if VSetRegion(VLogMemo.Handle,Region,True)=0 then VDeleteObject(Region);



#ifdef CodeWFSetupFixture



    Log('VEX FIXTURE changelog releases='+IntToStr(VShownCount)+' earlier='+IntToStr(Ord(VChangelogEarlier))+



      ' meta='+VLogMeta.Caption);



    { 诊断：最新一节正文的原始解析文本落 setup.log，与 PowerShell 从文档提取的期望正文比对，



      解析切分错位与 RichEdit 渲染错位由此各负其责（渲染侧文本由夹具经 WM_GETTEXT 读取上报）。 }



    if VShownCount>0 then begin



      VFixLog:=Copy(VReleaseBodies[VShownReleases[0]],1,240);



      StringChangeEx(VFixLog,#13,' ',True);



      StringChangeEx(VFixLog,#10,' ',True);



      Log('VEX FIXTURE newestbody='+VFixLog);



    end;



    { 探针：日志栏 RichEdit 的窗口类与已加载的 RichEdit DLL，用于定位 \u 转义失效的层级。 }



    SetLength(VFixClass,64);



    SetLength(VFixClass,VProbeClassName(VLogMemo.Handle,VFixClass,64));



    Log('VEX FIXTURE richeditclass='+VFixClass+



      ' riched20='+IntToStr(Ord(VProbeModule('riched20.dll')<>0))+



      ' msftedit='+IntToStr(Ord(VProbeModule('msftedit.dll')<>0))+



      ' esclen='+IntToStr(Length(VRTFText('安'))));



#endif



  end;



  if not Home and not Finished and not Installing and not Maintenance then begin



    VStatus.Caption:=WizardForm.PageNameLabel.Caption;



    VDescription.Caption:=WizardForm.PageDescriptionLabel.Caption;



    VDescription.Visible:=Main; VBounds(VDescription,208,122,404,55);



    VBounds(WizardForm.OuterNotebook,208,190,404,260);



  end;



  VCustom.Visible:=Main and (Home or Maintenance);



  if Home then begin



    VCustom.Caption:=CustomMessage('VCustom');



  end else VCustom.Caption:=CustomMessage('VDetails');



  VBounds(VCustom,209,82,402,44);



  if Maintenance then VBounds(VCustom,209,196,110,26);



  WizardForm.DirEdit.Visible:=Main and Home and VExpanded;



  WizardForm.DirBrowseButton.Visible:=Main and Home and VExpanded;



  VPathLabel.Visible:=False; VScope.Visible:=False;



  VDesktop.Visible:=Main and Home and VExpanded; VAssociation.Visible:=Main and Home and VExpanded;



  VAssociationHint.Visible:=False;



  VBounds(WizardForm.DirEdit,234,148,286,20);



  VBounds(WizardForm.DirBrowseButton,538,138,60,38);



  VBounds(VDesktop,222,190,376,24); VBounds(VAssociation,222,222,376,24);



  WizardForm.ProgressGauge.Visible:=False; WizardForm.StatusLabel.Visible:=False; WizardForm.FilenameLabel.Visible:=False;



  VPercent.Visible:=Main and Installing; VProgressLabel.Visible:=False; VProgressBar.Visible:=Main and Installing;



  I:=VProgressShown*402 div 10000;



  if I<18 then I:=18; if I>384 then I:=384;



  VPercent.Alignment:=taCenter;



  VBounds(VPercent,209+I-18,120,36,18); VBounds(VProgressBar,209,146,402,5);



  if Installing then begin VProgressPixel:=-1; VDrawProgress; end;



  WizardForm.BackButton.Visible:=False;



  { Inno 的关闭查询依赖 CancelButton.Visible；安装前保留状态但移到窗口外。 }



  WizardForm.CancelButton.Visible:=not Finished; WizardForm.CancelButton.TabStop:=Main and Installing;



  VClose.Enabled:=VLicenseOpen or not Installing or WizardForm.CancelButton.Enabled;



  VLicense.Visible:=Main and Home;



  VLicense.Enabled:=True;



  VWebsite.Visible:=Main and Maintenance;



  ButtonY:=Height-58;



  { 整页唯一的强调色落在右下角的执行按钮上；「为当前用户安装」「升级并迁移」文案更长，给到 134。 }



  NextWidth:=104;



  if Maintenance and ((CWMRepairPath='') or VMigrationNeeded) then NextWidth:=134;



  VBounds(WizardForm.NextButton,640-16-NextWidth,ButtonY,NextWidth,36);



  if not Main then VBounds(WizardForm.NextButton,-300,-100,NextWidth,36);



  if Main and Installing then VBounds(WizardForm.CancelButton,508,ButtonY,104,36)



  else VBounds(WizardForm.CancelButton,-200,-100,104,36);



  if Installing then WizardForm.CancelButton.Caption:=CustomMessage('VCancelInstall');



  VBounds(VWebsite,16,Height-36,148,25); VBounds(VLicense,208,ButtonY,170,36);



  if Maintenance then begin



    VBounds(VWebsite,640-16-NextWidth-8-68,ButtonY,68,36); VWebsite.Caption:=CustomMessage('VClose');



  end;



  VBounds(VModalTitle,208,38,404,34); VBounds(VLicenseMemo,220,100,380,178);



  VLicenseMemo.Width:=ScaleX(380)+VSystemMetric(2);



  Region:=VRectRegion(0,0,ScaleX(380),VLicenseMemo.Height);



  if VSetRegion(VLicenseMemo.Handle,Region,True)=0 then VDeleteObject(Region);



  TextBounds.Left:=0; TextBounds.Top:=0; TextBounds.Right:=VLicenseMemo.ClientWidth; TextBounds.Bottom:=VLicenseMemo.ClientHeight;



  VSendRect(VLicenseMemo.Handle,$00B3,0,TextBounds); VSend(VLicenseMemo.Handle,$0448,0,0);



  VBounds(VModalBack,544,ButtonY,68,36);



  VModalTitle.Visible:=VLicenseOpen; VLicenseMemo.Visible:=VLicenseOpen; VModalBack.Visible:=VLicenseOpen;



  VModalBack.Cancel:=VLicenseOpen; VModalBack.Default:=VLicenseOpen;



  WizardForm.NextButton.Default:=not VLicenseOpen; WizardForm.NextButton.TabStop:=Main and not Installing;



  WizardForm.CancelButton.Cancel:=not VLicenseOpen;



  if Home then WizardForm.NextButton.Caption:=CustomMessage('VInstall');



  if Finished then begin



    WizardForm.RunList.Visible:=False;



    VStatus.Alignment:=taCenter; VDescription.Alignment:=taCenter;



    VBounds(VStatus,209,126,402,66); VBounds(VDescription,209,203,402,27);



    VBounds(WizardForm.NextButton,358,252,104,38);



    if WizardForm.RunList.Items.Count>0 then WizardForm.NextButton.Caption:=CustomMessage('VStartWriting')



    else WizardForm.NextButton.Caption:=CustomMessage('VClose');



  end;



  if Maintenance then begin



    if CWMPage.SelectedValueIndex>0 then WizardForm.NextButton.Caption:=CustomMessage('VRemove')



    else if CWMRepairPath='' then WizardForm.NextButton.Caption:=CustomMessage('VContinueInstall')



    else if VUpgradeAvailable then WizardForm.NextButton.Caption:=CustomMessage('VUpgrade')



    else WizardForm.NextButton.Caption:=CustomMessage('VRepair');



    if VMigrationNeeded and (CWMPage.SelectedValueIndex=0) then WizardForm.NextButton.Caption:=CustomMessage('VMigrate');



  end;



  VPaintBackground;



  { 仅在页面布局完成时提交所有子控件；进度更新不进入此路径。 }



  VRedraw(WizardForm.Handle,0,0,$0181);



end;







procedure VToggle(Sender: TObject);



begin



  if VIsMaintenance then VDetailsOpen:=not VDetailsOpen else VExpanded:=not VExpanded;



  VLayout;



end;







{ 胶囊切换右侧日志栏；收起时连同「更早版本」的展开状态一起复位。 }



procedure VToggleChangelog(Sender: TObject);



begin



  VChangelogOpen:=not VChangelogOpen;



  if not VChangelogOpen then VChangelogEarlier:=False;



  VLogRenderedFor:='';



  VLayout;



  WizardForm.ActiveControl:=VLog;



end;







procedure VToggleEarlier(Sender: TObject);



begin



  VChangelogEarlier:=not VChangelogEarlier;



  VLogRenderedFor:='';



  VLayout;



  WizardForm.ActiveControl:=VLogMore;



end;







procedure VShowLicense(Sender: TObject);



var LicenseText: AnsiString; Plain, RTF: String; I, Code: Integer; Format: TVCharFormat;



begin



  ExtractTemporaryFile('Vex-License.txt');



  if not LoadStringFromFile(ExpandConstant('{tmp}\Vex-License.txt'),LicenseText) then



    RaiseException('Unable to read the embedded MIT license.');



  Plain:=LicenseText;



  RTF:='{\rtf1\ansi\deff0{\fonttbl{\f0 Consolas;}}\uc1\pard\li0\ri0\fi0\sb0\sa0\sl297\slmult0\f0\fs16 ';



  for I:=1 to Length(Plain) do begin



    Code:=Ord(Plain[I]);



    if Plain[I]=#10 then RTF:=RTF+'\par '



    else if Plain[I]=#13 then begin end



    else if (Plain[I]='\') or (Plain[I]='{') or (Plain[I]='}') then RTF:=RTF+'\'+Plain[I]



    else if Code>127 then begin



      if Code>32767 then Code:=Code-65536;



      RTF:=RTF+'\u'+IntToStr(Code)+'?';



    end else RTF:=RTF+Plain[I];



  end;



  VLicenseMemo.RTFText:=RTF+'}';



  VLicenseMemo.Font.Color:=VText;



  { RTF 导入会恢复默认文字颜色；明确给整段许可应用当前主题。 }



  for I:=0 to 22 do Format[I]:=0;



  Format[0]:=92; Format[1]:=$40000000; Format[5]:=VText;



  VSendCharFormat(VLicenseMemo.Handle,$0444,4,Format);



  VLicenseOpen:=True; VLayout; WizardForm.ActiveControl:=VModalBack;



end;







procedure VDismissLicense(Sender: TObject);



begin



  VLicenseOpen:=False; VLayout; WizardForm.ActiveControl:=VLicense;



end;







procedure VMaintenanceSelection(Sender: TObject);



begin CWMSelectionChanged(Sender); VLayout; end;







procedure VExit(Sender: TObject);



var I: Integer;



begin



  if VLicenseOpen then begin VDismissLicense(Sender); exit; end;



  if WizardForm.CurPageID=wpFinished then begin



    for I:=0 to WizardForm.RunList.Items.Count-1 do WizardForm.RunList.Checked[I]:=False;



    WizardForm.NextButton.OnClick(WizardForm.NextButton);



  end else if WizardForm.CurPageID<>wpInstalling then WizardForm.Close



  else if WizardForm.CancelButton.Enabled then WizardForm.CancelButton.OnClick(WizardForm.CancelButton);



end;







procedure VVisitWebsite(Sender: TObject);



var Code: Integer;



begin



  if VIsMaintenance then begin VExit(Sender); exit; end;



  #ifndef CodeWFSetupFixture



    ShellExec('open','{#VexWebsite}','','',SW_SHOWNORMAL,ewNoWait,Code);



  #endif



end;







{ 强调色只给真正执行操作的按钮：选择「卸载」时它退回文字按钮，避免用主色诱导移除。 }



function VIsPrimaryButton(I: Integer): Boolean;



begin



  Result:=I=0;



  if Result and VIsMaintenance then Result:=(CWMPage=nil) or (CWMPage.SelectedValueIndex<=0);



end;







procedure VPaintButton(I: Integer; DC: LongWord; Bounds: TVRect; State: LongWord);



var X,Y,Radius: Integer; Open: Boolean; TextBounds,ButtonBounds,WindowBounds: TVRect;



  Brush,Color,Foreground,PreviousFont,Pen,OldPen: LongWord; Saved: Integer;



begin



  Saved:=VSaveDC(DC);



  try



      Color:=VSurface; Foreground:=VMuted;



      { 「查看更新日志」胶囊平时是浅强调底，展开后与主按钮同样强调；「更早版本」始终是文字按钮。 }



      if I=5 then begin



        Color:=VAccentSoft; Foreground:=VAccent;



        if VChangelogOpen then begin Color:=VAccent; Foreground:=VOnAccent; end;



      end



      else if I=8 then begin Color:=VSurface; Foreground:=VMuted; end



      else if I=9 then begin Color:=VSoft; Foreground:=VText; end



      else if VIsPrimaryButton(I) then begin Color:=VAccent; Foreground:=VOnAccent; end;



      if (I=VHover) or ((State and 1)<>0) then begin



        Color:=VSoft; Foreground:=VAccent;



        if VIsPrimaryButton(I) then begin Color:=VAccentHover; Foreground:=VOnAccent; end;



        if (I=5) and VChangelogOpen then begin Color:=VAccentHover; Foreground:=VOnAccent; end



        else if I=5 then begin Color:=VAccentSoft; Foreground:=VAccentHover; end



        else if I=8 then begin Color:=VSurface; Foreground:=VAccent; end;



        if I=3 then begin Color:=$1C2BC4; Foreground:=clWhite; end;



      end;



      if (State and 4)<>0 then begin Color:=VSoft; Foreground:=VMuted; end;



      VGetRect(VButtons[I].Handle,ButtonBounds); VGetRect(WizardForm.Handle,WindowBounds);



      X:=ButtonBounds.Left-WindowBounds.Left; Y:=ButtonBounds.Top-WindowBounds.Top;



      VDrawingCopy(DC,0,0,Bounds.Right,Bounds.Bottom,VBack.Bitmap.Canvas.Handle,X,Y,$00CC0020);



      Radius:=8; if I=5 then Radius:=Bounds.Bottom div 2;



      { 「查看更早版本」是栏内文字按钮：保留刚拷贝来的栏底色，只画字。 }



      if I=8 then begin end



      else if I<>3 then VRound(DC,0,0,Bounds.Right,Bounds.Bottom,Radius,Color,False)



      else if Color<>VSurface then begin



        Brush:=VBrush(Color); VFill(DC,Bounds,Brush); VDeleteObject(Brush);



        VRound(DC,-X,-Y,WizardForm.ClientWidth,WizardForm.ClientHeight,8,VLine,True);



      end;



      if (I=1) and not VIsMaintenance then VRound(DC,0,0,Bounds.Right,Bounds.Bottom,7,VLine,True);



      VTextColor(DC,Foreground); VTextMode(DC,1);



      PreviousFont:=VSelectObject(DC,VButtonFonts[I]);



      TextBounds:=Bounds;



      if I=3 then begin



        Pen:=VCreatePen(0,ScaleX(1),Foreground); OldPen:=VSelectObject(DC,Pen);



        VMove(DC,ScaleX(19),ScaleY(12),0); VLineTo(DC,ScaleX(27),ScaleY(20));



        VMove(DC,ScaleX(27),ScaleY(12),0); VLineTo(DC,ScaleX(19),ScaleY(20));



        VSelectObject(DC,OldPen); VDeleteObject(Pen);



      end



      else if (I=1) or (I=8) then begin



        TextBounds.Left:=ScaleX(13);



        if I=8 then TextBounds.Left:=ScaleX(2);



        VDrawText(DC,VButtons[I].Caption,-1,TextBounds,$24);



        Pen:=VCreatePen(0,ScaleX(1),Foreground); OldPen:=VSelectObject(DC,Pen);



        X:=Bounds.Right-ScaleX(22); Y:=Bounds.Bottom div 2;



        if (I=8) then Open:=VChangelogEarlier



        else if VIsMaintenance then Open:=VDetailsOpen else Open:=VExpanded;



        if Open then begin VMove(DC,X-ScaleX(4),Y+ScaleY(2),0); VLineTo(DC,X,Y-ScaleY(2)); VLineTo(DC,X+ScaleX(4),Y+ScaleY(2)); end



        else begin VMove(DC,X-ScaleX(4),Y-ScaleY(2),0); VLineTo(DC,X,Y+ScaleY(2)); VLineTo(DC,X+ScaleX(4),Y-ScaleY(2)); end;



        VSelectObject(DC,OldPen); VDeleteObject(Pen);



      end



      else if (I=4) and not VIsMaintenance then VDrawText(DC,VButtons[I].Caption,-1,TextBounds,$24)



      else if I=2 then VDrawText(DC,VButtons[I].Caption,-1,TextBounds,$24)



      else VDrawText(DC,VButtons[I].Caption,-1,TextBounds,$25);



      VSelectObject(DC,PreviousFont);



      { 保留键盘焦点提示，使用当前主题的强调色圆角线。 }



      if ((State and $10)<>0) and ((State and $200)=0) then begin



        VRound(DC,2,2,Bounds.Right-5,Bounds.Bottom-5,6,VAccent,True);



      end;



  finally



    VRestoreDC(DC,Saved);



  end;



end;







function VButtonProc(Window, Message, WParam, LParam: LongWord): Longint;



var I: Integer; DC, State, ButtonState: LongWord; Paint: TVPaint; Bounds: TVRect;



begin



  Result:=0;



  for I:=0 to 9 do if (VButtons[I]<>nil) and (Window=VButtons[I].Handle) then begin



    if Message=$0014 then begin Result:=1; exit; end;



    if (Message=$000F) or (Message=$0317) or (Message=$0318) then begin



      if Message=$000F then DC:=VBeginPaint(Window,Paint) else DC:=WParam;



      try



        Bounds.Left:=0; Bounds.Top:=0;



        Bounds.Right:=VButtons[I].Width; Bounds.Bottom:=VButtons[I].Height;



        State:=0; ButtonState:=VSend(Window,$00F2,0,0);



        if (ButtonState and 4)<>0 then State:=State or 1;



        if not VButtons[I].Enabled then State:=State or 4;



        if VGetFocus=Window then begin



          State:=State or $10;



          if (VSend(Window,$0129,0,0) and 1)<>0 then State:=State or $200;



        end;



        VPaintButton(I,DC,Bounds,State);



      finally



        if Message=$000F then VEndPaint(Window,Paint);



      end;



      exit;



    end;



    if Message=$00F4 then WParam:=(WParam and not $F) or $B;



    Result:=VCallProc(VButtonProcs[I],Window,Message,WParam,LParam);



    exit;



  end;



end;







function VCheckProc(Window, Message, WParam, LParam: LongWord): Longint;



var Check: TNewCheckBox; Index: Integer; DC, Brush, Pen, Previous: LongWord; Paint: TVPaint; Bounds: TVRect;



begin



  Index:=0; Check:=VDesktop;



  if Window=VAssociation.Handle then begin Index:=1; Check:=VAssociation; end;



  if (Message=$000F) or (Message=$0317) or (Message=$0318) then begin



    if Message=$000F then DC:=VBeginPaint(Window,Paint) else DC:=WParam;



    Bounds.Left:=0; Bounds.Top:=0; Bounds.Right:=Check.Width; Bounds.Bottom:=Check.Height;



    Brush:=VBrush(VSurface); VFill(DC,Bounds,Brush); VDeleteObject(Brush);



    VRound(DC,0,ScaleY(3),ScaleX(13),ScaleY(13),2,VField,False);



    VRound(DC,0,ScaleY(3),ScaleX(13),ScaleY(13),2,$767676,True);



    if Check.Checked then begin



      VRound(DC,0,ScaleY(5),ScaleX(14),ScaleY(14),2,VAccent,False);



      Pen:=VCreatePen(0,ScaleX(2),VOnAccent); Previous:=VSelectObject(DC,Pen);



      VMove(DC,ScaleX(3),ScaleY(10),0); VLineTo(DC,ScaleX(6),ScaleY(13)); VLineTo(DC,ScaleX(11),ScaleY(7));



      VSelectObject(DC,Previous); VDeleteObject(Pen);



    end;



    Bounds.Left:=ScaleX(22); VTextColor(DC,VMuted); VTextMode(DC,1);



    Previous:=VSelectObject(DC,VCheckFont); VDrawText(DC,Check.Caption,-1,Bounds,$24); VSelectObject(DC,Previous);



    if VGetFocus=Window then VRound(DC,0,ScaleY(2),ScaleX(14),ScaleY(15),2,VAccent,True);



    if Message=$000F then VEndPaint(Window,Paint);



    Result:=0; exit;



  end;



  Result:=VCallProc(VCheckProcs[Index],Window,Message,WParam,LParam);



end;







{ 富文本控件只在获得焦点时才响应滚轮；许可浮层与更新日志栏共用这套按悬停滚动的处理。 }



function VMemoWindowProc(Window, Message, WParam, LParam: LongWord): Longint;



var Delta, I, Command: Integer; Lines: Cardinal; Previous: LongWord;



begin



  if (Message=$020A) and ((WParam and 8)=0) then begin



    Delta:=(WParam shr 16) and $FFFF; if Delta>=$8000 then Delta:=Delta-$10000;



    VWheelDelta:=VWheelDelta+Delta;



    Lines:=3; VParameters($0068,0,Lines,0);



    while (VWheelDelta>=120) or (VWheelDelta<=-120) do begin



      if VWheelDelta>0 then begin Command:=0; VWheelDelta:=VWheelDelta-120; end



      else begin Command:=1; VWheelDelta:=VWheelDelta+120; end;



      if Lines=$FFFFFFFF then VSend(Window,$0115,Command+2,0)



      else for I:=1 to Lines do VSend(Window,$0115,Command,0);



    end;



    Result:=0; exit;



  end;



  if Window=VLogMemo.Handle then Previous:=VLogProc else Previous:=VLicenseProc;



  Result:=VCallProc(Previous,Window,Message,WParam,LParam);



end;







procedure VStyleChildren(Parent: TWinControl);



var I: Integer; Control: TControl;



begin



  for I:=0 to Parent.ControlCount-1 do begin



    Control:=Parent.Controls[I];



    if Control is TWinControl then VStyleChildren(TWinControl(Control));



    if Control is TNewNotebookPage then TNewNotebookPage(Control).Color:=VSurface;



    if Control is TNewStaticText then TNewStaticText(Control).Font.Color:=VText;



    if Control is TLabel then TLabel(Control).Font.Color:=VText;



    if Control is TEdit then begin TEdit(Control).Color:=VField; TEdit(Control).Font.Color:=VText; end;



    if Control is TMemo then begin TMemo(Control).Color:=VField; TMemo(Control).Font.Color:=VText; end;



    if Control is TNewCheckListBox then begin TNewCheckListBox(Control).Color:=VSurface; TNewCheckListBox(Control).Font.Color:=VText; end;



    if Control is TNewCheckBox then begin



      VSetWindowTheme(TNewCheckBox(Control).Handle,' ',' ');



      TNewCheckBox(Control).Color:=VSurface; TNewCheckBox(Control).Font.Color:=VText;



    end;



  end;



end;







procedure VApplyLicenseColor;



var Format: TVCharFormat; I: Integer;



begin



  for I:=0 to 22 do Format[I]:=0;



  Format[0]:=92; Format[1]:=$40000000; Format[5]:=VText;



  VSendCharFormat(VLicenseMemo.Handle,$0444,4,Format);



end;







procedure VApplyTheme;



var Light: Cardinal;



begin



  Light := 1;



  RegQueryDWordValue(HKCU,'Software\Microsoft\Windows\CurrentVersion\Themes\Personalize','AppsUseLightTheme',Light);



  #ifdef CodeWFSetupFixture



    { 只在无注册测试夹具内覆盖主题，验收无需改系统设置。 }



    if ExpandConstant('{param:VEXTHEME|}')='dark' then Light:=0;



    if ExpandConstant('{param:VEXTHEME|}')='light' then Light:=1;



  #endif



  if Light = 0 then begin



    VSurface:=$23291C; VText:=$DEE6DC; VMuted:=$96A190;



    VField:=$1E2217; VSoft:=$272D20; VLine:=$323B2C; VAccent:=$B3C88A;



    VBrand:=$2D3A24; VBrandText:=$CFDEC9; VOnAccent:=$202814;



    VAccentHover:=$C4D8A1; VAccentSoft:=$333E28; VDanger:=$8995E4;



  end else begin



    VSurface:=clWhite; VText:=$313825; VMuted:=$80887B;



    VField:=clWhite; VSoft:=$F1F5F3; VLine:=$E3EAE5; VAccent:=$6B7624;



    VBrand:=$E5EEE8; VBrandText:=$364525; VOnAccent:=clWhite;



    VAccentHover:=$58611C; VAccentSoft:=$E9F0E5; VDanger:=$535BBD;



  end;



  if VSurfaceBrush<>0 then VDeleteObject(VSurfaceBrush);



  VSurfaceBrush:=VBrush(VSurface);



  if VSoftBrush<>0 then VDeleteObject(VSoftBrush);



  VSoftBrush:=VBrush(VSoft);



  WizardForm.Color:=VSurface; WizardForm.Font.Color:=VText;



  VStyleChildren(WizardForm);



  VTitle.Font.Color:=VMuted; VSlogan.Font.Color:=VMuted;



  VName.Font.Color:=VBrandText; VInstalled.Font.Color:=VMuted; VLocation.Font.Color:=VMuted;



  VScope.Font.Color:=VMuted; VAssociationHint.Font.Color:=VMuted;



  VDescription.Font.Color:=VMuted; VProgressLabel.Font.Color:=VMuted; VPercent.Font.Color:=VMuted;



  WizardForm.DirEdit.Color:=VField; WizardForm.DirEdit.Font.Color:=VText;



  VDesktop.Color:=VSurface; VAssociation.Color:=VSurface;



  VDesktop.Font.Color:=VText; VAssociation.Font.Color:=VText;



  VLicenseMemo.Color:=VSoft; VLicenseMemo.Font.Color:=VText;



  { 更新日志栏与折叠背景同色，深色下也要让原生滚动条跟随，否则白边会破坏描边。 }



  VReleasedLabel.Font.Color:=VMuted; VLogMeta.Font.Color:=VMuted;



  VLogTitle.Font.Color:=VText; VLogMemo.Color:=VSoft; VLogMemo.Font.Color:=VText;



  VProgressPixel:=-1;



  if Light=0 then begin



    VSetWindowTheme(VLicenseMemo.Handle,'DarkMode_Explorer','');



    VSetWindowTheme(VLogMemo.Handle,'DarkMode_Explorer','');



  end else begin



    VSetWindowTheme(VLicenseMemo.Handle,'Explorer','');



    VSetWindowTheme(VLogMemo.Handle,'Explorer','');



  end;



  VApplyLicenseColor;



  { RTF 里的调色板是渲染时取色的，换肤必须重渲染一次才能拿到新色。 }



  VLogRenderedFor:='';



  if VLogMemo.Visible then VRenderChangelog(VInstalledVersion);



  VPaintBackground;



  if Light=0 then begin



    ExtractTemporaryFile('vex-brand-dark.bmp'); VMark.Bitmap.LoadFromFile(ExpandConstant('{tmp}\vex-brand-dark.bmp'));



  end else begin



    ExtractTemporaryFile('vex-brand-light.bmp'); VMark.Bitmap.LoadFromFile(ExpandConstant('{tmp}\vex-brand-light.bmp'));



  end;



  WizardForm.Repaint;



end;







function VWindowProc(Window, Message, WParam, LParam: LongWord): Longint;



var I, X, Y, Hover: Integer; Bounds: TVRect; Point: TVPoint; Item: TVDrawItem;



begin



  if Message=$002B then begin



    VCopyItem(Item,LParam,48);



    for I:=0 to 9 do if (VButtons[I]<>nil) and (Item.ItemWindow=VButtons[I].Handle) then begin



      VPaintButton(I,Item.DC,Item.Bounds,Item.ItemState);



      Result:=1; exit;



    end;



  end;



  if (Message=$0113) and (WParam=42) and VInitialized then begin



    VAdvanceProgress; Result:=0; exit;



  end;



  if ((Message=$0135) or (Message=$0138)) and



    ((LParam=VDesktop.Handle) or (LParam=VAssociation.Handle)) then begin



    VTextColor(WParam,VText); VBackgroundColor(WParam,VSoft);



    Result:=VSoftBrush; exit;



  end;



  if Message = $0084 then begin



    VGetRect(Window,Bounds);



    X := LParam and $FFFF; if X >= $8000 then X := X-$10000;



    Y := (LParam shr 16) and $FFFF; if Y >= $8000 then Y := Y-$10000;



    if (Y-Bounds.Top < ScaleY(38)) and (X-Bounds.Left < ScaleX(VWindowWidth-46)) then begin Result:=2; exit; end;



  end;



  if (Message=$0113) and (WParam=41) and VInitialized then begin



    Hover:=-1;



    if VCursor(Point) then for I:=0 to 9 do if VButtons[I].Visible and VButtons[I].Enabled then begin



      VGetRect(VButtons[I].Handle,Bounds);



      if (Point.X>=Bounds.Left) and (Point.X<Bounds.Right) and (Point.Y>=Bounds.Top) and (Point.Y<Bounds.Bottom) then Hover:=I;



    end;



    if Hover<>VHover then begin



      I:=VHover; VHover:=Hover;



      if I>=0 then VInvalidate(VButtons[I].Handle,0,False);



      if Hover>=0 then VInvalidate(VButtons[Hover].Handle,0,False);



    end;



  end;



  Result:=VCallProc(VPreviousProc,Window,Message,WParam,LParam);



  if (Message=$0018) and (WParam<>0) and VInitialized then VSignalUpdateReady;



  if (Message=$001A) and VInitialized then VApplyTheme;



end;







<event('InitializeWizard')>



procedure VInitialize;



var I, Pixels, Weight: Integer; Face: String; Motion: Cardinal;



begin



  if WizardSilent then exit;



  WizardForm.BorderStyle:=bsNone;



  VSetClassLong(WizardForm.Handle,-26,VGetClassLong(WizardForm.Handle,-26) or $20000);



  WizardForm.ClientWidth:=ScaleX(640); WizardForm.ClientHeight:=ScaleY(360);



  VFont(WizardForm.Font,14,False); VHover:=-1;



  VProgressFrame:=TBitmap.Create; VProgressPixel:=-1; VProgressPercent:=-1;



  Motion:=1; VParameters($1042,0,Motion,0); VProgressAnimated:=Motion<>0;



#ifdef CodeWFSetupFixture



  VProgressAnimated:=ExpandConstant('{param:VEXMOTION|on}')<>'off';



#endif



  WizardForm.Position:=poScreenCenter;



  WizardForm.MainPanel.Visible:=False; WizardForm.Bevel.Visible:=False;



  WizardForm.Bevel1.Visible:=False; WizardForm.BeveledLabel.Visible:=False;



  WizardForm.WizardBitmapImage.Visible:=False; WizardForm.WizardBitmapImage2.Visible:=False;



  VBack:=TBitmapImage.Create(WizardForm); VBack.Parent:=WizardForm; VBack.SendToBack;



  VTitle:=VLabel(23,15,420,18,12,CustomMessage('VTitle'));



  VMark:=TBitmapImage.Create(WizardForm); VMark.Parent:=WizardForm;



  VMark.Bitmap.Width:=ScaleX(54); VMark.Bitmap.Height:=ScaleY(54); VMark.Stretch:=True;



  VBounds(VMark,233,53,54,54);



  VName:=VLabel(38,117,444,38,29,'Vex'); VName.Alignment:=taCenter; VFont(VName.Font,29,True);



  VName.Font.Name:='Segoe UI Semibold'; VName.Font.Style:=[];



  VSlogan:=VLabel(38,168,444,20,13,CustomMessage('VSlogan')); VSlogan.Alignment:=taCenter;



  VStatus:=VLabel(88,215,344,26,17,''); VStatus.Alignment:=taCenter; VFont(VStatus.Font,17,True);



  VDescription:=VLabel(60,250,400,40,12,''); VDescription.Alignment:=taCenter; VDescription.WordWrap:=True;



  VInstalled:=VLabel(209,29,402,18,12,CustomMessage('VInstalled'));



  VLocation:=VLabel(222,130,376,48,13,''); VLocation.WordWrap:=True;



  VReleasedLabel:=VLabel(400,63,114,18,12,''); VReleasedLabel.Alignment:=taRightJustify;



  VLog:=VButton(514,58,94,28,CustomMessage('VChangelog'),@VToggleChangelog);



  VLogTitle:=VLabel(656,14,90,18,13,CustomMessage('VChangelogHeading')); VFont(VLogTitle.Font,13,True);



  VLogMeta:=VLabel(748,17,196,18,11,''); VFont(VLogMeta.Font,11,False);



  VLogMemo:=TRichEditViewer.Create(WizardForm); VLogMemo.Parent:=WizardForm;



  VLogMemo.ReadOnly:=True; VLogMemo.ScrollBars:=ssVertical; VLogMemo.BorderStyle:=bsNone; VLogMemo.WordWrap:=True;



  VFont(VLogMemo.Font,12,False);



  VLogProc:=VSetLong(VLogMemo.Handle,-4,CreateCallback(@VMemoWindowProc));



  VLogMemo.Visible:=False;



  VLogMore:=VButton(656,320,288,24,CustomMessage('VChangelogEarlier'),@VToggleEarlier);



  VLogMore.Visible:=False;



  VCustom:=VButton(156,262,208,30,CustomMessage('VCustom'),@VToggle);



  VLicense:=VButton(360,323,128,24,CustomMessage('VLicense'),@VShowLicense);



  VWebsite:=VButton(28,323,170,25,'CodeWF · {#AppVersion}',@VVisitWebsite);



  VClose:=VButton(472,8,32,32,'×',@VExit); VClose.Hint:=CustomMessage('VClose'); VClose.ShowHint:=True;



  VPathLabel:=VLabel(54,322,412,18,12,CustomMessage('VLocation'));



  VScope:=VLabel(54,393,412,17,11,CustomMessage('VScope'));



  WizardForm.DirEdit.Parent:=WizardForm; VBounds(WizardForm.DirEdit,65,356,314,20);



  WizardForm.DirEdit.BorderStyle:=bsNone;



  VFont(WizardForm.DirEdit.Font,14,False);



  WizardForm.DirBrowseButton.Parent:=WizardForm; VBounds(WizardForm.DirBrowseButton,397,346,69,37);



  VDesktop:=TNewCheckBox.Create(WizardForm); VDesktop.Parent:=WizardForm;



  VDesktop.Caption:=CustomMessage('VDesktop'); VBounds(VDesktop,54,423,412,20); VFont(VDesktop.Font,12,False);



  VDesktop.Checked:=True;



  VAssociation:=TNewCheckBox.Create(WizardForm); VAssociation.Parent:=WizardForm;



  VAssociation.Caption:=CustomMessage('VAssociation'); VBounds(VAssociation,54,454,412,20); VFont(VAssociation.Font,12,False);



  VAssociation.Checked:=True;



  VAssociationHint:=VLabel(76,477,390,17,11,CustomMessage('VAssociationHint'));



  VProgressLabel:=VLabel(88,262,260,18,12,CustomMessage('VProgress'));



  VPercent:=VLabel(348,262,84,18,12,'0%'); VPercent.Alignment:=taRightJustify;



  VProgressBar:=TBitmapImage.Create(WizardForm); VProgressBar.Parent:=WizardForm; VBounds(VProgressBar,88,288,344,5);



  VModalTitle:=VLabel(31,80,458,29,19,CustomMessage('VLicenseTitle')); VFont(VModalTitle.Font,19,True);



  VLicenseMemo:=TRichEditViewer.Create(WizardForm); VLicenseMemo.Parent:=WizardForm;



  VLicenseMemo.ReadOnly:=True; VLicenseMemo.ScrollBars:=ssVertical; VLicenseMemo.BorderStyle:=bsNone; VLicenseMemo.WordWrap:=True;



  VFont(VLicenseMemo.Font,11,False); VLicenseMemo.Font.Name:='Consolas';



  VLicenseProc:=VSetLong(VLicenseMemo.Handle,-4,CreateCallback(@VMemoWindowProc));



  VModalBack:=VButton(389,358,100,40,CustomMessage('VBack'),@VDismissLicense);



  VButtons[0]:=WizardForm.NextButton; VButtons[1]:=VCustom; VButtons[2]:=VLicense;



  VButtons[3]:=VClose; VButtons[4]:=VWebsite; VButtons[5]:=VLog;



  VButtons[6]:=WizardForm.CancelButton; VButtons[7]:=WizardForm.DirBrowseButton;



  VButtons[8]:=VLogMore; VButtons[9]:=VModalBack;



  VPreviousProc:=VSetLong(WizardForm.Handle,-4,CreateCallback(@VWindowProc));



  for I:=0 to 9 do begin



    VFont(VButtons[I].Font,13,False);



    VButtons[I].Cursor:=crHand;



    Pixels:=13; Weight:=400; Face:='Segoe UI';



    if I=0 then begin Weight:=600; Face:='Segoe UI Semibold'; end;



    if ActiveLanguage='chinesesimplified' then Face:='Microsoft YaHei UI';



    if I=7 then Pixels:=14;



    if (I=2) or (I=5) or (I=8) then Pixels:=12;



    if I=3 then Pixels:=20;



    VButtonFonts[I]:=VCreateFont(-ScaleY(Pixels),0,0,0,Weight,0,0,0,1,0,0,5,0,Face);



    VSetLong(VButtons[I].Handle,-16,(VGetLong(VButtons[I].Handle,-16) and not $F) or $B);



    VButtonProcs[I]:=VSetLong(VButtons[I].Handle,-4,CreateCallback(@VButtonProc));



  end;



  VFont(WizardForm.NextButton.Font,13,True); VFont(VCustom.Font,12,False);



  VFont(VWebsite.Font,11,False); VFont(VLicense.Font,11,False); VFont(VClose.Font,20,False);



  VFont(WizardForm.DirBrowseButton.Font,12,False);



  WizardForm.DirBrowseButton.Caption:=CustomMessage('VBrowse');



  Face:='Segoe UI'; if ActiveLanguage='chinesesimplified' then Face:='Microsoft YaHei UI';



  VCheckFont:=VCreateFont(-ScaleY(14),0,0,0,400,0,0,0,1,0,0,5,0,Face);



  VCheckProcs[0]:=VSetLong(VDesktop.Handle,-4,CreateCallback(@VCheckProc));



  VCheckProcs[1]:=VSetLong(VAssociation.Handle,-4,CreateCallback(@VCheckProc));



  if CWMPage<>nil then CWMPage.CheckListBox.OnClickCheck:=@VMaintenanceSelection;



  VInitialized:=True; VApplyTheme; VLayout;



  VSetTimer(WizardForm.Handle,41,60,0);



end;







<event('ShouldSkipPage')>



function VSkip(PageID: Integer): Boolean;



var Tasks: String;



begin



  { Inno 在进入任务页前加载上次安装的选择，必须在此之后同步折叠区。 }



  if VInitialized and (PageID=wpSelectTasks) then begin



    if VDesktop.Checked then Tasks:='desktopicon' else Tasks:='!desktopicon';



    if VAssociation.Checked then Tasks:=Tasks+',markdownassociation'



    else Tasks:=Tasks+',!markdownassociation';



    WizardSelectTasks(Tasks);



  end;



  Result := (PageID=wpLicense) or (PageID=wpSelectDir) or (PageID=wpSelectTasks) or ((PageID=wpWelcome) and (CWMPage<>nil));



end;







<event('NextButtonClick')>



function VNext(PageID: Integer): Boolean;



var Path, WindowsPath: String;



begin



  Result:=True;



  if VIsMaintenance then VRepairing:=(CWMPage.SelectedValueIndex=0) and (CWMRepairPath<>'');



  if WizardSilent or (PageID<>wpWelcome) then exit;



  Path:=CWMNormalizePath(Trim(WizardForm.DirEdit.Text));



  WindowsPath:=Lowercase(AddBackslash(ExpandConstant('{win}')));



  if (Path='') or (Pos(WindowsPath,Lowercase(AddBackslash(Path)))=1) then begin



    VExpanded:=True; VLayout;



    MsgBox(CustomMessage('VPathError'),mbError,MB_OK);



    WizardForm.ActiveControl:=WizardForm.DirEdit; Result:=False; exit;



  end;



  WizardForm.DirEdit.Text:=Path;



end;







<event('CurPageChanged')>



procedure VPageChanged(PageID: Integer);



begin



  if WizardSilent then exit;



  if PageID<>wpInstalling then VKillTimer(WizardForm.Handle,42);



  if (CWMPage<>nil) and (PageID=CWMPage.ID) then CWMMaintenancePageChanged(PageID);



  VLayout;



end;







<event('CurInstallProgressChanged')>



procedure VProgressChanged(Current, Maximum: Integer);



var Target: Integer;



begin



  if VInitialized and (Maximum>0) then begin



    Target:=VProgressRatio(Current,10000,Maximum);



    if Target<0 then Target:=0;



    if Target>10000 then Target:=10000;



    if Target<>VProgressTarget then begin



      VProgressTarget:=Target; VProgressFrom:=VProgressShown; VProgressStarted:=VTickCount;



      if VProgressTarget<>VProgressShown then begin



        if VProgressAnimated then VSetTimer(WizardForm.Handle,42,16,0)



        else VAdvanceProgress;



      end;



    end;



    if (WizardForm.CurPageID=wpInstalling) and (VClose.Enabled<>WizardForm.CancelButton.Enabled) then



      VClose.Enabled:=WizardForm.CancelButton.Enabled;



  end;



end;







<event('DeinitializeSetup')>



procedure VRelease;



var I: Integer;



begin



  if not VInitialized then exit;



  VInitialized:=False;



  VKillTimer(WizardForm.Handle,41);



  VKillTimer(WizardForm.Handle,42);



  if VPreviousProc<>0 then VSetLong(WizardForm.Handle,-4,VPreviousProc);



  for I:=0 to 9 do if VButtonProcs[I]<>0 then begin



    if VIsWindow(VButtons[I].Handle) then VSetLong(VButtons[I].Handle,-4,VButtonProcs[I]);



    if VButtonFonts[I]<>0 then VDeleteObject(VButtonFonts[I]);



  end;



  if VSurfaceBrush<>0 then VDeleteObject(VSurfaceBrush);



  if VSoftBrush<>0 then VDeleteObject(VSoftBrush);



  if VCheckProcs[0]<>0 then VSetLong(VDesktop.Handle,-4,VCheckProcs[0]);



  if VCheckProcs[1]<>0 then VSetLong(VAssociation.Handle,-4,VCheckProcs[1]);



  if VCheckFont<>0 then VDeleteObject(VCheckFont);



  if VLicenseProc<>0 then VSetLong(VLicenseMemo.Handle,-4,VLicenseProc);



  if VLogProc<>0 then VSetLong(VLogMemo.Handle,-4,VLogProc);



  if VProgressFrame<>nil then VProgressFrame.Free;



  if VDrawingToken<>0 then begin VDrawingShutdown(VDrawingToken); VDrawingToken:=0; end;



end;







<event('InitializeUninstallProgressForm')>



procedure VUninstallInitialize;



begin



  if UninstallSilent then exit;



  { 卸载保持原生窗口与系统确认，不引入独立的动画或应用运行依赖。 }



  UninstallProgressForm.Caption:=CustomMessage('VUninstalling');



  UninstallProgressForm.PageNameLabel.Caption:=CustomMessage('VUninstalling');



  UninstallProgressForm.PageDescriptionLabel.Caption:=CustomMessage('VSlogan');



end;



