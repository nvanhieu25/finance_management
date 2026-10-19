; Inno Setup script for Construction Finance Management
; Dựng bằng tools/build-setup.sh (Docker + Inno Setup) — cần dist\ConstructionFinance.exe dựng sẵn bởi tools/build-windows-exe.sh.

#define MyAppName "Quản lý Tài chính Công trình"
#define MyAppNameAscii "ConstructionFinance"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Sở Xây dựng"
#define MyAppExeName "ConstructionFinance.exe"
; File .exe nguồn: build-setup.sh truyền bản KHÔNG nén (/DSourceExe=...) để app cài xong mở nhanh; mặc định là bản nén.
#ifndef SourceExe
  #define SourceExe "..\dist\ConstructionFinance.exe"
#endif

[Setup]
AppId={{B7A9F4F1-2C8E-4D6E-9F2A-1B3C4D5E6F7A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameAscii}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
;LicenseFile=assets\license.txt  ; bỏ trang giấy phép: app nội bộ, người dùng chỉ cần bấm Tiếp tục
; Cùng một icon với ConstructionFinance.exe (nguồn: src/.../Assets/app.ico)
SetupIconFile=assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename=ConstructionFinance-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"

[Files]
; MỘT file .exe tự chứa .NET (xem tools/build-windows-exe.sh). Không còn thư mục 286 tệp như bản ZIP cũ.
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
; Mặc định TÍCH SẴN: người dùng cuối mong thấy icon ở màn hình nền sau khi cài.
Name: "desktopicon"; Description: "Tạo biểu tượng ngoài màn hình nền (Desktop)"; GroupDescription: "Tùy chọn:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Mở {#MyAppName} ngay"; Flags: nowait postinstall skipifsilent
