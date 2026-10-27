// События игры поверх HUD: связь, сообщение и ссылка от сервера;
// стрелки у края экрана (миссия, своя метка), подписи над роботами; на стенде — псевдоязык и места текста сервера.
(() => {
  const $ = id => document.getElementById(id);
  const frame = $('frame'), W = window.kernWorld;

  // ── Связь — как в клиенте (ConnectionManager, ReconnectBackoff):
  //   автоповтор без предела попыток; пауза перед попыткой 1, 2, 4, 8, 16, дальше 30 с;
  //   ожидание — «Попробуем ещё раз через Nс...» (network.reconnect.retry), попытка — «Подключение...» (network.connecting);
  //   DisconnectPacket — причина от сервера, без автоповтора; обрыв вне попытки — network.error.connection_lost, без автоповтора
  const STEPS = [1, 2, 4, 8, 16, 30];
  const ATTEMPT_MS = 1500;                 // макет: сколько длится неудачная попытка
  let netTimer = 0;
  const stopNet = () => { clearInterval(netTimer); clearTimeout(netTimer); netTimer = 0; };
  function showNet(kind) {
    stopNet();
    $('netDialog').hidden = false;
    $('netReason').hidden = true;
    if (kind === 'reconnect') {
      $('netTitle').textContent = 'Связь потеряна';
      let attempt = 0, left = STEPS[0];
      const wait = () => {
        $('netText').textContent = `Попробуем ещё раз через ${left}с...`;
        netTimer = setInterval(() => {
          if (--left > 0) { $('netText').textContent = `Попробуем ещё раз через ${left}с...`; return; }
          stopNet();
          $('netText').textContent = 'Подключение...';
          // Попытка не удалась — пауза растёт по ступеням
          netTimer = setTimeout(() => { attempt++; left = STEPS[Math.min(attempt, STEPS.length - 1)]; wait(); }, ATTEMPT_MS);
        }, 1000);
      };
      wait();
      return;
    }
    $('netTitle').textContent = kind === 'kicked' ? 'Отключено сервером' : 'Связь потеряна';
    $('netText').textContent = kind === 'kicked' ? 'Сервер закрыл соединение.' : 'Соединение с сервером потеряно.';
    if (kind === 'kicked') { $('netReason').hidden = false; $('netReason').textContent = 'Причина: Перезапуск для обновления, вернёмся через 5 минут.'; }
  }
  $('netMenu').onclick = () => { stopNet(); $('netDialog').hidden = true; document.querySelector('.stand-seg [data-screen="menu"]').click(); };

  // ── Остальные окна событий ──
  const close = id => { $(id).hidden = true; };
  ['srvModal', 'linkDialog'].forEach(id => $(id).querySelectorAll('[data-close]').forEach(b => b.onclick = () => close(id)));
  $('linkOpen').onclick = () => close('linkDialog');            // в игре — открыть браузер; макет только закрывает
  document.addEventListener('keydown', e => { if (e.key === 'Escape') ['srvModal', 'linkDialog'].forEach(close); });

  const EVENTS = {
    reconnect: () => showNet('reconnect'), lost: () => showNet('lost'), kicked: () => showNet('kicked'),
    modal: () => { $('srvModal').hidden = false; },
    link: () => { $('linkDialog').hidden = false; },
  };
  function setEvent(kind) {
    stopNet(); ['netDialog', 'srvModal', 'linkDialog'].forEach(close);
    $('standEvent').value = kind;
    if (EVENTS[kind]) EVENTS[kind]();
  }
  $('standEvent').onchange = e => setEvent(e.target.value);

  // ── Стрелки у края экрана: цель миссии (MissionArrowPacket X, Y) и своя метка ──
  // Пока цель в кадре — значок над её клеткой; за кадром — стрелка у края в её сторону и расстояние в клетках
  const MISSION = { kind: 'mission', x: W.PX + 58, y: W.PY - 22, color: '#e4e8ea', name: 'Миссия', off: () => $('mission').hidden };
  function arrows() {
    const v = W.view; if (!v) return;
    const fw = frame.offsetWidth, fh = frame.offsetHeight, cs = getComputedStyle(frame);
    const list = W.marks.filter(m => !m.off?.());
    // Стрелка стоит на самом краю экрана (за безопасной зоной), подпись — внутрь; слой стрелок лежит в HUD, он сдвинут на безопасную зону
    const px = v => parseFloat(cs.getPropertyValue(v)) || 0;
    const left = px('--sl') + 22, right = fw - px('--sr') - 22, top = px('--st') + 22, bottom = fh - px('--sb') - 22;
    // Занятое HUD: панели и кнопки в координатах кадра
    const fr = frame.getBoundingClientRect(), sc = fr.width / fw;
    const busy = [...document.querySelectorAll('#hud .panel, #hud .sysbar, #hud .inventory, #hud .mini-skills, #hud .touch-sys, #hud .dock button, #hud .pad, #hud .low-row > *, #hud .status-bar, #hud .chat-peek, #hud .toggle, #hud .pad-cluster, #hud .item-hint')]
      .filter(el => el.offsetParent && !el.closest('[hidden]')).map(el => { const r = el.getBoundingClientRect(); return [(r.left - fr.left) / sc, (r.top - fr.top) / sc, (r.right - fr.left) / sc, (r.bottom - fr.top) / sc]; })
      .filter(([l, t, r, b]) => r > l && b > t);
    // Точки по периметру поля с шагом 8 px; первая свободная — ближайшая к идеальной
    function free(ix, iy, lw, taken) {
      const pts = [];
      for (let x = left; x <= right; x += 8) pts.push([x, top, 't'], [x, bottom, 'b']);
      for (let y = top; y <= bottom; y += 8) pts.push([left, y, 'l'], [right, y, 'r']);
      pts.sort((p, q) => Math.hypot(p[0] - ix, p[1] - iy) - Math.hypot(q[0] - ix, q[1] - iy));
      const boxOf = ([x, y, side]) => side === 'r' ? [x - 15 - lw, y - 15, x + 15, y + 15] : side === 'l' ? [x - 15, y - 15, x + 15 + lw, y + 15]
        : side === 't' ? [Math.min(x - 15, x - lw / 2), y - 15, Math.max(x + 15, x + lw / 2), y + 35] : [Math.min(x - 15, x - lw / 2), y - 35, Math.max(x + 15, x + lw / 2), y + 15];
      const hit = b => taken.some(([l, t, r, bb]) => b[0] < r && b[2] > l && b[1] < bb && b[3] > t);
      const p = pts.find(q => !hit(boxOf(q))) || pts[0];
      return { x: p[0], y: p[1], side: p[2], box: boxOf(p) };
    }
    $('edgeArrows').innerHTML = list.map(m => {
      const tx = v.offX + (m.x - v.ox + .5) * W.CELL, ty = v.offY + (m.y - v.oy + .5) * W.CELL;
      const dist = Math.round(Math.hypot(m.x - W.PX, m.y - W.PY));
      if (tx > left && tx < right && ty > top && ty < bottom) return `<div class="edge-pin" style="left:${tx - px('--sl')}px;top:${ty - W.CELL / 2 - px('--st')}px;--c:${m.color}"><span>${m.name}</span></div>`;
      // Луч от центра кадра к цели обрезается краем; затем стрелка сдвигается вдоль края к ближайшему месту без панелей HUD
      const cx = fw / 2, cy = fh / 2, dx = tx - cx, dy = ty - cy;
      const k = Math.min(dx > 0 ? (right - cx) / dx : dx < 0 ? (left - cx) / dx : Infinity, dy > 0 ? (bottom - cy) / dy : dy < 0 ? (top - cy) / dy : Infinity);
      const label = `${m.name} · ${dist}`, lw = label.length * 6.5 + 14;
      const at = free(cx + dx * k, cy + dy * k, lw, busy);
      busy.push(at.box);
      const ang = Math.atan2(ty - at.y, tx - at.x) * 180 / Math.PI;
      return `<div class="edge-arrow edge-arrow--${at.side}" style="left:${at.x - px('--sl')}px;top:${at.y - px('--st')}px;--c:${m.color};--a:${ang}deg"><i></i><span>${label}</span></div>`;
    }).join('');
  }

  // ── Подписи над роботами (WorldLabels): ник — от правого верхнего угла клетки, облако — хвостом к роботу ──
  const bubbles = new Map([['ШАХТЁР-102', 'тут голубые, иди сюда']]);
  const esc = s => s.replace(/[&<>]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[ch]));
  function labels() {
    const v = W.view; if (!v) return;
    const me = { nick: 'ШАХТЁР-774', clan: true, x: W.PX, y: W.PY };
    $('worldLabels').innerHTML = [...W.bots, me].map(b => {
      const x = v.offX + (b.x - v.ox) * W.CELL, y = v.offY + (b.y - v.oy) * W.CELL, say = bubbles.get(b.nick);
      return `<div class="wl-name" style="left:${x + W.CELL}px;top:${y}px">${b.clan ? '<img src="img/clan.png" alt="">' : ''}${esc(b.nick)}</div>`
        + (say ? `<div class="wl-chat" style="left:${x + W.CELL / 2}px;top:${y}px"><span>${esc(say)}</span></div>` : '');
    }).join('');
  }
  // Локальный чат: своя реплика встаёт облаком над своим роботом
  $('sayForm').addEventListener('submit', () => { const t = $('sayInput').value.trim(); if (t) { bubbles.set('ШАХТЁР-774', t); labels(); } }, true);
  W.onDraw = () => { labels(); arrows(); };
  new MutationObserver(() => W.draw()).observe($('mission'), { attributes: true, attributeFilter: ['hidden'] });

  // ── Стенд: текст под нагрузкой. Рост строки — по длине оригинала (замер en/ru из kern-ui-lab, 849 ключей) ──
  const BUCKETS = [[5, 2.2, 4.5], [10, 1.67, 3.11], [20, 1.47, 2.79], [40, 1.28, 1.64], [Infinity, 1.16, 1.38]];
  const FILLER = ['ша', 'ру', 'де', 'ло', 'ти', 'ва', 'но', 'ре', 'ки', 'ма'];
  const grow = (src, mode) => {
    const b = BUCKETS.find(x => src.trim().length <= x[0]), target = Math.round(src.trim().length * (mode === 'worst' ? b[2] : b[1]));
    let out = src.trim(), i = 0;
    while (out.length < target) out += (i++ === 0 ? ' ' : '') + FILLER[(i - 1) % FILLER.length];
    // Скобки — граница строки: виден «[» без «]» — хвост срезан
    return src.match(/^\s*/)[0] + '[' + out + ']' + src.match(/\s*$/)[0];
  };
  // Не переводятся: числа, координаты, ники и метки каналов (заглавные без пробелов), клавиши, слова игроков и имена программ
  const skip = (node, t) => !/[A-Za-zА-Яа-яЁё]{2}/.test(t) || /^\s*[A-ZА-ЯЁ0-9_\-.#$%:/]+\s*$/.test(t)
    || node.parentElement?.closest('kbd, .pg, script, style, [translate="no"], .stand-bar, .prog-cell, .nick-link, .nick, .wl-name, #chatPeek3, .chat-log li:not(.sys), .wl-chat, #progName, #progPickName');
  let mode = 'ru';
  const orig = new Map(), mine = new WeakMap();
  function apply(node) {
    if (mine.get(node) === node.data) return;                   // своя замена — не трогать
    const src = node.data;
    if (skip(node, src)) return;
    orig.set(node, src);
    node.data = grow(src, mode); mine.set(node, node.data);
  }
  const walk = root => { const it = document.createTreeWalker(root, NodeFilter.SHOW_TEXT); let n; while ((n = it.nextNode())) apply(n); };
  const obs = new MutationObserver(list => {
    if (mode !== 'p90' && mode !== 'worst') return;
    for (const m of list) {
      if (m.type === 'characterData') apply(m.target);
      else m.addedNodes.forEach(n => n.nodeType === 3 ? apply(n) : n.nodeType === 1 && walk(n));
    }
  });
  function setText(m) {
    if (m !== 'ru') window.kernLang?.set('ru');                 // проверка — по русскому тексту
    orig.forEach((src, node) => { if (node.isConnected && mine.get(node) === node.data) node.data = src; });
    orig.clear();
    mode = m;
    frame.classList.toggle('srv-slots', m === 'slots');
    $('standText').value = m;
    if (m === 'p90' || m === 'worst') { walk(frame); obs.observe(frame, { subtree: true, childList: true, characterData: true }); }
    else obs.disconnect();
  }
  $('standText').onchange = e => setText(e.target.value);
  window.kernText = { set: setText };

  const q = new URLSearchParams(location.search);
  W.ready.then(() => {
    W.marks.push(MISSION);
    W.draw();
    // Снимок: окна без плавного появления (в безголовом браузере кадры анимации не идут)
    if (q.get('event')) { ['netDialog', 'srvModal', 'linkDialog'].forEach(id => { $(id).style.transition = 'none'; }); setTimeout(() => setEvent(q.get('event'))); }
    if (q.get('text')) setTimeout(() => setText(q.get('text')));
  });
})();
