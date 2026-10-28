#nullable enable

using System;
using UnityEditor;

namespace Kern.Editor;

// Проекты MinesServerNetworking подключены как локальные пакеты
// (Packages/manifest.json: file:../../MinesServer/...), и Unity импортирует
// эти папки целиком: игнорируются только имена с суффиксом "~" и скрытые.
// Рабочая конвенция репозитория выводит сборку в bin~/obj~, но внешние
// проверочные сборки пишут в обычные bin/obj (например,
// -p:OutputPath=bin\agentcheck). Попавшие туда DLL Unity импортирует как
// плагины с Auto Reference, и компиляция клиента падает двойным импортом
// тех же сборок: CS1704 + масса CS0436.
//
// Удалять чужой вывод сборки нельзя, а перехватить -p:OutputPath со стороны
// MSBuild невозможно (глобальное свойство). Поэтому просто исключаем такие
// плагины из компиляции всех платформ: на диске они остаются, проверочную
// сборку не ломаем, в компиляцию клиента они не попадают. В bin/obj пакета
// легитимных плагинов не бывает, ложных срабатываний нет.
internal sealed class StrayPackagePluginExcluder : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths,
        bool didDomainReload)
    {
        foreach (string assetPath in importedAssets)
        {
            if (!assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                !IsStrayBuildOutput(assetPath))
            {
                continue;
            }

            if (AssetImporter.GetAtPath(assetPath) is PluginImporter importer &&
                importer.GetCompatibleWithAnyPlatform())
            {
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SaveAndReimport();
            }
        }
    }

    private static bool IsStrayBuildOutput(string assetPath)
    {
        string path = assetPath.Replace('\\', '/');

        // Локальные пакеты адресуются как Packages/<id>; на случай прямых
        // файловых путей допускаем и имя дерева MinesServerNetworking.
        bool inScope = path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("MinesServerNetworking", StringComparison.OrdinalIgnoreCase);
        if (!inScope)
        {
            return false;
        }

        return path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }
}
