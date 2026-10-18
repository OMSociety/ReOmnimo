// ReOmnimo 工具共享层（第五部分）：AutoIt 变体值语义
//
// AutoIt 的变量没有类型：同一个 $Color1Value 可能先是 INI 读出的字符串 "0,0,0,181"，
// 之后又被赋成取色框返回的整数 0xFF0000。`If $v`、`$a = $b`、`GUICtrlSetBkColor($c,$v)`
// 在两种形态下行为不同，靠 AutoItValue 把这三件事逐条复刻（语义均由 AutoIt3 3.3.18 探针实测）：
//   · 真值判定：字符串**只有空串为假**（"0"/"00"/"0.0"/" " 全为真）；数值 0 为假
//   · 相等比较：两边只要有一边是数值 → 按 Number() 数值比；两边都是字符串 → 不区分大小写字符串比
//   · 取色：按前导数值取（GUICtrlSetBkColor 对字符串就是这个行为）
using System;
using System.Drawing;

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
}
