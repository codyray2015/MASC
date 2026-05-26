using Microsoft.OpenApi.Models;

namespace MASC.Generation;

/// <summary>一次生成产出的单个文件。</summary>
public record GeneratedFile(string PartialClassName, string ApiName, string Code);

/// <summary>
/// 根据单个 <see cref="OpenApiPathItem"/> 生成对应的请求类代码。
/// $ref 已由 Microsoft.OpenApi 解析；被引用的数据模型登记到共享的 <see cref="ModelRegistry"/>，
/// 由其统一生成到 <c>{namespace}.Models</c>，请求文件通过 using 引用，避免跨文件重复定义。
/// </summary>
public class RequestGenerator
{
    private readonly string _namespace;
    private readonly string _modelsNamespace;
    private readonly HashSet<string> _ignoreParams;
    private readonly ModelRegistry _models;

    private CSharpWriter _w = new();
    private TypeEmitter _emitter;

    private static readonly (OperationType Type, string HttpMethod, string Title)[] Methods =
    {
        (OperationType.Post, "POST", "Post"),
        (OperationType.Put, "PUT", "Put"),
        (OperationType.Patch, "PATCH", "Patch"),
        (OperationType.Get, "GET", "Get"),
        (OperationType.Delete, "DELETE", "Delete"),
    };

    public RequestGenerator(string @namespace, string modelsNamespace, string ignoreParams, ModelRegistry models)
    {
        _namespace = @namespace;
        _modelsNamespace = modelsNamespace;
        _models = models;
        _emitter = new TypeEmitter(_w, models);
        _ignoreParams = (ignoreParams ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToHashSet();
    }

    /// <summary>为一个路径生成代码。<paramref name="requestedPartialName"/> 为空时由路径首段推导。</summary>
    public GeneratedFile Generate(OpenApiPathItem path, string apiPath, string requestedPartialName)
    {
        Console.WriteLine(apiPath);

        var segments = Naming.PathSegments(apiPath);
        var partial = string.IsNullOrEmpty(requestedPartialName)
            ? Naming.PartialClassName(segments)
            : Naming.SafeIdentifier(requestedPartialName);
        var apiName = Naming.OperationName(segments, partial);

        _w = new CSharpWriter();
        _emitter = new TypeEmitter(_w, _models);

        WriteFileHeader();
        _w.OpenBlock($"namespace {_namespace}.Request");
        _w.OpenBlock($"public partial class {partial}");

        foreach (var (type, httpMethod, title) in Methods)
        {
            if (path.Operations.TryGetValue(type, out var op))
            {
                WriteOperation(op, httpMethod, title, partial, apiName, apiPath);
            }
        }

        _w.CloseBlock(); // partial class
        _w.CloseBlock(); // namespace

        return new GeneratedFile(partial, apiName, _w.ToString());
    }

    private void WriteFileHeader()
    {
        _w.Line("#nullable disable");
        _w.Line("#pragma warning disable CS0612");
        _w.Line("using System;");
        _w.Line("using System.IO;");
        _w.Line("using System.Net.Http;");
        _w.Line("using Newtonsoft.Json;");
        _w.Line("using System.Collections.Generic;");
        _w.Line("using System.Collections.Specialized;");
        _w.Line($"using {_modelsNamespace};");
        _w.Line();
    }

    private void WriteOperation(
        OpenApiOperation op, string httpMethod, string methodTitle, string partial, string apiName, string apiPath)
    {
        var operationName = apiName + methodTitle;

        var body = op.BodySchema();

        // oneOf 请求体走独立分支(生成一组变体请求类)。
        if (body?.OneOf is { Count: > 0 })
        {
            WriteOneOf(op, body, operationName, partial, apiPath, httpMethod);
            return;
        }

        var contentTypeLiteral = ContentTypeLiteral(op.BodyContentType());
        var needsParams = NeedsQueryParams(op.Parameters);

        // 计算请求基类，并准备(在请求类之后)生成的 body 类。
        var (bodyBase, bodyClassAction) = ResolveBody(body, operationName);

        var bases = new List<string>();
        if (bodyBase is not null) bases.Add(bodyBase);
        bases.Add("IApiRequest");
        if (needsParams) bases.Add("IApiRequestQueryParams");

        _w.Summary(op.Summary);
        _w.OpenBlock($"public class {operationName}Request : {string.Join(", ", bases)}");

        WriteEntryPoint(partial, operationName, httpMethod, apiPath, contentTypeLiteral);

        if (op.HasUnsupportedBody())
        {
            _w.Line("// 注意: 该请求体的 Content-Type 不受支持(如 application/octet-stream)，已忽略请求体。");
        }

        if (needsParams)
        {
            WriteParameters(op.Parameters, operationName);
        }

        _w.CloseBlock(); // request class

        bodyClassAction?.Invoke();
    }

    /// <summary>
    /// 解析请求体，返回(请求基类名, 在请求类之后生成 body 类的动作)。
    /// - $ref 对象: 直接继承共享模型，无需额外 body 类。
    /// - 数组: 生成 List 派生壳类(元素为共享模型或内联类)。
    /// - 内联对象: 生成与操作同名的 body 类。
    /// </summary>
    private (string? Base, Action? Emit) ResolveBody(OpenApiSchema? body, string operationName)
    {
        if (body is null)
        {
            return (null, null);
        }

        if (body.IsReference())
        {
            return (_models.Register(body), null);
        }

        if (body.Kind() == SchemaType.Array)
        {
            var items = body.Items;
            if (items is { } && items.IsReference())
            {
                var model = _models.Register(items);
                return (operationName, () => _emitter.WriteClass(operationName, null, $"List<{model}>"));
            }

            var itemClass = operationName + "Item";
            return (operationName, () =>
            {
                _emitter.WriteClass(operationName, null, $"List<{itemClass}>");
                _emitter.WriteClass(itemClass, items);
            });
        }

        return (operationName, () => _emitter.WriteClass(operationName, body));
    }

    /// <summary>写入 ApiEntryPointInfo 静态描述符与对应属性。</summary>
    private void WriteEntryPoint(
        string partial, string operationName, string httpMethod, string apiPath, string contentTypeLiteral)
    {
        var field = $"{partial}_{operationName}Request";
        _w.Line($"private static readonly ApiEntryPointInfo {field} = new ApiEntryPointInfo(new HttpMethod(\"{httpMethod}\"), \"{apiPath}\", {contentTypeLiteral});");
        _w.Line("public ApiEntryPointInfo ApiEntryPointInfo { get => _apiEntryPointInfo; set => _apiEntryPointInfo = value; }");
        _w.Line($"private ApiEntryPointInfo _apiEntryPointInfo = {field};");
    }

    private void WriteOneOf(
        OpenApiOperation op, OpenApiSchema body, string operationName, string partial, string apiPath, string httpMethod)
    {
        var contentTypeLiteral = ContentTypeLiteral(op.BodyContentType());
        var field = $"{partial}_{operationName}Request";

        // 静态容器类只持有共享的 ApiEntryPointInfo 静态字段；实例成员放在各变体类内。
        _w.OpenBlock($"public static class {operationName}Requests");
        _w.Line($"private static readonly ApiEntryPointInfo {field} = new ApiEntryPointInfo(new HttpMethod(\"{httpMethod}\"), \"{apiPath}\", {contentTypeLiteral});");

        foreach (var variant in body.OneOf)
        {
            if (variant.RefId() is null) continue;

            var model = _models.Register(variant);
            var baseType = variant.Kind() == SchemaType.Array ? $"List<{model}>" : model;

            _w.OpenBlock($"public class {model}Request : {baseType}, IApiRequest");
            _w.Line("public ApiEntryPointInfo ApiEntryPointInfo { get => _apiEntryPointInfo; set => _apiEntryPointInfo = value; }");
            _w.Line($"private ApiEntryPointInfo _apiEntryPointInfo = {field};");

            if (NeedsQueryParams(op.Parameters))
            {
                WriteParameters(op.Parameters, model);
            }

            _w.CloseBlock();
        }

        _w.CloseBlock();
    }

    /// <summary>生成参数包装器(QueryParms 内部类、Parameters 字段、path 参数构造函数)。</summary>
    /// <param name="typeBaseName">用于命名嵌套类与请求构造函数的基名。</param>
    private void WriteParameters(IList<OpenApiParameter> parameters, string typeBaseName)
    {
        var containsQuery = parameters.Any(p => Location(p) == ParameterLocation.Query);
        var containsHeader = parameters.Any(p => Location(p) == ParameterLocation.Header);
        var containsPath = parameters.Any(p => Location(p) == ParameterLocation.Path);

        _w.Line($"private NameValueCollection _query = {Collection(containsQuery)};");
        _w.Line($"private NameValueCollection _header = {Collection(containsHeader)};");
        _w.Line($"private NameValueCollection _path = {Collection(containsPath)};");

        _w.OpenBlock($"public class {typeBaseName}QueryParms");
        _w.Line("private readonly IApiRequestQueryParams _params;");
        _w.OpenBlock($"public {typeBaseName}QueryParms(IApiRequestQueryParams queryParams)");
        _w.Line("_params = queryParams;");
        _w.CloseBlock();

        var ctorArgs = new List<string>();
        var ctorAssignments = new List<string>();

        foreach (var p in parameters)
        {
            var location = Location(p);
            if (location != ParameterLocation.Path && _ignoreParams.Contains(p.Name))
            {
                continue;
            }

            var collection = location switch
            {
                ParameterLocation.Header => "_params.HeaderParms",
                ParameterLocation.Path => "_params.PathParms",
                _ => "_params.QueryParms",
            };

            var kind = p.Schema?.Kind() ?? SchemaType.String;
            var (type, getter) = TypeMapper.ParameterAccessor(kind, p.Schema?.Format, collection, p.Name);
            var property = Naming.Identifier(p.Name);

            if (location == ParameterLocation.Path)
            {
                var argVar = p.Name.Replace(".", "_").Replace("-", "_");
                ctorArgs.Add($"{type} {argVar}");
                ctorAssignments.Add($"Parameters.{property} = {argVar};");
            }

            _w.Summary(p.Description);
            _w.Line($"public {type} {property} {{ get {getter} set => {collection}[\"{p.Name}\"] = value?.ToString(); }}");
        }

        _w.CloseBlock(); // QueryParms class

        _w.Line("[JsonIgnore]");
        _w.Line($"public readonly {typeBaseName}QueryParms Parameters;");
        WriteFrameworkOnlyAccessor("QueryParms", "_query");
        WriteFrameworkOnlyAccessor("HeaderParms", "_header");
        WriteFrameworkOnlyAccessor("PathParms", "_path");

        _w.OpenBlock($"public {typeBaseName}Request({string.Join(", ", ctorArgs)})");
        _w.Line($"Parameters = new {typeBaseName}QueryParms(this);");
        foreach (var assignment in ctorAssignments)
        {
            _w.Line(assignment);
        }
        _w.CloseBlock();

        static string Collection(bool present)
            => present ? "System.Web.HttpUtility.ParseQueryString(string.Empty)" : "default";
    }

    private void WriteFrameworkOnlyAccessor(string name, string backingField)
    {
        _w.Line("[Obsolete(\"this field only for framework\")]");
        _w.Line($"public NameValueCollection {name} => {backingField};");
    }

    /// <summary>是否需要实现 IApiRequestQueryParams(存在非全部被忽略、或含 path 参数)。</summary>
    private bool NeedsQueryParams(IList<OpenApiParameter>? parameters)
    {
        if (parameters is not { Count: > 0 })
        {
            return false;
        }

        var hasPath = parameters.Any(p => Location(p) == ParameterLocation.Path);
        var allIgnored = parameters.All(p => _ignoreParams.Contains(p.Name));

        return !(allIgnored && !hasPath);
    }

    private static ParameterLocation Location(OpenApiParameter parameter)
        => parameter.In ?? ParameterLocation.Query;

    private static string ContentTypeLiteral(string? contentType) => contentType switch
    {
        "application/json" => "\"application/json\"",
        "multipart/form-data" => "\"multipart/form-data\"",
        _ => "null",
    };
}
