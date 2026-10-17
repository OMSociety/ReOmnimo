using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SettingsCenter;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 皮肤右键菜单原样传入的位置参数（与老 AutoIt config.exe 一致）：
        //   config.exe <模式:text/huge/PanelType> <config:REO\Panels\X> [Item.ini] [SettingsPath] [SkinsPath] [ProgramPath]
        // 模式参数被忽略（老工具只拿它区分 bg 全局背景，已无调用点）。
        // 另支持：--config "REO\Panels\X" / --light / --dark
        string config = "REO\\Panels\\Slideshow";
        string? skinsRoot = null;
        string? programPath = null;
        bool forceLight = false, forceDark = false;
        var positional = args.Where(a => !a.StartsWith("--")).ToArray();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--config" && i + 1 < args.Length) { config = args[i + 1]; i++; }
            else if (a == "--light") forceLight = true;
            else if (a == "--dark") forceDark = true;
        }
        // 位置参数：REO\ 开头的是 config；含 Skins 的已存在目录是 skinsRoot；Rainmeter.exe 是 programPath
        foreach (var a in positional)
        {
            if (a.StartsWith("REO\\", StringComparison.OrdinalIgnoreCase)) config = a;
            else if (a.IndexOf("Skins", StringComparison.OrdinalIgnoreCase) >= 0 && Directory.Exists(a)) skinsRoot = a;
            else if (a.EndsWith("Rainmeter.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(a)) programPath = a;
        }

        // 未处理异常落盘：AppDomain 只接非 UI 线程异常，WinForms UI 线程异常走 ThreadException
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "sc_crash.txt"),
                    $"=== {DateTime.Now:O} THREAD ===\n{e.Exception}\n\n");
            }
            catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "sc_crash.txt"),
                    $"=== {DateTime.Now:O} DOMAIN ===\n{ex}\n\n");
            }
            catch { }
        };

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(config, forceLight, forceDark, skinsRoot, programPath));
    }
}

public class MainForm : Form
{
    private const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2, WM_NCHITTEST = 0x84;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    private readonly WebView2 web = new() { Dock = DockStyle.Fill };
    private string configName;               // REO\Panels\Slideshow
    private string panelName;                // Slideshow
    private readonly string skinsRoot;
    private List<SettingItem> schema = new();
    private Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    private string? localIncPath;            // UserVariables.local.inc 存在时写它（覆盖链）
    private System.Windows.Forms.Timer? refreshDebounce;
    private readonly bool light;
    private readonly string? programPath;    // 皮肤右键菜单传入的 #PROGRAMPATH#

    public MainForm(string config, bool forceLight = false, bool forceDark = false, string? skinsRootOverride = null, string? programPathOverride = null)
    {
        configName = config;
        panelName = config.Split('\\').Last();
        skinsRoot = skinsRootOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "Rainmeter", "Skins");
        programPath = programPathOverride;

        // 无边框 + 圆角（Win11 DWM）
        FormBorderStyle = FormBorderStyle.None;
        // 无边框窗口 WinForms 不会自动带上 exe 的图标，任务栏会退到系统占位图标（Win11 的四格彩色方块）；
        // 显式从自身 exe 取回嵌入的 config.ico，与其余六个工具一致。
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980; Height = 660;
        MinimumSize = new Size(520, 420);
        light = forceLight || (!forceDark && IsSystemLight());
        BackColor = light ? Color.FromArgb(250, 250, 250) : Color.FromArgb(24, 25, 27);
        Controls.Add(web);

        Load += async (_, _) =>
        {
            int pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
            // 高 DPI 下显式按物理像素定尺寸，避免 AutoScale 缩放混乱
            float scale = CreateGraphics().DpiX / 96f;
            int w = (int)(980 * scale), h = (int)(660 * scale);
            var area = Screen.FromHandle(Handle).WorkingArea;
            SetBounds(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);
            try
            {
                await InitAsync();
            }
            catch (Exception ex)
            {
                // WebView2 环境创建失败（常见：同面板双开，0x800700AA 用户数据目录被占用）
                MessageBox.Show(this,
                    "设置窗口初始化失败：\n" + ex.Message +
                    "\n\n若刚打开过同一面板的设置窗口，请先关闭它再试。",
                    "ReOmnimo 设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
        };
    }

    // 无边框窗口的边缘拉伸
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if ((int)m.Result == 1 /* HTCLIENT */)
            {
                var p = PointToClient(new Point((int)(m.LParam.ToInt64() & 0xFFFF), (int)(m.LParam.ToInt64() >> 16)));
                const int edge = 6;
                bool l = p.X <= edge, r = p.X >= Width - edge, t = p.Y <= edge, b = p.Y >= Height - edge;
                if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
                else if (b && l) m.Result = (IntPtr)16; else if (b && r) m.Result = (IntPtr)17;
                else if (t) m.Result = (IntPtr)12; else if (b) m.Result = (IntPtr)15;
                else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
            }
            return;
        }
        base.WndProc(ref m);
    }

    private async Task InitAsync()
    {
        // 按面板哈希分用户数据目录：不同面板的设置窗口可并行；
        // 同面板双开时后者拿不到目录锁 → 走 Load 里的提示分支（net48 无 Convert.ToHexString/SHA256.HashData）
        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReOmnimo", "SettingsCenter");
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var hash = BitConverter.ToString(
                sha.ComputeHash(Encoding.UTF8.GetBytes(configName))).Replace("-", "");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataRoot, hash.Substring(0, 12)));
            await web.EnsureCoreWebView2Async(env);
        }
        web.CoreWebView2.WebMessageReceived += OnWebMessage;
        web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        web.CoreWebView2.Settings.IsStatusBarEnabled = false;
        LoadPage();
        web.CoreWebView2.NavigateToString(BuildHtml());
    }

    private void LoadPage()
    {
        schema.Clear();
        values.Clear();
        incKeys = null;
        var cfgPath = CfgPath("RainConfigure.cfg");
        var incPath = CfgPath("UserVariables.inc");
        // 皮肤 @include 链是 base → local，local 的键覆盖 base；设置工具必须读两个、写 local
        //（与老 config.exe 一致，否则 Agenda 这类面板的改动会被 local 里的旧值静默盖回）
        localIncPath = null;
        if (File.Exists(incPath)) LoadValues(incPath);
        var localPath = CfgPath("UserVariables.local.inc");
        if (File.Exists(localPath)) { LoadValues(localPath); localIncPath = localPath; }
        if (File.Exists(cfgPath)) LoadSchema(cfgPath);
        // 剥掉推断标记（渲染端只认纯控件名）
        schema = schema.Select(s => s.Control.EndsWith("推断")
            ? new SettingItem(s.Key, s.Title, s.Control.Substring(0, s.Control.Length - 2)) : s).ToList();
        if (Environment.GetEnvironmentVariable("SC_DEBUG") == "1")
        {
            var log = $"cfg={cfgPath}\nexists={File.Exists(cfgPath)}\ninc={incPath}\nincExists={File.Exists(incPath)}\n" +
                      $"local={localIncPath ?? "(none)"}\n" +
                      $"values=[{string.Join(",", values.Keys)}]\nschema={schema.Count}\n" +
                      string.Join("\n", schema.Select(s => $"{s.Key}|{s.Title}|{s.Control}"));
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "sc_debug.txt"), log);
        }
    }

    private string CfgPath(string file)
    {
        // config 形如 REO\Panels\Slideshow → @Resources\Config\Panels\Slideshow\<file>
        var segs = configName.Split('\\');
        var rel = string.Join("\\", segs.Skip(1).ToArray());
        return Path.Combine(skinsRoot, "REO", "@Resources", "Config", rel, file);
    }

    // ---------- schema / values ----------

    // net48 兼容：不用 record（避免 IsExternalInit 依赖），普通类即可
    private sealed class SettingItem
    {
        public string Key; public string Title; public string Control;
        public SettingItem(string k, string t, string c) { Key = k; Title = t; Control = c; }
    }

    private void LoadSchema(string cfgPath)
    {
        var lines = ReadTextAuto(cfgPath).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var keys = KeysFromInc();   // 以 UserVariables.inc 的实际键集为锚点

        for (int i = 0; i < lines.Length; i++)
        {
            var key = lines[i].Trim();
            if (key.Length == 0 || key == "[Variables]") continue;
            if (key == "[Options]" || key == "[Files]") break;
            if (!keys.Contains(key)) continue;          // 不是变量键的行（说明文字/孤行）跳过

            // 下一条非空行做候选（跳过空行与 # 注释行）
            int j = i + 1;
            while (j < lines.Length && (lines[j].Trim().Length == 0 || lines[j].TrimStart().StartsWith("#"))) j++;
            if (j >= lines.Length) break;
            var candidate = lines[j].Trim();

            if (LooksLikeControl(candidate))
            {
                // 紧凑组：键/控件
                schema.Add(new SettingItem(key, "", candidate));
                i = j;
            }
            else if (!keys.Contains(candidate))
            {
                // candidate 是说明文字 → 标准组：键/标题/控件
                int k = j + 1;
                while (k < lines.Length && lines[k].Trim().Length == 0) k++;
                if (k < lines.Length && LooksLikeControl(lines[k].Trim()))
                {
                    schema.Add(new SettingItem(key, candidate == "!" ? "" : candidate, lines[k].Trim()));
                    i = k;
                }
                else
                {
                    // 裸键 + 说明文字（无控件行）→ 按 inc 值推断控件（标"推断"供排序沉底）
                    var val = values.TryGetValue(key, out var v) ? v : "";
                    var title0 = candidate == "!" ? "" : candidate;
                    schema.Add(new SettingItem(key, title0, InferControl(key, val, title0) + "推断"));
                    i = j;
                }
            }
            else
            {
                // 裸键 + 下一行还是键 → 无 hint，推断控件
                var val = values.TryGetValue(key, out var v) ? v : "";
                schema.Add(new SettingItem(key, "", InferControl(key, val, "") + "推断"));
            }
        }
    }

    // 无控件行的键：按值形态与说明文字推断最贴切的控件
    private static string InferControl(string key, string val, string title)
    {
        // 键名含 Color/Tint 且值是 r,g,b[,a] 数字 → 取色器（net48 无 Contains(StringComparison)，用 IndexOf）
        if ((key.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0
             || key.IndexOf("tint", StringComparison.OrdinalIgnoreCase) >= 0)
            && val.Split(',').Length >= 3
            && val.Split(',').Take(3).All(x => int.TryParse(x.Trim(), out _)))
            return "Color";
        // 0/1 → 开关
        if (val is "0" or "1") return "Checkbox:0:1";
        // 数值 + 说明文字含范围提示 → 滑杆
        if (int.TryParse(val.Trim(), out var num))
        {
            var m = Regex.Match(title, @"(\d+)\s*(?:-|to|~)\s*(\d+)");
            if (m.Success && int.Parse(m.Groups[1].Value) < int.Parse(m.Groups[2].Value))
                return $"Slider:{m.Groups[1].Value}:{m.Groups[2].Value}";
            if (title.IndexOf("opacity", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("不透明度", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Slider:1:255";
        }
        return "Text";
    }

    private HashSet<string>? incKeys;

    private HashSet<string> KeysFromInc()
    {
        incKeys ??= new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase);
        return incKeys;
    }

    private static bool LooksLikeControl(string s)
    {
        if (s.Length == 0) return false;
        var head = s.Split(':')[0].ToLowerInvariant();
        // Color 系：裸 "Color" 或 "Color:..."；带数字尾巴的 "Color1/Color2" 是变量键名，排除
        return head is "text" or "checkbox" or "slider" or "browse" or "color";
    }

    private void LoadValues(string incPath)
    {
        foreach (var line in ReadTextAuto(incPath).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var m = Regex.Match(line, @"^\s*([^=;\s][^=]*?)\s*=\s*(.*)$");
            if (m.Success && !line.TrimStart().StartsWith("[") && !line.TrimStart().StartsWith(";"))
                values[m.Groups[1].Value] = m.Groups[2].Value;
        }
    }

    private static string ReadTextAuto(string path)
    {
        var b = File.ReadAllBytes(path);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
        return Encoding.UTF8.GetString(b);
    }

    // ---------- messages ----------

    private void OnWebMessage(object? s, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = MiniJson.Parse(e.WebMessageAsJson);
        var type = MiniJson.Str(msg, "type");

        switch (type)
        {
            case "set":
                WriteValue(MiniJson.Str(msg, "key"), MiniJson.Str(msg, "value"));
                DebounceRefresh();
                break;
            case "browse":
                BrowseFolder(MiniJson.Str(msg, "key"));
                break;
            case "nav":
                var panel = MiniJson.Str(msg, "panel");
                double railY = MiniJson.Num(msg, "railY");
                // 同组内切换：保持组路径，只换最后一级
                var cfgSegs = configName.Split('\\');
                var groupRel = string.Join("\\", cfgSegs.Skip(1).Take(cfgSegs.Length - 2).ToArray());
                panelName = panel;
                configName = "REO\\" + groupRel + "\\" + panel;
                LoadPage();
                web.CoreWebView2.NavigateToString(BuildHtml());
                if (railY > 0)
                    web.CoreWebView2.ExecuteScriptAsync(
                        $"document.getElementById('rail').scrollTop={railY.ToString(System.Globalization.CultureInfo.InvariantCulture)};");
                break;
            case "win":
                HandleWin(MiniJson.Str(msg, "cmd"));
                break;
        }
    }

    private void HandleWin(string cmd)
    {
        switch (cmd)
        {
            case "min": WindowState = FormWindowState.Minimized; break;
            case "close": Close(); break;
            case "max":
                if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
                else { MaximizedBounds = Screen.FromHandle(Handle).WorkingArea; WindowState = FormWindowState.Maximized; }
                break;
            case "drag":
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                break;
        }
    }

    private void BrowseFolder(string key)
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var path = dlg.SelectedPath + "\\";
        WriteValue(key, path);
        web.CoreWebView2.ExecuteScriptAsync(
            $"var el=document.querySelector('[data-key={MiniJson.JsStr(key)}]');if(el){{el.value={MiniJson.JsStr(path)};}}");
        DebounceRefresh();
    }

    private void WriteValue(string key, string value)
    {
        // local 覆盖文件存在时写 local（皮肤 @include 链里 local 后加载、键值胜出）
        var incPath = localIncPath ?? CfgPath("UserVariables.inc");
        if (!File.Exists(incPath)) return;
        var raw = File.ReadAllBytes(incPath);
        bool u16 = raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE;
        var enc = u16 ? Encoding.Unicode : new UTF8Encoding(false);
        var text = enc.GetString(raw, u16 ? 2 : 0, raw.Length - (u16 ? 2 : 0));
        // Rainmeter 变量名大小写不敏感，文件里的键可能与 schema 键大小写不同
        // 值必须走 MatchEvaluator：替换串里拼 "$1"+value 时，数字开头的值（复选框写 0/1、
        // 滑杆与数字框写整数）会被当作组引用 "$10" 之类，.NET 找不到该组就把整个 "$10"
        // 原样写进文件，行内容被毁。正则也只吃到行尾前，保留原行尾符。
        var pattern = new Regex(@"(?im)^(" + Regex.Escape(key) + @"\s*=)[^\r\n]*");
        text = pattern.IsMatch(text)
            ? pattern.Replace(text, m => m.Groups[1].Value + value, 1)
            : text.TrimEnd() + "\r\n" + key + "=" + value + "\r\n";
        File.WriteAllBytes(incPath, u16
            ? new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(text)).ToArray()
            : enc.GetBytes(text));
    }

    private void DebounceRefresh()
    {
        refreshDebounce ??= new System.Windows.Forms.Timer { Interval = 300 };
        refreshDebounce.Stop();
        refreshDebounce.Tick -= OnRefreshTick;
        refreshDebounce.Tick += OnRefreshTick;
        refreshDebounce.Start();
    }

    private void OnRefreshTick(object? s, EventArgs _)
    {
        refreshDebounce!.Stop();
        RunRainmeterBang("!Refresh", configName);   // 实时预览：写盘后刷新面板
    }

    // Rainmeter 安装路径解析顺序与旧 AutoIt 版 _RainmeterExe() 一致：
    // #PROGRAMPATH# → ProgramW6432\Rainmeter → ProgramFiles\Rainmeter → ProgramFiles(x86)\Rainmeter
    private void RunRainmeterBang(string bang, string arg)
    {
        string? exe = programPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            exe = null;
            // net48 无 Environment.SpecialFolder.ProgramW6432 枚举，走环境变量
            var roots = new[]
            {
                Environment.GetEnvironmentVariable("ProgramW6432"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            };
            foreach (var root in roots)
            {
                var p = Path.Combine(root, "Rainmeter", "Rainmeter.exe");
                if (File.Exists(p)) { exe = p; break; }
            }
        }
        if (exe == null || !File.Exists(exe)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        { FileName = exe, Arguments = $"{bang} \"{arg}\"", UseShellExecute = false, CreateNoWindow = true });
    }

    // ---------- UI ----------

    private List<string> PanelList()
    {
        // 列出当前 config 所在目录的兄弟配置目录
        var segs = configName.Split('\\');
        var parentRel = string.Join("\\", segs.Skip(1).Take(segs.Length - 2).ToArray());
        var dir = Path.Combine(skinsRoot, "REO", "@Resources", "Config", parentRel);
        var list = new List<string>();
        if (Directory.Exists(dir))
            foreach (var d in Directory.GetDirectories(dir))
                if (HasSettings(d))
                    list.Add(Path.GetFileName(d));
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>配置目录里有可编辑项才算数：只有空行/注释的 RainConfigure.cfg 会在侧栏点出「0 项设置」。</summary>
    private bool HasSettings(string dirPath)
    {
        var cfg = Path.Combine(dirPath, "RainConfigure.cfg");
        if (!File.Exists(cfg)) return false;
        int rows = 0;
        foreach (var line in ReadTextAuto(cfg).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("#")) continue;
            rows++;
        }
        return rows >= 4;   // schema 四行一组（参数名/标题/控件类型/空行）
    }

    private string BuildHtml()
    {
        var labels = Lang.Labels;

        var rows = new StringBuilder();
        foreach (var item in schema)
        {
            var cur = values.TryGetValue(item.Key, out var v) ? v : "";
            // 中文标签优先，其次英文提示，都不像标签（说明文字）就回退键名
            var label = labels.TryGetValue(item.Key, out var zh) ? zh : item.Key;
            var isHint = item.Title.Length > 0 && item.Title != item.Key
                         && (item.Title.Contains(' ') || item.Title.Contains(','));
            var parts = item.Control.Split(':');
            rows.Append("<div class=\"row\"><div class=\"label\">").Append(H(label)).Append("</div><div class=\"ctl\">");
            var kind = parts[0].ToLowerInvariant() switch
            {
                "slider" => "Slider", "checkbox" => "Checkbox", "browse" => "Browse",
                "text" => "Text", var c when c.StartsWith("color") => "Color",
                _ => parts[0],
            };
            switch (kind)
            {
                case "Slider":
                    rows.AppendFormat("<input type=\"range\" min=\"{0}\" max=\"{1}\" value=\"{2}\" data-key=\"{3}\" oninput=\"send(this)\"><span class=\"val\" id=\"v-{3}\">{2}</span>",
                        parts[1], parts[2], H(cur), H(item.Key));
                    break;
                case "Checkbox":
                    bool on = cur == parts[2];
                    rows.AppendFormat("<label class=\"switch\"><input type=\"checkbox\" data-key=\"{0}\" data-on=\"{1}\" data-off=\"{2}\" {3} onchange=\"send(this)\"><span class=\"track\"></span></label>",
                        H(item.Key), H(parts[2]), H(parts[1]), on ? "checked" : "");
                    break;
                case "Color":
                    {
                        // 色板 + HEX 文本；值形如 "0,0,0,181"，alpha 由单独滑杆管
                        rows.AppendFormat(
                            "<span class=\"swatch\" id=\"sw-{0}\" style=\"background:{1}\" onclick=\"openPicker('{0}')\"></span>" +
                            "<code class=\"chex\" id=\"cv-{0}\" title=\"{2}\">{3}</code>",
                            H(item.Key), ToCss(cur), H(cur), H(ToHexDisplay(cur)));
                        break;
                    }
                case "Text":
                    {
                        // 说明文字明确是「0 禁用 / 1 启用」的文本框，渲染成开关
                        bool binaryHint = item.Title.Contains("0 - disable") || item.Title.Contains("1 - yes")
                                          || item.Title == "0 禁用，1 启用" || item.Title.Contains("1 to enable");
                        if (binaryHint && (cur == "0" || cur == "1"))
                        {
                            rows.AppendFormat("<label class=\"switch\"><input type=\"checkbox\" data-key=\"{0}\" data-on=\"1\" data-off=\"0\" {1} onchange=\"send(this)\"><span class=\"track\"></span></label>",
                                H(item.Key), cur == "1" ? "checked" : "");
                        }
                        else
                        {
                            rows.AppendFormat("<input type=\"text\" value=\"{0}\" data-key=\"{1}\" onchange=\"send(this)\">", H(cur), H(item.Key));
                        }
                        break;
                    }
                case "Browse":
                    rows.AppendFormat("<input type=\"text\" value=\"{0}\" data-key=\"{1}\" onchange=\"send(this)\"><button class=\"btn\" onclick=\"browse('{1}')\">{2}</button>",
                        H(cur), H(item.Key), H(Lang.T("browse", "浏览…")));
                    break;
                default:
                    rows.AppendFormat("<input type=\"text\" value=\"{0}\" data-key=\"{1}\" onchange=\"send(this)\">", H(cur), H(item.Key));
                    break;
            }
            rows.Append("</div></div>");
            // 说明文字做副标题（有中文翻译用中文）
            if (isHint)
            {
                var hint = Lang.Hints.TryGetValue(item.Title, out var hz) ? hz : item.Title;
                rows.Append("<div class=\"hint\">").Append(H(hint)).Append("</div>");
            }
        }

        var nav = new StringBuilder();
        foreach (var p in PanelList())
            nav.AppendFormat("<div class=\"nav-item{0}\" onclick=\"nav('{1}')\">{1}</div>",
                p == panelName ? " active" : "", H(p));

        var bg = light ? "#f6f7f9" : "#131417";
        var fg = light ? "#191b1f" : "#e8eaed";
        var card = light ? "#ffffff" : "#1b1d21";
        var rail = light ? "#eef0f3" : "#17181c";
        var border = light ? "#00000012" : "#ffffff10";
        var accent = SystemAccent();
        var sub = light ? "#5c6370" : "#8a919d";
        var input = light ? "#f2f4f7" : "#24262c";

        var css = $$"""
* { box-sizing: border-box; margin: 0; user-select: none; }
html, body { height: 100%; overflow: hidden; }
body { font-family: "Microsoft YaHei UI", "Segoe UI Variable", "Segoe UI", sans-serif;
       background: {{bg}}; color: {{fg}}; display: flex; flex-direction: column;
       border: 1px solid {{border}}; border-radius: 12px; }
#titlebar { height: 46px; flex: 0 0 46px; display: flex; align-items: center; padding: 0 8px 0 18px;
            font-size: 12.5px; color: {{sub}}; cursor: default; }
#titlebar .logo { display: flex; align-items: center; gap: 8px; font-weight: 600; color: {{fg}}; }
#titlebar .logo .dot { width: 8px; height: 8px; border-radius: 3px; background: {{accent}}; }
#titlebar .sep { color: {{sub}}; opacity: .6; margin: 0 2px; font-weight: 400; }
#titlebar .spacer { flex: 1; }
.wbtn { width: 40px; height: 32px; display: grid; place-items: center; border-radius: 8px; cursor: pointer; transition: background .12s; }
.wbtn:hover { background: {{border}}; }
.wbtn.close:hover { background: #e81123; }
.wbtn svg { width: 12px; height: 12px; stroke: {{sub}}; stroke-width: 1.5; fill: none; transition: stroke .12s; }
.wbtn:hover svg { stroke: {{fg}}; }
.wbtn.close:hover svg { stroke: #fff; }
#main { flex: 1; display: flex; overflow: hidden; }
#rail { width: 216px; flex: 0 0 216px; background: {{rail}}; border-right: 1px solid {{border}};
        padding: 16px 12px; overflow-y: auto; }
#rail h2 { font-size: 11px; font-weight: 600; color: {{sub}}; opacity: .8; padding: 4px 12px 10px;
           letter-spacing: .12em; }
.nav-item { padding: 8px 12px; border-radius: 8px; font-size: 13.5px; cursor: pointer; margin-bottom: 2px;
            color: {{sub}}; transition: background .12s, color .12s; }
.nav-item:hover { background: {{border}}; color: {{fg}}; }
.nav-item.active { background: {{accent}}20; color: {{accent}}; font-weight: 600; }
#content { flex: 1; overflow-y: auto; padding: 30px 40px 40px; }
.page-head { display: flex; align-items: baseline; gap: 12px; margin-bottom: 24px; }
h1 { font-size: 21px; font-weight: 650; letter-spacing: .01em; }
.count { font-size: 12px; color: {{sub}}; }
.row { display: flex; align-items: center; gap: 18px; background: {{card}}; border: 1px solid {{border}};
       border-radius: 10px; padding: 14px 18px; margin-bottom: 8px; transition: border-color .12s; }
.row:hover { border-color: {{accent}}55; }
.label { flex: 0 0 180px; font-size: 13.5px; font-weight: 500; }
.ctl { flex: 1; display: flex; align-items: center; gap: 10px; min-width: 0; }
input[type=text] { flex: 1; min-width: 0; background: {{input}}; color: {{fg}}; border: 1px solid transparent;
                   border-radius: 8px; padding: 8px 12px; font-size: 13px; user-select: text; transition: border-color .12s, box-shadow .12s; }
input[type=text]:focus { outline: none; border-color: {{accent}}; box-shadow: 0 0 0 3px {{accent}}2e; }
input[type=range] { flex: 1; accent-color: {{accent}}; }
.val { min-width: 34px; text-align: right; font-variant-numeric: tabular-nums; font-size: 13px; color: {{sub}}; }
.btn { background: {{accent}}; color: #fff; border: 0; border-radius: 8px; padding: 8px 16px;
       font-size: 12.5px; font-weight: 500; cursor: pointer; transition: filter .12s; }
.btn:hover { filter: brightness(1.12); }
.hint { font-size: 12px; color: {{sub}}; opacity: .75; margin: -2px 0 12px 18px; }
.switch input { display: none; }
.track { display: block; width: 42px; height: 24px; border-radius: 12px; background: {{sub}}66;
         position: relative; transition: background .15s; cursor: pointer; }
.track::after { content: ""; position: absolute; top: 3px; left: 3px; width: 18px; height: 18px;
                border-radius: 50%; background: #fff; transition: left .15s; box-shadow: 0 1px 3px #0006; }
.switch input:checked + .track { background: {{accent}}; }
.switch input:checked + .track::after { left: 21px; }
.swatch { width: 34px; height: 24px; border-radius: 6px; border: 1px solid {{border}};
          cursor: pointer; flex: 0 0 34px; }
.chex { font-family: "Cascadia Code", Consolas, monospace; font-size: 12.5px; color: {{sub}};
        background: {{input}}; border-radius: 6px; padding: 5px 9px; }
.cval { min-width: 90px; }
.picker { position: fixed; z-index: 50; width: 248px; background: {{card}}; border: 1px solid {{border}};
          border-radius: 12px; padding: 14px; box-shadow: 0 12px 40px #000a; }
.pv { position: relative; height: 150px; border-radius: 8px; cursor: crosshair; overflow: hidden;
      touch-action: none; }
.pv .g1 { position: absolute; inset: 0; background: linear-gradient(to right, #fff, transparent); }
.pv .g2 { position: absolute; inset: 0; background: linear-gradient(to top, #000, transparent); }
.pk-cur { position: absolute; width: 14px; height: 14px; border: 2px solid #fff; border-radius: 50%;
          transform: translate(-50%, -50%); box-shadow: 0 0 4px #0009; pointer-events: none; }
.ph { position: relative; height: 14px; border-radius: 7px; margin: 14px 0 12px; cursor: pointer;
      touch-action: none;
      background: linear-gradient(to right, #f00, #ff0, #0f0, #0ff, #00f, #f0f, #f00); }
.ph .pk-cur { top: 50%; }
.pk-row { display: flex; gap: 8px; align-items: center; }
.pk-row label { font-size: 11px; color: {{sub}}; width: 14px; }
.pk-row input { flex: 1; min-width: 0; background: {{input}}; color: {{fg}}; border: 1px solid {{border}};
                border-radius: 6px; padding: 6px 8px; font-size: 12.5px; user-select: text; }
.pk-row input:focus { outline: none; border-color: {{accent}}; }
.pk-preview { width: 34px; height: 34px; border-radius: 8px; border: 1px solid {{border}}; flex: 0 0 34px; }
::-webkit-scrollbar { width: 8px; }
::-webkit-scrollbar-thumb { background: {{sub}}44; border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: {{sub}}77; }
""";

        var js = $$"""
const tb = document.getElementById("titlebar");
tb.addEventListener("mousedown", e => { if (e.target.closest(".wbtn")) return; chrome.webview.postMessage({type:"win",cmd:"drag"}); });
tb.addEventListener("dblclick", e => { if (e.target.closest(".wbtn")) return; chrome.webview.postMessage({type:"win",cmd:"max"}); });
function win(cmd) { chrome.webview.postMessage({ type: "win", cmd: cmd }); }
function nav(p) { chrome.webview.postMessage({ type: "nav", panel: p, railY: document.getElementById("rail").scrollTop }); }
function send(el) {
  let v = el.type === "checkbox" ? (el.checked ? el.dataset.on : el.dataset.off) : el.value;
  if (el.type === "range") { let s = document.getElementById("v-" + el.dataset.key); if (s) s.textContent = el.value; }
  chrome.webview.postMessage({ type: "set", key: el.dataset.key, value: v });
}
function browse(key) { chrome.webview.postMessage({ type: "browse", key: key }); }

/* ---- 自绘深色取色器（明度方块 + 色相条） ---- */
let pk = null;
function hsv2rgb(h, s, v) {
  const c = v * s, x = c * (1 - Math.abs(((h / 60) % 2) - 1)), m = v - c;
  let r, g, b;
  if (h < 60) [r, g, b] = [c, x, 0]; else if (h < 120) [r, g, b] = [x, c, 0];
  else if (h < 180) [r, g, b] = [0, c, x]; else if (h < 240) [r, g, b] = [0, x, c];
  else if (h < 300) [r, g, b] = [x, 0, c]; else [r, g, b] = [c, 0, x];
  return [Math.round((r + m) * 255), Math.round((g + m) * 255), Math.round((b + m) * 255)];
}
function rgb2hsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
  let h = 0;
  if (d) {
    if (mx === r) h = ((g - b) / d) % 6; else if (mx === g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
    h *= 60; if (h < 0) h += 360;
  }
  return [h, mx ? d / mx : 0, mx];
}
function openPicker(key) {
  closePicker();
  const cur = (document.getElementById("cv-" + key)?.textContent || "128,128,128").split(",");
  const hsv = rgb2hsv(parseInt(cur[0]) || 0, parseInt(cur[1]) || 0, parseInt(cur[2]) || 0);
  const alpha = cur[3] ? cur[3].trim() : "";
  const sw = document.getElementById("sw-" + key);
  const el = document.createElement("div");
  el.className = "picker"; el.id = "picker";
  el.innerHTML =
    '<div class="pv"><div class="g1"></div><div class="g2"></div><div class="pk-cur"></div></div>' +
    '<div class="ph"><div class="pk-cur"></div></div>' +
    '<div class="pk-row"><span class="pk-preview"></span>' +
    '<label>R</label><input data-c="0" maxlength="3"><label>G</label><input data-c="1" maxlength="3"><label>B</label><input data-c="2" maxlength="3"></div>';
  document.body.appendChild(el);
  const r = sw.getBoundingClientRect();
  el.style.left = Math.min(r.left, innerWidth - 262) + "px";
  el.style.top = Math.min(r.bottom + 8, innerHeight - 260) + "px";
  pk = { key, hsv, alpha, el, sw };
  const pv = el.querySelector(".pv"), ph = el.querySelector(".ph");
  drag(pv, (x, y) => { pk.hsv[1] = x; pk.hsv[2] = 1 - y; paintPicker(); commit(); });
  drag(ph, (x) => { pk.hsv[0] = x * 360; paintPicker(); commit(); });
  el.querySelectorAll(".pk-row input").forEach(inp => {
    inp.addEventListener("change", () => {
      const rgb = [...el.querySelectorAll(".pk-row input")].map(i => Math.max(0, Math.min(255, parseInt(i.value) || 0)));
      pk.hsv = rgb2hsv(rgb[0], rgb[1], rgb[2]); paintPicker(); commit();
    });
  });
  paintPicker();
  setTimeout(() => document.addEventListener("mousedown", outsidePicker, true), 0);
}
function drag(el, fn) {
  const move = e => { const r = el.getBoundingClientRect();
    fn(Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)), Math.max(0, Math.min(1, (e.clientY - r.top) / r.height))); };
  el.addEventListener("pointerdown", e => { el.setPointerCapture(e.pointerId); move(e);
    const mv = e2 => move(e2);
    el.addEventListener("pointermove", mv);
    el.addEventListener("pointerup", () => el.removeEventListener("pointermove", mv), { once: true }); });
}
function paintPicker() {
  const [h, s, v] = pk.hsv;
  const rgb = hsv2rgb(h, s, v), pure = hsv2rgb(h, 1, 1);
  const el = pk.el;
  el.querySelector(".pv").style.background = "rgb(" + pure.join(",") + ")";
  const c1 = el.querySelector(".pv .pk-cur");
  c1.style.left = (s * 100) + "%"; c1.style.top = ((1 - v) * 100) + "%";
  el.querySelector(".ph .pk-cur").style.left = (h / 360 * 100) + "%";
  el.querySelector(".pk-preview").style.background = "rgb(" + rgb.join(",") + ")";
  const ins = el.querySelectorAll(".pk-row input");
  ins[0].value = rgb[0]; ins[1].value = rgb[1]; ins[2].value = rgb[2];
}
function commit() {
  const rgb = hsv2rgb(...pk.hsv);
  let val = rgb.join(",");
  let hex = "#" + rgb.map(x => x.toString(16).padStart(2, "0")).join("");
  if (pk.alpha !== "") {
    val += "," + pk.alpha;
    hex += (parseInt(pk.alpha) || 0).toString(16).padStart(2, "0");
  }
  pk.sw.style.background = "rgb(" + rgb.join(",") + ")";
  const cv = document.getElementById("cv-" + pk.key);
  if (cv) { cv.textContent = hex; cv.title = val; }
  chrome.webview.postMessage({ type: "set", key: pk.key, value: val });
}
function outsidePicker(e) { if (pk && !pk.el.contains(e.target) && e.target.id !== "sw-" + pk.key) closePicker(); }
function closePicker() {
  if (!pk) return;
  pk.el.remove(); pk = null;
  document.removeEventListener("mousedown", outsidePicker, true);
}
""";

        return $$"""
<!DOCTYPE html><html><head><meta charset="utf-8"><style>
{{css}}
</style></head><body>
<div id="titlebar">
  <span class="logo"><span class="dot"></span>ReOmnimo</span><span class="sep">/</span><span>{{panelName}} {{Lang.T("settings", "设置")}}</span>
  <span class="spacer"></span>
  <span class="wbtn" onclick="win('min')"><svg viewBox="0 0 12 12"><path d="M1 6h10"/></svg></span>
  <span class="wbtn" onclick="win('max')"><svg viewBox="0 0 12 12"><rect x="2" y="2" width="8" height="8" rx="1.5"/></svg></span>
  <span class="wbtn close" onclick="win('close')"><svg viewBox="0 0 12 12"><path d="M2 2l8 8M10 2l-8 8"/></svg></span>
</div>
<div id="main">
  <div id="rail"><h2>{{Lang.T("panels", "面板")}}</h2>{{nav}}</div>
  <div id="content">
    <div class="page-head"><h1>{{panelName}}</h1><span class="count">{{schema.Count}} {{Lang.T("items", "项设置")}}</span></div>
    {{rows}}
  </div>
</div>
<script>
{{js}}
</script>
</body></html>
""";
    }

    private static bool IsSystemLight()
    {
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 0);
            return v is int i && i == 1;
        }
        catch { return false; }
    }

    // Windows 系统强调色（个性化→颜色）；读不到时回退靛蓝
    private static string SystemAccent()
    {
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null);
            if (v is int abgr)
            {
                // DWM 存的是 AABBGGRR
                return $"#{abgr & 0xFF:x2}{(abgr >> 8) & 0xFF:x2}{(abgr >> 16) & 0xFF:x2}";
            }
        }
        catch { }
        return "#4d7cfe";
    }

    // "r,g,b[,a]" → #rrggbb 与 alpha；非数字值（white/black/空）回退灰
    private static (string hex, string alpha) ToHex(string v)
    {
        var p = v.Split(',');
        if (p.Length >= 3 && p.Take(3).All(x => int.TryParse(x.Trim(), out _)))
        {
            int r = int.Parse(p[0]), g = int.Parse(p[1]), b = int.Parse(p[2]);
            var alpha = p.Length >= 4 && int.TryParse(p[3].Trim(), out _) ? p[3].Trim() : "";
            return ($"#{r:x2}{g:x2}{b:x2}", alpha);
        }
        return ("#808080", "");
    }

    private static string ToCss(string v)
    {
        var p = v.Split(',');
        if (p.Length >= 3 && p.Take(3).All(x => int.TryParse(x.Trim(), out _)))
        {
            var a = p.Length >= 4 && int.TryParse(p[3].Trim(), out var ai) ? ai / 255.0 : 1.0;
            return $"rgba({p[0].Trim()},{p[1].Trim()},{p[2].Trim()},{a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
        }
        return v.Trim().Length > 0 ? v : "transparent";
    }

    // "0,0,0,181" → "#000000b5"；非数字原样返回
    private static string ToHexDisplay(string v)
    {
        var p = v.Split(',');
        if (p.Length < 3 || !p.Take(3).All(x => int.TryParse(x.Trim(), out _))) return v;
        int r = int.Parse(p[0]), g = int.Parse(p[1]), b = int.Parse(p[2]);
        var hex = $"#{r:x2}{g:x2}{b:x2}";
        if (p.Length >= 4 && int.TryParse(p[3].Trim(), out var a)) hex += a.ToString("x2");
        return hex;
    }

    private static string H(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
