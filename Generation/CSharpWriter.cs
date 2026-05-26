using System.Text;

namespace MASC.Generation;

/// <summary>
/// 一个带缩进管理的代码写入器。调用 <see cref="OpenBlock"/> / <see cref="CloseBlock"/>
/// 时自动维护缩进层级，因此生成的代码无需再做脆弱的「大括号后处理」即已正确缩进。
/// </summary>
public sealed class CSharpWriter
{
    private const string IndentUnit = "    "; // 4 空格

    private readonly StringBuilder _sb = new();
    private int _depth;

    /// <summary>写入一行(按当前缩进)。空字符串则写入空行。</summary>
    public CSharpWriter Line(string text = "")
    {
        if (text.Length == 0)
        {
            _sb.Append('\n');
        }
        else
        {
            _sb.Append(string.Concat(Enumerable.Repeat(IndentUnit, _depth)));
            _sb.Append(text);
            _sb.Append('\n');
        }
        return this;
    }

    /// <summary>写入一行并增加缩进(通常用于以 "{" 开块)。</summary>
    public CSharpWriter OpenBlock(string header)
    {
        Line(header);
        Line("{");
        _depth++;
        return this;
    }

    /// <summary>减少缩进并写入闭合行(默认 "}")。</summary>
    public CSharpWriter CloseBlock(string footer = "}")
    {
        _depth = Math.Max(0, _depth - 1);
        Line(footer);
        return this;
    }

    /// <summary>写入一段 XML 文档摘要(多行自动加 /// 前缀)。空摘要写出空 summary 以保持稳定输出。</summary>
    public CSharpWriter Summary(string? text)
    {
        var content = (text ?? string.Empty).Trim().Replace("\n", "\n/// ");
        return Line($"/// <summary>{content}</summary>");
    }

    public override string ToString() => _sb.ToString();
}
