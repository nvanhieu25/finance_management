# Building the Windows Installer

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
