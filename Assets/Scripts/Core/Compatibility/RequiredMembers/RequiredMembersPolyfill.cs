#nullable enable

// Полифилл C# 11+ required-членов: Unity-профиль BCL (netstandard 2.1) не содержит
// служебных атрибутов компилятора, и csc отвечает CS0656 на любой `required`
// (синтаксис при этом принимается). Атрибуты — компиляционные маркеры: в рантайме
// Mono/IL2CPP они игнорируются. Если будущая версия Unity привезёт настоящие
// определения, этот файл удалит дублирующее определение ошибкой сборки — тогда
// просто удалить файл целиком.

namespace System.Runtime.CompilerServices;

/// <summary>Маркер required-члена; испускается компилятором на каждом <c>required</c> поле/свойстве.</summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class RequiredMemberAttribute : Attribute
{
}

/// <summary>Помечает типы, которым нужна поддержка языковой возможности (например, RequiredMembers).</summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class CompilerFeatureRequiredAttribute : Attribute
{
    public CompilerFeatureRequiredAttribute(string featureName)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }
}

/// <summary>Конструктор, инициализирующий все required-члены и снимающий требование объектного инициализатора.</summary>
[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
public sealed class SetsRequiredMembersAttribute : Attribute
{
}
