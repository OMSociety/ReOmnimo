// ReOmnimo ActivePanels —— AutoIT\ActivePanels.au3 的 C# 等价实现
//
// 调用契约（皮肤侧 4 处，见 REO\Gallery\Alignment\Item.ini 与 REO\Gallery\Intro\introLang.ini）：
//   ActivePanels.exe "#SKINSPATH#" "#CURRENTCONFIGY#" "#CURRENTCONFIGX#" "#SETTINGSPATH#" <变体>
// 变体：3x3 4x4 5x5 6x6 7x7 8x8 9x9（按网格重排面板）| lock | unlock | missing
//       （dark darkwithpanels light lightwithpanels 四个分支保留但皮肤侧无调用点，
//         对应功能由 OmnimoApp.exe 的 Dark()/DarkWithPanels()/Light()/LightWithPanels() 承担）
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace OmnimoTools.ActivePanels
{
    internal static class Program
    {
        private static readonly Regex PanelSection = new Regex(@"^REO\\(Installed|Donator)?Panels.*$");
        private static readonly Regex LockableSection = new Regex(@"^REO\\(Panels|InstalledPanels|DonatorPanels|TextItems).*$");

        private static int Main(string[] args)
        {
            if (args.Length < 5)
                OmnimoDialogs.Error("Omnimo Panel Config", "Too few command line arguments specified.");

            string skinPath = args[0];
            string sY = args[1];
            string sX = args[2];
            string settingsPath = args[3];
            string variant = args[4];

            string scriptDir = Paths.ScriptDirectory();
            string colorVars = Paths.Combine(skinPath, @"REO\@Resources\Common\Color\color.inc");
            string userVars = Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc");

            try { File.Delete(Paths.Combine(scriptDir, "hue.ini")); }
            catch { /* AutoIt 的 FileDelete 失败不报错 */ }

            var language = LanguagePack.Load(skinPath);

            if (Same(variant, "lock"))
            {
                OmnimoDialogs.Info(LanguagePack.Text(language, "PanelsLocked"));
                ReadActiveSkinsLock(skinPath, settingsPath, variant);
            }
            else if (Same(variant, "unlock"))
            {
                OmnimoDialogs.Info(LanguagePack.Text(language, "PanelsUnlocked"));
                ReadActiveSkinsLock(skinPath, settingsPath, variant);
            }
            else if (Same(variant, "darkwithpanels"))
            {
                WriteVars(colorVars, "colorskin", "30,30,30", "colorskin2", "50,50,50", "textcolor2", "255,255,255");
                WriteVars(userVars, "DarkMode", "1", "ttgallerystyle", "Dark",
                    "ConfigBackgroundColor", "0x000000", "ConfigBackgroundColor2", "0x323232", "ConfigTextColor", "0xE6E6E6");
                Rainmeter.SendBang("[!Refresh *]");
            }
            else if (Same(variant, "lightwithpanels"))
            {
                WriteVars(colorVars, "colorskin", "230,230,230", "colorskin2", "250,250,250", "textcolor2", "30,30,30");
                WriteVars(userVars, "DarkMode", "0", "ttgallerystyle", "Light",
                    "ConfigBackgroundColor", "0xffffff", "ConfigBackgroundColor2", "0xd2d2d2", "ConfigTextColor", "0x323232");
                Rainmeter.SendBang("[!Refresh *]");
            }
            else if (Same(variant, "dark"))
            {
                WriteVars(userVars, "DarkMode", "1", "ttgallerystyle", "Dark",
                    "ConfigBackgroundColor", "0x000000", "ConfigBackgroundColor2", "0x323232", "ConfigTextColor", "0xE6E6E6");
                Rainmeter.SendBang("[!Refresh *]");
            }
            else if (Same(variant, "light"))
            {
                WriteVars(userVars, "DarkMode", "0", "ttgallerystyle", "Light",
                    "ConfigBackgroundColor", "0xffffff", "ConfigBackgroundColor2", "0xd2d2d2", "ConfigTextColor", "0x323232");
                Rainmeter.SendBang("[!Refresh *]");
            }
            else if (Same(variant, "missing"))
            {
                WriteVars(userVars, "MissingComponents", "!DeactivateConfig");
            }
            else
            {
                ReadActiveSkins(skinPath, settingsPath, variant, sX, sY, scriptDir);
            }

            return 0;
        }

        private static bool Same(string a, string b)
        {
            // AutoIt 的 `=` 对字符串是不区分大小写的比较
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteVars(string path, params string[] keysAndValues)
        {
            IniDoc doc = IniDoc.LoadOrCreate(path);
            for (int i = 0; i + 1 < keysAndValues.Length; i += 2)
                doc.Set("Variables", keysAndValues[i], keysAndValues[i + 1]);
            doc.Save();
        }

        /// <summary>等价于 AutoIt 的 ReadActiveSkinsLock()：对活动面板批量 !Draggable 0/1。</summary>
        private static void ReadActiveSkinsLock(string skinPath, string settingsPath, string variant)
        {
            string rainmeterIni = Paths.Combine(settingsPath, "Rainmeter.ini");
            IniDoc doc = IniDoc.TryLoad(rainmeterIni);
            if (doc == null)
                OmnimoDialogs.Error("Individual Panel Color", "Unable to open Rainmeter.ini for reading.");

            foreach (string section in doc.SectionNames())
            {
                if (!LockableSection.IsMatch(section)) continue;
                if (!AutoIt.IsNonZeroString(doc.Get(section, "Active", "0"))) continue;
                if (!Directory.Exists(Paths.Combine(skinPath, section))) continue;
                if (IsNotColorizable(section)) continue;

                Rainmeter.SendBang(Same(variant, "lock")
                    ? "!Draggable 0 " + section
                    : "!Draggable 1 " + section);
            }
        }

        /// <summary>
        /// 等价于 AutoIt 的 ReadActiveSkins()：把活动面板写进 hue.ini 并按所选网格发 !Move。
        /// 注意：hue.ini 写在 exe 所在目录（REO\@Resources\Common\Config\hue.ini），该文件被 git 跟踪。
        /// </summary>
        private static void ReadActiveSkins(string skinPath, string settingsPath, string variant, string sX, string sY, string scriptDir)
        {
            string rainmeterIni = Paths.Combine(settingsPath, "Rainmeter.ini");
            IniDoc doc = IniDoc.TryLoad(rainmeterIni);
            if (doc == null)
                OmnimoDialogs.Error("Individual Panel Color", "Unable to open Rainmeter.ini for reading.");

            // 等价于 IniRead(<UserVariables.inc>, "Variables", "ScaleDpi", "1")：文件或键缺失都取 "1"
            double scaleDpi = 1;
            IniDoc userVars = IniDoc.TryLoad(Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc"));
            if (userVars != null) scaleDpi = AutoIt.ToNumber(userVars.Get("Variables", "ScaleDpi", "1"));

            int iCount = 0;
            double moveX = 160 * scaleDpi;
            double moveY = 160 * scaleDpi;
            double initialX = 45 + AutoIt.ToNumber(sX);
            double initialY = 45 + AutoIt.ToNumber(sY);

            int row1 = 0, row2 = 0, row3 = 0, row4 = 0, row5 = 0, row6 = 0, row7 = 0, row8 = 0, row9 = 0;

            string huePath = Paths.Combine(scriptDir, "hue.ini");
            IniDoc hue = null;

            foreach (string section in doc.SectionNames())
            {
                if (!PanelSection.IsMatch(section)) continue;
                if (!AutoIt.IsNonZeroString(doc.Get(section, "Active", "0"))) continue;
                if (!Directory.Exists(Paths.Combine(skinPath, section))) continue;
                if (IsNotColorizable(section)) continue;

                if (hue == null) hue = IniDoc.LoadOrCreate(huePath);
                hue.Set("Variables", "Panel" + iCount, section);

                iCount++;

                double x, y;
                if (GridPosition(variant, iCount, initialX, initialY, moveX, moveY,
                        ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7, ref row8, ref row9,
                        out x, out y))
                {
                    Rainmeter.SendBang("!Move " + AutoIt.Num(x) + " " + AutoIt.Num(y) + " " + section);
                }
            }

            if (hue != null) hue.Save();
        }

        /// <summary>
        /// 网格排布：逐字对应 ActivePanels.au3 的 if/elseif 链（含原代码的边界重叠与空档）。
        /// 返回 false 表示原始代码在该 iCount 上不发 !Move（例如 3x3 的 25、26）。
        /// </summary>
        private static bool GridPosition(string variant, int iCount, double initialX, double initialY,
            double moveX, double moveY,
            ref int row1, ref int row2, ref int row3, ref int row4, ref int row5,
            ref int row6, ref int row7, ref int row8, ref int row9,
            out double x, out double y)
        {
            x = 0; y = 0;

            if (Same(variant, "4x4"))
            {
                if (iCount < 5) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 4 && 8 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 8 && 12 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 12 && 16 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 16 && 20 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else if (iCount >= 20 && 24 >= iCount) { row5 += 1; x = initialX + moveX * row5 - moveX; y = initialY + moveY * 6 - moveY; }
                else if (iCount >= 24 && 28 >= iCount) { row6 += 1; x = initialX + moveX * row6 - moveX; y = initialY + moveY * 7 - moveY; }
                else if (iCount >= 28 && 32 >= iCount) { row7 += 1; x = initialX + moveX * row7 - moveX; y = initialY + moveY * 8 - moveY; }
                else return false;
            }
            else if (Same(variant, "3x3"))
            {
                if (iCount < 4) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 3 && 6 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 6 && 9 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 9 && 12 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 12 && 15 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else if (iCount >= 15 && 18 >= iCount) { row5 += 1; x = initialX + moveX * row5 - moveX; y = initialY + moveY * 6 - moveY; }
                else if (iCount >= 18 && 21 >= iCount) { row6 += 1; x = initialX + moveX * row6 - moveX; y = initialY + moveY * 7 - moveY; }
                else if (iCount >= 21 && 24 >= iCount) { row7 += 1; x = initialX + moveX * row7 - moveX; y = initialY + moveY * 8 - moveY; }
                else if (iCount >= 27 && 30 >= iCount) { row8 += 1; x = initialX + moveX * row8 - moveX; y = initialY + moveY * 9 - moveY; }
                else if (iCount >= 30 && 33 >= iCount) { row9 += 1; x = initialX + moveX * row9 - moveX; y = initialY + moveY * 10 - moveY; }
                else return false;
            }
            else if (Same(variant, "5x5"))
            {
                if (iCount < 6) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 5 && 10 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 10 && 15 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 15 && 20 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 20 && 25 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else if (iCount >= 25 && 30 >= iCount) { row5 += 1; x = initialX + moveX * row5 - moveX; y = initialY + moveY * 6 - moveY; }
                else if (iCount >= 30 && 35 >= iCount) { row6 += 1; x = initialX + moveX * row6 - moveX; y = initialY + moveY * 7 - moveY; }
                else return false;
            }
            else if (Same(variant, "6x6"))
            {
                if (iCount < 7) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 6 && 12 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 12 && 18 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 18 && 24 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 24 && 30 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else if (iCount >= 30 && 36 >= iCount) { row5 += 1; x = initialX + moveX * row5 - moveX; y = initialY + moveY * 6 - moveY; }
                else return false;
            }
            else if (Same(variant, "7x7"))
            {
                if (iCount < 8) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 7 && 14 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 14 && 21 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 21 && 28 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 28 && 35 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else return false;
            }
            else if (Same(variant, "8x8"))
            {
                if (iCount < 9) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 8 && 16 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 16 && 24 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 24 && 32 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 32 && 40 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else return false;
            }
            else if (Same(variant, "9x9"))
            {
                if (iCount < 10) { x = initialX + moveX * iCount - moveX; y = initialY + moveY - moveY; }
                else if (iCount >= 9 && 18 >= iCount) { row1 += 1; x = initialX + moveX * row1 - moveX; y = initialY + moveY * 2 - moveY; }
                else if (iCount >= 18 && 27 >= iCount) { row2 += 1; x = initialX + moveX * row2 - moveX; y = initialY + moveY * 3 - moveY; }
                else if (iCount >= 27 && 36 >= iCount) { row3 += 1; x = initialX + moveX * row3 - moveX; y = initialY + moveY * 4 - moveY; }
                else if (iCount >= 36 && 45 >= iCount) { row4 += 1; x = initialX + moveX * row4 - moveX; y = initialY + moveY * 5 - moveY; }
                else return false;
            }
            else
            {
                // 未识别的变体：原代码在 if 链上全部落空，不发 !Move
                return false;
            }

            return true;
        }

        /// <summary>
        /// 面板是否不可着色（RainConfigure.cfg 的 Options\Colorizable=0）。
        /// 与 ActivePanels.au3:200 一致：配置路径**不带** #SKINSPATH# 前缀（相对当前工作目录），
        /// 因此实际几乎总是读不到而回落默认值 "1"。此处保持原行为，见 CHANGELOG 与本工具 README。
        /// </summary>
        private static bool IsNotColorizable(string section)
        {
            string config = section.Replace("REO\\", "REO\\@Resources\\Config\\") + "\\RainConfigure.cfg";
            IniDoc cfg = IniDoc.TryLoad(config);
            if (cfg == null) return false;
            return cfg.Get("Options", "Colorizable", "1") == "0";
        }
    }
}
