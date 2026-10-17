; Vex Windows 安装包。
; 从仓库根目录使用 Inno Setup 6 编译，并传入 /DAppVersion=x.y.z。

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
LicenseFile=..\LICENSE
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
; 按用户安装：应用以普通权限运行，可持久化自身设置（配置文件在安装目录内）。
PrivilegesRequired=lowest
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=Vex.exe
UninstallDisplayIcon={app}\Vex.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
english.OpenWithVex=Open with Vex
chinesesimplified.OpenWithVex=用 Vex 打开
english.MarkdownAssociation=Register Vex as an opening option for Markdown files
chinesesimplified.MarkdownAssociation=注册 Vex 为 Markdown 文件的打开方式候选
english.FileAssociations=File associations:
chinesesimplified.FileAssociations=文件关联：

[Registry]
; 注册候选、图标与右键入口，默认打开程序由用户在系统设置中选择。
Root: HKCU; Subkey: "Software\Classes\Vex.Markdown"; ValueType: string; ValueName: ""; ValueData: "Markdown"; Flags: uninsdeletekey; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\Vex.Markdown\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Vex.exe"",0"; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\Vex.Markdown\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Vex.exe"" ""%1"""; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\Applications\Vex.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Vex"; Flags: uninsdeletekey; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\Applications\Vex.exe\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\Vex.exe"",0"; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\Applications\Vex.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Vex.exe"" ""%1"""; Tasks: markdownassociation
#sub RegisterMarkdownExtension
Root: HKCU; Subkey: "Software\Classes\Applications\Vex.exe\SupportedTypes"; ValueType: string; ValueName: "{#MarkdownExtension}"; ValueData: ""; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\{#MarkdownExtension}\OpenWithProgids"; ValueType: none; ValueName: "Vex.Markdown"; Flags: uninsdeletevalue; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\{#MarkdownExtension}\shell\OpenWithVex"; ValueType: string; ValueName: ""; ValueData: "{cm:OpenWithVex}"; Flags: uninsdeletekey; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\{#MarkdownExtension}\shell\OpenWithVex"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\Vex.exe"",0"; Tasks: markdownassociation
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\{#MarkdownExtension}\shell\OpenWithVex\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Vex.exe"" ""%1"""; Tasks: markdownassociation
#endsub
#define MarkdownExtension ".md"
#emit RegisterMarkdownExtension
#define MarkdownExtension ".markdown"
#emit RegisterMarkdownExtension
#define MarkdownExtension ".mdown"
#emit RegisterMarkdownExtension

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "markdownassociation"; Description: "{cm:MarkdownAssociation}"; GroupDescription: "{cm:FileAssociations}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Vex.dll.config,Vex.exe.config"; Flags: ignoreversion recursesubdirs createallsubdirs
; 设置不随升级覆盖，也不随卸载删除；迁移配置在复制载荷前恢复。
Source: "{#SourceDir}\Vex.dll.config"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\Vex"; Filename: "{app}\Vex.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Vex"; Filename: "{app}\Vex.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Vex.exe"; Description: "{cm:LaunchProgram,Vex}"; Flags: nowait postinstall skipifsilent

#include "installer\Vex.Setup.iss"
