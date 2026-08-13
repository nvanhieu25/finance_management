# Building the Windows Installer

> **Trạng thái 2026-08-14:** bản ZIP xách tay đã dựng và nằm ở
> `dist/ConstructionFinance-1.0.0-win-x64-portable.zip` (67,7 MB). Icon đã có
> (`src/ConstructionFinance.Desktop/Assets/app.ico`, 7 cỡ 16→256px, nhúng sẵn
> trong `ConstructionFinance.exe`). Bước còn thiếu duy nhất là biên dịch
> `inno-setup.iss` thành `Setup.exe` — cần Windows, hoặc Linux có `wine32:i386`
> (máy phát triển hiện **chưa** có wine32).

## Ảnh chụp nhanh: dựng lại bản ZIP xách tay

```bash
# 1. publish self-contained (chạy được trên Linux, offline)
dotnet publish src/ConstructionFinance.Desktop/ \
  --configuration Release --runtime win-x64 --self-contained true \
  -p:PublishSingleFile=false -p:PublishReadyToRun=true \
  -o dist/win-x64

# 2. dựng cây thư mục phát hành: app/ + 3 file trợ giúp ở gốc
#    (xem mục "Bản ZIP xách tay" bên dưới để biết nội dung 3 file)
# 3. nén dist/stage/ConstructionFinance-<ver>-win-x64 thành .zip
```

Cấu trúc bên trong ZIP — cố ý để người dùng mở ra chỉ thấy 3 thứ, không phải
286 file runtime:

```
ConstructionFinance-1.0.0-win-x64/
├── Đọc trước.txt        (UTF-8 BOM: cài đặt, SmartScreen, chỗ lưu dữ liệu)
├── Tạo lối tắt.cmd      (ASCII thuần — không lệ thuộc bảng mã của cmd.exe)
├── tao-loi-tat.ps1      (UTF-8 BOM — chứa phần tiếng Việt, tạo shortcut)
└── app/                 (toàn bộ bản publish, gồm ConstructionFinance.exe)
```

`Tạo lối tắt.cmd` chỉ gọi sang `tao-loi-tat.ps1`; PowerShell tạo shortcut ra
Desktop và Start Menu bằng `WScript.Shell`, trỏ icon vào `ConstructionFinance.exe,0`.
Tách hai file vì `cmd.exe` hay làm hỏng tiếng Việt theo bảng mã, còn PowerShell
đọc UTF-8 BOM chuẩn.

## Icon

`src/ConstructionFinance.Desktop/Assets/app.ico` — nền xanh thương hiệu `#1C4E80`,
ba khối công trình trắng. Sinh bằng Pillow, chứa 16/24/32/48/64/128/256px; các cỡ
nhỏ đã kiểm bằng mắt để không bị bệt. Khai báo ở ba nơi:

- `ConstructionFinance.Desktop.csproj` → `<ApplicationIcon>` (icon của .exe)
- `Views/MainWindow.axaml` → `Icon="/Assets/app-icon.png"` (icon cửa sổ/taskbar lúc chạy)
- `installer/inno-setup.iss` → `SetupIconFile=assets\app.ico` (icon của file cài đặt)

Đổi icon thì thay cả ba, và nhớ `installer/assets/app.ico` là bản sao của file gốc.

## Prerequisites

- Windows 10/11 (or Linux with Wine for `iscc.exe`)
- .NET 8 SDK
- Inno Setup 6.x (https://jrsoftware.org/isinfo.php) — free

## Steps

### 1. Publish self-contained Windows binary

From repo root (Linux or Windows):

```bash
dotnet publish src/ConstructionFinance.Desktop/ \
    --configuration Release \
    --runtime win-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o src/ConstructionFinance.Desktop/bin/Release/net8.0/win-x64/publish
```

Output: `~150 MB` directory with `ConstructionFinance.exe` + dependencies.

### 2. Compile Inno Setup script

**Windows**:
```cmd
cd installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" inno-setup.iss
```

**Linux (Wine)**:
```bash
sudo apt install wine
# Install Inno Setup via Wine first (one-time):
#   wget https://jrsoftware.org/download.php/is.exe
#   wine is.exe /SILENT
cd installer
wine "$HOME/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe" inno-setup.iss
```

Output: `installer/output/ConstructionFinance-Setup-1.0.0.exe`

### 3. Test the installer

1. Copy `setup.exe` to a clean Windows VM
2. Run as a normal user (no admin)
3. Verify install goes to `%LocalAppData%\Programs\ConstructionFinance\`
4. Verify Start Menu shortcut + (optional) Desktop shortcut work
5. Verify `.cfb` file association: double-click any `.cfb` should open the app

## Alternative: Portable ZIP (no Inno Setup needed)

If you only need a copy-and-run build for another machine (no Start Menu
shortcut / `.cfb` association), skip Inno Setup entirely:

1. Run step 1 above (`dotnet publish … win-x64 self-contained`).
2. Zip the `publish` folder — the target user unzips it and runs
   `ConstructionFinance.exe`. No .NET install required (self-contained).

This is the fallback when building on Linux without `wine32:i386`
(Inno Setup's `ISCC.exe` is 32-bit and needs the WoW64 runtime).
The repo's `dist/` folder is the conventional output location (gitignored).

### 4. Version bump

For new versions:
1. Update `MyAppVersion` in `inno-setup.iss`
2. Update `<Version>` in `src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj` (`<Version>1.0.0</Version>` — add if missing)
3. Rebuild
