// ReOmnimo 工具共享层
// 本文件按源码链接（<Compile Include>）编进每个工具 exe，不产出独立 DLL，
// 部署形态与 AutoIt 时代一致：单个 exe 丢进 REO\@Resources\Common\ 下即可。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace OmnimoTools
{
    /// <summary>Rainmeter 进程间通信（等价于 AutoIT\Includes\Common.au3 的 SendBang）。</summary>
    internal static class Rainmeter
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        private const int WM_COPYDATA = 0x004A;

        /// <summary>
        /// 向 Rainmeter 的 RainmeterMeterWindow 发 WM_COPYDATA（dwData=1，载荷为 UTF-16 文本）。
        /// Rainmeter 未运行时静默失败（与 AutoIt 版一致）。
        /// </summary>
        public static bool SendBang(string bang)
        {
            IntPtr hWnd = FindWindow("RainmeterMeterWindow", null);
            if (hWnd == IntPtr.Zero) return false;

            byte[] bytes = Encoding.Unicode.GetBytes(bang + "\0");
            IntPtr mem = Marshal.AllocHGlobal(bytes.Length);
            IntPtr cds = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(COPYDATASTRUCT)));
            try
            {
                Marshal.Copy(bytes, 0, mem, bytes.Length);
                var payload = new COPYDATASTRUCT { dwData = (IntPtr)1, cbData = bytes.Length, lpData = mem };
                Marshal.StructureToPtr(payload, cds, false);
                SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, cds);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(cds);
                Marshal.FreeHGlobal(mem);
            }
        }
    }

    /// <summary>对话框（等价于 Common.au3 的 OmnimoError 与各工具的 MsgBox）。</summary>
    internal static class OmnimoDialogs
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxTimeoutW")]
        private static extern int MessageBoxTimeout(IntPtr hWnd, string lpText, string lpCaption,
            uint uType, ushort wLanguageId, uint dwMilliseconds);

        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONERROR = 0x00000010;
        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint MB_SETFOREGROUND = 0x00010000;

        /// <summary>等价于 Common.au3 的 OmnimoError()：错误框（等用户关闭）+ 退出进程。</summary>
        public static void Error(string title, string message)
        {
            string text = message + "\r\n\r\n"
                + "Please send an e-mail to omnimosupport@gmail.com or" + "\r\n"
                + "file an issue at github with the following information:" + "\r\n\r\n"
                + "- This error message" + "\r\n"
                + "- Steps to reproduce the issue" + "\r\n"
                + "- Your version of Rainmeter and Windows";
            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
        }

        /// <summary>等价于 MsgBox($MB_ICONINFORMATION, "Info", $text, 2)：2 秒后自动关闭的信息框。</summary>
        public static void Info(string text, uint milliseconds = 2000)
        {
            try
            {
                MessageBoxTimeout(IntPtr.Zero, text, "Info", MB_ICONINFORMATION | MB_SETFOREGROUND, 0, milliseconds);
            }
            catch (EntryPointNotFoundException)
            {
                // MessageBoxTimeoutW 缺失时退回阻塞式信息框（不自动关闭）
                MessageBox.Show(text, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>等价于 MsgBox(0, $title, $text)：无图标的消息框。</summary>
        public static void Message(string title, string text)
        {
            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.DefaultDesktopOnly);
        }

        /// <summary>等价于 AutoIt 的 DirCreate：目录已存在也算成功。</summary>
        public static bool EnsureDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) return true;
                Directory.CreateDirectory(path);
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>等价于 AutoIt 的 DirRemove($path, $DIR_REMOVE)。</summary>
        public static void RemoveDirectoryTree(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
                // AutoIt 的 DirRemove 失败也不报错
            }
        }

        /// <summary>等价于 AutoIt 的 FileCopy($src, $dst, 1)：覆盖式复制；失败返回 false。</summary>
        public static bool CopyFile(string source, string destination, bool createDestinationDirectory = false)
        {
            try
            {
                if (createDestinationDirectory)
                {
                    string dir = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
                File.Copy(source, destination, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 编码保真的 INI 编辑器。替代 AutoIt 的 IniRead / IniWrite / IniDelete /
    /// IniReadSectionNames / IniReadSection 五个函数。
    /// 写回时沿用原文件编码（含 BOM）与逐行行尾，绝不重排或改写其它行；
    /// 段名与键名按不区分大小写匹配（Windows 配置文件语义）。
    /// </summary>
    internal sealed class IniDoc
    {
        private struct Line
        {
            public string Text;
            public string Eol;
        }

        private readonly string _path;
        private readonly Encoding _encoding;
        private readonly bool _hasBom;
        private readonly List<Line> _lines = new List<Line>();

        private IniDoc(string path, Encoding encoding, bool hasBom)
        {
            _path = path;
            _encoding = encoding;
            _hasBom = hasBom;
        }

        public string PathName { get { return _path; } }

        /// <summary>读入失败（文件不存在 / 无权限 / 目录）返回 null，对应 AutoIt IniRead 的 @error。</summary>
        public static IniDoc TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                Encoding encoding;
                bool hasBom;
                DetectEncoding(bytes, out encoding, out hasBom);
                int offset = 0;
                if (hasBom)
                {
                    if (encoding.CodePage == 1200 || encoding.CodePage == 1201) offset = 2;
                    else if (encoding.CodePage == 65001) offset = 3;
                }
                string text = encoding.GetString(bytes, offset, bytes.Length - offset);
                var doc = new IniDoc(path, encoding, hasBom);
                doc._lines.AddRange(SplitLines(text));
                return doc;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>新建（或读入现有）文件；新文件按系统 ANSI 编码写出，与 AutoIt IniWrite 一致。</summary>
        public static IniDoc LoadOrCreate(string path)
        {
            IniDoc doc = TryLoad(path);
            if (doc != null) return doc;
            return new IniDoc(path, Encoding.Default, false);
        }

        /// <summary>编码嗅探（顺序与 AGENTS.md 一致）：UTF-16LE+BOM → UTF-8+BOM → UTF-8 → cp936 → cp1252。</summary>
        private static void DetectEncoding(byte[] bytes, out Encoding encoding, out bool hasBom)
        {
            hasBom = false;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = new UnicodeEncoding(false, true);
                hasBom = true;
                return;
            }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                hasBom = true;
                return;
            }
            // 必须用抛异常的 UTF-8（throwOnInvalidBytes:true）：默认实例会把非法字节
            // 静默换成 U+FFFD，导致 cp1252/cp936 老文件被"无损"误判成 UTF-8 而写坏。
            encoding = StrictDecode(bytes, new UTF8Encoding(false, true));
            if (encoding != null) return;
            encoding = StrictDecode(bytes, Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
            if (encoding != null) return;
            encoding = Encoding.GetEncoding(1252);
        }

        /// <summary>能否用该编码无损解码；能则返回该编码（无损解码保证原样回写，字节不变）。</summary>
        private static Encoding StrictDecode(byte[] bytes, Encoding candidate)
        {
            try
            {
                candidate.GetString(bytes);
                return candidate;
            }
            catch
            {
                return null;
            }
        }

        private static List<Line> SplitLines(string text)
        {
            var lines = new List<Line>();
            int i = 0, start = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\r' || c == '\n')
                {
                    string eol = (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') ? "\r\n" : c.ToString();
                    lines.Add(new Line { Text = text.Substring(start, i - start), Eol = eol });
                    i += eol.Length;
                    start = i;
                }
                else i++;
            }
            if (start < text.Length) lines.Add(new Line { Text = text.Substring(start), Eol = "" });
            return lines;
        }

        private string DominantEol()
        {
            foreach (Line l in _lines) if (l.Eol.Length > 0) return l.Eol;
            return "\r\n";
        }

        // ---- 行内容解析 ----

        private static bool IsSectionHeader(string text, out string name)
        {
            name = null;
            string t = text.Trim();
            if (t.Length < 2 || t[0] != '[' || t[t.Length - 1] != ']') return false;
            name = t.Substring(1, t.Length - 2);
            return true;
        }

        private static bool IsKeyLine(string text, out string key, out string value)
        {
            key = null;
            value = null;
            string t = text.TrimStart();
            if (t.Length == 0 || t[0] == ';' || t[0] == '#') return false;
            int eq = text.IndexOf('=');
            if (eq <= 0) return false;
            key = text.Substring(0, eq).Trim();
            value = text.Substring(eq + 1).Trim();
            return key.Length > 0;
        }

        private int FindSection(string section)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                string name;
                if (IsSectionHeader(_lines[i].Text, out name) && string.Equals(name, section, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private int FindSectionEnd(int sectionIndex)
        {
            for (int i = sectionIndex + 1; i < _lines.Count; i++)
            {
                string name;
                if (IsSectionHeader(_lines[i].Text, out name)) return i;
            }
            return _lines.Count;
        }

        // ---- 读 ----

        /// <summary>等价于 IniRead：段/键不存在或文件不可读时返回 default。</summary>
        public string Get(string section, string key, string defaultValue)
        {
            int s = FindSection(section);
            if (s < 0) return defaultValue;
            int end = FindSectionEnd(s);
            for (int i = s + 1; i < end; i++)
            {
                string k, v;
                if (!IsKeyLine(_lines[i].Text, out k, out v)) continue;
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return v;
            }
            return defaultValue;
        }

        /// <summary>等价于 IniReadSectionNames：按出现顺序返回全部段名。</summary>
        public List<string> SectionNames()
        {
            var names = new List<string>();
            foreach (Line l in _lines)
            {
                string name;
                if (IsSectionHeader(l.Text, out name)) names.Add(name);
            }
            return names;
        }

        /// <summary>等价于 IniReadSection：段内全部键值（不区分大小写的字典）。</summary>
        public Dictionary<string, string> Section(string section)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int s = FindSection(section);
            if (s < 0) return map;
            int end = FindSectionEnd(s);
            for (int i = s + 1; i < end; i++)
            {
                string k, v;
                if (!IsKeyLine(_lines[i].Text, out k, out v)) continue;
                if (!map.ContainsKey(k)) map[k] = v;
            }
            return map;
        }

        /// <summary>
        /// 等价于 IniReadSection 的“按行序取值”用法：返回段内全部键值（保持文件里的出现顺序）。
        /// ConfigBackground / PanelCreator 读 Colors.inc 时按 $Sections[$i][1] 的下标取值
        /// （规定 1=GuiBG 2=PanelBG … 13=Color6），必须按行序，不能按键名查。
        /// </summary>
        public List<KeyValuePair<string, string>> SectionOrdered(string section)
        {
            var list = new List<KeyValuePair<string, string>>();
            int s = FindSection(section);
            if (s < 0) return list;
            int end = FindSectionEnd(s);
            for (int i = s + 1; i < end; i++)
            {
                string k, v;
                if (!IsKeyLine(_lines[i].Text, out k, out v)) continue;
                list.Add(new KeyValuePair<string, string>(k, v));
            }
            return list;
        }

        // ---- 写 ----

        /// <summary>等价于 IniWrite：段不存在则补段头；键存在则原地换值，否则追加到该段末尾。</summary>
        public void Set(string section, string key, string value)
        {
            int s = FindSection(section);
            if (s < 0)
            {
                EnsureTrailingEol();
                string eol = DominantEol();
                _lines.Add(new Line { Text = "[" + section + "]", Eol = eol });
                _lines.Add(new Line { Text = key + "=" + value, Eol = eol });
                return;
            }

            int end = FindSectionEnd(s);
            for (int i = s + 1; i < end; i++)
            {
                string k, v;
                if (!IsKeyLine(_lines[i].Text, out k, out v)) continue;
                if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
                // 只换 "=" 之后的值，保留文件里原有的键名拼写与等号前后空白
                // （Windows 配置文件语义 / AutoIt IniWrite 实测行为：键名大小写不变）
                Line line = _lines[i];
                int eq = line.Text.IndexOf('=');
                if (eq >= 0) line.Text = line.Text.Substring(0, eq + 1) + value;
                else line.Text = k + "=" + value;
                _lines[i] = line;
                return;
            }

            // 追加到段末：插到最后一个非空行之后（保持段内紧凑、不留空行）
            int insertAt = end;
            while (insertAt - 1 > s && _lines[insertAt - 1].Text.Trim().Length == 0) insertAt--;
            string newEol = DominantEol();
            if (insertAt == _lines.Count) EnsureTrailingEol();
            _lines.Insert(insertAt, new Line { Text = key + "=" + value, Eol = newEol });
        }

        /// <summary>等价于 IniDelete：段或键不存在时什么都不做。</summary>
        public void DeleteKey(string section, string key)
        {
            int s = FindSection(section);
            if (s < 0) return;
            int end = FindSectionEnd(s);
            for (int i = s + 1; i < end; i++)
            {
                string k, v;
                if (!IsKeyLine(_lines[i].Text, out k, out v)) continue;
                if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
                _lines.RemoveAt(i);
                return;
            }
        }

        private void EnsureTrailingEol()
        {
            if (_lines.Count == 0) return;
            Line last = _lines[_lines.Count - 1];
            if (last.Eol.Length == 0)
            {
                last.Eol = DominantEol();
                _lines[_lines.Count - 1] = last;
            }
        }

        /// <summary>按原编码（含 BOM）与原始行尾写回磁盘。</summary>
        public void Save()
        {
            var sb = new StringBuilder();
            foreach (Line l in _lines)
            {
                sb.Append(l.Text);
                sb.Append(l.Eol);
            }
            string text = sb.ToString();
            string dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (_encoding.CodePage == 1200)
            {
                // UTF-16LE：BOM 显式前置
                var bytes = new List<byte>();
                if (_hasBom) { bytes.Add(0xFF); bytes.Add(0xFE); }
                bytes.AddRange(Encoding.Unicode.GetBytes(text));
                File.WriteAllBytes(_path, bytes.ToArray());
                return;
            }
            if (_encoding.CodePage == 65001)
            {
                var bytes = new List<byte>();
                if (_hasBom) { bytes.Add(0xEF); bytes.Add(0xBB); bytes.Add(0xBF); }
                bytes.AddRange(new UTF8Encoding(false).GetBytes(text));
                File.WriteAllBytes(_path, bytes.ToArray());
                return;
            }

            byte[] raw = _encoding.GetBytes(text);
            File.WriteAllBytes(_path, raw);
        }
    }

    /// <summary>界面语言包（等价于各 AutoIt 工具读 Background\Language\&lt;Language&gt;.cfg 的写法）。</summary>
    internal static class LanguagePack
    {
        public static Dictionary<string, string> Load(string skinPath)
        {
            string defaultLang = "English";
            IniDoc varrar = IniDoc.TryLoad(Paths.Combine(skinPath, @"REO\@Resources\Common\Background\Varrar.inc"));
            string language = varrar != null ? varrar.Get("Variables", "Language", defaultLang) : defaultLang;

            string langFile = Paths.Combine(skinPath, @"REO\@Resources\Common\Background\Language\" + language + ".cfg");
            IniDoc doc = IniDoc.TryLoad(langFile);
            if (doc == null)
                OmnimoDialogs.Error("Error loading language", "Unable to load language file for " + language);
            return doc.Section("Variables");
        }

        /// <summary>取语言键；缺失返回空串（AutoIt 的 Dictionary.Item 会抛错，这里退化为空串更稳）。</summary>
        public static string Text(Dictionary<string, string> language, string key)
        {
            string value;
            return language.TryGetValue(key, out value) ? value : string.Empty;
        }
    }

    /// <summary>路径拼接：与 AutoIt 的字符串相加一致（参数已含尾反斜杠时不重复）。</summary>
    internal static class Paths
    {
        public static string Combine(string basePath, string relative)
        {
            if (string.IsNullOrEmpty(basePath)) return relative;
            if (basePath.EndsWith("\\", StringComparison.Ordinal) || basePath.EndsWith("/", StringComparison.Ordinal))
                return basePath + relative;
            return basePath + "\\" + relative;
        }

        /// <summary>AutoIt 的 @ScriptDir（exe 所在目录，无尾反斜杠，无长路径前缀）。</summary>
        public static string ScriptDirectory()
        {
            return AppContext.BaseDirectory.TrimEnd('\\', '/');
        }
    }

    /// <summary>AutoIt 数值/字符串比较语义的复刻。</summary>
    internal static class AutoIt
    {
        /// <summary>
        /// AutoIt 把字符串当数值用时的强制转换（`Int()`、`Number()`、`$s + 0`、`Hex()` 参数等）：
        /// 取**前导**数值，读不到数字得 0。实测：`Int("1abc")=1`、`Int("0x10")=16`、
        /// `Number("0,0,0,181")=0`、`Int(" 10 ")=10`、`Int("abc")=0`。
        /// </summary>
        public static double ToNumber(string value)
        {
            return Number(value);
        }

        /// <summary>
        /// AutoIt 的 `$value = 1`：右侧是**数值字面量**，故左侧按 Number() 取**前导**数值再作数值比较。
        /// 实测：`"1abc" = 1` 为真、`"abc" = 0` 为真、`"0x10" = 0` 为假、`"0x10" = 16` 为真。
        /// </summary>
        public static bool NumericEquals(string value, double expected)
        {
            return Number(value) == expected;
        }

        /// <summary>
        /// AutoIt 的 `$a &lt;&gt; $b`（两边都是**字符串**）：不区分大小写的字符串比较，不做数值转换、
        /// 不 Trim。实测：`"00" &lt;&gt; "0"` 为真、`"0.0" &lt;&gt; "0"` 为真、`" 0 " &lt;&gt; "0"` 为真。
        /// </summary>
        public static bool StringNotEqual(string value, string other)
        {
            return !string.Equals(value ?? string.Empty, other ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// AutoIt 的 `IniRead(...) &lt;&gt; "0"`：IniRead 返回字符串、字面量也是字符串，
        /// 故是**字符串比较**——`"00"`/`"0.0"`/`" 0 "` 都算「非 0」（即视为活动）。
        /// </summary>
        public static bool IsNonZeroString(string value)
        {
            return StringNotEqual(value, "0");
        }

        /// <summary>AutoIt 数字转字符串（整数不带小数点，小数去掉尾随零）。</summary>
        public static string Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            if (Math.Abs(value) < 1e15 && value == Math.Floor(value))
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.###############", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// AutoIt 的 `Int($v)`：取前导数值后**向零截断**。实测：`Int(1.9)=1`、`Int(-1.9)=-1`、
        /// `Int("1abc")=1`、`Int("0x10")=16`、`Int("abc")=0`。
        /// </summary>
        public static long Int(string value)
        {
            return (long)Math.Truncate(ToNumber(value));
        }

        /// <summary>AutoIt 的 `Round($v)`：四舍五入、**.5 远离零**。实测 `Round(1.5)=2`、`Round(2.5)=3`。</summary>
        public static double RoundString(string value)
        {
            return Math.Round(ToNumber(value), MidpointRounding.AwayFromZero);
        }

        /// <summary>前导数值：可选符号 + 0x 十六进制 或 十进制（可带小数/指数），与 AutoIt 的字符串取数一致。</summary>
        private static readonly Regex NumberPrefix = new Regex(
            @"^\s*([+-]?(?:0[xX][0-9A-Fa-f]+|(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?))", RegexOptions.Compiled);

        /// <summary>
        /// AutoIt 的 Number()：取字符串的**前导**数值，读不到数字得 0。
        /// 实测：`Number("0,0,0,181")=0`、`Number("0xFF8000")=16744448`、`Number("1abc")=1`、
        /// `Number("0xZZ")=0`、`Number(" 10 ")=10`、`Number("1e3")=1000`。
        /// </summary>
        public static double Number(string value)
        {
            double result;
            return TryNumber(value, out result) ? result : 0;
        }

        /// <summary>能否取到前导数值；false 表示该串不含前导数字（AutoIt 里当字符串处理）。</summary>
        public static bool TryNumber(string value, out double result)
        {
            result = 0;
            if (string.IsNullOrEmpty(value)) return false;
            Match m = NumberPrefix.Match(value);
            if (!m.Success) return false;
            string token = m.Groups[1].Value;
            bool negative = false;
            if (token.StartsWith("+", StringComparison.Ordinal)) token = token.Substring(1);
            else if (token.StartsWith("-", StringComparison.Ordinal)) { negative = true; token = token.Substring(1); }

            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                ulong hex;
                if (!ulong.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex)) return false;
                result = negative ? -(double)hex : hex;
                return true;
            }
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return false;
            if (negative) result = -result;
            return true;
        }

        /// <summary>
        /// AutoIt 的 `If $var` 求值（$var 是**字符串**时）：只有空串为假，非空串一律为真——
        /// 实测 `If "0"` / `If "00"` / `If "0.0"` / `If " "` 在 AutoIt 里**全部为真**，
        /// 这里**不能**按前导数值判真假。
        /// </summary>
        public static bool IsTrue(string value)
        {
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>AutoIt 的 `If $var`（$var 是**数值**时）：非 0 为真。</summary>
        public static bool IsTrue(double value)
        {
            return value != 0;
        }
    }
}
