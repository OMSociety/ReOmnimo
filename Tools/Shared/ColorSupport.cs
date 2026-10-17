// ReOmnimo 工具共享层（第四部分）：颜色文本与色板生成
//
// 收拢三处共用件：
//   · ColorText     —— Common.au3 的 HexToRGB / RGBToHex（见 port_spec_4tools.md §0）
//   · PaletteFile   —— colors.txt / defaultcolors.txt 的读写（AutoIt _FileReadToArray/_FileWriteFromArray）
//   · ColorGenerator—— ColorGenerator.au3 的三个生成器（ColorChanger 与取色对话框共用）
//
// 数组口径与 AutoIt 完全一致：int[] colors，colors[0] = 行数，colors[1..count] = 0xRRGGBB。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace OmnimoTools
{
    /// <summary>Common.au3 的颜色文本换算。</summary>
    internal static class ColorText
    {
        /// <summary>
        /// 等价于 HexToRGB($iColor)：0xRRGGBB 整数 → "R,G,B"；-1 → "255,255,255"。
        /// 注意 AutoIt 版对负值（含 -1）统一返回白色。
        /// </summary>
        public static string HexToRGB(int color)
        {
            if (color < 0) return "255,255,255";
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}",
                (color >> 16) & 0xFF, (color >> 8) & 0xFF, color & 0xFF);
        }

        /// <summary>
        /// 等价于 RGBToHex("R,G,B[,A]") 的**字符串**返回值：'0xRRGGBB'（大写，两位一组）。
        /// AutoIt 版是 `'0x' & Hex($split[1],2) & Hex($split[2],2) & Hex($split[3],2)`——
        /// 每个分量先按 AutoIt 规则取前导数值，再按**字节截断**（Hex(256,2)='00'，不是饱和到 255）。
        /// ConfigBackground 用它做 `$Color1Value &lt;&gt; $Color2Value` 的字符串比较，故必须保留字符串形态。
        /// </summary>
        public static string RGBToHexString(string value)
        {
            if (value == null) value = string.Empty;
            string[] parts = value.Split(',');
            return "0x" + HexByte(parts, 0) + HexByte(parts, 1) + HexByte(parts, 2);
        }

        private static string HexByte(string[] parts, int index)
        {
            if (index >= parts.Length) return "00";
            int v = (int)AutoIt.Number(parts[index]) & 0xFF;
            return v.ToString("X2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 把 AutoIt 的颜色参数（'0xRRGGBB' 串 / 十进制数串 / -1）转成 Color。
        /// 等价于 AutoIt 控件内部对颜色参数的处理：Number() 取前导数值后取低 24 位。
        /// </summary>
        public static Color ParseColor(string value)
        {
            int v = (int)AutoIt.Number(value) & 0xFFFFFF;
            return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        }

        /// <summary>
        /// 等价于 RGBToHex("R,G,B[,A]")：返回 0xRRGGBB 整数；解析失败返回 -1。
        /// </summary>
        public static int RGBToHex(string value)
        {
            if (string.IsNullOrEmpty(value)) return -1;
            string[] parts = value.Split(',');
            if (parts.Length < 3) return -1;
            int r, g, b;
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return -1;
            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out g)) return -1;
            if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) return -1;
            return (Clamp(r) << 16) | (Clamp(g) << 8) | Clamp(b);
        }

        private static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
    }

    /// <summary>
    /// colors.txt / defaultcolors.txt 的读写。
    /// AutoIt 语义：每行一个 "0xRRGGBB"，CRLF 结尾；_FileReadToArray 把行数放在 [0]。
    /// </summary>
    internal static class PaletteFile
    {
        /// <summary>
        /// 读入最多 maxCount 行。成功返回 true 且 colors[0] = 实际行数、[1..n] 为颜色值；
        /// 文件不存在 / 读失败返回 false（调用方决定是报错还是留空）。
        /// </summary>
        public static bool TryRead(string path, int maxCount, out int[] colors)
        {
            colors = null;
            try
            {
                if (!File.Exists(path)) return false;
                string[] lines = File.ReadAllLines(path);
                var result = new int[maxCount + 1];
                int n = 0;
                for (int i = 0; i < lines.Length && n < maxCount; i++)
                {
                    string t = lines[i].Trim();
                    if (t.Length == 0) continue;
                    if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2);
                    int v;
                    if (!int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) continue;
                    result[++n] = v & 0xFFFFFF;
                }
                result[0] = n;
                colors = result;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 等价于 _FileWriteFromArray($path, $colors, 1)：从索引 1 写到数组末尾，每行一个 "0xRRGGBB"，CRLF。
        /// </summary>
        public static bool Write(string path, int[] colors)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 1; i < colors.Length; i++)
                    sb.Append("0x").Append((colors[i] & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture))
                      .Append("\r\n");
                File.WriteAllText(path, sb.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// ColorGenerator.au3 的三个色板生成器。随机序列与 AutoIt 不同（各自 PRNG），
    /// 但取值范围、遍历区间与比例算法逐条对齐。
    /// </summary>
    internal static class ColorGenerator
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int ColorHLSToRGB(int h, int l, int s);

        private static readonly Random Rnd = new Random();

        /// <summary>Random($min,$max,1)：闭区间整数。</summary>
        private static int RandInt(int min, int max) { return Rnd.Next(min, max + 1); }

        /// <summary>Random()：[0,1) 浮点。</summary>
        private static double RandFloat() { return Rnd.NextDouble(); }

        private static int CC_SwitchColor(int c)
        {
            return ((c & 0x00FF00) | ((c & 0x0000FF) << 16) | ((c & 0xFF0000) >> 16)) & 0xFFFFFF;
        }

        /// <summary>等价于 _HSL2RGB($HSL)：shlwapi ColorHLSToRGB(H,L,S) 后交换 R/B。</summary>
        public static int HslToRgb(int h, int s, int l)
        {
            int ret = ColorHLSToRGB(h, l, s);
            return CC_SwitchColor(ret & 0xFFFFFF);
        }

        /// <summary>GenerateColorful($colors)：三通道各取随机基准色，再与随机值取中。</summary>
        public static void GenerateColorful(int[] colors)
        {
            int count = colors[0];
            int[] mix = { RandInt(100, 200), RandInt(100, 200), RandInt(100, 200) };
            for (int i = 1; i <= count; i++)
            {
                int r = (int)((RandInt(0, 255) + mix[0]) / 2.0);
                int g = (int)((RandInt(0, 255) + mix[1]) / 2.0);
                int b = (int)((RandInt(0, 255) + mix[2]) / 2.0);
                colors[i] = (r << 16) | (g << 8) | b;
            }
        }

        /// <summary>GenerateContigious($colors)：由首色按亮度比例递推。</summary>
        public static void GenerateContigious(int[] colors)
        {
            int count = colors[0];
            if (count < 1) return;
            int r = RandInt(50, 200), g = RandInt(50, 200), b = RandInt(50, 200);
            colors[1] = (r << 16) | (g << 8) | b;
            for (int i = 2; i <= count; i++)
            {
                double value = (r + g + b) / 3.0;
                double newValueS = value + 2 * RandFloat() * 30;
                double newValue = newValueS <= 30 ? newValueS : newValueS - 30;
                double ratio = value == 0 ? 0.9 : newValue / value;
                r = Math.Min((int)(r * ratio), 200);
                g = Math.Min((int)(g * ratio), 200);
                b = Math.Min((int)(b * ratio), 200);
                colors[i] = (r << 16) | (g << 8) | b;
            }
        }

        /// <summary>GenerateContrasting($colors)：固定色相、随机饱和度与亮度。</summary>
        public static void GenerateContrasting(int[] colors)
        {
            int count = colors[0];
            int baseH = RandInt(48, 216);
            for (int i = 1; i <= count; i++)
            {
                int s = RandInt(0, 240);
                int l = RandInt(40, 200);
                colors[i] = HslToRgb(baseH, s, l);
            }
        }
    }
}
