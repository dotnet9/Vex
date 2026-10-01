; Vex Windows installer.
; Build from the repository root with Inno Setup 6 and pass /DAppVersion=x.y.z.
; /DAppArch selects the target architecture: x64 (default) or x86.

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef AppArch
#define AppArch "x64"
#endif

#if AppArch != "x64" && AppArch != "x86"
#error AppArch must be x64 or x86
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-" + AppArch + "\Vex"
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
OutputBaseFilename=Vex-v{#AppVersion}-win-{#AppArch}-setup
Compression=lzma2/ultra64
SolidCompression=yes
#if AppArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
PrivilegesRequired=admin
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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Vex"; Filename: "{app}\Vex.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Vex"; Filename: "{app}\Vex.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Vex.exe"; Description: "Launch Vex"; Flags: nowait postinstall skipifsilent
