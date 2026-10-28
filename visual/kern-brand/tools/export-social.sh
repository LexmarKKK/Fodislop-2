#!/bin/sh
# Экспорт шаблонов соцсетей в PNG: ./tools/export-social.sh
# Сервер: python3 -m http.server 8765 из каталога games/. Кадр интерфейса для бренд-книги снимается со стенда макета.
cd "$(dirname "$0")/.." || exit 1
C="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
U=http://127.0.0.1:8765/Fodislop/visual/kern-brand/social.html
for f in avatar:1024x1024 x-header:1500x500 youtube:2560x1440 vk:1920x768 discord:960x540 telegram:640x640 post:1080x1080 story:1080x1920; do
  name=${f%%:*}; size=${f#*:}; key=$name; [ "$name" = x-header ] && key=x
  "$C" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size="${size%x*},${size#*x}" \
    --virtual-time-budget=4000 --screenshot="social/$name.png" "$U?f=$key" >/dev/null 2>&1
  echo "$name $size"
done
# Кадр HUD: стенд 1920×1200, кадр 1080 по центру сцены под полосой стенда — вырезается по рамке кадра
"$C" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=1920,1200 --virtual-time-budget=8000 \
  --screenshot=social/ui-full.png "http://127.0.0.1:8765/Fodislop/visual/kern-ui/index.html?state=busy&mark=1" >/dev/null 2>&1
echo ui-full
sips -c 1080 1920 --cropOffset 100 0 social/ui-full.png --out social/ui-hud.png >/dev/null && rm social/ui-full.png
echo ui-hud
