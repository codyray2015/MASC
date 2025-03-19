using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Cocona;
using YamlDotNet.Serialization;

CoconaLiteApp.Run<Run>(args);

// new Run().RequestOnly("example/case1/swagger.yaml", "test");

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
            try
            {
                var generator = new CodeGenerator(jsonObj.Components.Schemas, jsonObj.Components.Parameters, namespac, ignoreParams);

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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
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
        var yamlContent = ReadYamlContent(fileName);


        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(yamlContent);

        var serializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();

        return serializer.Serialize(yamlObject);
    }


    private static Dictionary<string, string> YamlContentCache = new Dictionary<string, string>();


    private static string ReadYamlContent(string fileName)
    {
        var tempFileName = Path.GetFileName(fileName);
        if (YamlContentCache.TryGetValue(tempFileName, out var value))
        {
            return value;
        }




        Console.WriteLine($"Read {fileName}");
        var sb = new StringBuilder();
        var content = File.ReadAllText(fileName);
        content = Regex.Replace(content, "\\$ref: >-\n", "$ref:");

        foreach (var item in content.Split("\n"))
        {
            if (item.TrimStart().StartsWith("$ref:") && item.TrimEnd().EndsWith("yaml"))
            {
                var prefix = item.Substring(0, item.IndexOf("$ref:"));


                var newfilePath = Path.Combine(Path.GetDirectoryName(fileName), item.TrimStart().Replace("$ref:", "").Trim());

                if (Path.GetFileName(newfilePath) == tempFileName)
                {
                    continue;
                }

                var stringReader = new StringReader(ReadYamlContent(newfilePath));

                string? tempLine;

                while ((tempLine = stringReader.ReadLine()) is not null)
                {
                    sb.AppendLine(prefix + tempLine);
                }

            }
            else if (item.TrimStart().StartsWith("- $ref:") && item.TrimEnd().EndsWith("yaml"))
            {
                var prefix = item.Substring(0, item.IndexOf("- $ref:"));


                var newfilePath = Path.Combine(Path.GetDirectoryName(fileName), item.TrimStart().Replace("- $ref:", "").Trim());

                if (Path.GetFileName(newfilePath) == tempFileName)
                {
                    continue;
                }

                var stringReader = new StringReader(ReadYamlContent(newfilePath));

                string? tempLine;
                bool isFirst = true;


                while ((tempLine = stringReader.ReadLine()) is not null)
                {
                    if (isFirst)
                    {
                        sb.AppendLine(prefix + "- " + tempLine);
                        isFirst = false;
                    }
                    else
                    {
                        sb.AppendLine(prefix + "  " + tempLine);
                    }

                }
            }
            else
            {
                sb.AppendLine(item);
            }
        }

        YamlContentCache.TryAdd(tempFileName, sb.ToString());

        return sb.ToString();
    }
}