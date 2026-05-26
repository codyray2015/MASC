using System.Text;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using YamlDotNet.Serialization;

namespace MASC.Loading;

/// <summary>
/// 读取并解析 OpenAPI 文档(JSON / YAML)。
///
/// 职责分三层:
/// 1. 跨文件组装(本类的 YAML 内联): Microsoft.OpenApi 的内存 reader 不解析跨文件
///    外部 $ref，因此先把形如 `$ref: other.yaml` 的引用递归内联成单一文档文本。
/// 2. YAML -> JSON(YamlDotNet): 用宽松的 YAML 解析归一化(顺带折叠 spec 中偶发的重复
///    key —— SharpYaml 对此会报致命错误)，再转成 JSON。
/// 3. 语义解析(Microsoft.OpenApi): 解析文档、解析文档内 $ref、构建对象模型。
/// </summary>
public static class OpenApiLoader
{
    public static OpenApiDocument Load(string fileName)
    {
        var json = IsYaml(fileName)
            ? YamlToJson(InlineExternalYamlRefs(fileName))
            : File.ReadAllText(fileName);

        var settings = new OpenApiReaderSettings
        {
            // 解析文档内 $ref(被引用对象同时保留 Reference.Id 与已解析内容)。
            ReferenceResolution = ReferenceResolutionSetting.ResolveLocalReferences,
        };

        var document = new OpenApiStringReader(settings).Read(json, out var diagnostic);

        if (diagnostic?.Errors is { Count: > 0 } errors)
        {
            foreach (var error in errors)
            {
                Console.WriteLine($"[WARN] {error}");
            }
        }

        if (document?.Paths is null)
        {
            throw new InvalidOperationException($"无法解析 OpenAPI 文档(无 paths): {fileName}");
        }

        return document;
    }

    /// <summary>用 YamlDotNet 把 YAML 文本转为 JSON(宽松解析，折叠重复 key)。</summary>
    private static string YamlToJson(string yaml)
    {
        var yamlObject = new DeserializerBuilder().Build().Deserialize(new StringReader(yaml));
        return new SerializerBuilder().JsonCompatible().Build().Serialize(yamlObject);
    }

    private static bool IsYaml(string fileName)
        => fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    // 按绝对路径缓存已内联的文件内容，避免重复读取同一被引用文件。
    private static readonly Dictionary<string, string> InlineCache = new();

    private static string InlineExternalYamlRefs(string fileName)
        => InlineFile(Path.GetFullPath(fileName));

    /// <summary>
    /// 递归地把形如 `$ref: other.yaml` 的跨文件引用替换为目标文件内容(保持缩进对齐)，
    /// 支持普通引用与数组元素引用(`- $ref: other.yaml`)两种写法；文档内 `#/...` 引用原样保留。
    /// </summary>
    private static string InlineFile(string absolutePath)
    {
        if (InlineCache.TryGetValue(absolutePath, out var cached))
        {
            return cached;
        }
        // 先占位，避免循环引用时无限递归。
        InlineCache[absolutePath] = string.Empty;

        Console.WriteLine($"Read {absolutePath}");

        var content = File.ReadAllText(absolutePath);
        // 把折叠写法 "$ref: >-\n" 归一为单行，便于逐行处理。
        content = Regex.Replace(content, "\\$ref: >-\n", "$ref:");

        var directory = Path.GetDirectoryName(absolutePath) ?? ".";
        var output = new StringBuilder();

        foreach (var line in content.Split('\n'))
        {
            if (TryGetYamlRef(line, "- $ref:", out var indent, out var refFile))
            {
                AppendInlined(output, directory, refFile, absolutePath, indent, asArrayItem: true);
            }
            else if (TryGetYamlRef(line, "$ref:", out indent, out refFile))
            {
                AppendInlined(output, directory, refFile, absolutePath, indent, asArrayItem: false);
            }
            else
            {
                output.AppendLine(line);
            }
        }

        var result = output.ToString();
        InlineCache[absolutePath] = result;
        return result;
    }

    /// <summary>识别指向 .yaml/.yml 文件的引用行，返回其缩进前缀与目标文件名(去引号)。</summary>
    private static bool TryGetYamlRef(string line, string marker, out string indent, out string refFile)
    {
        indent = string.Empty;
        refFile = string.Empty;

        var markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var value = line[(markerIndex + marker.Length)..].Trim().Trim('\'', '"');
        if (!value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
         && !value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        indent = line[..markerIndex];
        refFile = value;
        return true;
    }

    private static void AppendInlined(
        StringBuilder output, string directory, string refFile, string currentPath, string indent, bool asArrayItem)
    {
        var targetPath = Path.GetFullPath(Path.Combine(directory, refFile));

        // 自引用直接跳过。
        if (targetPath == currentPath)
        {
            return;
        }

        // 引用的文件不存在(常见于不完整的多文件 spec)：警告并跳过，避免整轮崩溃。
        if (!File.Exists(targetPath))
        {
            Console.WriteLine($"[WARN] 引用文件不存在，已跳过: {targetPath}");
            return;
        }

        using var reader = new StringReader(InlineFile(targetPath));

        string? refLine;
        var first = true;
        while ((refLine = reader.ReadLine()) is not null)
        {
            if (asArrayItem)
            {
                // 第一行加 "- " 形成数组元素，其余行多缩进两格以对齐。
                output.AppendLine(first ? indent + "- " + refLine : indent + "  " + refLine);
                first = false;
            }
            else
            {
                output.AppendLine(indent + refLine);
            }
        }
    }
}
