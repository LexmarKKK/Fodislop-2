// Мир за HUD — из настоящих текстур клеток Kern (Assets/Textures/Cells), клетка 32 px.
// Карта детерминирована (зерно), чтобы снимки сравнивались без шума.
(() => {
  const CELL = 32;
  const W = 220, H = 140;                 // карта шире кадра: её же рисует миникарта
  const TYPES = {
    empty: { file: 'Empty', mm: '#2a1d12' },
    rock: { file: 'Rock', mm: '#2b2f86' },
    grock: { file: 'GRock', mm: '#3b4a3b' },
    heavy: { file: 'HeavyRock', mm: '#3a3a44' },
    black: { file: 'BlackRock', mm: '#1d1f24' },
    sand: { file: 'YellowSand', mm: '#c99b4f' },
    rusty: { file: 'RustySand', mm: '#9a5a33' },
    white: { file: 'WhiteSand', mm: '#b9b6ad' },
    boulder: { file: 'Boulder1', mm: '#5d5a55' },
    boulder2: { file: 'Boulder2', mm: '#5d5a55' },
    road: { file: 'Road', mm: '#6d6a64' },
    support: { file: 'Support', mm: '#8a7a5a' },
    g: { file: 'Green', mm: '#3ee86b', crystal: 'g' },
    b: { file: 'Blue', mm: '#6f8cff', crystal: 'b' },
    r: { file: 'Red', mm: '#ff7a74', crystal: 'r' },
    c: { file: 'Cyan', mm: '#39e8f0', crystal: 'c' },
    w: { file: 'White', mm: '#eef1f3', crystal: 'w' },
    v: { file: 'Violet', mm: '#f15fd6', crystal: 'v' },
  };
  let seed = 7;
  const rand = () => (seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296;
  const noise = (x, y) => { const s = Math.sin(x * 12.9898 + y * 78.233) * 43758.5453; return s - Math.floor(s); };
  const smooth = (x, y, f) => {
    const xi = Math.floor(x / f), yi = Math.floor(y / f), tx = x / f - xi, ty = y / f - yi;
    const a = noise(xi, yi), b = noise(xi + 1, yi), c = noise(xi, yi + 1), d = noise(xi + 1, yi + 1);
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
  };

  // Порода слоями, жилы кристаллов, ходы игрока, дорога с опорами
  const map = [];
  for (let y = 0; y < H; y++) {
    const row = [];
    for (let x = 0; x < W; x++) {
      const n = smooth(x, y, 6) * .85 + noise(x, y) * .15, m = smooth(x + 50, y + 30, 3);
      let t = y < 30 ? (n > .55 ? 'sand' : n > .35 ? 'rusty' : 'grock') : y < 80 ? (n > .6 ? 'rock' : n > .42 ? 'heavy' : 'sand') : (n > .5 ? 'black' : 'rock');
      if (m > .84) t = ['g', 'b', 'r', 'c', 'w', 'v'][Math.floor(smooth(x, y, 3) * 6) % 6];
      else if (m < .12) t = rand() > .5 ? 'boulder' : 'boulder2';
      row.push(t);
    }
    map.push(row);
  }
  const PX = 110, PY = 70;                 // игрок
  const carve = (x, y) => { if (map[y] && map[y][x]) map[y][x] = 'empty'; };
  for (let x = PX - 40; x <= PX + 10; x++) { carve(x, PY); carve(x, PY + 1); }       // штрек
  for (let y = PY - 24; y <= PY; y++) { carve(PX + 10, y); carve(PX + 11, y); }      // шахта вверх
  for (let x = PX - 6; x <= PX + 3; x++) for (let y = PY - 3; y <= PY + 2; y++) carve(x, y); // забой
  for (let x = PX - 40; x <= PX - 8; x++) map[PY + 2][x] = 'road';
  for (let x = PX - 38; x <= PX - 8; x += 6) map[PY - 1][x] = 'support';
  for (let i = 0; i < 9; i++) map[PY - 5 + (i % 3)][PX + 4 + Math.floor(i / 3)] = ['c', 'g', 'b'][i % 3];

  const images = {};
  const load = Object.values(TYPES).map(t => new Promise(res => {
    const img = new Image(); img.onload = res; img.onerror = res; img.src = `img/cells/${t.file}.png`; images[t.file] = img;
  }));
  const robot = new Image();
  load.push(new Promise(res => { robot.onload = res; robot.onerror = res; }));
  robot.src = 'img/bee.png';

  const world = { map, PX, PY, TYPES, CELL };
  world.draw = () => {
    const canvas = document.getElementById('world');
    const frame = document.getElementById('frame');
    // Холст в физических пикселях экрана: кадр стенда масштабирован под окно, иначе клетки растягиваются швами
    const W = frame.offsetWidth, H = frame.offsetHeight;
    const r = (frame.getBoundingClientRect().width / W || 1) * (devicePixelRatio || 1);
    canvas.width = Math.round(W * r); canvas.height = Math.round(H * r);
    const ctx = canvas.getContext('2d');
    ctx.imageSmoothingQuality = 'high';
    const cols = Math.ceil(W / CELL) + 1, rows = Math.ceil(H / CELL) + 1;
    const ox = PX - Math.floor(cols / 2), oy = PY - Math.floor(rows / 2);
    const offX = (W / 2) - (PX - ox) * CELL - CELL / 2, offY = (H / 2) - (PY - oy) * CELL - CELL / 2;
    world.view = { ox, oy, offX, offY };
    const px = v => Math.round(v * r);                           // края клеток на целых пикселях — без щелей
    const cell = (img, x, y) => ctx.drawImage(img, px(offX + x * CELL), px(offY + y * CELL),
      px(offX + (x + 1) * CELL) - px(offX + x * CELL), px(offY + (y + 1) * CELL) - px(offY + y * CELL));
    for (let y = 0; y < rows; y++) for (let x = 0; x < cols; x++) {
      const t = TYPES[map[oy + y]?.[ox + x] || 'black'];
      const img = images[t.file];
      if (img?.naturalWidth) cell(img, x, y);
    }
    ctx.imageSmoothingEnabled = false;
    if (robot.complete) cell(robot, PX - ox, PY - oy);
    world.drawMinimap();
  };
  world.drawMinimap = () => {
    const mm = document.getElementById('minimap');
    if (!mm) return;
    const ctx = mm.getContext('2d'), s = 2;
    const cw = mm.width / s, ch = mm.height / s;
    for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
      const t = TYPES[map[PY - Math.floor(ch / 2) + y]?.[PX - Math.floor(cw / 2) + x] || 'black'];
      ctx.fillStyle = t.mm; ctx.fillRect(x * s, y * s, s, s);
    }
    ctx.fillStyle = 'rgba(11,15,18,.35)'; ctx.fillRect(0, 0, mm.width, mm.height);
    ctx.fillStyle = '#e8b04a'; ctx.fillRect(mm.width / 2 - 2, mm.height / 2 - 2, 4, 4);
  };
  world.cellAt = (px, py) => {
    const v = world.view; if (!v) return null;
    const x = v.ox + Math.floor((px - v.offX) / CELL), y = v.oy + Math.floor((py - v.offY) / CELL);
    return map[y] && map[y][x] ? { x, y, type: map[y][x] } : null;
  };
  world.dig = (x, y) => { const t = TYPES[map[y][x]]; map[y][x] = 'empty'; world.draw(); return t; };
  world.ready = Promise.all(load);
  window.kernWorld = world;
})();
