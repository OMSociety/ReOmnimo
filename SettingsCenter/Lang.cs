using System;
using System.Collections.Generic;
using System.IO;

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
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "lang", name + ".json");
            if (!File.Exists(path)) return;
            var doc = MiniJson.Parse(File.ReadAllText(path));
            Labels = Read(doc, "labels");
            Hints = Read(doc, "hints");
            Ui = Read(doc, "ui");
        }
        catch { /* 语言包损坏时静默回退 */ }
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

    public static string T(string key, string fallback) =>
        Ui.TryGetValue(key, out var v) ? v : fallback;
}
