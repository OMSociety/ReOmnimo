// ReOmnimo 工具共享层（第二部分）
// 与 OmnimoShared.cs 一起按源码链接进每个工具 exe，不产出独立 DLL。
//
// 本文件收拢 4 个 GUI 工具（ColorChanger / ConfigBackground / PanelCreator / OmnimoApp）
// 与既有 ActivePanels / MultiManager 共用的公共件：
//   · RunBang    —— OmnimoApp 专用：把 bang 作为命令行参数交给 Rainmeter.exe（非 WM_COPYDATA）
//   · Aligner    —— ActivePanels 与 OmnimoApp.Align 共用的网格排布（含原版 $row9 修复）
//   · LayoutManager —— MultiManager 与 OmnimoApp.Layouts 共用的多布局存取
//   · DesktopIcons / ShellLink / IconExtractor / ZipWriter 等
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace OmnimoTools
{
    /// <summary>
    /// OmnimoApp 专用的 bang 通道：不发给窗口，而是把 Rainmeter.exe 当进程拉起、bang 走命令行。
    /// 对应 AutoIt 的 Run('"' & $CmdLine[N] & 'Rainmeter.exe" [bang]')，
    /// N 因调用点而异（SendBang=[4]、SendBangTray=[3]、SendBangLonger=[7]），故由调用方传入路径。
    /// </summary>
    internal static class RunBang
    {
        /// <summary>等价于 SendBang()：Run('"path Rainmeter.exe" [bang]')（带方括号）。</summary>
        public static void Bracketed(string rainmeterExePath, string bang)
        {
            Start(rainmeterExePath, "[" + bang + "]");
        }

        /// <summary>等价于 SendBangMulti()：Run('"path Rainmeter.exe" bang')（不带方括号）。</summary>
        public static void Plain(string rainmeterExePath, string bang)
        {
            Start(rainmeterExePath, bang);
        }

        private static void Start(string rainmeterExePath, string arguments)
        {
            try
            {
                // AutoIt 的 Run 是异步拉起、不等结果；路径与 AutoIt 一样是裸拼接（可能为空 → 启动失败静默）
                Process.Start(new ProcessStartInfo
                {
                    FileName = rainmeterExePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch
            {
                // AutoIt 的 Run 失败同样不报错
            }
        }
    }

    /// <summary>
    /// 面板网格排布。ActivePanels 的 ReadActiveSkins 与 OmnimoApp 的 Align() 是同一份 if/elseif 链，
    /// 只是 bang 通道不同（前者 WM_COPYDATA，后者命令行），故把排布算法收在这里。
    /// </summary>
    internal static class Aligner
    {
        private static readonly Regex PanelSection = new Regex(@"^REO\\(Installed|Donator)?Panels.*$");

        private static readonly Regex PanelPrefix = new Regex(@"REO\\(Installed|Donator)?Panels\\");

        /// <summary>
        /// 段名是否属于面板（StringRegExp '^REO\\(Installed|Donator)?Panels.*$'，区分大小写）。
        /// ColorChanger 的 ReadActiveSkins 与 ActivePanels / OmnimoApp.Align 共用同一条规则。
        /// </summary>
        public static bool MatchesPanelSection(string section)
        {
            return section != null && PanelSection.IsMatch(section);
        }

        /// <summary>
        /// 列表里显示的面板名：等价于 StringRegExpReplace($section, "REO\\(Installed|Donator)?Panels\\", "")。
        /// </summary>
        public static string PanelDisplayName(string section)
        {
            return section == null ? "" : PanelPrefix.Replace(section, "");
        }

        /// <summary>排布结果：目标面板 + 计算出的 x/y。</summary>
        internal struct Move
        {
            public string Section;
            public double X;
            public double Y;
        }

        /// <summary>
        /// 读出活动面板并算出每个面板的目标位置（不发送任何 bang，由调用方决定通道）。
        /// settingsPath 应为 Rainmeter.ini 所在目录；skinPath 含尾反斜杠。
        /// </summary>
        public static List<Move> Compute(string skinPath, string settingsPath,
            string variant, string sX, string sY)
        {
            var moves = new List<Move>();

            string rainmeterIni = Paths.Combine(settingsPath, "Rainmeter.ini");
            IniDoc doc = IniDoc.TryLoad(rainmeterIni);
            if (doc == null)
                OmnimoDialogs.Error("Individual Panel Color", "Unable to open Rainmeter.ini for reading.");

            // 等价于 IniRead(<UserVariables.inc>, "Variables", "ScaleDpi", "1")
            double scaleDpi = 1;
            IniDoc userVars = IniDoc.TryLoad(Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc"));
            if (userVars != null) scaleDpi = AutoIt.ToNumber(userVars.Get("Variables", "ScaleDpi", "1"));

            int iCount = 0;
            double moveX = 160 * scaleDpi;
            double moveY = 160 * scaleDpi;
            double initialX = 45 + AutoIt.ToNumber(sX);
            double initialY = 45 + AutoIt.ToNumber(sY);

            int row1 = 0, row2 = 0, row3 = 0, row4 = 0, row5 = 0, row6 = 0, row7 = 0, row8 = 0, row9 = 0;

            foreach (string section in doc.SectionNames())
            {
                if (!PanelSection.IsMatch(section)) continue;
                if (!AutoIt.IsNonZeroString(doc.Get(section, "Active", "0"))) continue;
                if (!Directory.Exists(Paths.Combine(skinPath, section))) continue;
                if (IsNotColorizable(section)) continue;

                iCount++;

                double x, y;
                if (GridPosition(variant, iCount, initialX, initialY, moveX, moveY,
                        ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7, ref row8, ref row9,
                        out x, out y))
                {
                    moves.Add(new Move { Section = section, X = x, Y = y });
                }
            }

            return moves;
        }

        /// <summary>
        /// 面板是否不可着色（RainConfigure.cfg 的 Options\Colorizable=0）。
        /// 与 ActivePanels.au3:200 一致：配置路径**不带** #SKINSPATH# 前缀（相对当前工作目录），
        /// 因此实际几乎总是读不到而回落默认值 "1"。此处保持原行为。
        /// </summary>
        private static bool IsNotColorizable(string section)
        {
            string config = section.Replace("REO\\", "REO\\@Resources\\Config\\") + "\\RainConfigure.cfg";
            IniDoc cfg = IniDoc.TryLoad(config);
            if (cfg == null) return false;
            return cfg.Get("Options", "Colorizable", "1") == "0";
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 网格排布：逐字对应 ActivePanels.au3 / OmnimoApp.au3 Align() 的 if/elseif 链
        /// （含原代码的边界重叠与空档）。返回 false 表示原始代码在该 iCount 上不发 !Move
        /// （例如 3x3 的 25、26）。
        /// 注：原版 3x3 分支的 $row9 从未声明（ActivePanels.au3:278 / OmnimoApp.au3:841），
        /// 启用面板 ≥31 时 AutoIt 报 "Variable used without being declared" 后中止；此处按源码
        /// 本来的行表补上声明，故该点是唯一有意保留的差异（见 CHANGELOG）。
        /// ActivePanels.ReadActiveSkins 与 OmnimoApp.Align（经 Compute）共用本实现。
        /// </summary>
        internal static bool GridPosition(string variant, int iCount, double initialX, double initialY,
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
    }

    /// <summary>
    /// 多布局存档（MultiManager 与 OmnimoApp.Layouts 共用同一套流程，只是 bang 通道不同）。
    /// </summary>
    internal static class LayoutManager
    {
        /// <summary>布局存档根目录（skins 含尾反斜杠）。</summary>
        public static string SavedRoot(string skins)
        {
            return Paths.Combine(skins, @"REO\Gallery\MultiManager\Saved\");
        }

        /// <summary>抓主屏全屏并等比压成 400 像素宽的 PNG（MultiManager 的 Save0/Save1 用）。</summary>
        public static void SaveScreenshot(string path)
        {
            try
            {
                Rectangle screen = Screen.PrimaryScreen.Bounds;
                if (screen.Width <= 0 || screen.Height <= 0) return;

                int width = 400;
                int height = (int)(400.0 * screen.Height / screen.Width);
                if (height <= 0) height = 1;

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using (var full = new Bitmap(screen.Width, screen.Height))
                {
                    using (Graphics g = Graphics.FromImage(full))
                        g.CopyFromScreen(0, 0, 0, 0, new Size(screen.Width, screen.Height), CopyPixelOperation.SourceCopy);

                    using (var thumb = new Bitmap(width, height))
                    {
                        using (Graphics g2 = Graphics.FromImage(thumb))
                            g2.DrawImage(full, new Rectangle(0, 0, width, height));
                        thumb.Save(path, ImageFormat.Png);
                    }
                }
            }
            catch
            {
                // AutoIt 的截图失败不会中断保存流程
            }
        }

        /// <summary>
        /// 等价于 Load0 里的 Shortcuts\1..3 处理：把当前 Rainmeter.ini 的 Active/WindowX/WindowY
        /// 抄进待加载的存档，让快捷方式面板跟随存档状态。
        /// </summary>
        public static void CopyShortcutSettings(string settings, string saved, string index)
        {
            IniDoc live = IniDoc.TryLoad(Paths.Combine(settings, "Rainmeter.ini"));
            if (live == null) return;

            IniDoc target = IniDoc.LoadOrCreate(Paths.Combine(saved, index + "\\Rainmeter.ini"));
            for (int n = 1; n <= 3; n++)
            {
                string section = @"REO\Gallery\MultiManager\Shortcuts\" + n;
                if (AutoIt.NumericEquals(live.Get(section, "Active", "0"), 1))
                {
                    target.Set(section, "Active", "1");
                    target.Set(section, "WindowX", live.Get(section, "WindowX", "0"));
                    target.Set(section, "WindowY", live.Get(section, "WindowY", "0"));
                }
                else
                {
                    target.Set(section, "Active", "0");
                }
            }
            target.Save();
        }

        /// <summary>存取结果的返回码（调用方据此决定 exit code 与 bang）。</summary>
        public enum Result
        {
            Ok,
            CreatingDirectoryFailed,
            CopyRainmeterIniFailed,
            CopyColorIncFailed
        }

        /// <summary>
        /// Save0/Save1：抓图 + 抄 Rainmeter.ini / color.inc（Save1 另抄壁纸）+ 改写存档 ini。
        /// 返回 Ok 时调用方应发 !Refresh REO\Gallery\MultiManager。
        /// </summary>
        public static Result Save(string settings, string skins, string command, string index, string wallpaper)
        {
            string saved = SavedRoot(skins);
            string savedIni = Paths.Combine(saved, index + "\\Rainmeter.ini");

            OmnimoDialogs.EnsureDirectory(Paths.Combine(saved, index));
            SaveScreenshot(Paths.Combine(saved, index + "\\screenshot.png"));

            if (!OmnimoDialogs.CopyFile(Paths.Combine(settings, "Rainmeter.ini"), savedIni))
                return Result.CopyRainmeterIniFailed;
            if (string.Equals(command, "Save1", StringComparison.OrdinalIgnoreCase))
                OmnimoDialogs.CopyFile(wallpaper, Paths.Combine(saved, index + "\\Wallpaper.bmp"));
            OmnimoDialogs.CopyFile(Paths.Combine(skins, @"REO\@Resources\Common\Color\color.inc"),
                Paths.Combine(saved, index + "\\color.inc"));

            IniDoc doc = IniDoc.LoadOrCreate(savedIni);
            doc.Set(@"REO\TextItems\Extra\Missing", "Active", "1");
            doc.Set(@"REO\Gallery\MultiManager", "Active", "0");
            doc.DeleteKey("Rainmeter", "SkinPath");
            doc.DeleteKey("Rainmeter", "ConfigEditor");
            doc.DeleteKey("Rainmeter", "TrayExecuteR");
            doc.DeleteKey("Rainmeter", "Language");
            doc.Save();

            return Result.Ok;
        }

        /// <summary>
        /// Load0/Load1：搭临时布局目录 → 拷 Rainmeter.ini / color.inc → 等 5 秒让 Rainmeter 载完 → 删临时目录。
        /// 返回 Ok 时调用方应发 !LoadLayout MultiManagerTemp（且必须先发 bang 再等待）。
        /// </summary>
        public static Result PrepareLoad(string settings, string skins, string command, string index)
        {
            string saved = SavedRoot(skins);
            string savedIni = Paths.Combine(saved, index + "\\Rainmeter.ini");

            if (string.Equals(command, "Load0", StringComparison.OrdinalIgnoreCase))
                CopyShortcutSettings(settings, saved, index);

            string temp = Paths.Combine(settings, @"Layouts\MultiManagerTemp");
            if (!OmnimoDialogs.EnsureDirectory(temp)) return Result.CreatingDirectoryFailed;
            if (!OmnimoDialogs.CopyFile(savedIni, Paths.Combine(temp, "Rainmeter.ini")))
                return Result.CopyRainmeterIniFailed;
            if (!OmnimoDialogs.CopyFile(Paths.Combine(saved, index + "\\color.inc"),
                    Paths.Combine(skins, @"REO\@Resources\Common\Color\color.inc")))
                return Result.CopyColorIncFailed;

            return Result.Ok;
        }

        /// <summary>Load 完成后清理临时布局目录（AutoIt 的 Sleep(5000) + DirRemove）。</summary>
        public static void FinishLoad(string settings)
        {
            Thread.Sleep(5000);
            OmnimoDialogs.RemoveDirectoryTree(Paths.Combine(settings, @"Layouts\MultiManagerTemp"));
        }

        /// <summary>Delete：删存档目录 + 放回占位截图。返回后调用方发 !Refresh。</summary>
        public static void Delete(string skins, string index)
        {
            string saved = SavedRoot(skins);
            OmnimoDialogs.RemoveDirectoryTree(Paths.Combine(saved, index));
            OmnimoDialogs.CopyFile(Paths.Combine(skins, @"REO\Gallery\MultiManager\screenshot.png"),
                Paths.Combine(saved, index + "\\screenshot.png"), createDestinationDirectory: true);
        }
    }

    /// <summary>桌面图标显示/隐藏（OmnimoApp 的 ToggleIcons 与 Common.au3 的 ShowIcons）。</summary>
    internal static class DesktopIcons
    {
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;

        /// <summary>桌面 SysListView32 的窗口句柄（Progman → SHELLDLL_DefView → SysListView32）。</summary>
        private static IntPtr ListView()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
            {
                // 壁纸改用 WorkerW 承载时 SHELLDLL_DefView 挂在 WorkerW 下
                IntPtr worker = IntPtr.Zero;
                while ((worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
                {
                    defView = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero) break;
                }
            }
            if (defView == IntPtr.Zero) return IntPtr.Zero;
            return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        }

        /// <summary>
        /// 等价于 AutoIt 的 WinGetState($hListView)：返回"7=显示中 / 5=隐藏"这组语义。
        /// AutoIt 的 state 位掩码里 1=存在 2=可见 4=启用；桌面图标可见时为 7，隐藏时为 5。
        /// </summary>
        public static int State()
        {
            IntPtr lv = ListView();
            if (lv == IntPtr.Zero) return 5;
            int style = GetWindowLong(lv, GWL_STYLE);
            bool visible = (style & WS_VISIBLE) != 0;
            // 存在(1) + 启用(4) 恒为 1+4；可见时再加 2
            return visible ? 7 : 5;
        }

        public static void Show()
        {
            IntPtr lv = ListView();
            if (lv != IntPtr.Zero) ShowWindow(lv, SW_SHOW);
        }

        public static void Hide()
        {
            IntPtr lv = ListView();
            if (lv != IntPtr.Zero) ShowWindow(lv, SW_HIDE);
        }
    }

    /// <summary>.lnk 快捷方式解析（等价于 AutoIt 的 FileGetShortcut）。</summary>
    internal static class ShellLink
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        /// <summary>
        /// 解析 .lnk，返回 { 目标路径, 参数, 工作目录, 图标路径, 图标索引 }；
        /// 解析失败返回 null（对应 AutoIt 的 @error=1）。
        /// 走 IShellLink + IPersistFile，不改动进程当前目录（AutoIt 的 FileGetShortcut 同样不改）。
        /// </summary>
        public static string[] Resolve(string lnkPath)
        {
            object link = null;
            try
            {
                if (!File.Exists(lnkPath)) return null;

                link = new ShellLinkCoClass();
                ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);

                var shell = (IShellLinkW)link;
                var target = new StringBuilder(260);
                var args = new StringBuilder(260);
                var working = new StringBuilder(260);
                var iconPath = new StringBuilder(260);
                int iconIndex;

                // SLGP_UNCPRIORITY(0x0002)：与 FileGetShortcut 一样拿原始写入的目标路径
                shell.GetPath(target, target.Capacity, IntPtr.Zero, 0x0002);
                shell.GetArguments(args, args.Capacity);
                shell.GetWorkingDirectory(working, working.Capacity);
                shell.GetIconLocation(iconPath, iconPath.Capacity, out iconIndex);

                return new[]
                {
                    target.ToString(),
                    args.ToString(),
                    working.ToString(),
                    iconPath.ToString(),
                    iconIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                };
            }
            catch
            {
                return null;
            }
            finally
            {
                // 释放 COM 对象（否则 .lnk 会被占用到进程退出）
                if (link != null && Marshal.IsComObject(link)) Marshal.ReleaseComObject(link);
            }
        }
    }
}
