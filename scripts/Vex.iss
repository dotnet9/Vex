; Vex Windows installer.
; Build from the repository root with Inno Setup 6 and pass /DAppVersion=x.y.z.

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-x64\Vex"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{8F8111FF-79BD-4B8A-91F3-C23FAF6906EE}
AppName=Vex
AppVersion={#AppVersion}
AppPublisher=Dotnet9
AppPublisherURL=https://github.com/dotnet9/Vex
AppSupportURL=https://github.com/dotnet9/Vex/issues
DefaultDirName={autopf}\Vex
DefaultGroupName=Vex
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Vex-v{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; per-user 安装：应用以普通权限运行，可持久化自身设置（配置文件在安装目录内）。
PrivilegesRequired=lowest
ChangesAssociations=no
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=Vex.exe
UninstallDisplayIcon={app}\Vex.exe
WizardStyle=modern

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Vex"; Filename: "{app}\Vex.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Vex"; Filename: "{app}\Vex.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Vex.exe"; Description: "Launch Vex"; Flags: nowait postinstall skipifsilent
