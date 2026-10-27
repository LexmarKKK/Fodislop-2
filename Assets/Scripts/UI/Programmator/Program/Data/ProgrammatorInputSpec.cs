#nullable enable

using System.Collections.Generic;
using MinesServer.Data;

namespace Kern.UI.Programmator;

// Каталог операторов, принимающих вводимое значение — перенос старого
// клиента (ProgAction.updateInput): там у ячейки были два встроенных поля,
// текстовое input и числовое numInput, показываемые в зависимости от id.
// Ординалы ProgAction совпадают со старыми id один к одному.
//
// Текстовое поле в новом клиенте пишется в ProgrammatorData.Labels (имя
// метки/переменной), числовое — в Values строкой. Сеть и файл программ уже
// переносят обе колонки на каждую инструкцию.
internal static class ProgrammatorInputSpec
{
    internal enum InputKind
    {
        // Только текст: имя метки для переходов и Label, имя отслеживаемой
        // переменной для отладочных операторов.
        Text,

        // Текст (имя переменной) + число для сравнений с константой
        // (старые VAR_MORE/VAR_LESS/VAR_EQUAL: input + numInput).
        TextAndNumber,
    }

    internal readonly struct Spec(InputKind kind, int textMaxLength)
    {
        internal InputKind Kind { get; } = kind;

        // Старый клиент урезал текст до 3 символов при компиляции
        // (119/120/123/181/182); метки переходов не ограничивал. Здесь лимит
        // на вводе: 3 для переменных, у меток оставлен разумный потолок.
        internal int TextMaxLength { get; } = textMaxLength;
    }

    private static readonly IReadOnlyDictionary<ProgAction, Spec> s_specs =
        new Dictionary<ProgAction, Spec>
        {
            // Переходы/вызовы: имя метки (старые id 24, 25, 26, 137, 139, 140, 166).
            [ProgAction.Goto] = new(InputKind.Text, 24),
            [ProgAction.Call] = new(InputKind.Text, 24),
            [ProgAction.CallArg] = new(InputKind.Text, 24),
            [ProgAction.CallState] = new(InputKind.Text, 24),
            [ProgAction.YesNoGoto] = new(InputKind.Text, 24),
            [ProgAction.NoYesGoto] = new(InputKind.Text, 24),
            [ProgAction.CallWhenDied] = new(InputKind.Text, 24),

            // Определение метки (старый id 40).
            [ProgAction.Label] = new(InputKind.Text, 24),

            // Сравнение переменной с числом (старые id 119, 120, 123):
            // input — имя переменной до 3 символов, numInput — число.
            [ProgAction.VarGreaterThanNumber] = new(InputKind.TextAndNumber, 3),
            [ProgAction.VarLessThanNumber] = new(InputKind.TextAndNumber, 3),
            [ProgAction.VarEqualsNumber] = new(InputKind.TextAndNumber, 3),

            // Отладка (старые id 181, 182): имя переменной до 3 символов.
            [ProgAction.DebugPause] = new(InputKind.Text, 3),
            [ProgAction.DebugShow] = new(InputKind.Text, 3),
        };

    internal static bool TryGet(ProgAction action, out Spec spec)
    {
        return s_specs.TryGetValue(action, out spec);
    }
}
