# Данные программатора для макета — из клиента, руками не правятся:
# коды команд — ProgAction (протокол), разделы — ProgrammatorOperators.cs,
# названия — ru.json (programmator.op.*), иконки — Resources/Programmator/Icons/**/<код>.png.
# Запуск из корня репозитория: python3 -I visual/kern-ui/tools/prog-data.py
import json, pathlib, re, shutil

root = pathlib.Path(__file__).resolve().parents[3]
out = root / 'visual/kern-ui'

names = re.findall(r'^\s*(\w+),', (root / 'Packages/MinesServerNetworking/MinesServer.Data/ProgAction.cs').read_text('utf-8-sig'), re.M)
code = {n: i for i, n in enumerate(names)}

ops_src = (root / 'Assets/Scripts/UI/Programmator/Program/Data/ProgrammatorOperators.cs').read_text('utf-8')
cats = []
for cat, body in re.findall(r'\[(CAT_\w+)\] = new\[\]\s*\{(.*?)\}', ops_src, re.S):
    cats.append([cat, [code[a] for a in re.findall(r'ProgAction\.(\w+)', body)]])

ru = json.loads((root / 'Assets/Resources/Localization/Locales/ru.json').read_text('utf-8'))
title = {c: ru.get(f'programmator.cat.{c}', c) for c, _ in cats}
label = {i: ru.get(f'programmator.op.{n}', n) for n, i in code.items()}

icons = {int(p.stem): p for p in (root / 'Assets/Resources/Programmator/Icons').rglob('*.png')}
dst = out / 'img/prog'
dst.mkdir(exist_ok=True)
used = {0} | {i for _, ops in cats for i in ops}
for i in sorted(used & icons.keys()):
    shutil.copyfile(icons[i], dst / f'{i}.png')

# Клавиши ввода (ProgrammatorKeymap.cs): повторное нажатие перебирает команды группы
keys_src = (root / 'Assets/Scripts/UI/Programmator/Input/Keyboard/ProgrammatorKeymap.cs').read_text('utf-8')
keys = [[('Shift+' if shift == 'true' else '') + key, [code[a] for a in re.findall(r'ProgAction\.(\w+)', body)]]
        for key, shift, body in re.findall(r'new\(Key\.(\w+), (true|false),\s*\[(.*?)\]\)', keys_src, re.S)]

data = {
    'cats': [{'id': c, 'name': title[c], 'ops': ops} for c, ops in cats],
    'names': {i: label[i] for i in sorted(used)},
    'loc': {i: f'programmator.op.{n}' for n, i in code.items() if i in used},   # ключ словаря игры — для перевода
    'code': {n: i for n, i in code.items() if i in used},
    'keys': keys,
    'icons': sorted(used & icons.keys()),
}
(out / 'js/prog-data.js').write_text('// Создано tools/prog-data.py из клиента — не править руками\nwindow.PROG_DATA = '
                                     + json.dumps(data, ensure_ascii=False, separators=(',', ':')) + ';\n', 'utf-8')
print(f'{sum(len(o) for _, o in cats)} команд в {len(cats)} разделах, иконок {len(data["icons"])}')
