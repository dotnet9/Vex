[CustomMessages]
; Vex 统一为当前用户安装。跨范围升级只通过精确注册的卸载器完成。
english.VMigrate=Upgrade and migrate
chinesesimplified.VMigrate=升级并迁移
english.VMigrationIntro=An older all-users installation will be removed before installing for this user.
chinesesimplified.VMigrationIntro=将先移除旧的全用户安装，再升级到当前用户目录。
english.VMigrationConfirm=An all-users Vex installation was found:%n%1%n%nRemove it and continue installing for the current user? Other Windows users will no longer have this all-users copy. Administrator permission is required. Existing settings will be backed up; your documents will not be removed.
chinesesimplified.VMigrationConfirm=发现全用户安装的 Vex：%n%1%n%n是否移除这份旧安装，并继续为当前用户安装？其他 Windows 用户将无法再使用这份全用户安装。系统会请求管理员权限；已有设置会先备份，文档不会被移除。
english.VMigrationBlocked=The all-users installation could not be migrated. Installation has stopped to avoid creating another copy. Remove the old Vex from Windows Installed apps, then run this setup again.
chinesesimplified.VMigrationBlocked=未能完成全用户安装迁移。为避免产生第二份安装，本次安装已停止。请在 Windows“已安装的应用”中移除旧版 Vex 后重试。
english.VMigrationInteractive=An all-users Vex installation exists. Run setup interactively to migrate it before using silent installation.
chinesesimplified.VMigrationInteractive=检测到全用户安装的 Vex。请先打开安装界面完成迁移，再使用静默安装。
english.VMigrationPath=Choose a different current-user installation folder. The target cannot overlap the old all-users installation or a system program folder.
chinesesimplified.VMigrationPath=请选择当前用户的安装目录，不能与旧的全用户目录重叠，也不能位于系统程序目录内。
english.VMigrationBackupFailed=Settings could not be backed up or restored. Installation has stopped. The original settings and any completed backups are retained.
chinesesimplified.VMigrationBackupFailed=无法备份或恢复设置，安装已停止。原始设置及已完成的备份均保留。

[Code]
#ifdef VexMigrationTest
  #ifndef CodeWFSetupFixture
    #error Migration test backend requires CodeWFSetupFixture
  #endif
#endif
type
  TVMigrationRegistrations = array of TCWMRegistration;
var
  VMigrationNeeded: Boolean;
  VMigrationBackup: String;
  #ifdef VexMigrationTest
    VTestEntries: TVMigrationRegistrations;
    VTestConfirm, VTestStart, VTestKeepRegistration, VTestChangeRegistration: Boolean;
    VTestCode, VTestLaunches: Integer;
    VTestBackupRoot: String;
  #endif

function VPathsOverlap(const First, Second: String): Boolean;
var A, B: String;
begin
  A:=Lowercase(AddBackslash(CWMNormalizePath(First)));
  B:=Lowercase(AddBackslash(CWMNormalizePath(Second)));
  Result:=(A<>'\') and (B<>'\') and ((Pos(A,B)=1) or (Pos(B,A)=1));
end;

function VMachineRegistrations(var Entries: TVMigrationRegistrations): Integer;
var Entry: TCWMRegistration;
begin
  Result:=0;
  SetArrayLength(Entries,2);
  #ifdef VexMigrationTest
    Entries:=VTestEntries;
    Result:=GetArrayLength(Entries);
  #endif
  #ifndef CodeWFSetupFixture
    if IsWin64 then
      if CWMReadRegistration(HKLM64,CustomMessage('CWMAllUsers'),Entry) then begin
        Entries[Result]:=Entry; Result:=Result+1;
      end;
    if CWMReadRegistration(HKLM32,CustomMessage('CWMAllUsers'),Entry) then begin
      Entries[Result]:=Entry; Result:=Result+1;
    end;
  #endif
end;

function VReadMigrationRegistration(const Previous: TCWMRegistration; var Current: TCWMRegistration): Boolean;
var I: Integer;
begin
  Result:=False;
  #ifdef VexMigrationTest
    for I:=0 to GetArrayLength(VTestEntries)-1 do
      if VTestEntries[I].RootKey=Previous.RootKey then begin
        Current:=VTestEntries[I]; Result:=True;
        if VTestChangeRegistration then Current.AppPath:=Current.AppPath+' changed';
        exit;
      end;
  #else
    Result:=CWMReadRegistration(Previous.RootKey,Previous.ScopeName,Current);
  #endif
end;

function VConfirmMigration(const Locations: String): Boolean;
begin
  #ifdef VexMigrationTest
    Result:=VTestConfirm;
  #else
    Result:=MsgBox(FmtMessage(CustomMessage('VMigrationConfirm'),[Locations]),mbConfirmation,MB_YESNO or MB_DEFBUTTON2)=IDYES;
  #endif
end;

function VRemoveMachineInstallation(const Entry: TCWMRegistration): Boolean;
var Started: Boolean; Code,I,J: Integer;
begin
  Started:=False; Code:=-1;
  #ifdef VexMigrationTest
    VTestLaunches:=VTestLaunches+1;
    Started:=VTestStart; Code:=VTestCode;
    if Started and (Code=0) and not VTestKeepRegistration then
      for I:=GetArrayLength(VTestEntries)-1 downto 0 do
        if CompareText(VTestEntries[I].AppPath,Entry.AppPath)=0 then begin
          for J:=I to GetArrayLength(VTestEntries)-2 do VTestEntries[J]:=VTestEntries[J+1];
          SetArrayLength(VTestEntries,GetArrayLength(VTestEntries)-1);
        end;
    Result:=CWMUninstallSucceeded(Started,Code,VTestKeepRegistration);
  #else
    #ifndef CodeWFSetupFixture
      Started:=ShellExec('runas',Entry.Uninstaller,'',Entry.AppPath,SW_SHOWNORMAL,ewWaitUntilTerminated,Code);
    #endif
    Result:=CWMUninstallSucceeded(Started,Code,CWMRegistrationRemains(Entry.RootKey));
  #endif
end;

function VMigrationRegistrationMatches(const Previous, Current: TCWMRegistration): Boolean;
begin
  Result:=(Previous.RootKey=Current.RootKey) and (Current.Uninstaller<>'') and
    (CompareText(Previous.AppPath,Current.AppPath)=0) and
    (Previous.UninstallCommand=Current.UninstallCommand);
end;

function VBackupMigrationSettings(const Path: String; Index: Integer): Boolean;
var Folder, Name, Source: String; I: Integer;
begin
  Result:=False;
  Folder:=AddBackslash(VMigrationBackup)+IntToStr(Index);
  if not ForceDirectories(Folder) then exit;
  if not SaveStringToFile(Folder+'\source.txt',Path,False) then exit;
  for I:=0 to 1 do begin
    if I=0 then Name:='Vex.exe.config' else Name:='Vex.dll.config';
    Source:=AddBackslash(Path)+Name;
    if FileExists(Source) then
      if not FileCopy(Source,AddBackslash(Folder)+Name,False) then exit;
  end;
  Result:=True;
end;

<event('InitializeWizard')>
procedure VCheckMigration;
var Entries: TVMigrationRegistrations;
begin
  VMigrationNeeded:=VMachineRegistrations(Entries)>0;
end;

procedure VRestoreSettingsTo(const Target: String); forward;

function VRunMigration(const InstallTarget: String; Silent: Boolean): String;
var Entries: TVMigrationRegistrations; Current: TCWMRegistration;
    Count,I,J: Integer; Target, Locations, BackupRoot: String; AlreadyRemoved: Boolean;
begin
  Result:='';
  Count:=VMachineRegistrations(Entries);
  Target:=CWMNormalizePath(InstallTarget);
  if (Target='') or VPathsOverlap(Target,ExpandConstant('{commonpf}')) or
    VPathsOverlap(Target,ExpandConstant('{commonpf32}')) or
    (IsWin64 and VPathsOverlap(Target,ExpandConstant('{commonpf64}'))) or
    VPathsOverlap(Target,ExpandConstant('{win}')) then begin
    Result:=CustomMessage('VMigrationPath'); exit;
  end;
  if Count=0 then exit;
  if Silent then begin Result:=CustomMessage('VMigrationInteractive'); exit; end;
  for I:=0 to Count-1 do begin
    if (Entries[I].Uninstaller='') or not FileExists(Entries[I].Uninstaller) then begin
      Result:=CustomMessage('VMigrationBlocked'); exit;
    end;
    if VPathsOverlap(Target,Entries[I].AppPath) then begin Result:=CustomMessage('VMigrationPath'); exit; end;
    Locations:=Locations+Entries[I].AppPath+#13#10;
  end;
  if not VConfirmMigration(Locations) then begin
    Result:=CustomMessage('VMigrationBlocked'); exit;
  end;
  // 设置备份位于用户数据目录，不交给旧卸载器，也不登记进新安装的卸载日志。
  BackupRoot:=ExpandConstant('{localappdata}\Vex\MigrationBackup');
  #ifdef VexMigrationTest
    BackupRoot:=VTestBackupRoot;
  #endif
  VMigrationBackup:=AddBackslash(BackupRoot)+GetDateTimeString('yyyymmdd-hhnnss-zzz','-','-');
  for I:=0 to Count-1 do
    if not VBackupMigrationSettings(Entries[I].AppPath,I) then begin Result:=CustomMessage('VMigrationBackupFailed'); exit; end;
  for I:=0 to Count-1 do begin
    // 同一路径可能在两个注册视图出现；第一次卸载已移除两份注册时不重复运行。
    if not VReadMigrationRegistration(Entries[I],Current) then begin
      AlreadyRemoved:=False;
      for J:=0 to I-1 do
        if (CompareText(Entries[J].AppPath,Entries[I].AppPath)=0) and
          (Entries[J].UninstallCommand=Entries[I].UninstallCommand) then AlreadyRemoved:=True;
      if not AlreadyRemoved then begin Result:=CustomMessage('VMigrationBlocked'); exit; end;
    end else begin
      if not VMigrationRegistrationMatches(Entries[I],Current) then begin
        Result:=CustomMessage('VMigrationBlocked'); exit;
      end;
      if not VRemoveMachineInstallation(Current) then begin
        Result:=CustomMessage('VMigrationBlocked'); exit;
      end;
    end;
  end;
  if VMachineRegistrations(Entries)<>0 then begin Result:=CustomMessage('VMigrationBlocked'); exit; end;
  // 卸载完成即恢复设置；即使随后取消复制，下次运行安装器仍可复用，避免只剩备份。
  try
    VRestoreSettingsTo(Target);
  except
    Result:=CustomMessage('VMigrationBackupFailed');
  end;
end;

<event('PrepareToInstall')>
function VPrepareMigration(var NeedsRestart: Boolean): String;
begin
  Result:=VRunMigration(ExpandConstant('{app}'),WizardSilent);
end;

procedure VRestoreSettingsTo(const Target: String);
var Name,Source: String; I,J: Integer;
begin
  if VMigrationBackup='' then exit;
  // 已有用户级配置优先，避免旧版覆盖用户当前偏好。两个旧配置名保持原样。
  if FileExists(AddBackslash(Target)+'Vex.exe.config') or FileExists(AddBackslash(Target)+'Vex.dll.config') then exit;
  if not ForceDirectories(Target) then RaiseException(CustomMessage('VMigrationBackupFailed'));
  for I:=0 to 1 do
    for J:=0 to 1 do begin
      if J=0 then Name:='Vex.exe.config' else Name:='Vex.dll.config';
      Source:=AddBackslash(VMigrationBackup)+IntToStr(I)+'\'+Name;
      if FileExists(Source) and not FileExists(AddBackslash(Target)+Name) then
        if not FileCopy(Source,AddBackslash(Target)+Name,True) then RaiseException(CustomMessage('VMigrationBackupFailed'));
    end;
end;

<event('CurStepChanged')>
procedure VRestoreMigrationSettings(Step: TSetupStep);
begin
  if Step=ssInstall then VRestoreSettingsTo(ExpandConstant('{app}'));
end;
