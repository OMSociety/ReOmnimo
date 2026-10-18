using System;
using System.Collections.Generic;
using System.Text;

namespace SettingsCenter;

// 迷你 JSON：只支持本项目用到的子集（对象/数组/字符串/数字/true/false/null），
// 用来替代 System.Text.Json —— 避免 net48 引入 8 个依赖 DLL。
// 解析结果是 object：Dictionary<string,object> / List<object> / string / double / bool / null。
// 解析错误（RO-23）：坏输入不再静默产出垃圾结构或抛裸 FormatException，
// 调用方（OnWebMessage / Lang.Load）捕获后给提示或降级
public sealed class MiniJsonException : Exception
{
    public MiniJsonException(string message) : base(message) { }
}

public static class MiniJson
{
    public static object? Parse(string text)
    {
        int i = 0;
        var v = ReadValue(text, ref i);
        Ws(text, ref i);
        if (i < text.Length)
            throw new MiniJsonException($"尾随内容：位置 {i} 附近「{Snippet(text, i)}」");
        return v;
    }

    private static string Snippet(string s, int i) =>
        s.Substring(i, Math.Min(16, s.Length - i)).Replace("\r", "\\r").Replace("\n", "\\n");

    private static void Ws(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static void Expect(string s, ref int i, string word)
    {
        if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
            throw new MiniJsonException($"非法字面量：位置 {i} 附近「{Snippet(s, i)}」");
        i += word.Length;
    }

    private static object? ReadValue(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw new MiniJsonException("内容意外结束（应为值）");
        switch (s[i])
        {
            case '{': return ReadObject(s, ref i);
            case '[': return ReadArray(s, ref i);
            case '"': return ReadString(s, ref i);
            case 't': Expect(s, ref i, "true"); return true;
            case 'f': Expect(s, ref i, "false"); return false;
            case 'n': Expect(s, ref i, "null"); return null;
            default: return ReadNumber(s, ref i);
        }
    }

    private static Dictionary<string, object?> ReadObject(string s, ref int i)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        i++; // {
        Ws(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return d; }
        while (true)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new MiniJsonException("对象未闭合（缺 }）");
            if (s[i] == '}') throw new MiniJsonException($"尾随逗号产生幽灵键：位置 {i}");
            if (s[i] != '"') throw new MiniJsonException($"应为字符串键：位置 {i} 附近「{Snippet(s, i)}」");
            var key = ReadString(s, ref i);
            Ws(s, ref i);
            if (i >= s.Length || s[i] != ':')
                throw new MiniJsonException($"应为冒号：位置 {i} 附近「{Snippet(s, i)}」");
            i++; // :
            d[key] = ReadValue(s, ref i);
            Ws(s, ref i);
            if (i >= s.Length) throw new MiniJsonException("对象未闭合（缺 }）");
            if (s[i] == ',') { i++; continue; }
            if (s[i] == '}') { i++; break; }
            throw new MiniJsonException($"应为 , 或 }}：位置 {i} 附近「{Snippet(s, i)}」");
        }
        return d;
    }

    private static List<object?> ReadArray(string s, ref int i)
    {
        var list = new List<object?>();
        i++; // [
        Ws(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return list; }
        while (true)
        {
            list.Add(ReadValue(s, ref i));
            Ws(s, ref i);
            if (i >= s.Length) throw new MiniJsonException("数组未闭合（缺 ]）");
            if (s[i] == ',') { i++; continue; }
            if (s[i] == ']') { i++; break; }
            throw new MiniJsonException($"应为 , 或 ]：位置 {i} 附近「{Snippet(s, i)}」");
        }
        return list;
    }

    private static string ReadString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // "
        while (true)
        {
            if (i >= s.Length) throw new MiniJsonException("字符串未闭合");
            var c = s[i++];
            if (c == '"') break;
            if (c == '\\')
            {
                if (i >= s.Length) throw new MiniJsonException("字符串未闭合（转义符在末尾）");
                var e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '/': sb.Append('/'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                            throw new MiniJsonException("\\u 转义不足 4 位十六进制");
                        var hex = s.Substring(i, 4);
                        if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                          System.Globalization.CultureInfo.InvariantCulture, out var cp))
                            throw new MiniJsonException($"非法 \\u 转义：\\{hex}");
                        sb.Append((char)cp);
                        i += 4;
                        break;
                    default: throw new MiniJsonException($"非法转义字符：\\{e}");
                }
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static double ReadNumber(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
        if (i == start)
            throw new MiniJsonException($"意外的字符：位置 {i} 附近「{Snippet(s, i)}」");
        var raw = s.Substring(start, i - start);
        // 前导零等宽松容忍（"01" 按 1 解）：本项目输入只来自自家 JS 与语言包，不会出现
        if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var v))
            throw new MiniJsonException($"非法数字：{raw}");
        return v;
    }

    // JS 字符串字面量（带引号），替代 JsonSerializer.Serialize(string)
    public static string JsStr(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    // 便捷：取对象字段（字符串）
    public static string Str(object? obj, string key, string def = "")
    {
        if (obj is Dictionary<string, object?> d && d.TryGetValue(key, out var v) && v is string s) return s;
        return def;
    }

    public static double Num(object? obj, string key, double def = 0)
    {
        if (obj is Dictionary<string, object?> d && d.TryGetValue(key, out var v) && v is double n) return n;
        return def;
    }
}
