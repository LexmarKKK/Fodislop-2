// HUD MainGame: данные, форматы и поведение.
// Форматы — Legacy GUIManager (ур.N, NHP, $N, Груз N%, пороги груза, сокращения
// кристаллов), чат — Legacy ChatManager (3 режима, каналы FED/DNO сервера),
// мини-навыки — Legacy MiniSkill (видны 5 с при росте, готовые остаются).
(() => {
  const $ = id => document.getElementById(id);
  const group = n => String(Math.trunc(n)).replace(/\B(?=(\d{3})+(?!\d))/g, ' ');

  const state = {
    hp: 113, maxHp: 136, money: 14850, creds: 340,
    basket: { g: 412, r: 96, b: 1204, v: 0, w: 0, c: 57 }, capacity: 1800,
    chat: 0, channel: 'FED', autodig: false, prog: false,
  };
  const CRYSTALS = [['g', 'green'], ['r', 'red'], ['b', 'blue'], ['v', 'violet'], ['w', 'white'], ['c', 'cyan']];

  // SetBasketCrys: до миллиона — с пробелами, дальше K и KK
  const crys = n => n < 1e6 ? group(n) : n < 1e10 ? group(n / 1e3) + ' K' : group(n / 1e6) + ' KK';
  // SetBasket: цвет и полосы по порогам 50 / 100 / 115 / 200
  const cargoTone = p => p < 50 ? ['#9aa3a9', '#7e878d', null] : p < 100 ? ['#79d98a', '#5cc46f', null]
    : p < 115 ? ['#ffe066', '#e8c845', '#b3801f'] : p < 200 ? ['#ff9d5c', '#f59048', '#b3002a'] : ['#ff6b5e', '#e65959', '#b3002a'];
  // SetHP: цвет числа и полосы по доле
  const hpTone = f => `rgb(${Math.round(Math.sqrt(Math.sqrt(1 - f)) * 255)},${Math.round(Math.sqrt(Math.sqrt(f)) * 255)},${Math.round(f * (1 - f) * 255)})`;

  function renderPlayer() {
    const f = Math.max(0, Math.min(1, state.hp / state.maxHp));
    $('hpFill').style.width = f * 100 + '%';
    $('hpFill').style.background = hpTone(f);
    $('hpValue').textContent = state.hp + 'HP';
    $('hpValue').style.color = hpTone(f);
    $('money').textContent = '$' + group(state.money);
    $('creds').textContent = group(state.creds);
  }
  function renderCargo() {
    const values = CRYSTALS.map(([k]) => state.basket[k]);
    const percent = Math.round(Math.max(...values) * 100 / state.capacity);
    const [text, bar, over] = cargoTone(percent);
    $('cargoPercent').textContent = percent + '%';
    $('cargoPercent').style.color = text;
    $('cargoFill').style.width = Math.min(100, percent) + '%';
    $('cargoFill').style.background = bar;
    $('cargoOver').style.width = over ? Math.min(100, percent - 100) + '%' : '0';
    $('cargoOver').style.background = over || 'transparent';
    // Между иконкой и числом — заполнение по этому кристаллу (лимит груза у каждого типа свой)
    $('crystals').innerHTML = CRYSTALS.filter(([k]) => state.basket[k] > 0)
      .map(([k, f]) => `<li style="color:var(--cry-${k})"><img src="img/crystals/${f}.png" alt=""><i class="share" style="--w:${Math.min(100, state.basket[k] / state.capacity * 100).toFixed(1)}%"></i><span>${crys(state.basket[k])}</span></li>`).join('');
    $('cargo').hidden = values.every(v => v === 0);   // пустая корзина — панели нет
  }

  // ── Навыки: начальные слоты игрока (PlayerSkills), цвета SkillButton ──
  const skills = {
    d: { name: 'Копание', level: 14, exp: .2, color: '#ffff00' },
    m: { name: 'Добыча', level: 9, exp: .4, color: '#ff6600' },
    M: { name: 'Передвижение', level: 12, exp: 1, color: '#00ffe6' },
    l: { name: 'Защита', level: 12, exp: .6, color: '#ff00ff' },
  };
  window.kernSkills = skills;
  const SLOT_OF = { m: 0, d: 1, M: 2, l: 3 };
  const timers = {};
  function showMini(code) {
    const s = skills[code];
    let el = document.querySelector(`.mini-skill[data-code="${code}"]`);
    if (!el) {
      el = document.createElement('li');
      el.className = 'mini-skill';
      el.dataset.code = code;
      el.title = s.name;
      el.innerHTML = `<i class="ico sk sk-${code}"></i><span class="bar"><i></i></span><i class="up"></i>`;
      el.onclick = () => window.openSkills?.(SLOT_OF[code]);
      $('miniSkills').appendChild(el);
    }
    const ready = s.exp >= 1;
    el.style.setProperty('--c', s.color);
    el.classList.toggle('is-ready', ready);
    el.querySelector('.bar i').style.height = (ready ? 100 : s.exp * 100) + '%';
    clearTimeout(timers[code]);
    if (!ready) timers[code] = setTimeout(() => el.remove(), 5000);
  }
  const grow = (code, step) => { skills[code].exp = Math.min(1, skills[code].exp + step); showMini(code); };

  // ── Копание: клик по клетке рядом с игроком ──
  function dig(x, y) {
    const t = window.kernWorld.dig(x, y);
    grow('d', .06);
    if (t.crystal) { state.basket[t.crystal] += 1 + Math.floor(Math.random() * 4); grow('m', .1); renderCargo(); }
    $('coords').textContent = `${1184 + x - window.kernWorld.PX}:${902 + y - window.kernWorld.PY}`;
  }
  $('world').addEventListener('click', e => {
    const r = $('world').getBoundingClientRect(), k = r.width / $('world').width;
    const c = window.kernWorld.cellAt((e.clientX - r.left) / k, (e.clientY - r.top) / k);
    if (c && c.type !== 'empty') dig(c.x, c.y);
  });
  let digTimer = null;
  function autodigTick() {
    const w = window.kernWorld;
    for (let d = 1; d < 12; d++) for (const [dx, dy] of [[d, 0], [-d, 0], [0, d], [0, -d]]) {
      const t = w.map[w.PY + dy]?.[w.PX + dx];
      if (t && t !== 'empty' && t !== 'road' && t !== 'support') { dig(w.PX + dx, w.PY + dy); return; }
    }
  }

  // ── Чат: 0 — три строки, 1 — колонка, 2 — скрыт (Tab по кругу, как в Legacy) ──
  const chat = {
    FED: [{ sys: 'Сервер: добро пожаловать.' },
      { t: '18:42', nick: 'ШАХТЁР-102', c: '#ffd9a8', text: 'кто у респа?' },
      { t: '18:43', nick: 'ШАХТЁР-774', c: '#c8f0ff', text: 'я, иду вниз' },
      { t: '18:44', nick: 'DVM_LEADER', c: '#e6ccff', text: 'голубые на 1200:840' }],
    DNO: [{ t: '18:20', nick: 'ШАХТЁР-305', c: '#d8ffc8', text: 'кто на дне?' }],
  };
  const esc = s => s.replace(/[&<>]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[ch]));
  function renderChat() {
    const lines = chat[state.channel];
    $('chatLog').innerHTML = lines.map(m => m.sys ? `<li class="sys">${esc(m.sys)}</li>`
      : `<li><time>${m.t}</time><b style="color:${m.c}">${esc(m.nick)}</b>: ${esc(m.text)}</li>`).join('');
    $('chatPeek3').innerHTML = lines.filter(m => !m.sys).slice(-3)
      .map(m => `<li><b style="color:${m.c}">${esc(m.nick)}</b>: ${esc(m.text)}</li>`).join('');
    $('chatPeekTag').textContent = state.channel;
    document.querySelectorAll('.chat-tab').forEach(b => b.classList.toggle('is-on', b.dataset.tag === state.channel));
  }
  function setChat(mode) {
    state.chat = mode;
    $('hud').dataset.chat = mode;
    press($('touchChat'), mode === 1);
    if (mode === 1) setTimeout(() => $('chatInput').focus(), 0); else $('chatInput').blur();
  }
  $('chatPeek').onclick = () => setChat(1);
  $('chatClose').onclick = () => setChat(0);
  document.querySelectorAll('.chat-tab').forEach(b => b.onclick = () => { state.channel = b.dataset.tag; renderChat(); });
  $('chatForm').onsubmit = e => {
    e.preventDefault();
    const text = $('chatInput').value.trim();
    if (!text) return;
    const d = new Date();
    chat[state.channel].push({ t: `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`, nick: 'ШАХТЁР-774', c: '#c8f0ff', text });
    $('chatInput').value = '';
    renderChat();
  };

  // ── Переключатели и кнопки ──
  const press = (el, on) => el.setAttribute('aria-pressed', String(on));
  $('autodig').onclick = () => {
    state.autodig = !state.autodig; press($('autodig'), state.autodig);
    clearInterval(digTimer); if (state.autodig) digTimer = setInterval(autodigTick, 450);
  };
  $('progRun').onclick = () => {
    state.prog = !state.prog;
    $('progRun').classList.toggle('is-running', state.prog);
    $('progRun').title = state.prog ? 'Остановить программу' : 'Запустить программу';
    $('progRun').innerHTML = `<svg><use href="#i-${state.prog ? 'stop' : 'play'}"/></svg>`;
    $('progName').textContent = state.prog ? 'Программа 1' : 'Программатор';   // имя активной программы, programmator.program «Программа {0}»
    document.querySelectorAll('.online em').forEach(em => em.textContent = state.prog ? '(7)' : '(6)');
  };
  $('bonus').onclick = () => { state.money += 500; state.creds += 25; renderPlayer(); };
  $('respawnBtn').onclick = () => { $('respawnDialog').hidden = false; $('respawnOk').focus(); };
  $('respawnDialog').querySelector('[data-close]').onclick = () => { $('respawnDialog').hidden = true; };
  $('respawnOk').onclick = () => {
    for (const [k] of CRYSTALS) state.basket[k] = Math.floor(state.basket[k] * .9);
    renderCargo(); $('respawnDialog').hidden = true;
  };
  document.querySelector('.player .hp').onclick = () => { state.hp = state.hp > 40 ? state.hp - 30 : state.maxHp; renderPlayer(); };

  // ── Инвентарь: 4 видимых слота, остальное по раскрытию ──
  // Предметы и описания — DummyItemInfo клиента; количество — пример набора опытного игрока
  const items = [
    ['geopack', 12, 'Геопак', 'Предмет, который позволяет упаковать живой кристалл в инвентарь'],
    ['battery', 3, 'Аккумулятор', 'Компонент/Предмет используемый в крафтинге и при помощи которого можно увеличить скорость робота'],
    ['c190', 1, 'С-190', 'Компонент/Предмет используемый в крафтинге и при помощи которого можно наносить урон другим игрокам'],
    ['charge', 5, 'Заряд защиты', 'Предмет, увеличивающий здоровье строительных паков'],
    ['craft', 2, 'Крафт', 'Строительный пак, в котором можно создать паки и предметы'],
    ['teleport', 1, 'Телепорт', 'Строительный пак, который позволяет игрокам телепортироваться на другой телепорт'],
    ['resp', 2, 'Респаун', 'Строительный пак, который позволяет возрождаться и ремонтировать робота'],
    ['up', 1, 'UP', 'Строительный пак, который позволяет игрокам устанавливать и прокачивать умения'],
    ['market', 1, 'Маркет', 'Строительный пак, который позволяет покупать и продавать кристаллы, а также обмениваться друг с другом'],
    ['storage', 3, 'Склад', 'Строительный пак, в котором можно хранить кристаллы'],
    ['plasmBomb', 7, 'Плазменная бомба', 'Предмет, который позволяет взорвать блоки в радиусе 3 клеток(Красноскал с 1% шансом)'],
    ['protonBomb', 4, 'Протонная бомба', 'Предмет, который позволяет взорвать блоки 3х3 от центра(Красноскал с 100% шансом)'],
    ['razBomb', 9, 'Бомба-разряд', 'Предмет, который позволяет нанести урон игрокам (500 HP) и Строительным пакам(10 HP)'],
    ['rem', 6, 'Ремонтный бот', 'Предмет, который позволяет полностью восстановить здоровье робота'],
    ['nano', 24, 'Нано бот', 'Компонент/Предмет используемый в крафтинге и при помощи которого можно восстановить здоровье робота на 50 HP'],
    ['geoCyan', 2, 'Голубая жива', 'Живка, которая даёт плод голубыми кристаллами'],
    ['geoRed', 1, 'Красная жива', 'Живка, которая даёт плод красными кристаллами, если поблизости есть черноскал'],
    ['geoViolet', 1, 'Фиолетовая жива', 'Живка, которая даёт плод фиолетовыми кристаллами, если поблизости есть черноскал'],
    ['geoBlue', 3, 'Синяя жива', 'Живка, которая даёт плод синими кристаллами, если есть место для передвижения живки'],
    ['geoWhite', 1, 'Белая жива', 'Живка, которая даёт плод белыми кристаллами, если сверху стоит магма'],
    ['geoRainbow', 1, 'Радужная жива', 'Живка, которая даёт плод любым блоком, если с одной из сторон по горизонтали или вертикали не пусто'],
    ['aliveRadar', 2, 'Радар живок', 'Предмет, который позволяет обнаружить живые кристаллы в радиусе 200 блоков'],
    ['robotRadar', 1, 'Радар роботов', 'Предмет, который позволяет обнаружить роботов в радиусе 300 блоков'],
    ['vulkanRadar', 1, 'Радар вулканов', 'Предмет, который позволяет обнаружить вулканы'],
    ['portableTeleporter', 2, 'ТПР', 'Предмет, который позволяет игроку телепортироваться на Респаун без потери кристаллов'],
    ['poly', 40, 'Полимер', 'Компонент/Предмет используемый в крафтинге и при помощи которого можно строить полимерную дорогу'],
    ['compressor', 5, 'Компрессор', 'Компонент/Предмет используемый в крафтинге'],
    ['trans', 1, 'Транслятор', 'Компонент/Предмет используемый в крафтинге'],
    ['mineBooster', 1, 'Добыча x4', 'Предмет, который увеличивает добычу кристалла в 4 раза (12ч)'],
    ['upgradeBooster', 1, 'Прокачка x3', 'Предмет, который ускоряет прокачку в 3 раза (24ч)'],
    ['scanner', 2, 'Сканер паков', 'Предмет, при использовании которого показываются характеристики строительного пака'],
    ['disassembler', 1, 'Диззассемблер', 'Предмет, позволяющий собрать строительный пак в инвентарь'],
    ['eMI', 3, 'ЭМИ', 'Предмет, который запрещает игрокам в радиусе 20 блоков использовать инвентарь/копать'],
    ['geoBlackRock', 8, 'Чёрная скала', 'Предмет, который мгновенно ставит черноскал на пустоте'],
  ];
  $('invSlots').innerHTML = items.map(([f, n, name], i) => `<li title="${name}" data-i="${i}"><img src="img/items/${f}.png" alt=""><span>${n}</span></li>`).join('');
  $('invSlots').onclick = e => {
    const li = e.target.closest('li'); if (!li) return;
    const on = !li.classList.contains('is-on');
    document.querySelectorAll('.inv-slots li').forEach(x => x.classList.remove('is-on'));
    li.classList.toggle('is-on', on);
    $('itemHint').hidden = !on;
    if (on) $('itemHint').textContent = `${li.title}: Enter — применить, Esc — отмена`;
  };
  // На ПК раскрытие — как было; на пальце — отдельное окно со всеми предметами
  let invSelected = -1;
  function renderInvWindow() {
    $('invCount').textContent = `${items.length} предметов`;
    $('invGrid').innerHTML = items.map(([f, n, name], i) => `<li><button type="button" class="inv-cell${i === invSelected ? ' is-on' : ''}" data-i="${i}" title="${name}"><img src="img/items/${f}.png" alt=""><span>${n}</span></button></li>`).join('');
    const it = items[invSelected];
    $('invInfo').innerHTML = it
      ? `<img class="inv-window-big" src="img/items/${it[0]}.png" alt=""><h3>${it[2]}</h3><p class="inv-window-qty">× ${it[1]}</p><p class="inv-window-desc">${it[3]}</p>
         <button type="button" class="btn btn--primary" id="invUse">Использовать</button>`
      : '<p class="inv-window-empty">Выберите предмет</p>';
    if (it) $('invUse').onclick = () => { $('invDialog').hidden = true; document.querySelector(`.inv-slots li[data-i="${invSelected}"]`)?.click(); };
  }
  $('invGrid').onclick = e => { const b = e.target.closest('.inv-cell'); if (b) { invSelected = Number(b.dataset.i); renderInvWindow(); } };
  $('invDialog').querySelector('[data-close]').onclick = () => { $('invDialog').hidden = true; };
  window.openInventory = () => { invSelected = -1; renderInvWindow(); $('invDialog').hidden = false; };
  $('invExpand').onclick = () => {
    if ($('frame').classList.contains('touch')) { window.openInventory(); return; }
    const open = $('inventory').classList.toggle('is-open');
    $('invExpand').setAttribute('aria-expanded', String(open));
  };

  // ── Клавиши ──
  window.kernHud = { skillChanged: code => document.querySelector(`.mini-skill[data-code="${code}"]`)?.remove() };
  document.addEventListener('keydown', e => {
    if ($('frame').dataset.screen !== 'game' || !$('skillsDialog').hidden) return;
    const typing = e.target === $('chatInput');
    if (e.key === 'Tab') { e.preventDefault(); setChat((state.chat + 1) % 3); return; }
    if (e.key === 'Escape') {
      if (!$('invDialog').hidden) $('invDialog').hidden = true;
      else if (!$('respawnDialog').hidden) $('respawnDialog').hidden = true;
      else if (state.chat === 1) setChat(0);
      else { document.querySelectorAll('.inv-slots li').forEach(x => x.classList.remove('is-on')); $('itemHint').hidden = true; }
      return;
    }
    if (typing) return;
    if (e.key === 'Enter') { e.preventDefault(); setChat(1); }
    if (e.key === 'e' || e.key === 'E' || e.key === 'у' || e.key === 'У') $('autodig').click();
  });

  // ── Стенд: пропорция кадра, состояние данных, вписывание кадра в окно ──
  const frame = $('frame');
  function fit() {
    const st = $('stage'), k = Math.min(st.clientWidth / frame.offsetWidth, st.clientHeight / frame.offsetHeight);
    frame.style.transform = `translate(-50%, -50%) scale(${k})`;
    window.kernWorld?.draw();                                     // холст перерисовать под новый масштаб
  }
  document.querySelectorAll('[data-state]').forEach(b => b.onclick = () => {
    document.querySelectorAll('[data-state]').forEach(x => x.classList.toggle('is-on', x === b));
    const busy = b.dataset.state === 'busy';
    $('mission').hidden = !busy; $('geo').hidden = !busy; $('states').hidden = !busy;
    if (busy !== state.prog) $('progRun').click();
    state.basket = busy ? { g: 2340, r: 96, b: 1204, v: 12, w: 3, c: 57 } : { g: 412, r: 96, b: 1204, v: 0, w: 0, c: 57 };
    renderCargo();
  });
  addEventListener('resize', fit);

  // Мониторы — кадр в логических пикселях: физическое разрешение / масштаб ОС.
  // Устройства — MobileDeviceMatrix: размер в dp и безопасная область (сверху, справа, снизу, слева)
  const DEVICES = {
    d768: { w: 1366, h: 768, px: '1366×768', scale: 1 },
    d800: { w: 1280, h: 800, px: '1280×800', scale: 1 },
    d1080: { w: 1920, h: 1080, px: '1920×1080', scale: 1 },
    d1440: { w: 2560, h: 1440, px: '2560×1440', scale: 1 },
    d219: { w: 2560, h: 1080, px: '2560×1080', scale: 1 },
    d4k150: { w: 2560, h: 1440, px: '3840×2160', scale: 1.5 },
    d4k200: { w: 1920, h: 1080, px: '3840×2160', scale: 2 },
    se: { w: 654, h: 368, form: 'phone', corner: 0, short: true },
    promax: { w: 973, h: 449, safe: [0, 62, 22, 62], form: 'phone', corner: 55 },
    pixel8: { w: 897, h: 404, safe: [0, 0, 0, 44], form: 'phone', corner: 40 },
    ipadmini: { w: 1112, h: 730, safe: [0, 0, 20, 0], form: 'tablet', corner: 22 },
    ipadpro: { w: 1668, h: 1251, safe: [0, 0, 24, 0], form: 'tablet', corner: 18 },
    foldin: { w: 924, h: 794, form: 'tablet', corner: 20 },
  };
  const sysOrig = {};
  function setDevice(name) {
    const d = DEVICES[name] || DEVICES.d1080, touch = !!d.form;
    document.querySelectorAll('[data-device]').forEach(x => x.classList.toggle('is-on', x.dataset.device === name));
    frame.dataset.device = name;
    frame.classList.toggle('touch', touch);
    frame.classList.toggle('phone', touch && d.form === 'phone');
    frame.classList.toggle('tablet', touch && d.form === 'tablet');
    frame.classList.toggle('short', !!d.short);
    const tier = d.form === 'phone' ? 'compact' : d.form === 'tablet' ? 'standard' : d.w < 900 ? 'compact' : d.w < 1600 ? 'standard' : 'wide';
    ['compact', 'standard', 'wide'].forEach(t => frame.classList.toggle('tier-' + t, t === tier));
    ['--fw', '--fh', '--corner', '--st', '--sr', '--sb', '--sl'].forEach(v => frame.style.removeProperty(v));
    frame.style.setProperty('--fw', d.w + 'px'); frame.style.setProperty('--fh', d.h + 'px');
    $('standRes').textContent = touch ? `${d.w}×${d.h} dp` : d.scale === 1 ? `${d.w}×${d.h}` : `${d.px} → ${d.w}×${d.h} при ${d.scale * 100}%`;
    if (touch) {
      frame.style.setProperty('--corner', d.corner + 'px');
      (d.safe || [0, 0, 0, 0]).forEach((v, i) => frame.style.setProperty(['--st', '--sr', '--sb', '--sl'][i], v + 'px'));
    }
    // На пальце у миникарты — бонусы, «Сказать», чат и ☰; остальные системные кнопки и карта — в меню ☰.
    // Перенесённое помечено data-relocated: это ПК-элемент на новом месте, а не новый
    const touchNav = document.querySelector('.touch-sys'), pauseSys = $('pauseSys');
    const mapBtn = sysOrig.map ||= touchNav.querySelector('[title="Карта мира"]');
    sysOrig.list ||= [...document.querySelector('.sysbuttons').children];
    if (touch) {
      touchNav.prepend($('bonus'));
      pauseSys.append(mapBtn, ...sysOrig.list.filter(b => b.tagName === 'BUTTON'));
    } else {
      document.querySelector('.sysbuttons').append(...sysOrig.list);
      touchNav.append(mapBtn);
      document.querySelector('.sysbar').prepend($('bonus'));     // бонусы — первыми в ряду системных кнопок
    }
    // Программатор и автокопание: на пальце — над хотбаром и в дуге действий
    const prog = document.querySelector('.prog');
    if (touch) { $('lowRow').append(prog, $('miniSkills')); $('touchBuild').after($('autodig')); }
    else {
      document.querySelector('.col-bottom').insertBefore(prog, document.querySelector('.col-bottom .minimap'));
      document.querySelector('.col-bottom').insertBefore($('autodig'), prog);
      $('itemHint').after($('miniSkills'));
    }
    [$('bonus'), prog, $('autodig'), $('miniSkills'), ...sysOrig.list, mapBtn].forEach(el => el.toggleAttribute('data-relocated', touch));
    $('keyHint').style.visibility = touch ? 'hidden' : '';
    const mm = $('minimap'), side = touch ? (d.form === 'tablet' ? 136 : 104) * 2 : 0;
    mm.width = touch ? side : 264; mm.height = touch ? side : 264;
    fit();
  }
  document.querySelectorAll('[data-device]').forEach(b => b.onclick = () => setDevice(b.dataset.device));

  $('statusBar').append(document.querySelector('.diag'), $('assetLoad'));

  // Ресурсы догружаются фоном после входа: «N / M», тонкая полоса; по завершении строка пропадает
  let assetTimer = 0;
  window.kernAssets = (total = 148, freezeAt = 0) => {
    clearInterval(assetTimer);
    let done = 0;
    $('assetLoad').style.setProperty('--al-digits', String(total).length);
    const tick = () => {
      $('assetLoad').hidden = done >= total;
      $('assetLoadText').textContent = `${done}/${total}`;
      $('assetLoadFill').style.width = (done * 100 / total) + '%';
      if (freezeAt && done >= freezeAt) { clearInterval(assetTimer); return; }
      if (done >= total) clearInterval(assetTimer);
      done = Math.min(total, done + 3);
    };
    tick(); assetTimer = setInterval(tick, 120);
  };

  // ── Сенсорное управление (PlayerHUD MobileControls) ──
  const pad = $('pad'), knob = $('padKnob');
  pad.onpointerdown = e => {
    pad.setPointerCapture(e.pointerId);
    const r = pad.getBoundingClientRect(), k = pad.offsetWidth / r.width, max = pad.offsetWidth / 2 - 27;
    const move = ev => {
      let x = (ev.clientX - r.left - r.width / 2) * k, y = (ev.clientY - r.top - r.height / 2) * k;
      const len = Math.hypot(x, y); if (len > max) { x *= max / len; y *= max / len; }
      knob.style.transform = `translate(${x}px, ${y}px)`;
    };
    move(e);
    pad.onpointermove = move;
    pad.onpointerup = pad.onpointercancel = () => { pad.onpointermove = null; knob.style.transform = ''; };
  };
  const toggleBtn = b => press(b, b.getAttribute('aria-pressed') !== 'true');
  $('fastMove').onclick = () => toggleBtn($('fastMove'));
  // «Строить»: выключено — открывает веер типов; выбранный тип включает установку и встаёт на кнопку
  const builds = [...document.querySelectorAll('.build-fan .build')];
  const setBuild = b => {
    builds.forEach(x => press(x, x === b));
    press($('touchBuild'), !!b);
    if (b) $('touchBuildImg').src = b.querySelector('img').src;
    $('buildFan').hidden = true;
  };
  builds.forEach(b => b.onclick = () => setBuild(b.getAttribute('aria-pressed') === 'true' ? null : b));
  $('touchBuild').onclick = () => {
    if ($('touchBuild').getAttribute('aria-pressed') === 'true') setBuild(null);
    else $('buildFan').hidden = !$('buildFan').hidden;
  };
  function setSay(on) {
    $('sayForm').hidden = !on; press($('touchSay'), on);
    if (on) { setChat(0); setTimeout(() => $('sayInput').focus(), 0); }
  }
  $('touchChat').onclick = () => { setSay(false); setChat(state.chat === 1 ? 0 : 1); };
  $('touchSay').onclick = () => setSay($('sayForm').hidden);
  $('sayForm').onsubmit = e => { e.preventDefault(); $('sayInput').value = ''; setSay(false); };
  // Мои здания — пустое окно сервера
  document.querySelector('[title="Мои здания"]').addEventListener('click', () => { $('buildingsDialog').hidden = false; });
  $('buildingsDialog').querySelector('[data-close]').onclick = () => { $('buildingsDialog').hidden = true; };
  document.addEventListener('keydown', e => { if (e.key === 'Escape') $('buildingsDialog').hidden = true; });
  $('touchMenu').onclick = () => { $('pauseDialog').hidden = false; };
  $('pauseSys').addEventListener('click', e => { if (e.target.closest('button')) $('pauseDialog').hidden = true; });
  $('pauseDialog').querySelector('[data-close]').onclick = () => { $('pauseDialog').hidden = true; };
  $('touchDig').onclick = () => autodigTick();
  $('touchHeal').onclick = () => { state.hp = state.maxHp; renderPlayer(); };
  $('touchGeo').onclick = () => document.querySelector('.inv-slots li[title="Геопак"]').click();

  // Параметры адреса для снимков: ?aspect=1.6&state=busy&chat=1
  const q = new URLSearchParams(location.search);
  window.kernWorld.ready.then(() => {
    setDevice(q.get('device') || 'd1080');
    renderPlayer(); renderCargo(); renderChat(); showMini('M');
    if (q.get('state')) document.querySelector(`[data-state="${q.get('state')}"]`)?.click();
    if (q.get('chat')) setChat(Number(q.get('chat')));
    if (q.get('build')) $('buildFan').hidden = false;
    if (q.get('pause')) $('pauseDialog').hidden = false;
    if (q.get('buildings')) $('buildingsDialog').hidden = false;
    if (q.get('assets')) window.kernAssets(148, Number(q.get('assets')));
    if (q.get('inv')) { $('invExpand').click(); if (q.get('inv') !== '1') document.querySelector(`.inv-cell[data-i="${q.get('inv')}"]`)?.click(); }
  });
})();
