#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kern.Core;

/// <summary>
/// Контракт окна «Диагностика и целостность файлов» главного меню: банки FMOD,
/// дисковый кэш офлайн-карты, коллекция шейдерных состояний. Чистая диагностика —
/// ничего не пишет и не чинит; отчёт кэшируется сервисом на короткое время,
/// чтобы переоткрытия модалки не пересчитывали контрольные суммы зря.
/// </summary>
public enum MenuFileCheckStatus
{
    Ok,
    Warning,
    Failed,
}

/// <summary>Результат одной проверки: ключ локализации строки, статус и техническая деталь.</summary>
public sealed record MenuFileCheckResult(string LabelKey, MenuFileCheckStatus Status, string Detail);

/// <summary>Отчёт всех проверок окна; AllOk — ни одной не хуже Ok.</summary>
public sealed record MenuFileDiagnosticsReport(IReadOnlyList<MenuFileCheckResult> Checks, bool AllOk);

public interface IMenuFileDiagnosticsService
{
    /// <summary>Ключи локализации проверок; порядок = порядок строк в окне.</summary>
    IReadOnlyList<string> CheckLabelKeys { get; }

    /// <summary>
    /// Прогоняет все проверки. Колбэки получают индекс активной проверки и долю
    /// прогресса (0..1); forceRefresh отключает короткий кэш отчёта.
    /// </summary>
    UniTask<MenuFileDiagnosticsReport> RunAllAsync(
        Action<int>? activeCheck,
        Action<float>? fraction,
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}
