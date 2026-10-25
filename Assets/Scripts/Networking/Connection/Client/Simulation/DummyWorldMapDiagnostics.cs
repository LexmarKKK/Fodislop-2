#nullable enable

using System;
using System.IO;

namespace MinesServer.Networking.Connection.Client;

/// <summary>Состояние отметки кэша относительно источника карты.</summary>
public enum DummyMapCacheStampState
{
    /// <summary>Отметка совпадает с источником — кэш распакован из текущего архива.</summary>
    Current,
    /// <summary>Источник обновился — при следующем входе карта распакуется заново.</summary>
    Stale,
    /// <summary>Сравнить не с чем: источник недоступен файлово или кэша ещё нет.</summary>
    Unknown,
}

/// <summary>
/// Снимок локального состояния офлайн-карты: источник в StreamingAssets
/// (сырая карта или zip-архив) и распакованный кэш в persistentDataPath.
/// </summary>
public sealed record DummyMapCacheSnapshot(
    string WorldCodeName,
    /// <summary>Источник виден как обычный файл (десктоп/редактор); на упакованных платформах — false.</summary>
    bool SourcePresent,
    /// <summary>Источник едет внутри пакета: сверить можно только отметку кэша по buildGUID.</summary>
    bool SourceInsidePackage,
    string? SourcePath,
    long SourceBytes,
    DummyMapCacheStampState Stamp,
    bool CachedMapPresent,
    string? CachedMapPath,
    long CachedMapBytes,
    /// <summary>Заголовок и таблица смещений кэша читаются и валидны.</summary>
    bool HeaderValid,
    /// <summary>Заголовок испорчен (невалидные размеры/версия/смещения). false при IO-сбое чтения — тот файл занят, и это не вердикт о порче.</summary>
    bool HeaderCorrupted,
    int WorldWidthCells,
    int WorldHeightCells);

/// <summary>
/// Снимок для окна диагностики главного меню: чистое файловое чтение без
/// побочных эффектов — в отличие от <see cref="DummyWorldMapArchive.ResolveMapFileAsync"/>
/// ничего не распаковывает и не пишет. Форматы отметок должны совпадать с теми,
/// что пишет сам архив при распаковке; связку сторожат тесты
/// <c>MenuFileDiagnosticsTests</c>.
/// </summary>
public static class DummyWorldMapDiagnostics
{
    /// <summary>Штамп zip-архива — формат из DummyWorldMapArchive.ResolveFromLocalStreamingAssets.</summary>
    internal static string BuildArchiveStamp(string archivePath)
    {
        var archive = new FileInfo(archivePath);
        return $"file:{archive.Length}:{archive.LastWriteTimeUtc.Ticks}";
    }

    /// <summary>Штамп сырой карты из StreamingAssets — формат из DummyWorldMapArchive.CopyLocalMapToCache.</summary>
    internal static string BuildRawMapStamp(string rawMapPath)
    {
        var source = new FileInfo(rawMapPath);
        return $"local:{source.FullName}:{source.Length}:{source.LastWriteTimeUtc.Ticks}";
    }

    /// <summary>
    /// Инспектирует состояние офлайн-карты <paramref name="worldCodeName"/>.
    /// <paramref name="packagedSourceStamp"/> — штамп источника на платформах,
    /// где StreamingAssets недоступны файлово ($"package:{buildGUID}").
    /// </summary>
    public static DummyMapCacheSnapshot InspectLocalCache(
        string worldCodeName,
        string streamingRoot,
        string persistentRoot,
        string? packagedSourceStamp = null)
    {
        string streamingDirectory = Path.Combine(streamingRoot, DummyWorldMapArchive.StreamingFolderName);
        string cacheDirectory = Path.Combine(persistentRoot, DummyWorldMapArchive.CacheFolderName);
        string cachedMapPath = Path.Combine(cacheDirectory, DummyWorldMapArchive.MapFileName(worldCodeName));

        string? sourcePath = null;
        long sourceBytes = 0;
        string? sourceStamp = packagedSourceStamp;
        bool sourceInsidePackage = packagedSourceStamp != null;

        string rawMapPath = Path.Combine(streamingDirectory, DummyWorldMapArchive.MapFileName(worldCodeName));
        string archivePath = Path.Combine(streamingDirectory, DummyWorldMapArchive.ArchiveFileName(worldCodeName));
        if (File.Exists(rawMapPath))
        {
            sourcePath = rawMapPath;
            sourceBytes = new FileInfo(rawMapPath).Length;
            sourceStamp = BuildRawMapStamp(rawMapPath);
        }
        else if (File.Exists(archivePath))
        {
            sourcePath = archivePath;
            sourceBytes = new FileInfo(archivePath).Length;
            sourceStamp = BuildArchiveStamp(archivePath);
        }

        bool cachedMapPresent = File.Exists(cachedMapPath);
        long cachedMapBytes = cachedMapPresent ? new FileInfo(cachedMapPath).Length : 0;

        var stamp = DummyMapCacheStampState.Unknown;
        if (cachedMapPresent && sourceStamp != null)
        {
            stamp = DummyWorldMapArchive.IsCacheCurrent(cachedMapPath, sourceStamp)
                ? DummyMapCacheStampState.Current
                : DummyMapCacheStampState.Stale;
        }

        bool headerValid = false;
        bool headerCorrupted = false;
        int worldWidthCells = 0;
        int worldHeightCells = 0;
        if (cachedMapPresent)
        {
            try
            {
                (worldWidthCells, worldHeightCells) = DummyWorldMapArchive.ReadDimensions(cachedMapPath);
                headerValid = true;
            }
            catch (InvalidDataException exception)
            {
                // Та же семантика, что у HasValidHeader: IO-сбой чтения (файл
                // занят соседним процессом) о содержимом ничего не говорит,
                // всё остальное — повреждение.
                headerCorrupted = exception.InnerException is not (IOException and not EndOfStreamException);
            }
        }

        return new DummyMapCacheSnapshot(
            worldCodeName,
            SourcePresent: sourcePath != null,
            SourceInsidePackage: sourceInsidePackage,
            SourcePath: sourcePath,
            SourceBytes: sourceBytes,
            Stamp: stamp,
            CachedMapPresent: cachedMapPresent,
            CachedMapPath: cachedMapPresent ? cachedMapPath : null,
            CachedMapBytes: cachedMapBytes,
            HeaderValid: headerValid,
            HeaderCorrupted: headerCorrupted,
            WorldWidthCells: worldWidthCells,
            WorldHeightCells: worldHeightCells);
    }
}
