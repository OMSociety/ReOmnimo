// ReOmnimo 工具共享层（第五部分）：AutoIt 变体值语义 + GUI 配色表
//
// AutoIt 的变量没有类型：同一个 $Color1Value 可能先是 INI 读出的字符串 "0,0,0,181"，
// 之后又被赋成取色框返回的整数 0xFF0000。`If $v`、`$a = $b`、`GUICtrlSetBkColor($c,$v)`
// 在两种形态下行为不同，靠 AutoItValue 把这三件事逐条复刻（语义均由 AutoIt3 3.3.18 探针实测）：
//   · 真值判定：字符串**只有空串为假**（"0"/"00"/"0.0"/" " 全为真）；数值 0 为假
//   · 相等比较：两边只要有一边是数值 → 按 Number() 数值比；两边都是字符串 → 不区分大小写字符串比
//   · 取色：按前导数值取（GUICtrlSetBkColor 对字符串就是这个行为）
using System;
using System.Drawing;
using System.Globalization;

namespace OmnimoTools
{
    /// <summary>AutoIt 变量的值语义（字符串或数值，行为随上下文而定）。</summary>
    internal struct AutoItValue
    {
        private readonly string _text;
        private readonly bool _isNumber;
        private readonly double _number;

        private AutoItValue(string text, bool isNumber, double number)
        {
            _text = text;
            _isNumber = isNumber;
            _number = number;
        }

        /// <summary>来自字符串（IniRead / 拼接结果 / GUICtrlRead 的输入框）。字符串**不**因可解析成数字而变成数值。</summary>
        public static AutoItValue FromString(string text)
        {
            return new AutoItValue(text ?? string.Empty, false, 0);
        }

        /// <summary>来自数值（取色框返回值、GUICtrlRead 的滑杆值等）。</summary>
        public static AutoItValue FromNumber(double number)
        {
            return new AutoItValue(null, true, number);
        }

        /// <summary>
        /// 按数值解释：数值形态取自身，字符串形态取**前导**数值（AutoIt 的 Number()，取不到得 0）。
        /// 等价于把它放到位运算/Hex()/颜色参数等需要数字的位置。
        /// </summary>
        public double Number
        {
            get { return _isNumber ? _number : AutoIt.Number(_text); }
        }

        /// <summary>
        /// 等价于 `If $var`：数值形态非 0 为真；字符串形态**只有空串为假**——
        /// 实测 `"0"`/`"00"`/`"0.0"`/`" "` 在 AutoIt 里全为真。
        /// </summary>
        public bool IsTrue
        {
            get { return _isNumber ? _number != 0 : !string.IsNullOrEmpty(_text); }
        }

        /// <summary>
        /// 等价于 AutoIt 的 `=`：只要**有一边是数值**，另一边按 Number() 转成数值后作数值比较；
        /// 两边都是字符串时作**不区分大小写**的字符串比较（不 Trim、不转换）。
        /// </summary>
        public static bool Equal(AutoItValue a, AutoItValue b)
        {
            if (a._isNumber || b._isNumber) return a.Number == b.Number;
            return string.Equals(a._text ?? string.Empty, b._text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public static bool NotEqual(AutoItValue a, AutoItValue b) { return !Equal(a, b); }

        private static string AsText(AutoItValue v)
        {
            return v._isNumber ? AutoIt.Num(v._number) : (v._text ?? string.Empty);
        }

        /// <summary>等价于 Common.au3 的 HexToRGB($v)：取数值后拆成 "R,G,B"。</summary>
        public string ToRgbText()
        {
            return ColorText.HexToRGB((int)Number);
        }

        /// <summary>按 0xRRGGBB 取色（GUICtrlSetBkColor 对变量值的实际效果）。</summary>
        public Color ToColor()
        {
            int rgb = (int)Number & 0xFFFFFF;
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        public override string ToString() { return AsText(this); }
    }

    /// <summary>
    /// GUI 配色表：等价于各工具 `IniReadSection(<Colors.inc>, "Variables")` 后按**行序下标**取值。
    /// 下标约定（源码写死）：1=GuiBG 2=PanelBG 3=BrowseBG 4=CreateBG 5=FooterBG 6=FieldsBG 7=FontColor
    /// 8..13=Color1..Color6。索引越界时返回 fallback（AutoIt 会在读数组时得到空串）。
    /// </summary>
    internal sealed class GuiColors
    {
        private readonly string[] _values;

        private GuiColors(string[] values) { _values = values; }

        /// <summary>读取配色表；文件读不到时全部返回 fallback（对应 AutoIt 的 IniReadSection 失败）。</summary>
        public static GuiColors Load(string path)
        {
            IniDoc doc = IniDoc.TryLoad(path);
            if (doc == null) return new GuiColors(new string[0]);
            var ordered = doc.SectionOrdered("Variables");
            var values = new string[ordered.Count];
            for (int i = 0; i < ordered.Count; i++) values[i] = ordered[i].Value;
            return new GuiColors(values);
        }

        /// <summary>按 1 基下标取值（AutoIt 的 $Sections[$i][1]）。越界返回空串。</summary>
        public string At(int oneBasedIndex, string fallback = "")
        {
            int i = oneBasedIndex - 1;
            if (i < 0 || i >= _values.Length) return fallback;
            return _values[i] ?? fallback;
        }

        /// <summary>取颜色值并转成 System.Drawing.Color（AutoIt 把值交给 GUICtrlSetBkColor 的效果）。</summary>
        public Color ColorAt(int oneBasedIndex, Color fallback)
        {
            string raw = At(oneBasedIndex, null);
            if (string.IsNullOrEmpty(raw)) return fallback;
            double number;
            if (!AutoIt.TryNumber(raw, out number)) return fallback;   // 如 "0x[&Lua:...]" 这类非数值
            int rgb = (int)number & 0xFFFFFF;
            return System.Drawing.Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        /// <summary>取十六进制串原样（用于 GDI+ 画笔等需要原始值的地方）。</summary>
        public string HexAt(int oneBasedIndex, string fallback)
        {
            return At(oneBasedIndex, fallback);
        }

        /// <summary>把 "0xRRGGBB" 或纯数值串解析成 ARGB 颜色，失败返回 fallback。</summary>
        public static Color ParseHex(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            double number;
            if (!AutoIt.TryNumber(value, out number)) return fallback;
            int rgb = (int)number & 0xFFFFFF;
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        /// <summary>把十进制数值（GDI+ 的 0xRRGGBB 整数）转成颜色。</summary>
        public static Color FromRgb(int rgb)
        {
            rgb &= 0xFFFFFF;
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        /// <summary>等价于 AutoIt 的 Hex($value, 6)：大写、不足补零。</summary>
        public static string Hex6(double value)
        {
            long v = ((long)value) & 0xFFFFFF;
            return v.ToString("X6", CultureInfo.InvariantCulture);
        }
    }
}
