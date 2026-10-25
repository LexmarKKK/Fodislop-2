# Извлекает из m1client.unity (Unity Legacy OpenMines) дерево RectTransform с
# компонентами и цепочки якорей выбранных узлов. Цепочки читает
# js/legacy-overlay.js и сам решает раскладку UGUI под размер окна,
# поэтому сверка макета не зависит от его CSS.
# Запуск: python3 -I tools/legacy_scene_rects.py ../../../OpenMines/client tree.txt "Canvas/MiniSkills,..."
import re, sys, os, json, glob
root = sys.argv[1]
scene = os.path.join(root, 'Assets/Scenes/m1client.unity')
# guid map
gmap = {}
for base in ['Assets', 'Library/PackageCache']:
    for dp, dn, fn in os.walk(os.path.join(root, base)):
        for f in fn:
            if f.endswith('.cs.meta') or f.endswith('.png.meta') or f.endswith('.ttf.meta') or f.endswith('.otf.meta') or f.endswith('.fontsettings.meta') or f.endswith('.mat.meta'):
                p = os.path.join(dp, f)
                try:
                    m = re.search(r'guid: ([0-9a-f]{32})', open(p, errors='ignore').read())
                    if m: gmap[m.group(1)] = os.path.relpath(p, root)[:-5]
                except: pass
txt = open(scene, errors='ignore').read()
docs = re.split(r'^--- !u!(\d+) &(\d+).*$', txt, flags=re.M)
objs = {}
for i in range(1, len(docs), 3):
    t, fid, body = docs[i], docs[i+1], docs[i+2]
    objs[fid] = (t, body)
def f(body, key):
    m = re.search(r'^\s+' + key + r': (.*)$', body, flags=re.M)
    return m.group(1).strip() if m else None
def vec(s):
    if not s: return None
    return {k: float(v) for k, v in re.findall(r'(\w+): ([-\d.e]+)', s)}
go = {}
for fid, (t, b) in objs.items():
    if t == '1':
        comps = re.findall(r'component: \{fileID: (\d+)\}', b)
        go[fid] = dict(name=f(b, 'm_Name'), active=f(b, 'm_IsActive'), comps=comps)
rect = {}
for fid, (t, b) in objs.items():
    if t == '224':
        rect[fid] = dict(go=re.search(r'm_GameObject: \{fileID: (\d+)\}', b).group(1),
            parent=re.search(r'm_Father: \{fileID: (\d+)\}', b).group(1),
            children=re.findall(r'- \{fileID: (\d+)\}', b.split('m_Children:')[1].split('m_Father')[0]) if 'm_Children:' in b else [],
            amin=vec(f(b, 'm_AnchorMin')), amax=vec(f(b, 'm_AnchorMax')), pos=vec(f(b, 'm_AnchoredPosition')),
            size=vec(f(b, 'm_SizeDelta')), pivot=vec(f(b, 'm_Pivot')), scale=vec(f(b, 'm_LocalScale')))
def compdesc(cid):
    if cid not in objs: return None
    t, b = objs[cid]
    if t != '114': return None
    m = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)', b)
    g = m.group(1) if m else '?'
    name = os.path.basename(gmap.get(g, g))
    d = {'type': name.replace('.cs', ''), 'enabled': f(b, 'm_Enabled')}
    if 'm_Text:' in b:
        mt = re.search(r'm_Text: (.*?)\n\s+m_|m_Text: (.*)$', b, flags=re.S|re.M)
        d['text'] = (mt.group(1) or mt.group(2) or '').strip()[:300] if mt else ''
        d['fontSize'] = f(b, 'm_FontSize'); d['align'] = f(b, 'm_Alignment')
        fm = re.search(r'm_Font: \{fileID: \d+, guid: ([0-9a-f]+)', b)
        if fm: d['font'] = os.path.basename(gmap.get(fm.group(1), fm.group(1)))
        d['style'] = f(b, 'm_FontStyle'); d['bestFit'] = f(b,'m_BestFit')
    c = f(b, 'm_Color')
    if c: d['color'] = c
    sm = re.search(r'm_Sprite: \{fileID: (-?\d+), guid: ([0-9a-f]+)', b)
    if sm: d['sprite'] = os.path.basename(gmap.get(sm.group(2), sm.group(2))) + '#' + sm.group(1)
    tm = re.search(r'm_Texture: \{fileID: (-?\d+), guid: ([0-9a-f]+)', b)
    if tm: d['texture'] = os.path.basename(gmap.get(tm.group(2), tm.group(2)))
    for k in ['m_UiScaleMode','m_ReferenceResolution','m_MatchWidthOrHeight','m_ScaleFactor','m_Spacing','m_CellSize','m_Padding','m_ChildAlignment','m_Constraint','m_ConstraintCount','m_StartCorner','m_StartAxis']:
        v = f(b, k)
        if v is not None: d[k] = v
    return d
roots = [r for r, v in rect.items() if v['parent'] == '0']
out = []
def walk(rid, depth):
    r = rect[rid]; g = go.get(r['go'], {})
    comps = [c for c in (compdesc(c) for c in g.get('comps', [])) if c]
    def fmt(v): return ','.join('%g' % v[k] for k in ('x','y')) if v else '-'
    line = '  ' * depth + f"{g.get('name')} [{'on' if g.get('active')=='1' else 'OFF'}] amin={fmt(r['amin'])} amax={fmt(r['amax'])} pos={fmt(r['pos'])} size={fmt(r['size'])} piv={fmt(r['pivot'])}"
    if r['scale'] and (r['scale'].get('x') != 1 or r['scale'].get('y') != 1): line += f" scale={fmt(r['scale'])}"
    out.append(line)
    for c in comps:
        out.append('  ' * depth + '    · ' + json.dumps(c, ensure_ascii=False))
    for ch in r['children']:
        if ch in rect: walk(ch, depth + 1)
for r in roots: walk(r, 0)
open(sys.argv[2], 'w').write('\n'.join(out))
print(len(out), 'lines', len(roots), 'roots')

# --- экспорт цепочек якорей для независимой сверки раскладки ---
paths = {}
def walk2(rid, prefix):
    r = rect[rid]; g = go.get(r['go'], {})
    p = (prefix + '/' if prefix else '') + (g.get('name') or '?')
    paths.setdefault(p, rid)
    for ch in r['children']:
        if ch in rect: walk2(ch, p)
for r0 in roots: walk2(r0, '')
want = sys.argv[3].split(',') if len(sys.argv) > 3 else []
export = {}
for w in want:
    rid = paths.get(w)
    if not rid: print('MISSING', w); continue
    chain = []
    cur = rid
    while cur in rect and rect[cur]['parent'] != '0':
        r = rect[cur]
        chain.append({k: r[k] for k in ('amin','amax','pos','size','pivot','scale')} | {'name': go.get(r['go'],{}).get('name')})
        cur = r['parent']
    export[w] = list(reversed(chain))
json.dump(export, open(sys.argv[4] if len(sys.argv) > 4 else 'legacy-rects.json','w'), ensure_ascii=False, indent=1)
print('exported', len(export))
