using Microsoft.OpenApi.Models;

namespace MASC.Generation;

/// <summary>生成器使用的 schema 分类(由 OpenAPI 的 type 字符串归一而来)。</summary>
public enum SchemaType
{
    Unknown,
    String,
    Integer,
    Number,
    Boolean,
    Array,
    Object,
}

/// <summary>把 Microsoft.OpenApi 模型适配为生成器友好的便捷访问。</summary>
public static class OpenApiExtensions
{
    private const string Json = "application/json";
    private const string FormData = "multipart/form-data";

    public static SchemaType Kind(this OpenApiSchema schema) => schema.Type?.ToLowerInvariant() switch
    {
        "string" => SchemaType.String,
        "integer" => SchemaType.Integer,
        "number" => SchemaType.Number,
        "boolean" => SchemaType.Boolean,
        "array" => SchemaType.Array,
        "object" => SchemaType.Object,
        _ => SchemaType.Unknown,
    };

    /// <summary>该 schema 是否来自 $ref(库已解析，但仍保留引用标识)。</summary>
    public static bool IsReference(this OpenApiSchema schema) => schema.Reference is not null;

    /// <summary>引用名(如 "Pet")，非引用时为 null。</summary>
    public static string? RefId(this OpenApiSchema schema) => schema.Reference?.Id;

    /// <summary>请求体 schema(仅取 application/json 或 multipart/form-data)。</summary>
    public static OpenApiSchema? BodySchema(this OpenApiOperation op)
    {
        var content = op.RequestBody?.Content;
        if (content is null) return null;
        if (content.TryGetValue(Json, out var json)) return json.Schema;
        if (content.TryGetValue(FormData, out var form)) return form.Schema;
        return null;
    }

    /// <summary>请求体对应的 Content-Type，无可识别请求体时为 null。</summary>
    public static string? BodyContentType(this OpenApiOperation op)
    {
        var content = op.RequestBody?.Content;
        if (content is null) return null;
        if (content.ContainsKey(Json)) return Json;
        if (content.ContainsKey(FormData)) return FormData;
        return null;
    }

    /// <summary>声明了 requestBody，但其 Content-Type 不被支持(如 octet-stream)。</summary>
    public static bool HasUnsupportedBody(this OpenApiOperation op)
        => op.RequestBody?.Content is { Count: > 0 } && op.BodyContentType() is null;
}
