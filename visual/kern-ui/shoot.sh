#!/bin/sh
# Снимок стенда: ./shoot.sh <имя> <ШxВ окна> "<параметры адреса>"
# Сервер: python3 -m http.server 8765 из каталога games/.
mkdir -p "$(dirname "$0")/shots"
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars \
  --force-device-scale-factor=1 --window-size="${2%x*},${2#*x}" --virtual-time-budget=4000 \
  --screenshot="$(dirname "$0")/shots/$1.png" "http://127.0.0.1:8765/Fodislop/visual/kern-ui/index.html?$3" >/dev/null 2>&1
echo "$1"
