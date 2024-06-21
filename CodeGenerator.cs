// See https://aka.ms/new-console-template for more information

using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public class CodeGenerator
{
    private StringBuilder _sb;
    private HashSet<string> _generatedClasses;
    private readonly Dictionary<string, SchemaModule> _gloablSchemas;
    private readonly string _namespac;

    private readonly string[] _ignoreParams;

    public CodeGenerator(Dictionary<string, SchemaModule> schemas, string namespac, string ignoreParams)
    {
        _sb = new StringBuilder();
        _generatedClasses = new HashSet<string>();
        _gloablSchemas = schemas;
        this._namespac = namespac;

        _ignoreParams = ignoreParams?.Split(',').ToArray() ?? Array.Empty<string>();
    }


    internal string? GenerateCode(PathsModule value, string apiPath, out string apiName, ref string constName)
    {
        Console.WriteLine(apiPath);
        _sb.Clear();
        _generatedClasses.Clear();

        var parts = apiPath.Split('/');
        var start = string.IsNullOrEmpty(parts[0]) ? 1 : 0;


        if (string.IsNullOrEmpty(constName))
        {
            constName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parts[start].Replace("-", ""));
        }

        var processedParts = parts.Skip(start).Select(part => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part.Replace("-", "").Replace("{", "").Replace("}", "")));
        apiName = String.Concat(processedParts);

        if (apiName.Length > constName.Length && apiName.Substring(0, constName.Length) == constName)
        {
            apiName = apiName.Substring(constName.Length);
        }

        var schema = value.Post?.Schema ?? value.Get?.Schema;

        _sb.AppendLine("#nullable disable");
        _sb.AppendLine("#pragma warning disable CS0612");
        _sb.AppendLine("using System;");
        _sb.AppendLine("using System.Net.Http;");
        _sb.AppendLine("using Newtonsoft.Json;");
        _sb.AppendLine("using System.Collections.Generic;");
        _sb.AppendLine("using System.Collections.Specialized;");
        _sb.AppendLine();
        _sb.AppendLine();
        _sb.AppendLine($"namespace {_namespac}.Request");
        _sb.AppendLine("{");
        _sb.AppendLine($"public partial class {constName}");
        _sb.AppendLine("{");


        var contentType = string.Empty;

        if (value.Post?.RequestBody?.Content?.JsonType is not null)
        {
            contentType = "application/json";
        }

        if (value.Post?.RequestBody?.Content?.FormDataType is not null)
        {
            contentType = "multipart/form-data";
        }

        if (value.Post?.Schema is not null)
        {
            _sb.AppendLine($"/// <summary>{value.Post.Summary?.Trim().Replace("\n", "\n///")}</summary>");
            GenerateByPostSchema(value.Post.Schema, apiName + "Post", constName, apiPath, value.Post.Parameters, contentType, "POST");
        }

        if (value.Patch?.Schema is not null)
        {
            _sb.AppendLine($"/// <summary>{value.Patch.Summary?.Trim().Replace("\n", "\n///")}</summary>");
            GenerateByPostSchema(value.Patch.Schema, apiName + "Patch", constName, apiPath, value.Patch.Parameters, contentType, "PATCH");
        }

        if (value.Get is not null)
        {
            _sb.AppendLine($"/// <summary>{value.Get.Summary?.Trim().Replace("\n", "\n///")}</summary>");
            GenerateByGetSchema(value.Get, apiName + "Get", constName, apiPath, "GET");
        }

        if (value.Delete is not null)
        {
            _sb.AppendLine($"/// <summary>{value.Delete.Summary?.Trim().Replace("\n", "\n///")}</summary>");
            GenerateByGetSchema(value.Delete, apiName + "Delete", constName, apiPath, "DELETE");
        }


        _sb.AppendLine("}");
        _sb.AppendLine("}");
        return _sb.ToString();
    }

    private void GenerateByGetSchema(RequestModule get, string constName, string apiName, string apiPath, string method)
    {

        var hasParameters = get.Parameters?.Length is not null and not 0;
        var parmetersAppend = string.Empty;

        if (hasParameters
         && !(get.Parameters!.All(x => _ignoreParams.Contains(x.Name))
         && get.Parameters!.All(x => x.In != InEnum.Path)))
        {
            parmetersAppend = ", IApiRequestQueryParams";
        }


        _sb.AppendLine($"public class {constName}Request : IApiRequest{parmetersAppend}");
        _sb.AppendLine("{");

        var code = $@"
private static readonly ApiEntryPointInfo {apiName}_{constName}Request
    = new ApiEntryPointInfo(new HttpMethod(""{method}""), ""{apiPath}"",default);

[JsonIgnore]
public ApiEntryPointInfo ApiEntryPointInfo {{ get => _apiEntryPointInfo; set => _apiEntryPointInfo = value; }}
private ApiEntryPointInfo _apiEntryPointInfo = {apiName}_{constName}Request;
            ";

        _sb.AppendLine(code);

        if (get.Parameters?.Length is not null and not 0)
        {
            GenerateQueryParms(get.Parameters, constName);
        }

        _sb.AppendLine("}");
    }

    private void GenerateByPostSchema(SchemaModule schema, string constName, string apiName, string apiPath, ParameterModule[]? parameters, string contentType, string method)
    {
        if (schema.OneOf?.Length is not null and not 0)
        {
            GenerateBySchemas(schema, constName, apiName, apiPath, parameters, contentType);
            return;
        }

        if (schema.Source is not null)
        {
            schema = _gloablSchemas[schema.Source.Split('/').Last()];
        }

        var hasParameters = parameters?.Length is not null and not 0;
        var parmetersAppend = string.Empty;

        if (hasParameters
         && !(parameters!.All(x => _ignoreParams.Contains(x.Name))
         && parameters!.All(x => x.In != InEnum.Path)))
        {
            parmetersAppend = ", IApiRequestQueryParams";
        }


        if (schema.Type == TypeEnum.Array)
        {
            _sb.AppendLine($"public class {constName}Request : {constName}, IApiRequest{parmetersAppend}");
        }
        else
        {
            _sb.AppendLine($"public class {constName}Request : {constName}, IApiRequest{parmetersAppend}");
        }

        _sb.AppendLine("{");

        var code = $@"
private static readonly ApiEntryPointInfo {apiName}_{constName}Request
    = new ApiEntryPointInfo(new HttpMethod(""{method}""), ""{apiPath}"", ""{contentType}"");

[JsonIgnore]
public ApiEntryPointInfo ApiEntryPointInfo {{ get => _apiEntryPointInfo; set => _apiEntryPointInfo = value; }}
private ApiEntryPointInfo _apiEntryPointInfo = {apiName}_{constName}Request;
            ";

        _sb.AppendLine(code);

        if (parameters?.Length is not null and not 0)
        {
            GenerateQueryParms(parameters, constName);
        }

        _sb.AppendLine("}");

        var entrySchema = schema.Type == TypeEnum.Array ? schema.Items : schema;

        GenerateClass(entrySchema, constName);
    }

    private void GenerateQueryParms(ParameterModule[] parameters, string name)
    {
        if (parameters.All(x => _ignoreParams.Contains(x.Name))
         && parameters.All(x => x.In != InEnum.Path))
        {
            return;
        }

        var containsQuery = parameters.Any(x => x.In == InEnum.Query);
        var containsHeader = parameters.Any(x => x.In == InEnum.Header);
        var containsPath = parameters.Any(x => x.In == InEnum.Path);

        var queryAppend = containsQuery ? "System.Web.HttpUtility.ParseQueryString(string.Empty)" : "default";
        var headerAppend = containsHeader ? "System.Web.HttpUtility.ParseQueryString(string.Empty)" : "default";
        var pathAppend = containsPath ? "System.Web.HttpUtility.ParseQueryString(string.Empty)" : "default";


        _sb.AppendLine($"private NameValueCollection _query = {queryAppend};");
        _sb.AppendLine($"private NameValueCollection _header = {headerAppend};");
        _sb.AppendLine($"private NameValueCollection _path = {pathAppend};");


        _sb.AppendLine($"public class {name}QueryParms");
        _sb.AppendLine("{");
        _sb.AppendLine("private readonly IApiRequestQueryParams _params;");

        _sb.AppendLine($"public {name}QueryParms(IApiRequestQueryParams queryParams)");
        _sb.AppendLine("{");
        _sb.AppendLine("_params = queryParams;");
        _sb.AppendLine("}");

        var pathParms = new NameValueCollection();

        foreach (var parameter in parameters)
        {
            if (parameter.In != InEnum.Path && _ignoreParams.Contains(parameter.Name))
            {
                continue;
            }

            var location = parameter.In switch
            {
                InEnum.Header => "_params.HeaderParms",
                InEnum.Path => "_params.PathParms",
                InEnum.Query => "_params.QueryParms",
            };



            var getParm = parameter.Schema.Type switch
            {
                TypeEnum.String => $"=> {location}[\"{parameter.Name}\"];",
                TypeEnum.Integer => $"{{ return int.TryParse({location}[\"{parameter.Name}\"], out var result) ? result : null; }}",
                TypeEnum.Boolean => $"{{ return bool.TryParse({location}[\"{parameter.Name}\"], out var result) ? result : null; }}",
                TypeEnum.Number => $"{{ return float.TryParse({location}[\"{parameter.Name}\"], out var result) ? result : null; }}",
                TypeEnum.Array => $"=> throw new NotImplementedException();"
            };

            var type = parameter.Schema.Type switch
            {
                TypeEnum.String => "string",
                _ => $"{ParseType(parameter.Schema.Type, parameter.Schema.Format)}?"
            };

            if (parameter.In == InEnum.Path)
            {
                pathParms.Add($"{type} {parameter.Name.Replace(".", "_").Replace("-", "_")}", $"Parameters.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parameter.Name.Replace(".", "_").Replace("-", "_"))} = {parameter.Name.Replace(".", "_").Replace("-", "_")}");
            }

            _sb.AppendLine($"/// <summary>{parameter.Description?.Trim().Replace("\n", "\n///")}</summary>");
            _sb.AppendLine($"public {type} {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parameter.Name.Replace(".", "_").Replace("-", "_"))} {{ get {getParm} set => {location}[\"{parameter.Name}\"] = value.ToString(); }}");
        }
        _sb.AppendLine("}");


        _sb.AppendLine($"[JsonIgnore]");
        _sb.AppendLine($"public readonly {name}QueryParms Parameters;");

        _sb.AppendLine("[JsonIgnore]");
        _sb.AppendLine("[Obsolete(\"this field only for framework\")]");
        _sb.AppendLine("public NameValueCollection QueryParms => _query;");
        _sb.AppendLine("[JsonIgnore]");
        _sb.AppendLine("[Obsolete(\"this field only for framework\")]");
        _sb.AppendLine("public NameValueCollection HeaderParms => _header;");
        _sb.AppendLine("[JsonIgnore]");
        _sb.AppendLine("[Obsolete(\"this field only for framework\")]");
        _sb.AppendLine("public NameValueCollection PathParms => _path;");



        _sb.AppendLine($"public {name}Request({string.Join(',', pathParms.AllKeys)})");
        _sb.AppendLine("{");

        _sb.AppendLine($"Parameters = new {name}QueryParms(this);");

        foreach (var item in pathParms.AllKeys)
        {
            _sb.AppendLine($"{pathParms[item]};");
        }

        _sb.AppendLine("}");
    }

    private void GenerateBySchemas(SchemaModule schema, string constName, string apiName, string apiPath, ParameterModule[]? parameters, string contentType)
    {
        _sb.AppendLine($"public static class {constName}Requests");
        _sb.AppendLine($"{{");

        var code = $@"
private static readonly ApiEntryPointInfo {apiName}_{constName}Request
    = new ApiEntryPointInfo(new HttpMethod(""POST""), ""{apiPath}"", ""{contentType}"");
                    ";

        _sb.AppendLine(code);

        foreach (var item in schema.OneOf)
        {
            var key = item.Source.Split('/').Last();
            var tempSchema = _gloablSchemas[key];
            key = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key);

            if (tempSchema.Type == TypeEnum.Array)
            {
                _sb.AppendLine($"public class {key}Request : List<{key}> , IApiRequest");
            }
            else
            {
                _sb.AppendLine($"public class {key}Request : {key} , IApiRequest");
            }

            _sb.AppendLine("{");

            code = $@"
public ApiEntryPointInfo ApiEntryPointInfo {{ get => _apiEntryPointInfo; set => _apiEntryPointInfo = value; }}
private ApiEntryPointInfo _apiEntryPointInfo = {apiName}_{constName}Request;
            ";

            _sb.AppendLine(code);

            if (parameters?.Length is not null and not 0)
            {
                GenerateQueryParms(parameters, constName);
            }

            _sb.AppendLine("    }");

            var entrySchema = tempSchema.Type == TypeEnum.Array ? tempSchema.Items : tempSchema;

            GenerateClass(entrySchema, key);

        }

        _sb.AppendLine($"}}");
    }

    private void GenerateClass(SchemaModule schema, string className)
    {
        if (schema is null)
        {
            return;
        }


        Console.WriteLine($"public class {className}");
        _sb.AppendLine($"public class {className}");
        _sb.AppendLine($"{{");


        if (schema.Properties != null)
        {
            foreach (var prop in schema.Properties)
            {
                GenerateFields(prop.Value, prop.Key);
            }
        }
        _sb.AppendLine($"}}");
        _sb.AppendLine();
    }

    private void GenerateFields(SchemaModule schema, string name)
    {
        Console.WriteLine(@$"[JsonProperty(""{name}"")]");

        _sb.AppendLine($"/// <summary>{schema.Description?.Trim().Replace("\n", "\n///")}</summary>");
        _sb.AppendLine(@$"[JsonProperty(""{name}"")]");


        name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);

        switch (schema.Type)
        {
            case TypeEnum.String:
                if (schema.Format == "binary")
                {
                    _sb.AppendLine($"public Stream {name} {{ get; set; }}");
                }
                else
                {
                    _sb.AppendLine($"public string {name} {{ get; set; }}");
                }
                break;
            case TypeEnum.Integer:
            case TypeEnum.Boolean:
            case TypeEnum.Number:
                _sb.AppendLine($"public {ParseType(schema.Type, schema.Format)} {name} {{ get; set; }}");
                break;
            case TypeEnum.Object:
                _sb.AppendLine($"public {name}Object {name} {{ get; set; }}");
                GenerateClass(schema, name + "Object");
                break;
            case TypeEnum.Array:
                if (schema.Items is null)
                {
                    _sb.AppendLine($"public object {name} {{ get; set; }}");
                    break;
                }

                if (schema.Items.Type == TypeEnum.Object)
                {
                    var arrayItemClassName = $"{name}Item";
                    _sb.AppendLine($"public List<{arrayItemClassName}> {name} {{ get; set; }}");
                    GenerateClass(schema.Items, arrayItemClassName);
                }
                else
                {
                    _sb.AppendLine($"public List<{ParseType(schema.Items.Type, schema.Items.Format)}> {name} {{ get; set; }}");
                }
                break;
        }

    }

    public string ParseType(TypeEnum typeEnum, string format)
    {
        switch (typeEnum)
        {
            case TypeEnum.Array:
                return "object";
            case TypeEnum.Object:
                return "object";
            case TypeEnum.Boolean:
                return "bool";
            case TypeEnum.Number:
                return "float";
            case TypeEnum.String:
                return "string";
            case TypeEnum.Integer:
                return format switch
                {
                    "int32" => "int",
                    "int64" => "long",
                    _ => throw new NotImplementedException()
                };
            default:
                throw new NotImplementedException();
        }
    }


}
