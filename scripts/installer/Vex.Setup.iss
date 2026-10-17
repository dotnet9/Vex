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
english.VInstall=Install now
chinesesimplified.VInstall=立即安装
english.VCustom=Custom installation
chinesesimplified.VCustom=自定义安装
english.VLocation=Install location
chinesesimplified.VLocation=安装位置
english.VScope=Install for the current user
chinesesimplified.VScope=仅为当前用户安装
english.VAssociation=Add Vex as an option for Markdown files
chinesesimplified.VAssociation=添加 Markdown 打开方式
english.VLicense=MIT license
chinesesimplified.VLicense=MIT 开源许可
english.VClose=Close
chinesesimplified.VClose=关闭
english.VOpen=Open Vex
chinesesimplified.VOpen=打开 Vex
english.VInstalling=Installing Vex…
chinesesimplified.VInstalling=正在安装 Vex…
english.VFinished=Installation complete
chinesesimplified.VFinished=安装完成
english.VPathError=Choose an absolute local folder outside Windows and the drive root.
chinesesimplified.VPathError=请选择有效的本地绝对路径，不能使用系统目录或磁盘根目录。
english.VUninstalling=Uninstalling Vex…
chinesesimplified.VUninstalling=正在卸载 Vex…

; 复用已验证的精确 AppId 探测和卸载启动逻辑，不加载旧的视觉引擎。
#include "CodeWF.Setup.Maintenance.iss"

[Code]
type
  TVPoint = record X, Y: Integer; end;
  TVRect = record Left, Top, Right, Bottom: Integer; end;
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
function VFocusRect(DC: LongWord; var Bounds: TVRect): Boolean;
  external 'DrawFocusRect@user32.dll stdcall';
function VIsWindow(Window: LongWord): Boolean;
  external 'IsWindow@user32.dll stdcall';
function VSetWindowTheme(Window: LongWord; Application, IDs: String): Integer;
  external 'SetWindowTheme@uxtheme.dll stdcall';

var
  VTitle, VName, VSlogan, VVersion, VPathLabel, VScope, VPercent: TLabel;
  VMark: TBitmapImage;
  VCustom, VLicense, VClose, VWebsite, VNoLaunch: TNewButton;
  VDesktop, VAssociation: TNewCheckBox;
  VButtons: array[0..7] of TNewButton;
  VButtonProcs: array[0..7] of LongWord;
  VPreviousProc, VSurfaceBrush: LongWord;
  VExpanded, VInitialized: Boolean;
  VSurface, VText, VMuted, VField: TColor;

procedure VBounds(Control: TControl; X, Y, Width, Height: Integer);
begin
  Control.SetBounds(ScaleX(X), ScaleY(Y), ScaleX(Width), ScaleY(Height));
end;

procedure VShape(Window: LongWord; Width, Height: Integer);
var Points: array[0..4] of TVPoint; Region, Rounded: LongWord;
begin
  { 只有右下角切口，其他角使用圆角区域。 }
  Points[0].X:=0; Points[0].Y:=0;
  Points[1].X:=Width; Points[1].Y:=0;
  Points[2].X:=Width; Points[2].Y:=Height-ScaleY(26);
  Points[3].X:=Width-ScaleX(26); Points[3].Y:=Height;
  Points[4].X:=0; Points[4].Y:=Height;
  Region := VPolygon(Points[0], 5, 1);
  Rounded:=VRoundedRegion(0,0,Width+1,Height+1,ScaleX(20),ScaleY(20));
  VCombineRegion(Region,Region,Rounded,1); VDeleteObject(Rounded);
  if VSetRegion(Window, Region, True) = 0 then VDeleteObject(Region);
end;

function VLabel(X, Y, Width, Height, Size: Integer; Text: String): TLabel;
begin
  Result := TLabel.Create(WizardForm);
  Result.Parent := WizardForm; Result.AutoSize := False;
  Result.Transparent := True;
  Result.Caption := Text; Result.Font.Size := Size;
  VBounds(Result, X, Y, Width, Height);
end;

function VButton(X, Y, Width, Height: Integer; Text: String; Handler: TNotifyEvent): TNewButton;
begin
  Result := TNewButton.Create(WizardForm); Result.Parent := WizardForm;
  Result.Caption := Text; Result.OnClick := Handler;
  VBounds(Result, X, Y, Width, Height);
end;

procedure VLayout;
var Page, Height, I, ButtonY: Integer; Home, Finished, Installing: Boolean;
begin
  if not VInitialized then exit;
  Page := WizardForm.CurPageID;
  Home := Page = wpWelcome; Finished := Page = wpFinished; Installing := Page = wpInstalling;
  Height := 366;
  if Home and VExpanded then Height := 550;
  if not Home and not Finished and not Installing then Height := 620;
  WizardForm.ClientHeight := ScaleY(Height);
  VShape(WizardForm.Handle, WizardForm.ClientWidth, WizardForm.ClientHeight);
  WizardForm.OuterNotebook.Visible := not Home and not Finished and not Installing;
  VBounds(WizardForm.OuterNotebook,32,154,456,350);
  VBounds(WizardForm.InnerPage,0,0,456,350);
  VBounds(WizardForm.InnerNotebook,0,0,456,350);
  for I := 0 to WizardForm.InnerNotebook.PageCount-1 do
    VBounds(WizardForm.InnerNotebook.Pages[I],0,0,456,350);
  VMark.Visible := Home or Finished or Installing;
  VName.Caption := 'Vex'; VName.Font.Size := 25;
  if Installing then begin VName.Caption := CustomMessage('VInstalling'); VName.Font.Size := 18; end;
  if Finished then begin VName.Caption := CustomMessage('VFinished'); VName.Font.Size := 20; end;
  if not Home and not Finished and not Installing then begin
    VName.Caption := WizardForm.PageNameLabel.Caption; VName.Font.Size := 18;
  end;
  VSlogan.Visible := Home or Finished or Installing;
  VCustom.Visible := Home; WizardForm.DirEdit.Visible := Home and VExpanded;
  WizardForm.DirBrowseButton.Visible := Home and VExpanded;
  VPathLabel.Visible := Home and VExpanded; VScope.Visible := Home and VExpanded;
  VDesktop.Visible := Home and VExpanded; VAssociation.Visible := Home and VExpanded;
  WizardForm.ProgressGauge.Visible := Installing; WizardForm.StatusLabel.Visible := Installing;
  WizardForm.FilenameLabel.Visible := Installing; VPercent.Visible := Installing;
  WizardForm.BackButton.Visible := False;
  { Inno 的关闭查询依赖 CancelButton.Visible；安装前保留状态但移到窗口外。 }
  WizardForm.CancelButton.Visible := not Finished;
  WizardForm.CancelButton.TabStop:=Installing;
  VNoLaunch.Visible := Finished and (WizardForm.RunList.Items.Count > 0);
  VClose.Enabled:=not Installing or WizardForm.CancelButton.Enabled;
  VLicense.Enabled:=Home or Finished;
  ButtonY := 218;
  if not Home and not Finished then ButtonY := Height-100;
  VBounds(WizardForm.NextButton,120,ButtonY,280,42);
  if Installing then VBounds(WizardForm.CancelButton,210,290,100,28)
  else VBounds(WizardForm.CancelButton,-200,-100,100,28);
  VBounds(VNoLaunch,210,270,100,30);
  VBounds(VWebsite,26,Height-43,76,24);
  VBounds(VVersion,108,Height-38,150,20);
  VBounds(VLicense,360,Height-43,128,24);
  if Home then WizardForm.NextButton.Caption := CustomMessage('VInstall');
  if Finished then begin
    WizardForm.RunList.Visible := False;
    if WizardForm.RunList.Items.Count > 0 then WizardForm.NextButton.Caption := CustomMessage('VOpen')
    else WizardForm.NextButton.Caption := CustomMessage('VClose');
  end;
end;

procedure VToggle(Sender: TObject);
begin VExpanded := not VExpanded; VLayout; end;

procedure VShowLicense(Sender: TObject);
var Dialog: TSetupForm; Memo: TRichEditViewer; Close: TNewButton; LicenseText: AnsiString;
begin
  ExtractTemporaryFile('Vex-License.txt');
  if not LoadStringFromFile(ExpandConstant('{tmp}\Vex-License.txt'),LicenseText) then
    RaiseException('Unable to read the embedded MIT license.');
  Dialog := CreateCustomForm(ScaleX(600), ScaleY(410), False, False);
  try
    Dialog.Caption := CustomMessage('VLicense');
    Memo := TRichEditViewer.Create(Dialog); Memo.Parent := Dialog;
    Memo.SetBounds(ScaleX(20),ScaleY(20),ScaleX(560),ScaleY(320));
    Memo.ReadOnly := True; Memo.ScrollBars := ssVertical;
    Memo.RTFText := LicenseText;
    Close := TNewButton.Create(Dialog); Close.Parent := Dialog;
    Close.SetBounds(ScaleX(470),ScaleY(360),ScaleX(110),ScaleY(30));
    Close.Caption := CustomMessage('VClose'); Close.ModalResult := mrOk;
    Close.Default := True; Close.Cancel := True;
    Dialog.ShowModal;
  finally Dialog.Free; end;
end;

procedure VExit(Sender: TObject);
var I: Integer;
begin
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
  for I := 0 to 7 do if (VButtons[I] <> nil) and (Window = VButtons[I].Handle) then begin
    Result := VCallProc(VButtonProcs[I],Window,Message,WParam,LParam); exit;
  end;
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
  if Light = 0 then begin VSurface:=$281A1D; VText:=$FBF0F3; VMuted:=$C9ADB8; VField:=$302024; end
  else begin VSurface:=clWhite; VText:=$372126; VMuted:=$806770; VField:=$FCF7F8; end;
  if VSurfaceBrush<>0 then VDeleteObject(VSurfaceBrush);
  VSurfaceBrush:=VBrush(VSurface);
  WizardForm.Color:=VSurface; WizardForm.Font.Color:=VText;
  VStyleChildren(WizardForm);
  VTitle.Font.Color:=VMuted; VSlogan.Font.Color:=VMuted;
  VScope.Font.Color:=VMuted; VVersion.Font.Color:=VMuted;
  WizardForm.DirEdit.Color:=VField; WizardForm.DirEdit.Font.Color:=VText;
  VDesktop.Font.Color:=VText; VAssociation.Font.Color:=VText;
  WizardForm.Repaint;
end;

function VWindowProc(Window, Message, WParam, LParam: LongWord): Longint;
var Item: TVDrawItem; I, X, Y: Integer; Bounds: TVRect; Brush, Color, Foreground: LongWord;
begin
  if ((Message=$0135) or (Message=$0138)) and
    ((LParam=VDesktop.Handle) or (LParam=VAssociation.Handle)) then begin
    VTextColor(WParam,VText); VBackgroundColor(WParam,VSurface);
    Result:=VSurfaceBrush; exit;
  end;
  if Message = $0084 then begin
    VGetRect(Window,Bounds);
    X := LParam and $FFFF; if X >= $8000 then X := X-$10000;
    Y := (LParam shr 16) and $FFFF; if Y >= $8000 then Y := Y-$10000;
    if (Y-Bounds.Top < ScaleY(38)) and (X-Bounds.Left < ScaleX(470)) then begin Result:=2; exit; end;
  end;
  if Message = $002B then begin
    VCopyItem(Item,LParam,48);
    for I := 0 to 7 do if (VButtons[I] <> nil) and (Item.ItemWindow = VButtons[I].Handle) then begin
      Bounds:=Item.Bounds; Color:=VSurface; Foreground:=VMuted;
      if I=0 then begin Color:=$D42B51; Foreground:=clWhite; end;
      if (Item.ItemState and 1)<>0 then begin Color:=VField; Foreground:=VText; end;
      if (Item.ItemState and 4)<>0 then begin Color:=VField; Foreground:=VMuted; end;
      Brush:=VBrush(Color); VFill(Item.DC,Bounds,Brush); VDeleteObject(Brush);
      VTextColor(Item.DC,Foreground); VTextMode(Item.DC,1);
      VDrawText(Item.DC,VButtons[I].Caption,-1,Bounds,$25);
      if (Item.ItemState and $10)<>0 then begin
        Bounds.Left:=Bounds.Left+4; Bounds.Top:=Bounds.Top+4;
        Bounds.Right:=Bounds.Right-4; Bounds.Bottom:=Bounds.Bottom-4;
        VFocusRect(Item.DC,Bounds);
      end;
      Result:=1; exit;
    end;
  end;
  Result:=VCallProc(VPreviousProc,Window,Message,WParam,LParam);
  if (Message=$001A) and VInitialized then VApplyTheme;
end;

<event('InitializeWizard')>
procedure VInitialize;
var I: Integer;
begin
  if WizardSilent then exit;
  WizardForm.BorderStyle:=bsNone;
  WizardForm.ClientWidth:=ScaleX(520); WizardForm.ClientHeight:=ScaleY(366);
  WizardForm.Font.Name:='Segoe UI'; WizardForm.Font.Size:=10;
  WizardForm.Position:=poScreenCenter;
  WizardForm.MainPanel.Visible:=False; WizardForm.Bevel.Visible:=False;
  WizardForm.Bevel1.Visible:=False; WizardForm.BeveledLabel.Visible:=False;
  WizardForm.WizardBitmapImage.Visible:=False; WizardForm.WizardBitmapImage2.Visible:=False;
  VTitle:=VLabel(20,13,420,24,9,CustomMessage('VTitle'));
  VMark:=TBitmapImage.Create(WizardForm); VMark.Parent:=WizardForm;
  VMark.Bitmap.Width:=48; VMark.Bitmap.Height:=48; VMark.Stretch:=True;
  VMark.Bitmap.Canvas.Brush.Color:=$D42B51; VMark.Bitmap.Canvas.Pen.Color:=$D42B51;
  VMark.Bitmap.Canvas.Rectangle(0,0,48,48);
  VMark.Bitmap.Canvas.Font.Name:='Segoe UI'; VMark.Bitmap.Canvas.Font.Size:=25;
  VMark.Bitmap.Canvas.Font.Style:=[fsBold]; VMark.Bitmap.Canvas.Font.Color:=clWhite;
  VMark.Bitmap.Canvas.TextOut(12,1,'V');
  VBounds(VMark,236,64,48,48);
  VName:=VLabel(32,126,456,40,25,'Vex'); VName.Alignment:=taCenter; VName.Font.Style:=[fsBold];
  VSlogan:=VLabel(25,177,470,30,10,CustomMessage('VSlogan')); VSlogan.Alignment:=taCenter;
  VVersion:=VLabel(108,328,150,20,8,'{#AppVersion}');
  VCustom:=VButton(160,268,200,28,CustomMessage('VCustom'),@VToggle);
  VLicense:=VButton(360,323,128,24,CustomMessage('VLicense'),@VShowLicense);
  VWebsite:=VButton(26,323,76,24,'CodeWF',@VVisitWebsite);
  VClose:=VButton(470,5,36,30,'×',@VExit); VClose.Hint:=CustomMessage('VClose'); VClose.ShowHint:=True;
  VNoLaunch:=VButton(210,270,100,30,CustomMessage('VClose'),@VFinishWithoutLaunch);
  VPathLabel:=VLabel(32,313,456,24,10,CustomMessage('VLocation'));
  VScope:=VLabel(32,390,456,24,9,CustomMessage('VScope'));
  WizardForm.DirEdit.Parent:=WizardForm; VBounds(WizardForm.DirEdit,32,344,350,30);
  WizardForm.DirBrowseButton.Parent:=WizardForm; VBounds(WizardForm.DirBrowseButton,394,344,94,30);
  VDesktop:=TNewCheckBox.Create(WizardForm); VDesktop.Parent:=WizardForm;
  VDesktop.Caption:=CustomMessage('CreateDesktopIcon'); VBounds(VDesktop,32,423,456,24);
  VDesktop.Checked:=False;
  VAssociation:=TNewCheckBox.Create(WizardForm); VAssociation.Parent:=WizardForm;
  VAssociation.Caption:=CustomMessage('VAssociation'); VBounds(VAssociation,32,457,456,24);
  VAssociation.Checked:=True;
  WizardForm.ProgressGauge.Parent:=WizardForm; VBounds(WizardForm.ProgressGauge,60,224,400,10);
  WizardForm.StatusLabel.Parent:=WizardForm; VBounds(WizardForm.StatusLabel,60,240,400,20);
  WizardForm.FilenameLabel.Parent:=WizardForm; VBounds(WizardForm.FilenameLabel,60,264,400,20);
  VPercent:=VLabel(60,202,400,22,10,'0%'); VPercent.Alignment:=taCenter;
  VButtons[0]:=WizardForm.NextButton; VButtons[1]:=VCustom; VButtons[2]:=VLicense;
  VButtons[3]:=VClose; VButtons[4]:=VWebsite; VButtons[5]:=VNoLaunch;
  VButtons[6]:=WizardForm.CancelButton; VButtons[7]:=WizardForm.DirBrowseButton;
  VPreviousProc:=VSetLong(WizardForm.Handle,-4,CreateCallback(@VWindowProc));
  for I:=0 to 7 do begin
    VSetLong(VButtons[I].Handle,-16,(VGetLong(VButtons[I].Handle,-16) and not $F) or $B);
    VButtonProcs[I]:=VSetLong(VButtons[I].Handle,-4,CreateCallback(@VButtonProc));
  end;
  VInitialized:=True; VApplyTheme; VLayout;
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
  Result := (PageID=wpLicense) or (PageID=wpSelectDir) or (PageID=wpSelectTasks);
end;

<event('NextButtonClick')>
function VNext(PageID: Integer): Boolean;
var Path, WindowsPath: String;
begin
  Result:=True;
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
  VLayout;
  if (CWMPage<>nil) and (PageID=CWMPage.ID) then CWMMaintenancePageChanged(PageID);
end;

<event('CurInstallProgressChanged')>
procedure VProgressChanged(Current, Maximum: Integer);
begin
  if VInitialized and (Maximum>0) then begin
    VPercent.Caption:=IntToStr((Current*100) div Maximum)+'%';
    if WizardForm.CurPageID=wpInstalling then VClose.Enabled:=WizardForm.CancelButton.Enabled;
  end;
end;

<event('DeinitializeSetup')>
procedure VRelease;
var I: Integer;
begin
  VInitialized:=False;
  if VPreviousProc<>0 then VSetLong(WizardForm.Handle,-4,VPreviousProc);
  for I:=0 to 7 do if VButtonProcs[I]<>0 then begin
    if VIsWindow(VButtons[I].Handle) then VSetLong(VButtons[I].Handle,-4,VButtonProcs[I]);
  end;
  if VSurfaceBrush<>0 then VDeleteObject(VSurfaceBrush);
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
