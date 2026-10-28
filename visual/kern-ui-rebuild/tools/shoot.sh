#!/bin/sh
# Снимки макета: tools/shoot.sh <страница> <состояние> <ШxВ> [overlay]
# Сервер: python3 -m http.server 8765 из каталога games/ (рядом Fodislop и OpenMines).
page=$1; state=$2; size=$3; ov=$4
q="state=$state"; [ -n "$DEVICE" ] && q="$q&device=$DEVICE"; [ -n "$ov" ] && q="$q&overlay=$ov"
out="$(dirname "$0")/../shots/${page%.html}-${DEVICE:+$DEVICE-}${state}-${size}${ov:+-overlay-$ov}.png"
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars \
  --force-device-scale-factor=1 --window-size="${size%x*},${size#*x}" --virtual-time-budget=3000 \
  --screenshot="$out" "http://127.0.0.1:8765/Fodislop/visual/kern-ui-rebuild/$page?$q" >/dev/null 2>&1
echo "$out"
