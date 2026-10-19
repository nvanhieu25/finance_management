#!/usr/bin/env bash
# Dựng file cài đặt dist/ConstructionFinance-Setup-<phiên bản>.exe (Inno Setup chạy trong Docker, không cần Windows).
#   tools/build-setup.sh
# Cần: docker (engine chạy được: `docker --context default version`), và image amake/innosetup (tự kéo lần đầu).
set -euo pipefail
cd "$(dirname "$0")/.."

tools/build-windows-exe.sh                 # bản nén độc lập: dist/ConstructionFinance.exe
COMPRESS=0 tools/build-windows-exe.sh      # bản không nén cho bộ cài

ctx="${DOCKER_CONTEXT:-default}"
docker --context "$ctx" run --rm -v "$PWD:/work" amake/innosetup "/DSourceExe=..\\dist\\win-x64-khong-nen\\ConstructionFinance.exe" installer/inno-setup.iss
ls -lh dist/ConstructionFinance-Setup-*.exe
