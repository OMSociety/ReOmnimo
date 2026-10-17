using System;
using System.Collections.Generic;
using System.Text;

namespace SettingsCenter;

// 迷你 JSON：只支持本项目用到的子集（对象/数组/字符串/数字/true/false/null），
// 用来替代 System.Text.Json —— 避免 net48 引入 8 个依赖 DLL。
// 解析结果是 object：Dictionary<string,object> / List<object> / string / double / bool / null。
public static class MiniJson
{
    public static object? Parse(string text)
    {
        int i = 0;
        var v = ReadValue(text, ref i);
        return v;
    }

    private static void Ws(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static object? ReadValue(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) return null;
        switch (s[i])
        {
            case '{': return ReadObject(s, ref i);
            case '[': return ReadArray(s, ref i);
            case '"': return ReadString(s, ref i);
            case 't': i += 4; return true;          // true
            case 'f': i += 5; return false;         // false
            case 'n': i += 4; return null;          // null
            default: return ReadNumber(s, ref i);
        }
    }

    private static Dictionary<string, object?> ReadObject(string s, ref int i)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        i++; // {
        Ws(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return d; }
        while (i < s.Length)
        {
            Ws(s, ref i);
            var key = ReadString(s, ref i);
            Ws(s, ref i);
            i++; // :
            var val = ReadValue(s, ref i);
            d[key] = val;
            Ws(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; continue; }
            if (i < s.Length && s[i] == '}') { i++; break; }
            break;
        }
        return d;
    }

    private static List<object?> ReadArray(string s, ref int i)
    {
        var list = new List<object?>();
        i++; // [
        Ws(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return list; }
        while (i < s.Length)
        {
            list.Add(ReadValue(s, ref i));
            Ws(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; continue; }
            if (i < s.Length && s[i] == ']') { i++; break; }
            break;
        }
        return list;
    }

    private static string ReadString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // "
        while (i < s.Length)
        {
            var c = s[i++];
            if (c == '"') break;
            if (c == '\\' && i < s.Length)
            {
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
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
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
        double.TryParse(s.Substring(start, i - start), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v);
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
