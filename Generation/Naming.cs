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

    /// <summary>
    /// 按分隔符切词，每段大写首字母并保留其余字符的大小写——避免 ToTitleCase 把内部 CamelCase 拍平。
    /// 例: "last_id" -> "LastId"，"productImportProductsBySKURequestItem" -> "ProductImportProductsBySKURequestItem"，
    /// "actions.v1.ActionsAutoAddProductsCandidatesRequest" -> "ActionsV1ActionsAutoAddProductsCandidatesRequest"。
    /// </summary>
    private static string PascalJoin(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var parts = name.Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part, 1, part.Length - 1);
        }
        return sb.ToString();
    }

    /// <summary>把 JSON 属性名/参数名转换为合法的 PascalCase C# 标识符(保留 CamelCase)。</summary>
    public static string Identifier(string name)
        => SafeIdentifier(PascalJoin(name ?? string.Empty));

    /// <summary>从 "#/components/schemas/Pet" 这样的引用取末段名(清洗为合法标识符；保留 CamelCase)。</summary>
    public static string RefName(string source) => SafeIdentifier(PascalJoin(source.Split('/').Last()));

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
