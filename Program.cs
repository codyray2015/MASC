using Cocona;
using MASC.Generation;
using MASC.Loading;

CoconaLiteApp.Run<Commands>(args);

/// <summary>CLI 命令定义(由 Cocona 绑定)。</summary>
public class Commands
{
    /// <summary>仅生成运行时框架代码(对 template/framework 下的模板做占位符替换)。</summary>
    public void FrameworkOnly(
        [Argument] string outPath,
        [Option] string namespac = "Swagger",
        [Option] string clientName = "")
    {
        Directory.CreateDirectory(outPath);

        foreach (var templatePath in Directory.GetFiles("template/framework"))
        {
            var content = File.ReadAllText(templatePath)
                .Replace("{{namespac}}", namespac)
                .Replace("{{client_name}}", clientName);

            var outName = Path.GetFileName(templatePath)
                .Replace(".template", string.Empty)
                .Replace("{{namespac}}", namespac)
                .Replace("{{client_name}}", clientName);

            File.WriteAllText(Path.Combine(outPath, outName), content);
        }
    }

    /// <summary>根据 Swagger/OpenAPI 文档(JSON 或 YAML)生成各路径的请求类。</summary>
    public void RequestOnly(
        [Argument] string fileName,
        [Argument] string outPath,
        [Option] string namespac = "Swagger",
        [Option] string partialName = "",
        [Option(Description = "Ignore the params in generater (Does not include Path params), exp: --ignore-params param1,param2,param3")]
        string ignoreParams = "")
    {
        var document = OpenApiLoader.Load(fileName);
        Directory.CreateDirectory(outPath);

        // 整轮共享一个模型注册表：被 $ref 引用的模型统一收集，最后生成到 Models 文件。
        // 指定 partial-name 时(同一项目多次生成的典型用法)，模型放入按其区分的子命名空间，
        // 避免不同来源的同名模型在同一命名空间下冲突。
        var modelsNamespace = string.IsNullOrEmpty(partialName)
            ? $"{namespac}.Models"
            : $"{namespac}.Models.{Naming.SafeIdentifier(partialName)}";

        var models = new ModelRegistry();

        foreach (var (apiPath, pathItem) in document.Paths)
        {
            try
            {
                var generator = new RequestGenerator(namespac, modelsNamespace, ignoreParams, models);
                var file = generator.Generate(pathItem, apiPath, partialName);
                var outFile = Path.Combine(outPath, $"{file.PartialClassName}.{file.ApiName}.cs");
                File.WriteAllText(outFile, file.Code);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {apiPath}: {ex}");
            }
        }

        File.WriteAllText(Path.Combine(outPath, "Models.cs"), models.EmitFile(modelsNamespace));
    }
}
