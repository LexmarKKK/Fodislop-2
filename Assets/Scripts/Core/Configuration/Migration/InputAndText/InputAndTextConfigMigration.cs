#nullable enable

using System;

namespace Kern.Core.Configuration;

internal static class InputAndTextConfigMigration
{
    public static void Apply(InterfaceSettings settings, string sourceJson)
    {
        // A saved action can already own a newly configurable movement key.
        // Preserve that action; leave the conflicting new binding unassigned.
        foreach (System.Reflection.FieldInfo field in typeof(InterfaceSettings).GetFields())
        {
            if (field.IsStatic || field.FieldType != typeof(string) ||
                !(field.Name.StartsWith("KeyMove", StringComparison.Ordinal) ||
                  field.Name.StartsWith("KeyTurn", StringComparison.Ordinal) ||
                  field.Name.StartsWith("KeyFast", StringComparison.Ordinal)) ||
                sourceJson.Contains("\"" + field.Name + "\"", StringComparison.Ordinal))
            {
                continue;
            }

            string key = (string)field.GetValue(settings)!;
            foreach (System.Reflection.FieldInfo existing in typeof(InterfaceSettings).GetFields())
            {
                if (!existing.IsStatic && existing.FieldType == typeof(string) &&
                    existing.Name.StartsWith("Key", StringComparison.Ordinal) &&
                    sourceJson.Contains("\"" + existing.Name + "\"", StringComparison.Ordinal) &&
                    string.Equals((string?)existing.GetValue(settings), key, StringComparison.OrdinalIgnoreCase))
                {
                    field.SetValue(settings, "None");
                    break;
                }
            }
        }
    }
}
