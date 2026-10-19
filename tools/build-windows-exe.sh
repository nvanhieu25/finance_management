#!/usr/bin/env bash
# Dựng MỘT file ConstructionFinance.exe cho Windows (tự chứa .NET, không cần cài gì, bấm đúp là chạy).
# Chạy được trên Linux, offline nếu NuGet cache đã có runtime pack win-x64.
#   tools/build-windows-exe.sh            → dist/ConstructionFinance.exe
set -euo pipefail
cd "$(dirname "$0")/.."

out="dist/win-x64-mot-file"
rm -rf "$out"

# -f net8.0-windows: đích phát hành (có adapter máy in GDI); đích net8.0 chỉ dành cho bộ test.
# Có nén (EnableCompressionInSingleFile): 294 MB → ~119 MB, đổi lại mở app chậm thêm ~1-2 giây.
dotnet publish src/ConstructionFinance.Desktop \
  -c Release -f net8.0-windows -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false \
  -o "$out"

cp "$out/ConstructionFinance.exe" dist/ConstructionFinance.exe

# Kiểm bắt buộc: tính năng in trực tiếp phải nằm trong .exe (xem bài học NET8_0_WINDOWS ở spec in thẳng).
if ! grep -q "WindowsPrinterPort" dist/ConstructionFinance.exe; then
  echo "LỖI: ConstructionFinance.exe không chứa WindowsPrinterPort — bản này mất tính năng in." >&2
  exit 1
fi
ls -lh dist/ConstructionFinance.exe
