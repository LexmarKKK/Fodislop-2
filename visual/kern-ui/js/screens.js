// Поток экранов: Bootstrap → вход → онбординг (один раз) → меню → спуск → игра,
// плюс окно навыков в игре. Тексты — строки словаря Kern и сервера.
(() => {
  const $ = id => document.getElementById(id);
  const frame = $('frame');
  const timers = new Set();
  const later = (fn, ms) => { const t = setTimeout(() => { timers.delete(t); fn(); }, ms); timers.add(t); return t; };
  const clearAll = () => { timers.forEach(clearTimeout); timers.clear(); };
  const hold = Number(new URLSearchParams(location.search).get('hold')) || 0;

  function show(screen) {
    clearAll();
    const screenName = screen === 'descent' ? 'menu' : screen;
    const enteringScreen = document.querySelector(`.screen[data-screen="${screenName}"]`);
    enteringScreen?.classList.add('is-entering');
    frame.dataset.screen = screen;
    if (enteringScreen) requestAnimationFrame(() => enteringScreen.classList.remove('is-entering'));
    document.querySelectorAll('[data-screen]').forEach(b => { if (b.tagName === 'BUTTON') b.classList.toggle('is-on', b.dataset.screen === screen); });
    document.querySelectorAll('[data-for]').forEach(g => g.hidden = g.dataset.for !== (screen === 'descent' ? 'menu' : screen));
    $('menuMain').hidden = screen === 'descent';
    $('descent').hidden = screen !== 'descent';
    if (screen === 'boot') runBoot();
    if (screen === 'descent') runDescent();
    if (screen === 'game') { window.kernWorld?.draw(); window.kernAssets?.(); }
  }
  document.querySelectorAll('button[data-screen]').forEach(b => b.addEventListener('click', () => show(b.dataset.screen)));

  // UI Toolkit-аналог: видимые состояния переключаются классами, свойства
  // opacity/scale/translate плавно интерполируются самими стилями.
  document.querySelectorAll('.dialog-layer').forEach(layer => {
    new MutationObserver(() => {
      if (layer.hidden) return;
      layer.classList.add('is-entering');
      requestAnimationFrame(() => layer.classList.remove('is-entering'));
    }).observe(layer, { attributes: true, attributeFilter: ['hidden'] });
  });

  // ── Bootstrap: фаза загрузки, затем вход ──
  function runBoot() {
    let p = 0;
    $('bootPhase').textContent = 'Компиляция шейдеров…';
    const tick = () => {
      p = Math.min(100, p + 7);
      $('bootFill').style.width = p + '%';
      if (p >= 60) $('bootPhase').textContent = 'Загрузка ресурсов…';
      if (hold && p >= hold) return;                              // ?hold=N — снимок на N %
      if (p < 100) later(tick, 90); else later(() => show('auth'), 400);
    };
    $('bootFill').style.width = '0';
    later(tick, 200);
  }

  // ── Вход: логин и пароль или VK ──
  const hint = $('authHint');
  function enter(text) {
    hint.textContent = text; hint.classList.add('is-live');
    $('authSubmit').disabled = $('authVk').disabled = true;
    later(() => {
      $('authSubmit').disabled = $('authVk').disabled = false;
      hint.textContent = ''; hint.classList.remove('is-live');
      show(onboardingDone ? 'menu' : 'onboarding');
    }, 1100);
  }
  $('authForm').onsubmit = e => {
    e.preventDefault();
    const empty = [$('authLogin'), $('authPassword')].find(f => !f.value.trim());
    if (empty) { hint.textContent = 'Введите логин и пароль'; hint.classList.remove('is-live'); empty.focus(); return; }
    enter('Подключение…');
  };
  $('authVk').onclick = () => enter('VK: откройте ссылку подтверждения в браузере…');

  // ── Онбординг: три шага, показывается один раз ──
  let onboardingDone = false, step = 0;
  const titles = ['Доступность', 'Графика', 'Звук'];
  function renderStep() {
    document.querySelectorAll('.onb-step').forEach(f => f.classList.toggle('is-on', Number(f.dataset.step) === step));
    document.querySelectorAll('#onbSteps li').forEach((li, i) => { li.classList.toggle('is-on', i === step); li.classList.toggle('is-done', i < step); });
    $('onbTitle').textContent = titles[step];
    $('onbPrev').disabled = step === 0;
    $('onbNext').textContent = step === 2 ? 'Готово' : 'Далее';
  }
  const finish = () => { onboardingDone = true; step = 0; renderStep(); show('menu'); };
  $('onbNext').onclick = () => step === 2 ? finish() : (step++, renderStep());
  $('onbPrev').onclick = () => { if (step > 0) { step--; renderStep(); } };
  $('onbSkip').onclick = finish;
  $('onbVolume').oninput = e => { $('onbVolumeOut').textContent = e.target.value + '%'; };

  // ── Меню: обновление вместо спуска, выбор сервера ──
  let outdated = false;
  function setOutdated(on) {
    outdated = on;
    $('descendBtn').hidden = on;
    $('updateBlock').hidden = !on;
    $('versionLabel').textContent = '0.8.14';
    document.querySelectorAll('[data-update]').forEach(b => b.classList.toggle('is-on', b.dataset.update === (on ? '1' : '0')));
  }
  document.querySelectorAll('[data-update]').forEach(b => b.onclick = () => setOutdated(b.dataset.update === '1'));
  // Окно обновления: список изменений, затем загрузка с прогрессом
  function openUpdate() {
    $('updProgress').hidden = true; $('updConfirm').disabled = !outdated; $('updDefer').disabled = false;
    $('updateDialog').hidden = false;
  }
  $('updateBtn').onclick = openUpdate;
  $('updateDialog').querySelector('[data-close]').onclick = $('updDefer').onclick = () => { $('updateDialog').hidden = true; };
  $('updConfirm').onclick = () => {
    let p = 0;
    $('updConfirm').disabled = $('updDefer').disabled = true;
    $('updProgress').hidden = false;
    const tick = () => {
      p += 4;
      $('updFill').style.width = p + '%';
      $('updPercent').textContent = p + '%';
      if (p < 100) later(tick, 60);
      else later(() => { $('updateDialog').hidden = true; setOutdated(false); $('versionLabel').textContent = '0.9.0'; }, 300);
    };
    $('updFill').style.width = '0'; tick();
  };

  // Проверка доступности файлов, на которые ссылается клиентский интерфейс.
  const healthDialog = $('healthDialog');
  $('railHealth').onclick = () => { healthDialog.hidden = false; };
  healthDialog.querySelectorAll('[data-close]').forEach(button => {
    button.onclick = () => { healthDialog.hidden = true; };
  });
  $('healthCheck').onclick = async () => {
    const urls = [...new Set([...document.images]
      .map(image => image.currentSrc || image.src)
      .filter(url => url && new URL(url, document.baseURI).origin === location.origin))];
    const total = urls.length;
    let checked = 0, missing = 0;
    $('healthCheck').disabled = true;
    $('healthProgress').hidden = false;
    $('healthStatus').textContent = 'Проверка файлов…';
    $('healthStatus').className = '';
    $('healthCount').textContent = `0 / ${total}`;
    $('healthFill').style.width = '0%';

    for (let index = 0; index < urls.length; index += 8) {
      const batch = urls.slice(index, index + 8);
      const results = await Promise.all(batch.map(async url => {
        try {
          const response = await fetch(url, { method: 'HEAD', cache: 'no-store' });
          return response.ok;
        } catch {
          return false;
        }
      }));
      checked += results.length;
      missing += results.filter(ok => !ok).length;
      $('healthCount').textContent = `${checked} / ${total}`;
      $('healthFill').style.width = `${total === 0 ? 0 : checked / total * 100}%`;
    }

    const healthy = total > 0 && missing === 0;
    $('healthStatus').textContent = healthy
      ? 'Все ресурсы доступны'
      : total === 0 ? 'Ресурсы для проверки не найдены' : `Не найдены ресурсы: ${missing}`;
    $('healthStatus').classList.add(healthy ? 'is-healthy' : 'has-errors');
    $('healthCheck').disabled = false;
    $('healthCheck').textContent = 'Проверить ещё раз';
  };
  $('descendBtn').onclick = () => show('descent');

  // ── Спуск: пять фаз MenuLoaderProgress, затем игра ──
  const PHASES = ['Подключение к серверу', 'Загрузка ресурсов', 'Получение позиции', 'Построение мира', 'Загрузка текстур'];
  function runDescent() {
    let i = 0, p = 0;
    const items = [...document.querySelectorAll('#descentPhases li')];
    const tick = () => {
      p = Math.min(100, p + 2);
      i = Math.min(4, Math.floor(p / 20));
      $('descentFill').style.width = p + '%';
      $('descentPhase').textContent = PHASES[i];
      $('descentCount').textContent = `${i + 1} / 5`;
      items.forEach((li, k) => { li.classList.toggle('is-done', k < i || p === 100); li.classList.toggle('is-on', k === i && p < 100); });
      if (hold && p >= hold) return;                              // ?hold=N — снимок на N %
      if (p < 100) later(tick, 70); else later(() => show('game'), 500);
    };
    $('descentFill').style.width = '0';
    tick();
  }
  $('descentCancel').onclick = () => show('menu');

  // ── Окно навыков (UpPage) ──
  const SLOT_POS = [20,110,21,153,38,75,38,191,74,94,74,169,59,132,100,132,51,22,93,36,51,241,93,226,281,50,256,121,312,23,307,87,377,27,376,76,374,137,375,176,165,46,145,131,185,142,332,153,203,24,248,76,341,57,343,103,138,87,248,17,297,133,214,115,175,98,205,68];
  const SLOT_AMOUNT = 20;
  const skills = window.kernSkills;
  const slots = { 0: 'm', 1: 'd', 2: 'M', 3: 'l' };            // начальные навыки (PlayerSkills)
  const INSTALLABLE = ['A', 'L', 'Y', 'E', 'O', 'xL', 'F', 't', 'p', 'b', 'c', 'g', 'r', 'v', 'w', 'xI', 'u', 'e'];
  // build_skill_description: формат сервера, числа — по его формулам
  function describe(code) {
    const s = skills[code], exp = `${s.exp.toFixed(1)}/1.0`, x = s.level;
    if (code === 'M') return `Передвижение Уровень:${x}\nExp - ${exp}\nСкорость передвижения ${(1 / (Math.max(30, 70 - x * 0.05) * 1.2e-3) * 0.3 * 3.6).toFixed(2)} км/ч`;
    if (code === 'd') return `Копание Уровень:${x}\nExp - ${exp}\nСила копания: ${100 + x * 10}`;
    if (code === 'l') return `Защита Уровень:${x}\nExp - ${exp}\nМакс. здоровье: ${100 + x * 3}`;
    return `Добыча Уровень:${x}\nExp - ${exp}\nМножитель добычи: ${(0.08 + Math.log10(x) * Math.sqrt(x) / 4).toFixed(2)}`;
  }
  let selected = -1, extraSlots = 0;
  function renderSkills() {
    const robot = $('skillsRobot'), cs = getComputedStyle(robot);
    const s = parseFloat(cs.getPropertyValue('--s')), pad = parseFloat(cs.getPropertyValue('--pad'));
    robot.querySelectorAll('.slot').forEach(n => n.remove());
    const amount = SLOT_AMOUNT + extraSlots;
    for (let i = amount - 1; i >= 0; i--) {                     // как PopupManager: первый рисуется поверх
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'slot';
      b.style.left = `${pad + SLOT_POS[2 * i] * s}px`;
      b.style.top = `${pad + SLOT_POS[2 * i + 1] * s}px`;
      const code = slots[i];
      if (code) {
        const k = skills[code];
        b.style.setProperty('--c', k.color);
        b.title = k.name;
        b.innerHTML = `<i class="sk sk-${code}"></i><span class="lvl">${k.level}</span>` + (k.exp >= 1 ? '<span class="ready" title="Готово к улучшению"></span>' : '');
      } else { b.classList.add('is-empty'); b.title = 'Пустой слот'; }
      b.classList.toggle('is-on', i === selected);
      b.onclick = () => { selected = i; renderSkills(); };
      robot.appendChild(b);
    }
    $('skillsSlotsInfo').textContent = `${Object.keys(slots).length} из ${amount} слотов занято`;
    const info = $('skillInfo'), code = slots[selected];
    if (selected < 0) {
      info.innerHTML = `<p class="skill-desc">Выберите скилл или пустой слот</p>
        <div class="skill-tools"><button type="button" class="btn btn--primary" id="buySlot">Купить слот (1000 кредов)</button></div>`;
      $('buySlot').onclick = () => { if (amount < 34) { extraSlots++; renderSkills(); } };
    } else if (code) {
      const k = skills[code];
      info.innerHTML = `<i class="big sk sk-${code}"></i><h3>${k.name}</h3><p class="skill-desc">${describe(code)}</p>
        <div class="skill-tools">${k.exp >= 1 ? '<button type="button" class="btn btn--primary" id="upSkill">Улучшить</button>' : '<span class="spacer"></span>'}
        <button type="button" class="icon-btn" title="Замок"><svg><use href="#i-lock"/></svg></button>
        <button type="button" class="icon-btn" title="Удалить навык"><svg><use href="#i-trash"/></svg></button></div>`;
      if ($('upSkill')) $('upSkill').onclick = () => { k.level++; k.exp = 0; window.kernHud?.skillChanged(code); renderSkills(); };
    } else {
      info.innerHTML = `<p class="skill-desc">Выберите навык для установки</p>
        <div class="install">${INSTALLABLE.map(c => `<button type="button" class="sk sk-${c}" title="Установить"></button>`).join('')}</div>`;
    }
  }
  window.openSkills = (slot = -1) => { selected = slot; renderSkills(); $('skillsDialog').hidden = false; };
  $('openSkills').onclick = () => { show('game'); window.openSkills(-1); };
  $('skillsDialog').querySelector('[data-close]').onclick = () => { $('skillsDialog').hidden = true; };
  document.addEventListener('keydown', e => { if (e.key === 'Escape') { $('skillsDialog').hidden = true; $('updateDialog').hidden = true; healthDialog.hidden = true; } });

  // Адрес: ?screen=menu&update=1&updwin=1&skills=2&onbstep=1
  const q = new URLSearchParams(location.search);
  renderStep();
  if (q.get('update') === '1') setOutdated(true);
  if (q.get('onbstep')) { step = Number(q.get('onbstep')); renderStep(); }
  show(q.get('screen') || 'game');
  if (q.get('skills')) window.kernWorld.ready.then(() => window.openSkills(Number(q.get('skills'))));
  if (q.get('updwin')) openUpdate();
  if (q.get('health')) {
    healthDialog.hidden = false;
    if (q.get('health') === 'run') $('healthCheck').click();
  }
})();
