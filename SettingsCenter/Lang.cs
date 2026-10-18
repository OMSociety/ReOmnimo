using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SettingsCenter;

// 语言包：exe 旁 lang\zh-CN.json（键标签 + hint 文案 + 界面文案），缺失回退英文/键名
public static class Lang
{
    public static Dictionary<string, string> Labels = new();
    public static Dictionary<string, string> Hints = new();
    public static Dictionary<string, string> Ui = new();

    static Lang() => Load("zh-CN");

    public static void Load(string name)
    {
        string text;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "lang", name + ".json");
            if (!File.Exists(path)) return;
            text = File.ReadAllText(path);
        }
        catch { return; /* 文件读不出（锁/权限）：保持空表回退英文 */ }

        try
        {
            var doc = MiniJson.Parse(text);
            Labels = Read(doc, "labels");
            Hints = Read(doc, "hints");
            Ui = Read(doc, "ui");
            return;
        }
        catch (MiniJsonException) { }

        // 整包解析失败（常见：一处坏 \u 转义）：退到逐键容错——坏的那个键丢弃
        //（界面按既有约定回退英文键名），其余键照常生效。
        // 旧 catch{} 是整包静默回退英文，一处坏转义毁掉全部翻译（RO-23）。
        var (labels2, hints2, ui2) = ReadTolerant(text);
        Labels = labels2;
        Hints = hints2;
        Ui = ui2;
    }

    private static Dictionary<string, string> Read(object? root, string section)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root is Dictionary<string, object?> obj
            && obj.TryGetValue(section, out var el)
            && el is Dictionary<string, object?> items)
            foreach (var p in items)
                d[p.Key] = p.Value as string ?? "";
        return d;
    }

    // 容错读取：按节定位 "labels"/"hints"/"ui" 的对象片段，片段内逐对提取字符串值；
    // 每对单独走 MiniJson 反转义并各自捕获错误——一个坏转义只影响该键
    private static (Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>) ReadTolerant(string text)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ui = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int posL = SectionPos(text, "labels"), posH = SectionPos(text, "hints"), posU = SectionPos(text, "ui");
        if (posL >= 0) CollectPairs(text, posL, NextPos(text, posL, posH, posU), labels);
        if (posH >= 0) CollectPairs(text, posH, NextPos(text, posH, posL, posU), hints);
        if (posU >= 0) CollectPairs(text, posU, NextPos(text, posU, posL, posH), ui);
        return (labels, hints, ui);
    }

    private static int SectionPos(string text, string key)
    {
        var m = Regex.Match(text, "\"" + key + "\"\\s*:\\s*\\{");
        return m.Success ? m.Index : -1;
    }

    // 该节片段的终点 = 在它之后出现的下一个节的位置，没有则到文末
    private static int NextPos(string text, int self, int a, int b)
    {
        int end = text.Length;
        if (a > self && a < end) end = a;
        if (b > self && b < end) end = b;
        return end;
    }

    private static void CollectPairs(string text, int start, int end, Dictionary<string, string> into)
    {
        var pair = new Regex("\"((?:[^\"\\\\]|\\\\.)+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        foreach (Match m in pair.Matches(text, start))
        {
            if (m.Index >= end) break;
            try
            {
                // 键与值各自包回 JSON 字符串用同一套解析器反转义，坏转义在此抛出并被跳过
                var key = MiniJson.Parse("\"" + m.Groups[1].Value + "\"") as string;
                var val = MiniJson.Parse("\"" + m.Groups[2].Value + "\"") as string;
                if (key != null && val != null) into[key] = val;
            }
            catch (MiniJsonException) { /* 该键坏转义：丢弃，其余键不受影响 */ }
        }
    }

    public static string T(string key, string fallback) =>
        Ui.TryGetValue(key, out var v) ? v : fallback;
}
