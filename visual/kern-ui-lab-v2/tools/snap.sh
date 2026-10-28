#!/bin/sh
# tools/snap.sh <имя> <страница от visual/> <ШxВ> "<вызовы>"
# Сервер: python3 -m http.server 8765 из каталога games/.
name=$1; page=$2; size=$3; calls=$4
enc=$(python3 -I -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1]))' "$calls")
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars \
  --force-device-scale-factor=1 --window-size="${size%x*},${size#*x}" --virtual-time-budget=${BUDGET:-6000} \
  --screenshot="$(dirname "$0")/shots/$name.png" \
  "http://127.0.0.1:8765/Fodislop/visual/kern-ui-lab-v2/tools/snap.html?page=../../$page&do=$enc" >/dev/null 2>&1
echo "$name"
