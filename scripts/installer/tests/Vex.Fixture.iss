; 安装界面夹具：只复制 LICENSE，不注册、不建快捷方式、不启动应用。
#define AppVersion "1.0.0"
#define CodeWFSetupFixture
#define VexLicensePath "..\..\..\LICENSE"
[Setup]
AppId=Vex.Installer.Verification.Only
AppName=Vex Installer Fixture
AppVersion={#AppVersion}
DefaultDirName={tmp}\Vex.Installer.Fixture
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
OutputBaseFilename=Vex.Installer.Fixture
WizardStyle=modern
LicenseFile=..\..\..\LICENSE
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "..\..\Languages\ChineseSimplified.isl"
[Files]
Source: "..\..\..\LICENSE"; DestDir: "{app}"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "markdownassociation"; Description: "{cm:VAssociation}"
#include "..\Vex.Setup.iss"
#include "Fixture.Progress.iss"
[Code]
<event('CurPageChanged')>
procedure VFixtureLaunchVisual(PageID: Integer);
begin
  if PageID<>wpFinished then exit;
  { 只添加无执行项的视觉选项；夹具没有 [Run] 声明，绝不启动应用。 }
  WizardForm.RunList.AddCheckBox(CustomMessage('VOpen'),'',0,False,True,False,False,nil);
  VLayout;
end;

<event('CurStepChanged')>
procedure VFixtureVerifyTasks(Step: TSetupStep);
begin
  if Step<>ssInstall then exit;
  if WizardIsTaskSelected('desktopicon') then Log('VEX FIXTURE desktop=1') else Log('VEX FIXTURE desktop=0');
  if WizardIsTaskSelected('markdownassociation') then Log('VEX FIXTURE association=1') else Log('VEX FIXTURE association=0');
end;

#ifdef VexFixtureMaintenance
<event('InitializeWizard')>
procedure VFixtureExistingInstallation;
begin
  { 安装注册仅为内存样例，CodeWFSetupFixture 已在编译期移除真实注册探测与卸载启动。 }
  CWMRegistrationCount:=1;
  CWMRepairPath:=ExpandConstant('{tmp}\维刻 maintenance with spaces');
  CWMRegistrations[0].ScopeName:=CustomMessage('CWMCurrentUser');
  CWMRegistrations[0].AppPath:=CWMRepairPath;
  CWMRegistrations[0].Uninstaller:=AddBackslash(CWMRepairPath)+'unins000.exe';
  #ifdef VexFixtureUnknownLocation
    CWMRepairPath:=''; CWMRegistrations[0].AppPath:=''; CWMRegistrations[0].Uninstaller:='';
    CWMRegistrations[0].ScopeName:=CustomMessage('CWMAllUsers');
  #endif
  #ifdef VexFixtureMultiple
    CWMRegistrationCount:=2;
    CWMRegistrations[1].ScopeName:=CustomMessage('CWMAllUsers');
    CWMRegistrations[1].AppPath:='C:\Program Files\Vex';
    CWMRegistrations[1].Uninstaller:='C:\Program Files\Vex\unins000.exe';
  #endif
  #ifdef VexFixtureMigration
    VMigrationNeeded:=True;
  #endif
  CWMCreatePage;
  CWMPage.CheckListBox.OnClickCheck:=@VMaintenanceSelection;
end;
#endif
