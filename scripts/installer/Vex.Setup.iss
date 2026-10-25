; Vex 专用安装外观；不读取软件原型、应用源码或其他仓库。
; 安装、注册、覆盖、取消及回滚继续交给 Inno Setup。
#ifndef VexLicensePath
#define VexLicensePath "..\LICENSE"
#endif
#define VexBrandDirectory ExtractFilePath(__PATHFILENAME__)
#define VexWebsite "https://vex.codewf.com/"
[Files]
Source: "{#VexLicensePath}"; DestName: "Vex-License.txt"; Flags: dontcopy
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
english.VMaintenance=Continue your writing.
chinesesimplified.VMaintenance=继续你的写作。
english.VUpgradeHeading=Upgrade to %1.
chinesesimplified.VUpgradeHeading=升级到 %1。
english.VInstalled=Installed · %1
chinesesimplified.VInstalled=已安装 · %1
english.VInstalledUpgrade=Installed · %1 → %2
chinesesimplified.VInstalledUpgrade=已安装 · %1 → %2
english.VInstalledUnknown=Installed · Version unavailable
chinesesimplified.VInstalledUnknown=已安装 · 版本未知
english.VMaintenanceIntro=Repair the installation or remove the app.
chinesesimplified.VMaintenanceIntro=可以修复安装，或移除此应用。
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
    VAssociationHint, VProgressLabel, VModalTitle, VInstalled, VLocation: TLabel;
  VBack, VMark, VProgressBar: TBitmapImage;
  VProgressFrame: TBitmap;
  VCustom, VLicense, VClose, VWebsite, VNoLaunch, VRemove, VModalBack: TNewButton;
  VLicenseMemo: TRichEditViewer;
  VDesktop, VAssociation: TNewCheckBox;
  VButtons: array[0..9] of TNewButton;
  VButtonProcs: array[0..9] of LongWord;
  VButtonFonts: array[0..9] of LongWord;
  VCheckProcs: array[0..1] of LongWord;
  VCheckFont: LongWord;
  VLicenseProc: LongWord;
  VWheelDelta: Integer;
  VPreviousProc, VSurfaceBrush, VSoftBrush: LongWord;
  VExpanded, VInitialized, VDetailsOpen, VLicenseOpen, VRepairing: Boolean;
  VUpgradeAvailable: Boolean;
  VHover, VModalY: Integer;
  VProgressShown, VProgressTarget, VProgressFrom, VProgressPixel, VProgressPercent: Integer;
  VProgressStarted: LongWord;
  VProgressAnimated: Boolean;
  VSurface, VText, VMuted, VField, VSoft, VLine, VAccent, VBrand, VBrandText, VOnAccent, VAccentHover, VAccentSoft: TColor;
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

procedure VFont(Font: TFont; Pixels: Integer; Strong: Boolean);
begin
  Font.Name:='Segoe UI';
  if Strong then Font.Name:='Segoe UI Semibold';
  if ActiveLanguage='chinesesimplified' then begin
    Font.Name:='Microsoft YaHei UI';
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
function VIsOlderVersion(const Installed: String): Boolean;
var Package: String; I,J,Left,Right: Integer;
begin
  Result:=False;
  Package:='{#AppVersion}';
  { Inno 的 or 不保证短路，空串取 [1] 会越界，因此先行退出。 }
  if Installed='' then exit;
  if not ((Installed[1]>='0') and (Installed[1]<='9')) then exit;
  I:=1; J:=1;
  while (I<=Length(Installed)) or (J<=Length(Package)) do begin
    Left:=VVersionPart(Installed,I); Right:=VVersionPart(Package,J);
    if Left<>Right then begin Result:=Left<Right; exit; end;
  end;
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
var Page, Height, I, ButtonY, BrandY: Integer; Home, Finished, Installing, Maintenance, Main: Boolean;
  Region: LongWord; TextBounds: TVRect; InstalledVersion, InstalledPath: String;
begin
  if not VInitialized then exit;
  Page:=WizardForm.CurPageID;
  Home:=Page=wpWelcome; Finished:=Page=wpFinished; Installing:=Page=wpInstalling;
  Maintenance:=VIsMaintenance; Main:=not VLicenseOpen;
  Height:=360;
  if Maintenance and VDetailsOpen then Height:=620;
  if not Home and not Finished and not Installing and not Maintenance then Height:=560;
  WizardForm.ClientHeight:=ScaleY(Height);
  Region:=VRegion(WizardForm.ClientWidth,WizardForm.ClientHeight);
  if VSetRegion(WizardForm.Handle,Region,True)=0 then VDeleteObject(Region);
  VBounds(VBack,0,0,640,Height);
  VTitle.Visible:=False;
  BrandY:=(Height-135) div 2;
  VMark.Visible:=True; VName.Visible:=True;
  VBounds(VMark,63,BrandY,56,56); VBounds(VName,17,BrandY+74,148,30);
  VName.Caption:='维刻 Vex'; VName.Alignment:=taCenter; VFont(VName.Font,20,True);
  VBounds(VClose,594,0,46,32);
  VSlogan.Visible:=True; VSlogan.Alignment:=taCenter; VSlogan.WordWrap:=True;
  VBounds(VSlogan,17,BrandY+119,148,34); VFont(VSlogan.Font,12,False);
  VStatus.Visible:=Main; VStatus.Alignment:=taLeftJustify; VFont(VStatus.Font,22,True);
  VBounds(VStatus,209,30,402,34);
  VDescription.Visible:=Main and (Installing or Finished);
  VDescription.Alignment:=taLeftJustify; VFont(VDescription.Font,14,False);
  VBounds(VDescription,209,71,402,26);
  InstalledPath:=CWMRepairPath;
  I:=VSelectedRegistration;
  if I>=0 then InstalledPath:=CWMRegistrations[I].AppPath;
  InstalledVersion:=VRegisteredVersion(I);
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
    if VUpgradeAvailable then VStatus.Caption:=FmtMessage(CustomMessage('VUpgradeHeading'),['{#AppVersion}'])
    else VStatus.Caption:=CustomMessage('VMaintenance');
    VBounds(VStatus,209,60,402,34);
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
  if InstalledVersion='' then VInstalled.Caption:=CustomMessage('VInstalledUnknown')
  else if VUpgradeAvailable then
    VInstalled.Caption:=FmtMessage(CustomMessage('VInstalledUpgrade'),[InstalledVersion,'{#AppVersion}'])
  else VInstalled.Caption:=FmtMessage(CustomMessage('VInstalled'),[InstalledVersion]);
#ifdef CodeWFSetupFixture
  if Maintenance then Log('VEX FIXTURE installed='+VInstalled.Caption);
#endif
  VLocation.Caption:=CustomMessage('VLocation')+#13#10+MinimizePathName(CWMDisplayPath(InstalledPath),VLocation.Font,ScaleX(376));
  VLocation.Hint:=CWMDisplayPath(InstalledPath); VLocation.ShowHint:=True;
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
  if Maintenance then VBounds(VCustom,338,197,100,30);
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
  VNoLaunch.Visible:=Main and Maintenance and (CWMRepairPath<>'') and not VMigrationNeeded;
  VNoLaunch.Enabled:=FileExists(AddBackslash(CWMRepairPath)+'Vex.exe');
  VNoLaunch.Caption:=CustomMessage('VOpen');
  VRemove.Visible:=Main and Maintenance; VRemove.Enabled:=not CWMBusy and not CWMUninstalled;
  if CWMRegistrationCount=1 then VRemove.Enabled:=VRemove.Enabled and (CWMRegistrations[0].Uninstaller<>'');
  VRemove.Hint:=CustomMessage('CWMInvalidRegistration'); VRemove.ShowHint:=not VRemove.Enabled;
  VClose.Enabled:=VLicenseOpen or not Installing or WizardForm.CancelButton.Enabled;
  VLicense.Visible:=Main and Home;
  VWebsite.Visible:=Main and Maintenance and (CWMRepairPath<>'') and not VMigrationNeeded;
  VLicense.Enabled:=True;
  ButtonY:=Height-58;
  VBounds(WizardForm.NextButton,507,ButtonY,104,36);
  if Maintenance then VBounds(WizardForm.NextButton,209,202,124,30);
  if Maintenance and (ActiveLanguage='chinesesimplified') then VBounds(WizardForm.NextButton,209,197,74,30);
  if Maintenance and ((CWMRepairPath='') or VMigrationNeeded) then VBounds(WizardForm.NextButton,447,ButtonY,164,36);
  if not Main then VBounds(WizardForm.NextButton,-300,-100,104,36);
  if Main and Installing then VBounds(WizardForm.CancelButton,508,ButtonY,104,36)
  else VBounds(WizardForm.CancelButton,-200,-100,104,36);
  if Installing then WizardForm.CancelButton.Caption:=CustomMessage('VCancelInstall');
  VBounds(VNoLaunch,507,ButtonY,104,36); VBounds(VRemove,333,202,48,30);
  if Maintenance and (ActiveLanguage='chinesesimplified') then VBounds(VRemove,293,197,32,30);
  VBounds(VWebsite,16,Height-36,148,25); VBounds(VLicense,208,ButtonY,170,36);
  if Maintenance then begin VBounds(VWebsite,429,ButtonY,68,36); VWebsite.Caption:=CustomMessage('VClose'); end;
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

procedure VShowLicense(Sender: TObject);
var LicenseText: AnsiString; Plain, RTF: String; I, Code: Integer; Format: TVCharFormat;
begin
  ExtractTemporaryFile('Vex-License.txt');
  if not LoadStringFromFile(ExpandConstant('{tmp}\Vex-License.txt'),LicenseText) then
    RaiseException('Unable to read the embedded MIT license.');
  Plain:=UTF8Decode(LicenseText);
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

procedure VUninstall(Sender: TObject);
begin
  if CWMRegistrationCount=1 then begin
    CWMUninstallSelected(0);
    if not CWMUninstalled then VLayout;
  end else begin VDetailsOpen:=True; VLayout; WizardForm.ActiveControl:=CWMPage.CheckListBox; end;
end;

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

procedure VFinishWithoutLaunch(Sender: TObject);
var I, Code: Integer; Executable: String;
begin
  if VIsMaintenance then begin
    Executable:=AddBackslash(CWMRepairPath)+'Vex.exe';
    #ifndef CodeWFSetupFixture
      if (CWMRepairPath<>'') and FileExists(Executable) then
        ShellExec('open',Executable,'',CWMRepairPath,SW_SHOWNORMAL,ewNoWait,Code);
    #endif
    exit;
  end;
  for I := 0 to WizardForm.RunList.Items.Count-1 do WizardForm.RunList.Checked[I] := False;
  WizardForm.NextButton.OnClick(WizardForm.NextButton);
end;

procedure VVisitWebsite(Sender: TObject);
var Code: Integer;
begin
  if VIsMaintenance then begin VExit(Sender); exit; end;
  #ifndef CodeWFSetupFixture
    ShellExec('open','{#VexWebsite}','','',SW_SHOWNORMAL,ewNoWait,Code);
  #endif
end;

{ 强调色只落在主操作上：非维护页与可升级时都是 Next 按钮，普通维护页留给「打开」。 }
function VIsPrimaryButton(I: Integer): Boolean;
begin
  if I=0 then Result:=(not VIsMaintenance) or VUpgradeAvailable
  else if I=5 then Result:=VIsMaintenance and not VUpgradeAvailable
  else Result:=False;
end;

procedure VPaintButton(I: Integer; DC: LongWord; Bounds: TVRect; State: LongWord);
var X,Y: Integer; TextBounds,ButtonBounds,WindowBounds: TVRect;
  Brush,Color,Foreground,PreviousFont,Pen,OldPen: LongWord; Saved: Integer;
begin
  Saved:=VSaveDC(DC);
  try
      Color:=VSurface; Foreground:=VMuted;
      if VIsPrimaryButton(I) then begin Color:=VAccent; Foreground:=VOnAccent; end;
      if I=9 then begin Color:=VSoft; Foreground:=VText; end;
      if (I=VHover) or ((State and 1)<>0) then begin
        Color:=VSoft; Foreground:=VAccent;
        if VIsPrimaryButton(I) then begin Color:=VAccentHover; Foreground:=VOnAccent; end;
        if I=3 then begin Color:=$1C2BC4; Foreground:=clWhite; end;
      end;
      if (State and 4)<>0 then begin Color:=VSoft; Foreground:=VMuted; end;
      VGetRect(VButtons[I].Handle,ButtonBounds); VGetRect(WizardForm.Handle,WindowBounds);
      X:=ButtonBounds.Left-WindowBounds.Left; Y:=ButtonBounds.Top-WindowBounds.Top;
      VDrawingCopy(DC,0,0,Bounds.Right,Bounds.Bottom,VBack.Bitmap.Canvas.Handle,X,Y,$00CC0020);
      if I<>3 then VRound(DC,0,0,Bounds.Right,Bounds.Bottom,8,Color,False)
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
      else if (I=1) and not VIsMaintenance then begin
        TextBounds.Left:=ScaleX(13); VDrawText(DC,VButtons[I].Caption,-1,TextBounds,$24);
        Pen:=VCreatePen(0,ScaleX(1),Foreground); OldPen:=VSelectObject(DC,Pen);
        X:=Bounds.Right-ScaleX(22); Y:=ScaleY(22);
        if VExpanded then begin VMove(DC,X-ScaleX(4),Y+ScaleY(2),0); VLineTo(DC,X,Y-ScaleY(2)); VLineTo(DC,X+ScaleX(4),Y+ScaleY(2)); end
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

function VLicenseWindowProc(Window, Message, WParam, LParam: LongWord): Longint;
var Delta, I, Command: Integer; Lines: Cardinal;
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
  Result:=VCallProc(VLicenseProc,Window,Message,WParam,LParam);
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
    VAccentHover:=$C4D8A1; VAccentSoft:=$333E28;
  end else begin
    VSurface:=clWhite; VText:=$313825; VMuted:=$80887B;
    VField:=clWhite; VSoft:=$F1F5F3; VLine:=$E3EAE5; VAccent:=$6B7624;
    VBrand:=$E5EEE8; VBrandText:=$364525; VOnAccent:=clWhite;
    VAccentHover:=$58611C; VAccentSoft:=$E9F0E5;
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
  VProgressPixel:=-1;
  if Light=0 then VSetWindowTheme(VLicenseMemo.Handle,'DarkMode_Explorer','')
  else VSetWindowTheme(VLicenseMemo.Handle,'Explorer','');
  VApplyLicenseColor;
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
    if (Y-Bounds.Top < ScaleY(38)) and (X-Bounds.Left < ScaleX(594)) then begin Result:=2; exit; end;
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
  VCustom:=VButton(156,262,208,30,CustomMessage('VCustom'),@VToggle);
  VLicense:=VButton(360,323,128,24,CustomMessage('VLicense'),@VShowLicense);
  VWebsite:=VButton(28,323,170,25,'CodeWF · {#AppVersion}',@VVisitWebsite);
  VClose:=VButton(472,8,32,32,'×',@VExit); VClose.Hint:=CustomMessage('VClose'); VClose.ShowHint:=True;
  VNoLaunch:=VButton(210,270,100,30,CustomMessage('VClose'),@VFinishWithoutLaunch);
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
  VRemove:=VButton(210,341,100,40,CustomMessage('VRemove'),@VUninstall);
  VModalTitle:=VLabel(31,80,458,29,19,CustomMessage('VLicenseTitle')); VFont(VModalTitle.Font,19,True);
  VLicenseMemo:=TRichEditViewer.Create(WizardForm); VLicenseMemo.Parent:=WizardForm;
  VLicenseMemo.ReadOnly:=True; VLicenseMemo.ScrollBars:=ssVertical; VLicenseMemo.BorderStyle:=bsNone; VLicenseMemo.WordWrap:=True;
  VFont(VLicenseMemo.Font,11,False); VLicenseMemo.Font.Name:='Consolas';
  VLicenseProc:=VSetLong(VLicenseMemo.Handle,-4,CreateCallback(@VLicenseWindowProc));
  VModalBack:=VButton(389,358,100,40,CustomMessage('VBack'),@VDismissLicense);
  VButtons[0]:=WizardForm.NextButton; VButtons[1]:=VCustom; VButtons[2]:=VLicense;
  VButtons[3]:=VClose; VButtons[4]:=VWebsite; VButtons[5]:=VNoLaunch;
  VButtons[6]:=WizardForm.CancelButton; VButtons[7]:=WizardForm.DirBrowseButton;
  VButtons[8]:=VRemove; VButtons[9]:=VModalBack;
  VPreviousProc:=VSetLong(WizardForm.Handle,-4,CreateCallback(@VWindowProc));
  for I:=0 to 9 do begin
    VFont(VButtons[I].Font,13,False);
    VButtons[I].Cursor:=crHand;
    Pixels:=13; Weight:=400; Face:='Segoe UI';
    if I=0 then begin Weight:=600; Face:='Segoe UI Semibold'; end;
    if ActiveLanguage='chinesesimplified' then Face:='Microsoft YaHei UI';
    if I=7 then Pixels:=14;
    if I=2 then Pixels:=12;
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
