#!/usr/bin/env bash
# Dựng MỘT file ConstructionFinance.exe cho Windows (tự chứa .NET, không cần cài gì, bấm đúp là chạy).
# Chạy được trên Linux, offline nếu NuGet cache đã có runtime pack win-x64.
#   tools/build-windows-exe.sh            → dist/ConstructionFinance.exe
set -euo pipefail
cd "$(dirname "$0")/.."

# COMPRESS=0: bản không nén (294 MB) dành cho bộ cài — Inno tự nén lại, còn app cài xong mở nhanh hơn.
compress="${COMPRESS:-1}"
if [ "$compress" = "1" ]; then out="dist/win-x64-mot-file"; else out="dist/win-x64-khong-nen"; fi
rm -rf "$out"

# -f net8.0-windows: đích phát hành (có adapter máy in GDI); đích net8.0 chỉ dành cho bộ test.
# Có nén (EnableCompressionInSingleFile): 294 MB → ~119 MB, đổi lại mở app chậm thêm ~1-2 giây.
dotnet publish src/ConstructionFinance.Desktop \
  -c Release -f net8.0-windows -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=$([ "$compress" = "1" ] && echo true || echo false) \
  -p:DebugType=none -p:DebugSymbols=false \
  -o "$out"

if [ "$compress" = "1" ]; then target=dist/ConstructionFinance.exe; else target="$out/ConstructionFinance.exe"; fi
[ "$target" = "$out/ConstructionFinance.exe" ] || cp "$out/ConstructionFinance.exe" "$target"

# Kiểm bắt buộc: tính năng in trực tiếp phải nằm trong bản dựng (xem bài học NET8_0_WINDOWS ở spec in thẳng).
# Không grep thẳng .exe được vì bản nén giấu chữ; kiểm ConstructionFinance.dll (IL chưa gói) và dll adapter máy in.
bin=src/ConstructionFinance.Desktop/bin/Release/net8.0-windows/win-x64
if ! grep -q "WindowsPrinterPort" "$bin/ConstructionFinance.dll" || [ ! -f "$bin/ConstructionFinance.Printing.Windows.dll" ]; then
  echo "LỖI: bản dựng không chứa WindowsPrinterPort / adapter máy in — bản này mất tính năng in." >&2
  exit 1
fi
ls -lh "$target"
