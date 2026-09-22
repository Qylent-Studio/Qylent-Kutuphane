#define MyAppName "Qylent Kütüphane"
#define MyAppVersion "1.2.3"
#define MyAppPublisher "Qylent Studio"
#define MyAppExeName "Qylent.Kutuphane.exe"

[Setup]
AppId={{ACF32232-B76D-4DAE-BB1B-7E2D2D45C2C8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Qylent Studio\Qylent Kütüphane
DefaultGroupName={#MyAppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=Qylent-Kutuphane-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayName={#MyAppName}
UsePreviousAppDir=yes
UsePreviousGroup=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek kısayollar:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} uygulamasını aç"; Flags: nowait postinstall skipifsilent
