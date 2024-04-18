
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public class SwaggerModule
{
    public Dictionary<string, PathsModule> Paths { get; set; }
    public SchemasModule Components { get; set; }
}

public class PathsModule
{
    public RequestModule Post { get; set; }
    public RequestModule Patch { get; set; }
    public RequestModule Get { get; set; }
    public RequestModule Delete { get; set; }
}

public class SchemasModule
{
    public Dictionary<string, SchemaModule> Schemas { get; set; }
}

public class RequestModule
{
    public ParameterModule[] Parameters { get; set; }
    public string Summary { get; set; }
    public SchemaModule? Schema => RequestBody?.Content?.JsonType?.Schema ?? RequestBody?.Content?.FormDataType?.Schema;
    public RequestBodyModule RequestBody { get; set; }
}

public class ParameterModule
{
    public string Name { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public InEnum In { get; set; }
    public SchemaModule Schema { get; set; }
    public string Description { get; set; }
}

public class RequestBodyModule
{
    public ContentModule Content { get; set; }
}

public class ContentModule
{
    [JsonProperty("application/json")]
    public TypeModule JsonType { get; set; }

    [JsonProperty("multipart/form-data")]
    public TypeModule FormDataType { get; set; }
}

public class TypeModule
{
    public SchemaModule Schema { get; set; }
}

public class SchemaModule
{
    [JsonConverter(typeof(StringEnumConverter))]
    public TypeEnum Type { get; set; }
    public SchemaModule Items { get; set; }
    public string Format { get; set; }
    public string[] Required { get; set; }
    public string Description { get; set; }
    public Dictionary<string, SchemaModule> Properties { get; set; }
    public SchemaModule[] OneOf { get; set; }
    public string Source { get; set; }
}

public enum TypeEnum
{
    Array,
    Object,
    Integer,
    Boolean,
    Number,
    String,
}

public enum InEnum
{
    Query,
    Path,
    Header
}