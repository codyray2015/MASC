namespace MASC.Generation;

/// <summary>OpenAPI 标量类型 -> C# 类型的映射。</summary>
public static class TypeMapper
{
    /// <summary>把标量类型映射为 C# 类型名(integer 按 format 区分 int/long)。</summary>
    public static string Scalar(SchemaType type, string? format) => type switch
    {
        SchemaType.String => "string",
        SchemaType.Boolean => "bool",
        SchemaType.Number => "float",
        SchemaType.Integer => format == "int32" ? "int" : "long",
        // 数组 / 对象 / 未知 在标量上下文中退化为 object。
        _ => "object",
    };

    /// <summary>
    /// 为请求参数生成「类型 + getter 表达式」。getter 会从给定的集合表达式解析字符串值。
    /// </summary>
    /// <param name="location">如 "_params.QueryParms"。</param>
    public static (string Type, string Getter) ParameterAccessor(
        SchemaType type, string? format, string location, string rawName)
    {
        var key = $"{location}[\"{rawName}\"]";

        return type switch
        {
            SchemaType.String => ("string", $"=> {key};"),
            SchemaType.Integer => Nullable(format == "int32" ? "int" : "long", key),
            SchemaType.Number => Nullable("float", key),
            SchemaType.Boolean => Nullable("bool", key),
            // 数组/未知类型的参数按逗号分隔字符串处理，保证可编译且可用。
            _ => ("string", $"=> {key};"),
        };

        static (string, string) Nullable(string clrType, string key)
            => ($"{clrType}?", $"{{ return {clrType}.TryParse({key}, out var result) ? result : null; }}");
    }
}
