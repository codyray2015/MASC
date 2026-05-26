using Microsoft.OpenApi.Models;

namespace MASC.Generation;

/// <summary>
/// 收集整轮生成中被 $ref 引用的组件 schema，作为共享的数据模型类，
/// 最终统一生成到独立的 <c>{namespace}.Models</c> 命名空间(每个模型只生成一次)。
///
/// 这样可避免把同一模型重复塞进按文件拆分的 partial 请求类中(会导致跨文件重复定义)。
/// </summary>
public class ModelRegistry
{
    private readonly Dictionary<string, OpenApiSchema> _models = new();

    public bool HasAny => _models.Count > 0;

    /// <summary>登记一个被引用的 schema(已由库解析，携带属性)，返回其 C# 类型名。</summary>
    public string Register(OpenApiSchema referenced)
    {
        var name = Naming.RefName(referenced.RefId()!);
        _models.TryAdd(name, referenced);
        return name;
    }

    /// <summary>
    /// 生成包含全部模型类的源码文件内容。即使没有任何模型也会生成(仅含空命名空间声明)，
    /// 以保证各请求文件的 <c>using {modelsNamespace};</c> 始终有效。
    /// </summary>
    public string EmitFile(string modelsNamespace)
    {
        var w = new CSharpWriter();
        w.Line("#nullable disable");
        w.Line("using System;");
        w.Line("using System.IO;");
        w.Line("using Newtonsoft.Json;");
        w.Line("using System.Collections.Generic;");
        w.Line();
        w.OpenBlock($"namespace {modelsNamespace}");

        var emitter = new TypeEmitter(w, this);
        var emitted = new HashSet<string>();

        // 生成模型时其字段可能登记新的模型，循环直到全部生成。
        while (true)
        {
            var pending = _models.Keys.Where(name => !emitted.Contains(name)).ToList();
            if (pending.Count == 0)
            {
                break;
            }

            foreach (var name in pending)
            {
                emitted.Add(name);
                emitter.WriteClass(name, _models[name]);
            }
        }

        w.CloseBlock();
        return w.ToString();
    }
}
