// Независимая сверка: ?overlay=1 рисует поверх макета прямоугольники Legacy,
// посчитанные решателем UGUI из цепочек якорей m1client.unity
// (data/legacy-rects.json). CSS макета здесь не участвует.
(async () => {
  const params = new URLSearchParams(location.search);
  if (!params.has('overlay')) return;
  const chains = await (await fetch('data/legacy-rects.json')).json();
  const H = 654.5;
  // Группы: hud — всегда видимые блоки, chat — колонка чата, window — окно навыков
  const GROUPS = {
    chat: ['NewChat', 'ChatField', 'LineTxt', 'CurrentChatButton', 'ChatsButton', 'SettingsButton', 'ChatTitle'],
    window: ['GUIWindow', 'UpView', 'UpRobo', 'SkillPanel', 'TitleText'],
  };
  // LeftGUI в режиме открытого чата сдвигается на 213 (ChatManager.UpdateChatMode)
  const leftShift = document.body.dataset.chat === 'open' ? 213 : 0;

  const solve = (chain, W) => {
    let r = { x0: 0, y0: 0, x1: W, y1: H };
    for (const n of chain) {
      const pw = r.x1 - r.x0, ph = r.y1 - r.y0;
      const ox = n.pos.x - n.pivot.x * n.size.x, oy = n.pos.y - n.pivot.y * n.size.y;
      const x0 = r.x0 + n.amin.x * pw + ox, y0 = r.y0 + n.amin.y * ph + oy;
      const x1 = r.x0 + n.amax.x * pw + ox + n.size.x, y1 = r.y0 + n.amax.y * ph + oy + n.size.y;
      r = { x0, y0, x1, y1 };
      if (n.name === 'LeftGUI') { r.x0 += leftShift; r.x1 += leftShift; }
    }
    return r;
  };

  const group = params.get('overlay');
  const inGroup = (path, name) => group === 'hud'
    ? !Object.values(GROUPS).flat().includes(name) || path.includes('ButtonPanel')
    : GROUPS[group] ? GROUPS[group].includes(name) && !path.includes('ButtonPanel') : true;
  const layer = document.createElement('div');
  layer.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:9999';
  document.body.appendChild(layer);
  const render = () => {
    const vw = document.documentElement.clientWidth, vh = document.documentElement.clientHeight;
    const W = H * vw / vh;   // CanvasScaler: match = 1 (по высоте)
    const unit = vh / H;
    layer.replaceChildren();
    for (const [path, chain] of Object.entries(chains)) {
      const name = path.split('/').pop();
      if (!inGroup(path, name)) continue;
      const r = solve(chain, W);
      const w = (r.x1 - r.x0) * unit, h = (r.y1 - r.y0) * unit;
      const box = document.createElement('div');
      // Узлы с размером 0 подгоняет LayoutGroup — показываем точку привязки.
      const dot = w < 1 || h < 1;
      box.style.cssText = `position:absolute;left:${r.x0 * unit}px;top:${(H - r.y1) * unit}px;` +
        `width:${Math.max(w, 6)}px;height:${Math.max(h, 6)}px;` +
        `outline:1px ${dot ? 'dotted' : 'solid'} #ff2bd6;font:10px monospace;color:#ff2bd6;`;
      box.textContent = name;
      layer.appendChild(box);
    }
  };
  render();
  addEventListener('resize', render);
})();
