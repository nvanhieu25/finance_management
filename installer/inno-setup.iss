; Inno Setup script for Construction Finance Management
; Compile with: iscc.exe inno-setup.iss (Windows or Wine)

#define MyAppName "Quản lý Tài chính Công trình"
#define MyAppNameAscii "ConstructionFinance"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Sở Xây dựng"
#define MyAppExeName "ConstructionFinance.exe"

[Setup]
AppId={{B7A9F4F1-2C8E-4D6E-9F2A-1B3C4D5E6F7A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameAscii}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=assets\license.txt
OutputDir=output
OutputBaseFilename=ConstructionFinance-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"

[Files]
; Self-contained publish output (built before running this script)
Source: "..\src\ConstructionFinance.Desktop\bin\Release\net8.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut trên Desktop"; GroupDescription: "Tùy chọn:"; Flags: unchecked

[Registry]
; Associate .cfb files with this app
Root: HKCU; Subkey: "Software\Classes\.cfb"; ValueType: string; ValueData: "ConstructionFinance.Backup"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ConstructionFinance.Backup"; ValueType: string; ValueData: "{#MyAppName} Backup"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ConstructionFinance.Backup\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ConstructionFinance.Backup\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Khởi chạy {#MyAppName}"; Flags: nowait postinstall skipifsilent
