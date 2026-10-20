; Vex 专用安装外观；不读取软件原型、应用源码或其他仓库。
; 安装、注册、覆盖、取消及回滚继续交给 Inno Setup。
#ifndef VexLicensePath
#define VexLicensePath "..\LICENSE"
#endif
[Files]
Source: "{#VexLicensePath}"; DestName: "Vex-License.txt"; Flags: dontcopy

[Setup]
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
english.VSlogan=Vex · Write your thoughts. See the connections.
chinesesimplified.VSlogan=维刻 Vex · 写下想法，看见脉络。
english.VWelcome=Install Vex
chinesesimplified.VWelcome=安装维刻 Vex
english.VMeta=Version {#AppVersion} · Current user
chinesesimplified.VMeta=版本 {#AppVersion} · 仅为当前用户安装
english.VInstall=Install now
chinesesimplified.VInstall=立即安装
english.VCustom=Customize installation
chinesesimplified.VCustom=自定义安装
english.VLocation=Installation folder
chinesesimplified.VLocation=安装位置
english.VBrowse=Browse…
chinesesimplified.VBrowse=浏览…
english.VDesktop=Create a desktop shortcut
chinesesimplified.VDesktop=创建桌面快捷方式
english.VScope=Install for the current user
chinesesimplified.VScope=仅为当前用户安装
english.VAssociation=Add Markdown opening options
chinesesimplified.VAssociation=添加 Markdown 打开方式
english.VLicense=Open source license
chinesesimplified.VLicense=开源许可
english.VLicenseTitle=MIT open source license
chinesesimplified.VLicenseTitle=MIT 开源许可
english.VAssociationHint=Keeps your default app unchanged
chinesesimplified.VAssociationHint=不改变系统默认应用
english.VMaintenance=Vex is already installed
chinesesimplified.VMaintenance=维刻 Vex 已安装
english.VMaintenanceIntro=Repair the installation or remove the app.
chinesesimplified.VMaintenanceIntro=可以修复安装，或移除此应用。
english.VRepair=Repair installation
chinesesimplified.VRepair=修复安装
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
english.VBack=Back
chinesesimplified.VBack=返回
english.VClose=Close
chinesesimplified.VClose=关闭
english.VOpen=Open Vex
chinesesimplified.VOpen=打开 Vex
english.VInstalling=Installing Vex…
chinesesimplified.VInstalling=正在安装 Vex…
english.VFinished=Installation complete
chinesesimplified.VFinished=安装完成
english.VRepairing=Repairing Vex…
chinesesimplified.VRepairing=正在修复 Vex…
english.VRepaired=Repair complete
chinesesimplified.VRepaired=修复完成
english.VPathError=Choose an absolute local folder outside Windows and the drive root.
chinesesimplified.VPathError=请选择有效的本地绝对路径，不能使用系统目录或磁盘根目录。
english.VUninstalling=Uninstalling Vex…
chinesesimplified.VUninstalling=正在卸载 Vex…

; 复用已验证的精确 AppId 探测和卸载启动逻辑，不加载旧的视觉引擎。
#include "CodeWF.Setup.Maintenance.iss"
#include "Vex.Migration.iss"
#include "Vex.Update.iss"

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
function VFrameRegion(DC, Region, Brush: LongWord; Width, Height: Integer): Boolean;
  external 'FrameRgn@gdi32.dll stdcall';
function VInvalidate(Window, Rect: LongWord; Erase: Boolean): Boolean;
  external 'InvalidateRect@user32.dll stdcall';
function VGetFocus: LongWord;
  external 'GetFocus@user32.dll stdcall';
function VSelectObject(DC, Obj: LongWord): LongWord;
  external 'SelectObject@gdi32.dll stdcall';
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

var
  VTitle, VName, VSlogan, VPathLabel, VScope, VPercent, VStatus, VDescription,
    VAssociationHint, VProgressLabel, VModalTitle: TLabel;
  VBack, VMark, VProgressBar: TBitmapImage;
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
  VHover, VProgressValue, VModalY: Integer;
  VSurface, VText, VMuted, VField, VSoft, VLine, VAccent: TColor;

procedure VBounds(Control: TControl; X, Y, Width, Height: Integer);
begin
  Control.SetBounds(ScaleX(X), ScaleY(Y), ScaleX(Width), ScaleY(Height));
end;

function VRegion(Width, Height: Integer): LongWord;
begin
  Result:=VRoundedRegion(0,0,Width+1,Height+1,ScaleX(16),ScaleY(16));
end;

procedure VRound(DC: LongWord; X,Y,Width,Height,Radius: Integer; Color: TColor; Outline: Boolean);
var Region, Brush: LongWord;
begin
  Region:=VRoundedRegion(X,Y,X+Width+1,Y+Height+1,ScaleX(Radius*2),ScaleY(Radius*2));
  Brush:=VBrush(Color);
  if Outline then VFrameRegion(DC,Region,Brush,ScaleX(1),ScaleY(1))
  else VFillRegion(DC,Region,Brush);
  VDeleteObject(Brush); VDeleteObject(Region);
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

procedure VPaintBackground;
var Region, Brush: LongWord; Canvas: TCanvas;
begin
  VBack.Bitmap.Width:=WizardForm.ClientWidth; VBack.Bitmap.Height:=WizardForm.ClientHeight;
  Canvas:=VBack.Bitmap.Canvas;
  Canvas.Brush.Color:=VSurface; Canvas.Pen.Color:=VSurface;
  Canvas.Rectangle(0,0,VBack.Bitmap.Width,VBack.Bitmap.Height);
  Region:=VRegion(VBack.Bitmap.Width,VBack.Bitmap.Height); Brush:=VBrush(VLine);
  VFrameRegion(Canvas.Handle,Region,Brush,ScaleX(1),ScaleY(1));
  VDeleteObject(Brush); VDeleteObject(Region);
  Canvas.Brush.Color:=VSoft; Canvas.Pen.Color:=VSoft;
  Canvas.Rectangle(ScaleX(1),ScaleY(1),ScaleX(180),VBack.Bitmap.Height-ScaleY(1));
  Canvas.Pen.Color:=VLine;
  Canvas.MoveTo(ScaleX(180),ScaleY(1)); Canvas.LineTo(ScaleX(180),VBack.Bitmap.Height-ScaleY(1));
  if (WizardForm.CurPageID=wpWelcome) and VExpanded and not VLicenseOpen then begin
    VRound(Canvas.Handle,ScaleX(208),ScaleY(208),ScaleX(404),ScaleY(204),7,VSoft,False);
    VRound(Canvas.Handle,ScaleX(220),ScaleY(246),ScaleX(300),ScaleY(36),6,VField,False);
    VRound(Canvas.Handle,ScaleX(220),ScaleY(246),ScaleX(300),ScaleY(36),6,VLine,True);
  end;
  if VLicenseOpen then
    VRound(Canvas.Handle,ScaleX(208),ScaleY(88),ScaleX(404),ScaleY(202),7,VSoft,False);
  VBack.Repaint;
end;

procedure VDrawProgress;
begin
  VProgressBar.Bitmap.Width:=ScaleX(404); VProgressBar.Bitmap.Height:=ScaleY(5);
  VProgressBar.Bitmap.Canvas.Brush.Color:=VSurface;
  VProgressBar.Bitmap.Canvas.Pen.Color:=VSurface;
  VProgressBar.Bitmap.Canvas.Rectangle(0,0,ScaleX(404),ScaleY(5));
  VRound(VProgressBar.Bitmap.Canvas.Handle,0,0,ScaleX(404),ScaleY(5),3,VSoft,False);
  if VProgressValue>0 then
    VRound(VProgressBar.Bitmap.Canvas.Handle,0,0,(ScaleX(404)*VProgressValue div 100),ScaleY(5),3,$D42B51,False);
  VProgressBar.Repaint;
end;

function VIsMaintenance: Boolean;
begin
  Result:=False;
  if CWMPage<>nil then Result:=WizardForm.CurPageID=CWMPage.ID;
end;

procedure VLayout;
var Page, Height, I, ButtonY, BrandY: Integer; Home, Finished, Installing, Maintenance, Main: Boolean;
  Region: LongWord; TextBounds: TVRect;
begin
  if not VInitialized then exit;
  Page:=WizardForm.CurPageID;
  Home:=Page=wpWelcome; Finished:=Page=wpFinished; Installing:=Page=wpInstalling;
  Maintenance:=VIsMaintenance; Main:=not VLicenseOpen;
  Height:=360;
  if Home and VExpanded then Height:=500;
  if Maintenance and VDetailsOpen then Height:=620;
  if not Home and not Finished and not Installing and not Maintenance then Height:=560;
  WizardForm.ClientHeight:=ScaleY(Height);
  Region:=VRegion(WizardForm.ClientWidth,WizardForm.ClientHeight);
  if VSetRegion(WizardForm.Handle,Region,True)=0 then VDeleteObject(Region);
  VBounds(VBack,0,0,640,Height);
  VTitle.Visible:=False;
  BrandY:=(Height-104) div 2;
  VMark.Visible:=True; VName.Visible:=True;
  VBounds(VMark,62,BrandY,56,56); VBounds(VName,16,BrandY+74,148,30);
  VName.Caption:='Vex'; VName.Alignment:=taCenter; VFont(VName.Font,20,True);
  VBounds(VClose,594,0,46,32);
  VSlogan.Visible:=Main; VSlogan.Alignment:=taLeftJustify; VSlogan.WordWrap:=True;
  VBounds(VSlogan,208,80,404,40); VFont(VSlogan.Font,14,False);
  VStatus.Visible:=Main; VStatus.Alignment:=taLeftJustify; VFont(VStatus.Font,22,True);
  VBounds(VStatus,208,38,404,34);
  VDescription.Visible:=Main and (Home or Maintenance);
  VDescription.Alignment:=taLeftJustify; VFont(VDescription.Font,12,False);
  VBounds(VDescription,208,122,404,30);
  if Home then begin VStatus.Caption:=CustomMessage('VWelcome'); VDescription.Caption:=CustomMessage('VMeta'); end;
  if Installing then VStatus.Caption:=CustomMessage('VInstalling');
  if Finished then VStatus.Caption:=CustomMessage('VFinished');
  if Installing and VRepairing then VStatus.Caption:=CustomMessage('VRepairing');
  if Finished and VRepairing then VStatus.Caption:=CustomMessage('VRepaired');
  WizardForm.OuterNotebook.Visible:=Main and not Home and not Finished and not Installing and (not Maintenance or VDetailsOpen);
  VBounds(WizardForm.OuterNotebook,208,220,404,280);
  VBounds(WizardForm.InnerPage,0,0,404,280); VBounds(WizardForm.InnerNotebook,0,0,404,280);
  for I:=0 to WizardForm.InnerNotebook.PageCount-1 do VBounds(WizardForm.InnerNotebook.Pages[I],0,0,404,280);
  if Maintenance then begin
    CWMPage.SubCaptionLabel.Visible:=False;
    VBounds(CWMPage.CheckListBox,0,0,404,85); VFont(CWMPage.CheckListBox.Font,12,False);
    VBounds(CWMLocationLabel,0,94,404,20); VFont(CWMLocationLabel.Font,12,False);
    VBounds(CWMLocation,0,120,404,136); VFont(CWMLocation.Font,12,False);
    VStatus.Caption:=CustomMessage('VMaintenance');
    if CWMRepairPath<>'' then VDescription.Caption:=CustomMessage('VMaintenanceIntro')
    else VDescription.Caption:=CustomMessage('VRegistrationIntro');
    if VMigrationNeeded then VDescription.Caption:=CustomMessage('VMigrationIntro');
    VBounds(VDescription,208,122,404,44);
  end;
  if not Home and not Finished and not Installing and not Maintenance then begin
    VStatus.Caption:=WizardForm.PageNameLabel.Caption;
    VDescription.Caption:=WizardForm.PageDescriptionLabel.Caption;
    VDescription.Visible:=Main; VBounds(VDescription,208,122,404,55);
    VBounds(WizardForm.OuterNotebook,208,190,404,260);
  end;
  VCustom.Visible:=Main and (Home or Maintenance);
  if Home then begin
    if VExpanded then VCustom.Caption:=CustomMessage('VCustom')+'  ⌃'
    else VCustom.Caption:=CustomMessage('VCustom')+'  ⌄';
  end else VCustom.Caption:=CustomMessage('VDetails');
  VBounds(VCustom,208,164,404,40);
  WizardForm.DirEdit.Visible:=Main and Home and VExpanded;
  WizardForm.DirBrowseButton.Visible:=Main and Home and VExpanded;
  VPathLabel.Visible:=Main and Home and VExpanded; VScope.Visible:=Main and Home and VExpanded;
  VDesktop.Visible:=Main and Home and VExpanded; VAssociation.Visible:=Main and Home and VExpanded;
  VAssociationHint.Visible:=Main and Home and VExpanded;
  VBounds(VPathLabel,220,220,380,18); VBounds(WizardForm.DirEdit,230,254,280,20);
  VBounds(WizardForm.DirBrowseButton,528,246,72,36); VBounds(VScope,220,294,380,18);
  VBounds(VDesktop,220,326,380,20); VBounds(VAssociation,220,357,380,20);
  VBounds(VAssociationHint,242,383,358,18);
  WizardForm.ProgressGauge.Visible:=False; WizardForm.StatusLabel.Visible:=False; WizardForm.FilenameLabel.Visible:=False;
  VPercent.Visible:=Main and Installing; VProgressLabel.Visible:=Main and Installing; VProgressBar.Visible:=Main and Installing;
  VBounds(VProgressLabel,208,155,300,18); VBounds(VPercent,548,155,64,18); VBounds(VProgressBar,208,185,404,5);
  if Installing then VDrawProgress;
  WizardForm.BackButton.Visible:=False;
  { Inno 的关闭查询依赖 CancelButton.Visible；安装前保留状态但移到窗口外。 }
  WizardForm.CancelButton.Visible:=not Finished; WizardForm.CancelButton.TabStop:=Main and Installing;
  VNoLaunch.Visible:=Main and Finished and (WizardForm.RunList.Items.Count>0);
  VRemove.Visible:=Main and Maintenance; VRemove.Enabled:=not CWMBusy and not CWMUninstalled;
  if CWMRegistrationCount=1 then VRemove.Enabled:=VRemove.Enabled and (CWMRegistrations[0].Uninstaller<>'');
  VRemove.Hint:=CustomMessage('CWMInvalidRegistration'); VRemove.ShowHint:=not VRemove.Enabled;
  VClose.Enabled:=VLicenseOpen or not Installing or WizardForm.CancelButton.Enabled;
  VLicense.Visible:=Main; VWebsite.Visible:=True; VLicense.Enabled:=True;
  ButtonY:=Height-58;
  VBounds(WizardForm.NextButton,508,ButtonY,104,36);
  if Maintenance then VBounds(WizardForm.NextButton,448,ButtonY,164,36);
  if not Main then VBounds(WizardForm.NextButton,-300,-100,104,36);
  if Main and Installing then VBounds(WizardForm.CancelButton,508,ButtonY,104,36)
  else VBounds(WizardForm.CancelButton,-200,-100,104,36);
  VBounds(VNoLaunch,430,ButtonY,68,36); VBounds(VRemove,370,ButtonY,68,36);
  VBounds(VWebsite,16,Height-36,148,25); VBounds(VLicense,208,ButtonY,170,36);
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
    VStatus.Alignment:=taCenter; VSlogan.Alignment:=taCenter;
    VBounds(VStatus,208,145,404,36); VBounds(VSlogan,208,194,404,40);
    if WizardForm.RunList.Items.Count>0 then WizardForm.NextButton.Caption:=CustomMessage('VOpen')
    else WizardForm.NextButton.Caption:=CustomMessage('VClose');
  end;
  if Maintenance then begin
    if CWMPage.SelectedValueIndex>0 then WizardForm.NextButton.Caption:=CustomMessage('VRemove')
    else if CWMRepairPath<>'' then WizardForm.NextButton.Caption:=CustomMessage('VRepair')
    else WizardForm.NextButton.Caption:=CustomMessage('VContinueInstall');
    if VMigrationNeeded and (CWMPage.SelectedValueIndex=0) then WizardForm.NextButton.Caption:=CustomMessage('VMigrate');
  end;
  VPaintBackground;
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
var I: Integer;
begin
  for I := 0 to WizardForm.RunList.Items.Count-1 do WizardForm.RunList.Checked[I] := False;
  WizardForm.NextButton.OnClick(WizardForm.NextButton);
end;

procedure VVisitWebsite(Sender: TObject);
var Code: Integer;
begin
  #ifndef CodeWFSetupFixture
    ShellExec('open','https://codewf.com','','',SW_SHOWNORMAL,ewNoWait,Code);
  #endif
end;

function VButtonProc(Window, Message, WParam, LParam: LongWord): Longint;
var I: Integer;
begin
  if Message = $00F4 then WParam := (WParam and not $F) or $B;
  if Message = $000F then VSetLong(Window,-16,(VGetLong(Window,-16) and not $F) or $B);
  Result := 0;
  for I := 0 to 9 do if (VButtons[I] <> nil) and (Window = VButtons[I].Handle) then begin
    Result := VCallProc(VButtonProcs[I],Window,Message,WParam,LParam); exit;
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
    Brush:=VBrush(VSoft); VFill(DC,Bounds,Brush); VDeleteObject(Brush);
    VRound(DC,0,ScaleY(3),ScaleX(13),ScaleY(13),2,VField,False);
    VRound(DC,0,ScaleY(3),ScaleX(13),ScaleY(13),2,$767676,True);
    if Check.Checked then begin
      VRound(DC,0,ScaleY(3),ScaleX(13),ScaleY(13),2,$D42B51,False);
      Pen:=VCreatePen(0,ScaleX(2),clWhite); Previous:=VSelectObject(DC,Pen);
      VMove(DC,ScaleX(3),ScaleY(10),0); VLineTo(DC,ScaleX(6),ScaleY(13)); VLineTo(DC,ScaleX(11),ScaleY(7));
      VSelectObject(DC,Previous); VDeleteObject(Pen);
    end;
    Bounds.Left:=ScaleX(22); VTextColor(DC,VText); VTextMode(DC,1);
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
    VSurface:=$2C1A21; VText:=$FFF1F6; VMuted:=$C9ADB9;
    VField:=$331E26; VSoft:=$3B222B; VLine:=$4F3340; VAccent:=$FFABC2;
  end else begin
    VSurface:=clWhite; VText:=$322129; VMuted:=$806B74;
    VField:=clWhite; VSoft:=$FBF2F5; VLine:=$EFE2E8; VAccent:=$D42B51;
  end;
  if VSurfaceBrush<>0 then VDeleteObject(VSurfaceBrush);
  VSurfaceBrush:=VBrush(VSurface);
  if VSoftBrush<>0 then VDeleteObject(VSoftBrush);
  VSoftBrush:=VBrush(VSoft);
  WizardForm.Color:=VSurface; WizardForm.Font.Color:=VText;
  VStyleChildren(WizardForm);
  VTitle.Font.Color:=VMuted; VSlogan.Font.Color:=VMuted;
  VScope.Font.Color:=VMuted; VAssociationHint.Font.Color:=VMuted;
  VDescription.Font.Color:=VMuted; VProgressLabel.Font.Color:=VMuted; VPercent.Font.Color:=VMuted;
  WizardForm.DirEdit.Color:=VField; WizardForm.DirEdit.Font.Color:=VText;
  VDesktop.Color:=VSoft; VAssociation.Color:=VSoft;
  VDesktop.Font.Color:=VText; VAssociation.Font.Color:=VText;
  VLicenseMemo.Color:=VSoft; VLicenseMemo.Font.Color:=VText;
  if Light=0 then VSetWindowTheme(VLicenseMemo.Handle,'DarkMode_Explorer','')
  else VSetWindowTheme(VLicenseMemo.Handle,'Explorer','');
  VApplyLicenseColor;
  VPaintBackground;
  VMark.Bitmap.Canvas.Brush.Color:=VSoft;
  VMark.Bitmap.Canvas.Pen.Color:=VSoft;
  VMark.Bitmap.Canvas.Rectangle(0,0,ScaleX(54),ScaleY(54));
  VRound(VMark.Bitmap.Canvas.Handle,0,0,ScaleX(54),ScaleY(54),15,$D42B51,False);
  VMark.Bitmap.Canvas.Brush.Color:=$D42B51;
  VFont(VMark.Bitmap.Canvas.Font,30,True);
  VMark.Bitmap.Canvas.Font.Name:='Segoe UI Semibold'; VMark.Bitmap.Canvas.Font.Style:=[];
  VMark.Bitmap.Canvas.Font.Color:=clWhite;
  VMark.Bitmap.Canvas.TextOut(ScaleX(17),ScaleY(7),'V');
  WizardForm.Repaint;
end;

function VWindowProc(Window, Message, WParam, LParam: LongWord): Longint;
var Item: TVDrawItem; I, X, Y, Hover: Integer; Bounds, TextBounds: TVRect;
  Brush, Color, Foreground, PreviousFont, Pen, OldPen: LongWord; Point: TVPoint;
begin
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
  if Message = $002B then begin
    VCopyItem(Item,LParam,48);
    for I := 0 to 9 do if (VButtons[I] <> nil) and (Item.ItemWindow = VButtons[I].Handle) then begin
      Bounds:=Item.Bounds; Color:=VSurface; Foreground:=VMuted;
      if I=0 then begin Color:=$D42B51; Foreground:=clWhite; end;
      if (I=1) or (I=7) then begin Color:=VSoft; Foreground:=VText; end;
      if I=9 then begin Color:=VSoft; Foreground:=VText; end;
      if (I=VHover) or ((Item.ItemState and 1)<>0) then begin
        Color:=VSoft; Foreground:=VAccent;
        if I=0 then begin Color:=$BD2045; Foreground:=clWhite; end;
      end;
      if (Item.ItemState and 4)<>0 then begin Color:=VSoft; Foreground:=VMuted; end;
      if I=4 then Color:=VSoft;
      Brush:=VBrush(VSurface); if I=4 then begin VDeleteObject(Brush); Brush:=VBrush(VSoft); end;
      VFill(Item.DC,Bounds,Brush); VDeleteObject(Brush);
      VRound(Item.DC,0,0,Bounds.Right,Bounds.Bottom,8,Color,False);
      if I=7 then VRound(Item.DC,0,0,Bounds.Right,Bounds.Bottom,6,VLine,True);
      VTextColor(Item.DC,Foreground); VTextMode(Item.DC,1);
      PreviousFont:=VSelectObject(Item.DC,VButtonFonts[I]);
      TextBounds:=Bounds;
      if I=3 then begin
        Pen:=VCreatePen(0,ScaleX(1),Foreground); OldPen:=VSelectObject(Item.DC,Pen);
        VMove(Item.DC,ScaleX(10),ScaleY(10),0); VLineTo(Item.DC,ScaleX(22),ScaleY(22));
        VMove(Item.DC,ScaleX(22),ScaleY(10),0); VLineTo(Item.DC,ScaleX(10),ScaleY(22));
        VSelectObject(Item.DC,OldPen); VDeleteObject(Pen);
      end
      else if I=4 then VDrawText(Item.DC,VButtons[I].Caption,-1,TextBounds,$24)
      else if I=2 then VDrawText(Item.DC,VButtons[I].Caption,-1,TextBounds,$24)
      else VDrawText(Item.DC,VButtons[I].Caption,-1,TextBounds,$25);
      VSelectObject(Item.DC,PreviousFont);
      { 保留键盘焦点提示，使用与原型一致的紫色圆角线，替换系统虚线框。 }
      if ((Item.ItemState and $10)<>0) and ((Item.ItemState and $200)=0) then begin
        VRound(Item.DC,2,2,Bounds.Right-5,Bounds.Bottom-5,6,VAccent,True);
      end;
      Result:=1; exit;
    end;
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
var I, Pixels, Weight: Integer; Face: String;
begin
  if WizardSilent then exit;
  WizardForm.BorderStyle:=bsNone;
  VSetClassLong(WizardForm.Handle,-26,VGetClassLong(WizardForm.Handle,-26) or $20000);
  WizardForm.ClientWidth:=ScaleX(640); WizardForm.ClientHeight:=ScaleY(360);
  VFont(WizardForm.Font,14,False); VHover:=-1;
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
  VCustom:=VButton(156,262,208,30,CustomMessage('VCustom'),@VToggle);
  VLicense:=VButton(360,323,128,24,CustomMessage('VLicense'),@VShowLicense);
  VWebsite:=VButton(28,323,170,25,'CodeWF · {#AppVersion}',@VVisitWebsite);
  VClose:=VButton(472,8,32,32,'×',@VExit); VClose.Hint:=CustomMessage('VClose'); VClose.ShowHint:=True;
  VNoLaunch:=VButton(210,270,100,30,CustomMessage('VClose'),@VFinishWithoutLaunch);
  VPathLabel:=VLabel(54,322,412,18,12,CustomMessage('VLocation'));
  VScope:=VLabel(54,393,412,17,11,CustomMessage('VScope'));
  WizardForm.DirEdit.Parent:=WizardForm; VBounds(WizardForm.DirEdit,65,356,314,20);
  WizardForm.DirEdit.BorderStyle:=bsNone;
  VFont(WizardForm.DirEdit.Font,12,False); WizardForm.DirEdit.Font.Name:='Consolas';
  WizardForm.DirBrowseButton.Parent:=WizardForm; VBounds(WizardForm.DirBrowseButton,397,346,69,37);
  VDesktop:=TNewCheckBox.Create(WizardForm); VDesktop.Parent:=WizardForm;
  VDesktop.Caption:=CustomMessage('VDesktop'); VBounds(VDesktop,54,423,412,20); VFont(VDesktop.Font,12,False);
  VDesktop.Checked:=False;
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
    if (I=1) or (I=7) then Pixels:=12;
    if (I=2) or (I=4) then Pixels:=11;
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
  VCheckFont:=VCreateFont(-ScaleY(12),0,0,0,400,0,0,0,1,0,0,5,0,Face);
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
  if (CWMPage<>nil) and (PageID=CWMPage.ID) then CWMMaintenancePageChanged(PageID);
  VLayout;
end;

<event('CurInstallProgressChanged')>
procedure VProgressChanged(Current, Maximum: Integer);
begin
  if VInitialized and (Maximum>0) then begin
    VPercent.Caption:=IntToStr((Current*100) div Maximum)+'%';
    VProgressValue:=(Current*100) div Maximum; VDrawProgress;
    if WizardForm.CurPageID=wpInstalling then VClose.Enabled:=WizardForm.CancelButton.Enabled;
  end;
end;

<event('DeinitializeSetup')>
procedure VRelease;
var I: Integer;
begin
  if not VInitialized then exit;
  VInitialized:=False;
  VKillTimer(WizardForm.Handle,41);
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
