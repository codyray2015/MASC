using System.Globalization;
using System.Text.RegularExpressions;

namespace MASC.Generation;

/// <summary>命名约定相关的纯函数：标识符清洗、路径转类名、引用名提取等。</summary>
public static class Naming
{
    private static readonly TextInfo TextInfo = CultureInfo.InvariantCulture.TextInfo;

    /// <summary>首字母大写(其余保持)。</summary>
    public static string TitleCase(string value) => TextInfo.ToTitleCase(value);

    /// <summary>把任意字符串清洗为合法 C# 标识符(非法字符转 '_'，数字开头补前缀)。</summary>
    public static string SafeIdentifier(string name)
    {
        var cleaned = Regex.Replace(name, "[^A-Za-z0-9_]", "_");
        if (cleaned.Length == 0)
        {
            return "_";
        }
        return char.IsDigit(cleaned[0]) ? "_" + cleaned : cleaned;
    }

    /// <summary>把可能含 '.'、'-' 的名字转换为合法 C# 标识符(PascalCase)。</summary>
    public static string Identifier(string name)
        => SafeIdentifier(TitleCase(name.Replace(".", "_").Replace("-", "_")));

    /// <summary>从 "#/components/schemas/Pet" 这样的引用取末段名(清洗为合法标识符)。</summary>
    public static string RefName(string source) => SafeIdentifier(TitleCase(source.Split('/').Last()));

    /// <summary>路径首段决定外层 partial 类名，如 "/pet/..." -> "Pet"。</summary>
    public static string PartialClassName(IReadOnlyList<string> segments)
        => SafeIdentifier(TitleCase(segments[0].Replace("-", string.Empty)));

    /// <summary>
    /// 由路径各段拼出操作名(去掉 '-' 与花括号)，并去除与 partial 类名重复的前缀。
    /// 例: 路径 "/pet/{petId}"、partial "Pet" -> "Petid"。
    /// </summary>
    public static string OperationName(IReadOnlyList<string> segments, string partialClassName)
    {
        var joined = string.Concat(segments.Select(part =>
            TitleCase(part.Replace("-", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty))));

        if (joined.Length > partialClassName.Length && joined.StartsWith(partialClassName, StringComparison.Ordinal))
        {
            joined = joined[partialClassName.Length..];
        }

        return SafeIdentifier(joined);
    }

    /// <summary>把路径拆成非空段。</summary>
    public static IReadOnlyList<string> PathSegments(string apiPath)
        => apiPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
