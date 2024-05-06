using System.Diagnostics;
using System.Text;
using Cocona;
using YamlDotNet.Serialization;

CoconaLiteApp.Run<Run>(args);

public class Run
{
    public void FrameworkOnly([Argument] string outPath, [Option] string namespac = "Swagger", [Option] string clientName = "")
    {
        var fileNames = Directory.GetFiles("template/framework");

        if (!Directory.Exists(outPath))
        {
            Directory.CreateDirectory(outPath);
        }

        foreach (var fileName in fileNames)
        {
            var fileInfo = new FileInfo(fileName);

            var fileContent = File.ReadAllText(fileName);

            var outContent = fileContent.Replace("{{namespac}}", namespac)
                .Replace("{{client_name}}", clientName);

            var outP = Path.Combine(outPath, fileInfo.Name.Replace(".template", ""))
                .Replace("{{namespac}}", namespac)
                .Replace("{{client_name}}", clientName);

            File.WriteAllText(outP, outContent);
        }
    }


    public void RequestOnly(
        [Argument] string fileName, 
        [Argument] string outPath, 
        [Option] string namespac = "Swagger", 
        [Option] string partialName = "", 
        [Option(Description = "Ignore the params in generater (Does not include Path params), exp: --ignore-params param1,param2,param3")] string ignoreParams = "")
    {
        string json = ReadJsonString(fileName);

        var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject<SwaggerModule>(json);

        if (!Directory.Exists(outPath))
        {
            Directory.CreateDirectory(outPath);
        }

        foreach (var path in jsonObj.Paths)
        {
            var generator = new CodeGenerator(jsonObj.Components.Schemas, namespac,ignoreParams);

            var tpartialName = partialName;

            var code = generator.GenerateCode(path.Value, path.Key, out var apiName, ref tpartialName);

            var sr = new StringReader(code);
            var sb = new StringBuilder();

            var spanCount = 0;
            string? s = null;

            while ((s = sr.ReadLine()) is not null)
            {
                if (s.StartsWith("}"))
                {
                    spanCount--;
                }

                sb.AppendLine(new string('\t', spanCount) + s);

                if (s.StartsWith("{"))
                {
                    spanCount++;
                }
            }

            File.WriteAllText(Path.Combine(outPath, $"{tpartialName}.{apiName}.cs"), sb.ToString());
        }

    }

    static string ReadJsonString(string fileName)
    {
        string json = string.Empty;

        if (fileName.EndsWith("yaml"))
        {
            json = ReadByYaml(fileName);
        }
        else if (fileName.EndsWith("json"))
        {
            json = File.ReadAllText(fileName);
        }

        json = json.Replace("\"$ref\"", "\"source\"");
        return json;
    }

    private static string ReadByYaml(string fileName)
    {
        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(File.ReadAllText(fileName));

        var serializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();

        return serializer.Serialize(yamlObject);
    }
}