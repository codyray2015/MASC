using Microsoft.OpenApi.Models;

namespace MASC.Generation;

/// <summary>
/// 把 schema 写成 C# 数据类(类体 + 字段)的共享逻辑，供请求体类与共享模型类复用。
/// 遇到 $ref 字段时登记到 <see cref="ModelRegistry"/> 并按模型名引用(不在此就地展开)；
/// 内联对象/数组元素则作为嵌套类就地生成。
/// </summary>
public class TypeEmitter
{
    private readonly CSharpWriter _w;
    private readonly ModelRegistry _models;

    public TypeEmitter(CSharpWriter writer, ModelRegistry models)
    {
        _w = writer;
        _models = models;
    }

    /// <summary>
    /// 写一个数据类。<paramref name="schema"/> 为 null 时生成空类(仅用于 List 派生壳类)。
    /// </summary>
    public void WriteClass(string className, OpenApiSchema? schema, string? baseType = null)
    {
        var header = baseType is null
            ? $"public class {className}"
            : $"public class {className} : {baseType}";

        _w.OpenBlock(header);
        if (schema?.Properties is not null)
        {
            foreach (var (name, propSchema) in schema.Properties)
            {
                WriteField(propSchema, name, className);
            }
        }
        _w.CloseBlock();
    }

    private void WriteField(OpenApiSchema schema, string jsonName, string enclosingClass)
    {
        _w.Summary(schema.Description);
        _w.Line($"[JsonProperty(\"{jsonName}\")]");

        var property = Naming.Identifier(jsonName);

        // C# 不允许成员与外层类型同名；JsonProperty 已保留线上名，可安全改名。
        if (property == enclosingClass)
        {
            property += "_";
        }

        // 引用($ref)优先于 type 判断：引用类型登记为共享模型，按模型名引用。
        if (schema.IsReference())
        {
            var model = _models.Register(schema);
            _w.Line($"public {model} {property} {{ get; set; }}");
            return;
        }

        switch (schema.Kind())
        {
            case SchemaType.String when schema.Format == "binary":
                _w.Line($"public Stream {property} {{ get; set; }}");
                break;
            case SchemaType.String:
                _w.Line($"public string {property} {{ get; set; }}");
                break;
            case SchemaType.Integer:
            case SchemaType.Number:
            case SchemaType.Boolean:
                _w.Line($"public {TypeMapper.Scalar(schema.Kind(), schema.Format)} {property} {{ get; set; }}");
                break;
            case SchemaType.Object:
                // 内联对象作为嵌套类，名字加 Object 后缀以避免与属性同名。
                var objectClass = property + "Object";
                _w.Line($"public {objectClass} {property} {{ get; set; }}");
                WriteClass(objectClass, schema);
                break;
            case SchemaType.Array:
                WriteArrayField(schema, property);
                break;
            default:
                _w.Line($"public object {property} {{ get; set; }}");
                break;
        }
    }

    private void WriteArrayField(OpenApiSchema schema, string property)
    {
        var items = schema.Items;
        if (items is null)
        {
            _w.Line($"public object {property} {{ get; set; }}");
            return;
        }

        if (items.IsReference())
        {
            var model = _models.Register(items);
            _w.Line($"public List<{model}> {property} {{ get; set; }}");
        }
        else if (items.Kind() == SchemaType.Object)
        {
            // 内联对象元素作为嵌套类。
            var itemClass = property + "Item";
            _w.Line($"public List<{itemClass}> {property} {{ get; set; }}");
            WriteClass(itemClass, items);
        }
        else
        {
            _w.Line($"public List<{TypeMapper.Scalar(items.Kind(), items.Format)}> {property} {{ get; set; }}");
        }
    }
}
