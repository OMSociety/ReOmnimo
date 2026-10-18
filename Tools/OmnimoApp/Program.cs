// ReOmnimo OmnimoApp（原 AutoIt 版的等价实现；AutoIT\ 源树已于 0.7.0 移除）
//
// 调用契约（不许改）：OmnimoApp.exe <子命令> [参数…]
// 参数下标在 AutoIt 里逐个子命令写死（$CmdLine[N]，N 从 1 起），皮肤侧调用点因此形态各异：
//   Layouts  <命令> <序号> <programpath> <settings> <skins> <壁纸>
//   StartMenu
//   PowerPlan  <HighPerformance|Balanced|PowerSaver>
//   Skin  <主题名> <skinpath> [programpath]
//   Tray  <图标名> <programpath> <settings> <skins>
//   Eject | Monitor
//   LockUnlockPanels <settings> <skinpath> <programpath> <lock|unlock>
//   DarkWithPanels | LightWithPanels | Dark | Light  <skinpath> <programpath>
//   Missing  <skinpath>
//   Align  <settings> <skinpath> <programpath> <变体> <sY> <sX>
//   ToggleIcons  <Show|Hide|Toggle|ToggleFade> <skinpath> <programpath>
//   Power  <Shutdown|Restart|Logoff|Sleep|Hibernate|Lock> <延迟秒> <programpath>
//   Screenshot  <skinpath>
//   Browser  <url> [scaledpi]
//   Create  <名字> <Extra Builder 路径> [programpath]
//   CreatePanel  <名字> <模板根> <?> <?> <skinpath>
//   CreatePanelDelete  <序号> <?> <?> <?> <skinpath>
//   Select  <File|FileFolder|Image|Folder|App> <变量名> …（各子模式读的下标不同，见各分支注释）
//
// bang 通道与其余工具不同：**不发 WM_COPYDATA**，而是把 Rainmeter.exe 当进程拉起、bang 走命令行。
// 对应 AutoIt 的四个函数，参数下标分别是 SendBang=[4]、SendBangTray=[3]、SendBangLonger=[7]、
// SendBangMulti=[4]（不带方括号），Dark/Light 系直接读 [3]。共用件 RunBang 见 Tools\Shared。
//
// 部署：REO\@Resources\Common\OmnimoApp.exe；移植口径见 D:\WorkSpace\WorkBench\port_spec_4tools.md §6。
//
// 与 AutoIt 版的行为差异（逐处标注在各分支）：
//   · 主开关没有 default：未列出的子命令静默退出（原样保留），皮肤侧 ColorizationColor /
//     PanelCombos / SetBrightness / uTorrent 四个死调用即落在这里。
//   · 原代码多处直接读超出实参个数的 $CmdLine[N]（如 PowerPlan / Skin 的 SendBang 读 [4]）。
//     AutoIt 此时因数组维度越界中止脚本并弹错误框；此处按「取到空串」处理（见 Arg），
//     流程继续走到结束——已完成的文件写入不变，只是那一条 bang 发不出去。
//   · MsgBox(52, …) 的 Yes/No 判定按源码字面：只有 No(7) 才算取消。
//   · Align 写 hue.ini 的键序在原版行表出现空档时会前移一格（见 Align 的注释）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace OmnimoTools.OmnimoApp
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 原代码：If $CmdLine[0] < 1 Then OmnimoError("Error", "Too few command line arguments specified.")
            if (args.Length < 1)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string command = args[0];

            // 主开关：Switch $CmdLine[1]，**无 default** —— 未列出的子命令静默退出
            if (Same(command, "Layouts")) Layouts(args);
            else if (Same(command, "StartMenu")) StartMenu();
            else if (Same(command, "PowerPlan")) PowerPlan(args);
            else if (Same(command, "Skin")) Skin(args);
            else if (Same(command, "Tray")) Tray(args);
            else if (Same(command, "Eject")) Eject();
            else if (Same(command, "Monitor")) Monitor("off");
            else if (Same(command, "LockUnlockPanels")) LockUnlockPanels(args);
            else if (Same(command, "DarkWithPanels")) DarkWithPanels(args);
            else if (Same(command, "LightWithPanels")) LightWithPanels(args);
            else if (Same(command, "Dark")) Dark(args);
            else if (Same(command, "Light")) Light(args);
            else if (Same(command, "Missing")) Missing(args);
            else if (Same(command, "Align")) Align(args);
            else if (Same(command, "ToggleIcons")) ToggleIcons(args);
            else if (Same(command, "Power")) Power(args);
            else if (Same(command, "Screenshot")) Screenshot(args);
            else if (Same(command, "Browser")) Browser(args);
            else if (Same(command, "Create")) Create(args);
            else if (Same(command, "CreatePanel")) CreatePanel(args);
            else if (Same(command, "CreatePanelDelete")) CreatePanelDelete(args);
            else if (Same(command, "Select")) Select(args);

            return 0;
        }

        // ---------------------------------------------------------------- 参数与 bang

        /// <summary>
        /// 等价于 AutoIt 的 $CmdLine[N]（N 从 1 起）。原代码在若干分支直接读不存在的下标
        /// （PowerPlan / Skin 的 SendBang 读 [4]、Browser 只传 URL 时读 [3]），AutoIt 遇到越界
        /// 下标会报 "Array variable has incorrect number of subscripts…" 并中止脚本；
        /// 此处退回空串让流程走完，后续 RunBang 拿到空路径时启动失败即静默，等价于不发这条 bang。
        /// </summary>
        private static string Arg(string[] args, int n)
        {
            return n >= 1 && n <= args.Length ? args[n - 1] : string.Empty;
        }

        /// <summary>
        /// AutoIt 的 `=`：字符串比较**不区分大小写**。Switch/Case 与 Select 用的都是它。
        /// </summary>
        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>AutoIt 的 `==`：字符串比较**区分大小写**（PowerPlan 的方案名用这个）。</summary>
        private static bool SameExact(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        /// <summary>
        /// SendBang / SendBangTray / SendBangLonger / SendBangMulti 的公共形态：
        /// Run('"path Rainmeter.exe" [bang]')。path 与 AutoIt 一样是**裸拼接**，
        /// 只有参数本身就是带尾反斜杠的 #PROGRAMPATH# 时才拼得出正确路径。
        /// </summary>
        private static void Bang(string programPath, string bang)
        {
            RunBang.Bracketed(programPath + "Rainmeter.exe", bang);
        }

        /// <summary>SendBangMulti：Run('"path Rainmeter.exe" bang')，bang 自带方括号。</summary>
        private static void BangPlain(string programPath, string bang)
        {
            RunBang.Plain(programPath + "Rainmeter.exe", bang);
        }

        /// <summary>SendBangLonger：读 $CmdLine[7] 当 programpath。Select 的 App 模式把 [7] 同时
        /// 当 ConfigPath 源，是源码里的矛盾，原样保留。</summary>
        private static void BangLonger(string[] args, string bang)
        {
            Bang(Arg(args, 7), bang);
        }

        // ---------------------------------------------------------------- 子命令

        /// <summary>
        /// Layouts：多布局存档。五个子命令（Save0/Save1/Load0/Load1/Delete）与 MultiManager.exe
        /// 完全同构，流程本体复用 LayoutManager，只把 bang 通道换成 Rainmeter.exe 命令行。
        /// 原代码在 Case 开头就把 [2]..[7] 全读进 Const（缺参即越界中止），此处按 Arg() 取。
        /// </summary>
        private static void Layouts(string[] args)
        {
            string command = Arg(args, 2);
            string index = Arg(args, 3);
            string programPath = Arg(args, 4);
            string settings = Arg(args, 5);
            string skins = Arg(args, 6);
            string wallpaper = Arg(args, 7);

            if (Same(command, "Save0") || Same(command, "Save1"))
            {
                LayoutManager.Result result = LayoutManager.Save(settings, skins, command, index, wallpaper);
                if (result == LayoutManager.Result.CopyRainmeterIniFailed)
                {
                    OmnimoDialogs.Message("Debug", "Copying Rainmeter.ini file failed");
                    return;
                }
                Bang(programPath, @"!Refresh REO\Gallery\MultiManager");
                return;
            }

            if (Same(command, "Load0") || Same(command, "Load1"))
            {
                LayoutManager.Result result = LayoutManager.PrepareLoad(settings, skins, command, index);
                if (result == LayoutManager.Result.CreatingDirectoryFailed)
                {
                    OmnimoDialogs.Message("Debug", "Creating directory failed");
                    return;
                }
                if (result == LayoutManager.Result.CopyRainmeterIniFailed)
                {
                    OmnimoDialogs.Message("Debug", "Copying Rainmeter.ini file failed");
                    return;
                }
                if (result == LayoutManager.Result.CopyColorIncFailed)
                {
                    OmnimoDialogs.Message("Debug", "Copying color.inc file failed");
                    return;
                }
                // 先发 bang，再等 5 秒让 Rainmeter 载完临时布局，最后删临时目录
                Bang(programPath, "!LoadLayout MultiManagerTemp");
                LayoutManager.FinishLoad(settings);
                return;
            }

            if (Same(command, "Delete"))
            {
                LayoutManager.Delete(skins, index);
                Bang(programPath, @"!Refresh REO\Gallery\MultiManager");
            }
        }

        /// <summary>StartMenu：AutoIt 的 Send("{LWIN}")。WinForms 的 SendKeys 不支持 Win 键，
        /// 故直接用 keybd_event 按一下左 Win（打开开始菜单）。</summary>
        private static void StartMenu()
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>PowerPlan：切换电源方案后刷新对应面板。</summary>
        private static void PowerPlan(string[] args)
        {
            if (args.Length < 2)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            // 三个方案名用 `==`（区分大小写）比较，与主开关的 `=` 不同——原样保留
            string plan = Arg(args, 2);
            if (SameExact(plan, "HighPerformance"))
                RunWaitHidden("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            else if (SameExact(plan, "Balanced"))
                RunWaitHidden("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            else if (SameExact(plan, "PowerSaver"))
                RunWaitHidden("powercfg", "/setactive a1841308-3541-4fab-bc81-f71556f20b4a "); // 原串尾部有一个空格，原样保留

            // 皮肤侧三个调用点都只传方案名（如 `PowerPlan Balanced`），读不到 [4]：
            // 原代码在此越界中止（电源方案已切，!Refresh 发不出去），此处静默跳过
            Bang(Arg(args, 4), @"!Refresh REO\Panels\PowerPlan");
        }

        /// <summary>Skin：换主题——把主题 inc 覆盖成 color.inc 后刷新全部皮肤。</summary>
        private static void Skin(string[] args)
        {
            if (args.Length < 3)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string colorDir = Paths.Combine(Arg(args, 3), @"REO\@Resources\Common\Color");
            OmnimoDialogs.CopyFile(Paths.Combine(colorDir, Arg(args, 2) + ".inc"),
                Paths.Combine(colorDir, "color.inc"));

            // 皮肤侧多数 Skin 调用点只传了主题名与 skinpath（[4] 缺失）：主题文件已换，
            // 但这条 `!Refresh *` 发不出去（原代码在同一位置越界中止），要等 Rainmeter 自己刷新
            Bang(Arg(args, 4), "!Refresh *");
        }

        /// <summary>Tray：换托盘图标——整节替换 Rainmeter.ini 的 TrayMeasure，重启 Rainmeter。</summary>
        private static void Tray(string[] args)
        {
            if (args.Length < 5)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string name = Arg(args, 2);
            string programPath = Arg(args, 3);
            string settings = Arg(args, 4);
            string skins = Arg(args, 5);

            string trayFile = Paths.Combine(skins, @"REO\@Resources\Common\Gallery\Tray\" + name + ".txt");
            IniDoc source = IniDoc.TryLoad(trayFile);

            // IniReadSection 在文件缺失或段缺失时返回 0（@error=1），此时 IniWriteSection 什么都不做；
            // 只有真读到段才做整节替换：旧键全删、按源文件行序写回
            if (source != null && HasSection(source, "TrayMeasure"))
            {
                IniDoc target = IniDoc.LoadOrCreate(Paths.Combine(settings, "Rainmeter.ini"));
                foreach (string key in new List<string>(target.Section("TrayMeasure").Keys))
                    target.DeleteKey("TrayMeasure", key);
                foreach (KeyValuePair<string, string> pair in source.SectionOrdered("TrayMeasure"))
                    target.Set("TrayMeasure", pair.Key, pair.Value);
                target.Save();
            }

            Bang(programPath, "!Quit");            // SendBangTray 读 [3]
            WaitProcessClose("Rainmeter.exe");
            ShellExecute(programPath + "Rainmeter.exe");
        }

        /// <summary>
        /// Eject：DriveGetDrive("CDROM") 逐个 CDTray(..., "open")。
        /// AutoIt 的 CDTray 走 MCI；这里改用 IOCTL_STORAGE_EJECT_MEDIA——对数据盘、无音频支持的
        /// 光驱同样有效，且不必为每个盘建 MCI 别名。逐个弹出与「找不到光驱就报错」都照原样。
        /// </summary>
        private static void Eject()
        {
            var drives = new List<string>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom) drives.Add(drive.Name); // 形如 "D:\"
            }
            if (drives.Count == 0)
                OmnimoDialogs.Error("Error", "Unable to find an optical disc drive.");

            foreach (string drive in drives) EjectDrive(drive);
        }

        private static void EjectDrive(string drive)
        {
            try
            {
                using (SafeFileHandle handle = CreateFile(@"\\.\" + drive.TrimEnd('\\'),
                    GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                {
                    if (handle.IsInvalid) return;
                    uint returned;
                    DeviceIoControl(handle, IOCTL_STORAGE_EJECT_MEDIA,
                        IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero);
                }
            }
            catch
            {
                // CDTray 的失败原代码也不检查
            }
        }

        /// <summary>
        /// Monitor：给 Progman 发 WM_SYSCOMMAND(274) / SC_MONITORPOWER(61808)。
        /// 只有 "off"(2) 有调用点，"on"(-1) 与原版的命令用法提示一并保留。
        /// </summary>
        private static void Monitor(string ioControl)
        {
            IntPtr progman = FindWindow("Progman", null);
            switch ((ioControl ?? string.Empty).ToUpperInvariant())
            {
                case "OFF":
                    SendMessage(progman, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
                    break;
                case "ON":
                    SendMessage(progman, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)(-1));
                    break;
                default:
                    MessageBox.Show("Command usage: on/off", Path.GetFileName(Application.ExecutablePath),
                        MessageBoxButtons.OK, MessageBoxIcon.Question);
                    break;
            }
        }

        /// <summary>
        /// LockUnlockPanels：把 Rainmeter.ini 里**每个**段都发一条 !Draggable 0/1（原代码不过滤段名），
        /// 语言文件从 skinpath 前缀读，缺文件即报错退出。
        /// </summary>
        private static void LockUnlockPanels(string[] args)
        {
            string settings = Arg(args, 2);
            string skinPath = Arg(args, 3);
            string programPath = Arg(args, 4);
            string variant = Arg(args, 5);

            bool lockPanels = Same(variant, "lock");
            Dictionary<string, string> language = LanguagePack.Load(skinPath);

            IniDoc doc = IniDoc.TryLoad(Paths.Combine(settings, "Rainmeter.ini"));
            if (doc == null)
                OmnimoDialogs.Error("Error", "Unable to open Rainmeter.ini for reading.");

            foreach (string section in doc.SectionNames())
                Bang(programPath, (lockPanels ? "!Draggable 0 " : "!Draggable 1 ") + section);

            // MsgBox($MB_ICONINFORMATION, "Info", …, 2)：2 秒后自动关闭
            OmnimoDialogs.Info(LanguagePack.Text(language, lockPanels ? "PanelsLocked" : "PanelsUnlocked"));
        }

        /// <summary>
        /// Align：按网格重排面板。过滤条件与排布算法复用 Aligner（与 ActivePanels 同一份），
        /// 只有 bang 通道与 hue.ini 的落点不同：原代码写的是**相对 CWD** 的 'hue.ini'，且不先删。
        /// 差异：原代码对每个通过过滤的面板「先写 hue.ini 再算落位」，落在原版行表空档的面板
        /// （如 3x3 的第 25、26 个）会写进 hue.ini 但不发 !Move；Aligner.Compute 只返回有落位的面板，
        /// 故此处 hue.ini 只收有落位的面板，出现空档时后续键序整体前移一格（hue.ini 全仓无读取点）。
        /// </summary>
        private static void Align(string[] args)
        {
            string settings = Arg(args, 2);
            string skinPath = Arg(args, 3);
            string programPath = Arg(args, 4);
            string variant = Arg(args, 5);
            string sY = Arg(args, 6);
            string sX = Arg(args, 7);

            List<Aligner.Move> moves = Aligner.Compute(skinPath, settings, variant, sX, sY);

            IniDoc hue = IniDoc.LoadOrCreate("hue.ini");
            int index = 0;
            foreach (Aligner.Move move in moves)
            {
                // 原代码用「自增前的 $iCount」写 Panel<N>，故第 n 个面板的键是 Panel(n-1)
                hue.Set("Variables", "Panel" + index, move.Section);
                index++;
                Bang(programPath, "!Move " + AutoIt.Num(move.X) + " " + AutoIt.Num(move.Y) + " " + move.Section);
            }
            hue.Save();
        }

        /// <summary>
        /// ToggleIcons：切换桌面图标显示。状态 7 = 显示中、5 = 隐藏。
        /// 语义照原样：**隐藏桌面图标时反而 !Show 全部皮肤**（让面板接管桌面）。
        /// </summary>
        private static void ToggleIcons(string[] args)
        {
            if (args.Length < 2)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string mode = Arg(args, 2);
            string programPath = Arg(args, 4);
            int state = DesktopIcons.State();

            if (Same(mode, "Show"))
            {
                DesktopIcons.Show();
            }
            else if (Same(mode, "Hide"))
            {
                DesktopIcons.Hide();
            }
            else if (Same(mode, "Toggle"))
            {
                if (state == 7)
                {
                    DesktopIcons.Hide();
                    BangPlain(programPath, @"[!Show *][!Show REO\Panels\ShowDesktop]");
                }
                else if (state == 5)
                {
                    DesktopIcons.Show();
                    BangPlain(programPath, @"[!Hide *][!Show REO\Panels\ShowDesktop]");
                }
            }
            else if (Same(mode, "ToggleFade"))
            {
                if (state == 7)
                {
                    DesktopIcons.Hide();
                    BangPlain(programPath, @"[!ShowFade *][!Show REO\Panels\ShowDesktop]");
                }
                else if (state == 5)
                {
                    DesktopIcons.Show();
                    BangPlain(programPath, @"[!HideFade *][!Show REO\Panels\ShowDesktop]");
                }
            }
            // 其余值：原 Switch 没有 Case Else，什么都不做
        }

        /// <summary>Power：确认延迟后关掉确认对话框，再执行对应的关机/重启/休眠命令。</summary>
        private static void Power(string[] args)
        {
            if (args.Length < 3)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            // Sleep(Int($CmdLine[3]) * 1000)：Int() 取前导整数（"10abc" → 10），负数在 AutoIt 里无效即返回
            long milliseconds = AutoIt.Int(Arg(args, 3)) * 1000;
            if (milliseconds > 0) Thread.Sleep((int)Math.Min(milliseconds, int.MaxValue));

            Bang(Arg(args, 4), @"!DeactivateConfig REO\TextItems\Extra\PowerButtons\Dialogue");

            string action = Arg(args, 2);
            if (Same(action, "Shutdown")) RunHidden("shutdown.exe", "/s /t 00");
            else if (Same(action, "Restart")) RunHidden("shutdown.exe", "/r /t 00");
            else if (Same(action, "Logoff")) RunHidden("shutdown.exe", "/l");
            else if (Same(action, "Sleep")) RunHidden("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
            else if (Same(action, "Hibernate")) RunHidden("rundll32.exe", "PowrProf.dll,SetSuspendState");
            else if (Same(action, "Lock")) RunHidden("rundll32.exe", "user32.dll,LockWorkStation");
        }

        /// <summary>
        /// Screenshot：读面板自己的 UserVariables.inc 决定延时、格式、保存位置，抓整屏存盘。
        /// 文件名时间戳 = @MDAY-@MON-@YEAR _ @HOUR-@MIN（月/日/时/分两位补零）。
        /// </summary>
        private static void Screenshot(string[] args)
        {
            if (args.Length < 2)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string profile = UserProfileDir();
            IniDoc doc = IniDoc.TryLoad(Paths.Combine(Arg(args, 2),
                @"REO\@Resources\Config\Panels\Camera\UserVariables.inc"));

            string delay = Read(doc, "Delay", "0");
            string captureCursor = Read(doc, "CaptureCursor", "False");
            string fileFormat = Read(doc, "FileFormat", "jpg");
            string openImageAfter = Read(doc, "OpenImageAfter", "True");
            string saveTo = ReplaceAll(Read(doc, "SaveTo", Paths.Combine(profile, "Pictures")),
                "%USERPROFILE%", profile);

            long milliseconds = AutoIt.Int(delay) * 1000;
            if (milliseconds > 0) Thread.Sleep((int)Math.Min(milliseconds, int.MaxValue));

            string target = Paths.Combine(saveTo,
                DateTime.Now.ToString("dd-MM-yyyy_HH-mm.", CultureInfo.InvariantCulture) + fileFormat);

            // _ScreenCapture_Capture(..., $bCursor = $CaptureCursor <> "False")：字符串比较，故 "false" 也算关
            CaptureScreen(target, AutoIt.StringNotEqual(captureCursor, "False"));

            if (Same(openImageAfter, "True")) ShellExecute(target);
        }

        /// <summary>Browser：交给系统默认浏览器打开（原版是内嵌 IE 的 _IECreateEmbedded 窗口）。</summary>
        /// <remarks>
        /// 内嵌 WebBrowser 就是 IE 引擎，2026 年的页面在里面字体、排版、滚动条全崩（实测 Readme 与天气
        /// 帮助页都读不了），所以不再自建窗口。第二个参数 [scaledpi] 只在旧调用点里还传着，这里不再使用。
        ///
        /// 本地页要带参数（主题、语言）：ShellExecute 会把 file:///…?query 归一成文件路径，查询串与
        /// 片段都会在路上丢掉（实测页面收到空参数），所以先查注册表拿到默认浏览器的命令行，直接把
        /// file:// URI 当参数交给它；拿不到再退回 ShellExecute。
        /// </remarks>
        private static void Browser(string[] args)
        {
            string url = Arg(args, 2);
            if (string.IsNullOrEmpty(url))
                return;

            string query = string.Empty;
            string target = url;
            int q = url.IndexOf('?');
            if (q >= 0)
            {
                target = url.Substring(0, q);
                query = url.Substring(q);
            }

            try
            {
                string launch = target;
                if (File.Exists(target))
                {
                    // 本地页的参数只能放片段：Chrome/Edge 把 file:// 的查询串当成文件名的一部分，
                    // 打开会直接报「无法访问此网站」（实测），而 # 片段会原样保留到 location.hash。
                    // 页面脚本查询串与片段都读。
                    launch = new Uri(Path.GetFullPath(target)).AbsoluteUri
                             + (query.Length > 1 ? "#" + query.Substring(1) : string.Empty);
                }

                if (!LaunchDefaultBrowser(launch))
                    Process.Start(new ProcessStartInfo { FileName = launch, UseShellExecute = true });
            }
            catch
            {
                // 与旧实现一致：打不开就不出声，不弹错误框
            }
        }

        /// <summary>
        /// 用注册表里的默认浏览器命令行直接启动，URL（含查询串/片段）原样作为参数传过去。
        /// ShellExecute 会裁掉参数，只有这种走法能保住 ?theme=…&amp;lang=…。
        /// 返回 false 表示没查到、或启动失败，调用方退回 ShellExecute。
        /// </summary>
        private static bool LaunchDefaultBrowser(string url)
        {
            try
            {
                string progId;
                using (var choice = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"))
                {
                    progId = choice == null ? null : choice.GetValue("ProgId") as string;
                }
                if (string.IsNullOrEmpty(progId))
                    return false;

                string command;
                using (var cmd = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command"))
                {
                    command = cmd == null ? null : cmd.GetValue(null) as string;
                }
                if (string.IsNullOrEmpty(command))
                    return false;

                // 命令形如 "C:\…\msedge.exe" --single-argument %1
                string exe, rest;
                if (command[0] == '"')
                {
                    int close = command.IndexOf('"', 1);
                    if (close < 0)
                        return false;
                    exe = command.Substring(1, close - 1);
                    rest = command.Substring(close + 1).Trim();
                }
                else
                {
                    int sp = command.IndexOf(' ');
                    if (sp < 0)
                    {
                        exe = command;
                        rest = string.Empty;
                    }
                    else
                    {
                        exe = command.Substring(0, sp);
                        rest = command.Substring(sp + 1).Trim();
                    }
                }

                string arguments = rest.Contains("%1")
                    ? rest.Replace("%1", "\"" + url + "\"")
                    : (rest + " \"" + url + "\"").Trim();

                Process.Start(new ProcessStartInfo { FileName = exe, Arguments = arguments, UseShellExecute = false });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Create：从 Extra Builder 的 Template 复制出一个新 extra。</summary>
        private static void Create(string[] args)
        {
            if (args.Length < 3)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string extraBuilder = Arg(args, 3);
            string escapedName = EscapeName(Arg(args, 2));
            string createdDir = Paths.Combine(extraBuilder, @"Created\" + escapedName);

            if (Directory.Exists(createdDir))
            {
                // 原代码只把 7(No) 当取消（关窗返回 -1 会继续覆盖），故只判 No
                if (MessageBox.Show("An extra with the selected name already exists. Overwrite?",
                        "Extra Builder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    return;
            }

            string template = Paths.Combine(extraBuilder, "Template");
            CopyDirectoryTree(template, createdDir);
            OmnimoDialogs.CopyFile(IniRead(template, "ItemImage"), Paths.Combine(createdDir, "Item.png"));

            Bang(Arg(args, 4), "!RefreshApp");
            Bang(Arg(args, 4), @"!ActivateConfig REO\TextItems\Extra\ExtraBuilder\Created\" + escapedName);
        }

        /// <summary>CreatePanel：从 PanelTemplate 复制出一个自定义面板，并把槽位写进 panels.inc。</summary>
        private static void CreatePanel(string[] args)
        {
            // 原代码在参数个数校验**之前**就读了 $CmdLine[5]（缺参时先在这里越界中止），此处取空串
            string skinPath = Arg(args, 5);
            string panelsInc = Paths.Combine(skinPath, @"REO\Gallery\panels.inc");

            if (args.Length < 3)
                OmnimoDialogs.Error("Error", "Too few command line arguments specified.");

            string escapedName = EscapeName(Arg(args, 2));
            string createdDir = Paths.Combine(skinPath, @"REO\Panels\#CustomPanels\" + escapedName);

            if (Directory.Exists(createdDir))
            {
                if (MessageBox.Show("An extra with the selected name already exists. Overwrite?",
                        "Extra Builder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    return;
            }

            string template = Paths.Combine(Arg(args, 3), "PanelTemplate");
            CopyDirectoryTree(template, createdDir);
            OmnimoDialogs.CopyFile(IniRead(template, "IconLocation"), Paths.Combine(createdDir, "Item.png"));

            // For $i = 1 To 32：找第一个 Name<i> 为空或等于本名字的槽位；循环跑完 $i = 33
            IniDoc panels = IniDoc.LoadOrCreate(panelsInc);
            int slot = 33;
            for (int i = 1; i <= 32; i++)
            {
                string existing = panels.Get("Variables", "Name" + i, "");
                if (existing.Length == 0 || Same(existing, escapedName))
                {
                    slot = i;
                    break;
                }
            }
            if (slot == 33)
            {
                // 画廊满：Yes(6) 才回退到最后一个槽；答 No 时原代码照旧用 $i = 33 往下写（越出 32 槽表）
                if (MessageBox.Show("The Custom Panels gallery is full. Would you like to overwrite the last panel?",
                        "Omnimo Panel Creator", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    slot -= 1;
            }

            WriteIniValue(Paths.Combine(createdDir, "UserVariables.inc"), "IconLocation", "Item.png");
            panels.Set("Variables", "Name" + slot, escapedName);
            panels.Set("Variables", "Path" + slot, escapedName);
            // Icon 写的是**绝对路径**（PanelCreator.exe 的同名流程写的是相对路径，两处不同，原样保留）
            panels.Set("Variables", "Icon" + slot, Paths.Combine(createdDir, "Item.png"));
            panels.Save();

            Bang(Arg(args, 4), "!RefreshApp");
            Bang(Arg(args, 4), @"!ActivateConfig REO\Panels\#CustomPanels\" + escapedName);
        }

        /// <summary>CreatePanelDelete：按 panels.inc 的 Path&lt;序号&gt; 删掉一个自定义面板并存档表。</summary>
        private static void CreatePanelDelete(string[] args)
        {
            string skinPath = Arg(args, 5);
            string panelsInc = Paths.Combine(skinPath, @"REO\Gallery\panels.inc");
            string index = Arg(args, 2);

            IniDoc panels = IniDoc.TryLoad(panelsInc);
            string path = panels == null ? "" : panels.Get("Variables", "Path" + index, "");
            if (path.Length == 0) return;
            if (path.Contains("..")) return; // 拒绝会走出面板目录的 Path

            OmnimoDialogs.RemoveDirectoryTree(Paths.Combine(skinPath, @"REO\Panels\#CustomPanels\" + path));
            panels.DeleteKey("Variables", "Name" + index);
            panels.DeleteKey("Variables", "Path" + index);
            panels.DeleteKey("Variables", "Icon" + index);
            panels.Save();

            Bang(Arg(args, 4), @"!DeactivateConfig REO\Panels\#CustomPanels\" + path);
            Bang(Arg(args, 4), @"!Refresh REO\Gallery");
        }

        /// <summary>Missing：把缺失组件的处置方式写回 UserVariables.inc。</summary>
        private static void Missing(string[] args)
        {
            // 裸调用（第二参缺失或空串）时 Arg 返空串，WriteVars 会把相对路径落进当前目录、
            // IniDoc.Save 自建整棵目录树（au3 IniWrite 对缺失目录是静默失败、不建树）。
            // 这里改为明确报错：打印用法并以非零码退出，不落任何文件。
            if (Arg(args, 2).Length == 0)
            {
                Console.Error.WriteLine("Usage: OmnimoApp.exe Missing <skinPath>");
                Environment.Exit(2);
            }

            // 比 ActivePanels 的同名功能多写一个 EditManually（原样保留）
            WriteVars(Paths.Combine(Arg(args, 2), @"REO\@Resources\Common\Variables\UserVariables.inc"),
                "MissingComponents", "!DeactivateConfig", "EditManually", "0");
        }

        /// <summary>DarkWithPanels：连面板配色一起切到暗色。</summary>
        private static void DarkWithPanels(string[] args)
        {
            string skinPath = Arg(args, 2);
            WriteVars(Paths.Combine(skinPath, @"REO\@Resources\Common\Color\color.inc"),
                "colorskin", "30,30,30", "colorskin2", "50,50,50", "textcolor2", "255,255,255");
            WriteVars(Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc"),
                "DarkMode", "1", "ttgallerystyle", "Dark",
                "ConfigBackgroundColor", "0x000000", "ConfigBackgroundColor2", "0x323232", "ConfigTextColor", "0xE6E6E6");
            Bang(Arg(args, 3), "!Refresh *");
        }

        /// <summary>LightWithPanels：连面板配色一起切回亮色。</summary>
        private static void LightWithPanels(string[] args)
        {
            string skinPath = Arg(args, 2);
            WriteVars(Paths.Combine(skinPath, @"REO\@Resources\Common\Color\color.inc"),
                "colorskin", "230,230,230", "colorskin2", "250,250,250", "textcolor2", "30,30,30");
            WriteVars(Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc"),
                "DarkMode", "0", "ttgallerystyle", "Light",
                "ConfigBackgroundColor", "0xffffff", "ConfigBackgroundColor2", "0xd2d2d2", "ConfigTextColor", "0x323232");
            Bang(Arg(args, 3), "!Refresh *");
        }

        /// <summary>Dark：只切界面暗色，不动面板配色。</summary>
        private static void Dark(string[] args)
        {
            WriteVars(Paths.Combine(Arg(args, 2), @"REO\@Resources\Common\Variables\UserVariables.inc"),
                "DarkMode", "1", "ttgallerystyle", "Dark",
                "ConfigBackgroundColor", "0x000000", "ConfigBackgroundColor2", "0x323232", "ConfigTextColor", "0xE6E6E6");
            Bang(Arg(args, 3), "!Refresh *");
        }

        /// <summary>Light：只切界面亮色，不动面板配色。</summary>
        private static void Light(string[] args)
        {
            WriteVars(Paths.Combine(Arg(args, 2), @"REO\@Resources\Common\Variables\UserVariables.inc"),
                "DarkMode", "0", "ttgallerystyle", "Light",
                "ConfigBackgroundColor", "0xffffff", "ConfigBackgroundColor2", "0xd2d2d2", "ConfigTextColor", "0x323232");
            Bang(Arg(args, 3), "!Refresh *");
        }

        // ---------------------------------------------------------------- Select 的六个子模式

        /// <summary>
        /// Select：文件/文件夹/图片/应用选择器，把结果写进指定的 UserVariables.inc。
        /// 各子模式读的参数下标不同（尤其 Folder/App 的 ConfigPath 由 [4]/[7] 派生，**相对 CWD**），
        /// 逐个照原代码；对话框取消一律直接退出（不写任何值）。
        /// </summary>
        private static void Select(string[] args)
        {
            string mode = Arg(args, 2);
            string variable = Arg(args, 3);

            if (Same(mode, "File"))
            {
                // If $CmdLine[0] > 5 Then 起始目录 = [6] Else @UserProfileDir
                string starting = args.Length > 5 ? Arg(args, 6) : UserProfileDir();
                string file = OpenFile("Choose a file", starting, "All files (*.*)");
                if (file == null) return;
                WriteIniValue(Arg(args, 4), variable, file);
            }
            else if (Same(mode, "FileFolder"))
            {
                string starting = args.Length > 5 ? Arg(args, 6) : UserProfileDir();
                string folder = PickFolder("Choose a folder", starting);
                if (folder == null) return;
                WriteIniValue(Arg(args, 4), variable, folder);
            }
            else if (Same(mode, "Image"))
            {
                string starting = args.Length > 5
                    ? Arg(args, 6)
                    : Paths.Combine(UserProfileDir(), "Pictures");
                string file = OpenFile("Choose an image", starting, "Images (*.png;*.jpg;*.jpeg;*.bmp)");
                if (file == null) return;
                WriteIniValue(Arg(args, 4), variable, file);
                BangLonger(args, "!Refresh " + Arg(args, 5));
            }
            else if (Same(mode, "Folder"))
            {
                string folder = PickFolder("Choose a folder", null);
                if (folder == null) return;
                // ConfigPath 由 [4] 派生（把其中的 "REO\" 换成 "REO\@Resources\Config\"），
                // 皮肤侧传进来的是绝对路径；Relative 的写法在 CWD 变过时就落到别处，原样保留
                string configPath = Paths.Combine(
                    ReplaceAll(Arg(args, 4), @"REO\", @"REO\@Resources\Config\"), "UserVariables.inc");
                WriteIniValue(configPath, variable, folder);
                WriteIniValue(configPath, "FolderName", LastSegment(folder));
                BangLonger(args, "!Refresh " + Arg(args, 5));
            }
            else if (Same(mode, "App"))
            {
                string file = OpenFile("Choose an application", DesktopDir(), "Apps (*.exe;*.lnk)");
                if (file == null) return;

                // 原代码用 StringRight($file, 3) 取末三字符判类型（`=` 不区分大小写）
                string extension = file.Length <= 3 ? file : file.Substring(file.Length - 3);
                string info = "", path = "";
                if (Same(extension, "exe"))
                {
                    info = ProductName(file); // FileGetVersion($file, "ProductName")
                    path = file;
                }
                else if (Same(extension, "lnk"))
                {
                    string[] shortcut = ShellLink.Resolve(file); // FileGetShortcut
                    path = shortcut != null ? shortcut[0] : "";
                    info = Path.GetFileNameWithoutExtension(file); // _PathSplit 的文件名部分
                }

                // [7] 在这里既当 ConfigPath 的来源（原代码就这么写），又被 SendBangLonger 当 programpath
                string configPath = Paths.Combine(
                    ReplaceAll(Arg(args, 7), @"REO\", @"REO\@Resources\Config\"), "UserVariables.inc");
                WriteIniValue(configPath, variable, path);
                WriteIniValue(configPath, Arg(args, 4), info);

                string icon = OpenFile("Choose an icon",
                    Paths.Combine(Arg(args, 6), @"REO\@Resources\Graphics\Panels\Launcher\Icons"),
                    "Images (*.png;*.jpg;*.jpeg;*.bmp)");
                if (icon == null) return;
                WriteIniValue(configPath, Arg(args, 5), icon);
                BangLonger(args, "!Refresh " + Arg(args, 8));
            }
        }

        // ---------------------------------------------------------------- 通用小工具

        /// <summary>IniRead($file, "Variables", $key, $default)：文件读不到时返回默认值。</summary>
        private static string Read(IniDoc doc, string key, string defaultValue)
        {
            return doc == null ? defaultValue : doc.Get("Variables", key, defaultValue);
        }

        /// <summary>
        /// IniWrite($file, "Variables", $key, $value)。路径可能是相对 CWD 的（Select 的 Folder/App
        /// 模式即如此）；空路径时 AutoIt 的 IniWrite 失败且不报错，这里同样静默跳过。
        /// </summary>
        private static void WriteIniValue(string path, string key, string value)
        {
            if (string.IsNullOrEmpty(path)) return;
            IniDoc doc = IniDoc.LoadOrCreate(path);
            doc.Set("Variables", key, value);
            doc.Save();
        }

        /// <summary>按顺序写入一串键值（Dark / Light 四兄弟共用）。</summary>
        private static void WriteVars(string path, params string[] keysAndValues)
        {
            if (string.IsNullOrEmpty(path)) return;
            IniDoc doc = IniDoc.LoadOrCreate(path);
            for (int i = 0; i + 1 < keysAndValues.Length; i += 2)
                doc.Set("Variables", keysAndValues[i], keysAndValues[i + 1]);
            doc.Save();
        }

        /// <summary>读一个 ini 的 Variables 键（读不到返回空串），用于模板里的 ItemImage / IconLocation。</summary>
        private static string IniRead(string file, string key)
        {
            IniDoc doc = IniDoc.TryLoad(Paths.Combine(file, "UserVariables.inc"));
            return doc == null ? "" : doc.Get("Variables", key, "");
        }

        private static bool HasSection(IniDoc doc, string section)
        {
            foreach (string name in doc.SectionNames())
            {
                if (Same(name, section)) return true;
            }
            return false;
        }

        /// <summary>
        /// StringRegExpReplace($name, '[\s\\/\*\?\:<>|\"]', "")：去掉空白与文件名非法字符。
        /// </summary>
        private static string EscapeName(string name)
        {
            return Regex.Replace(name ?? string.Empty, @"[\s\\/*?:<>|""]", "");
        }

        /// <summary>
        /// StringReplace($text, $find, $replace)：默认替换**全部**出现处，且默认**不区分大小写**。
        /// </summary>
        private static string ReplaceAll(string text, string find, string replace)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find)) return text ?? string.Empty;

            int index = text.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return text;

            var builder = new StringBuilder();
            int start = 0;
            while (index >= 0)
            {
                builder.Append(text, start, index - start);
                builder.Append(replace);
                start = index + find.Length;
                index = text.IndexOf(find, start, StringComparison.OrdinalIgnoreCase);
            }
            builder.Append(text, start, text.Length - start);
            return builder.ToString();
        }

        /// <summary>StringSplit($folder, "\") 的最后一个元素（原代码用 UBound-1 取）。</summary>
        private static string LastSegment(string path)
        {
            string[] parts = (path ?? string.Empty).Split('\\');
            return parts.Length > 0 ? parts[parts.Length - 1] : string.Empty;
        }

        /// <summary>FileGetVersion($file, "ProductName")：读不到返回空串。</summary>
        private static string ProductName(string file)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(file).ProductName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>AutoIt 的 @UserProfileDir。</summary>
        private static string UserProfileDir()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(profile)
                ? Environment.ExpandEnvironmentVariables("%USERPROFILE%")
                : profile;
        }

        /// <summary>AutoIt 的 @DesktopDir。</summary>
        private static string DesktopDir()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return string.IsNullOrEmpty(desktop)
                ? Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Desktop")
                : desktop;
        }

        /// <summary>
        /// DirCopy($src, $dst, 1)：把源目录的**内容**递归复制进目标目录（覆盖同名文件），
        /// 目标不存在则创建——皮肤侧两个调用点（Create / CreatePanel）都依赖这个语义：
        /// 新建出来的面板目录要直接是模板内容，而不是模板的子目录。
        /// </summary>
        private static void CopyDirectoryTree(string source, string destination)
        {
            try
            {
                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination)) return;
                if (!Directory.Exists(source)) return;

                Directory.CreateDirectory(destination);
                foreach (string file in Directory.GetFiles(source))
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
                foreach (string directory in Directory.GetDirectories(source))
                    CopyDirectoryTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
            catch
            {
                // AutoIt 的 DirCopy 失败同样不报错
            }
        }

        /// <summary>Run($command, "", @SW_HIDE)：异步拉起、隐藏窗口、失败不报错。</summary>
        private static void RunHidden(string fileName, string arguments)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch
            {
                // AutoIt 的 Run 失败不报错
            }
        }

        /// <summary>RunWait($command, "", @SW_HIDE)：等命令跑完再继续。</summary>
        private static void RunWaitHidden(string fileName, string arguments)
        {
            try
            {
                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    if (process != null) process.WaitForExit();
                }
            }
            catch
            {
                // 同 Run
            }
        }

        /// <summary>ShellExecute($path)：用系统默认动词打开（原代码没给窗口样式，故不隐藏）。</summary>
        private static void ShellExecute(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch
            {
                // 原代码也不检查 ShellExecute 的返回值
            }
        }

        /// <summary>
        /// ProcessWaitClose("Rainmeter.exe")：等同名进程全部退出。原代码不带超时（＝无限等待），
        /// 这里轮询实现；枚举进程失败时直接返回，避免死循环。
        /// </summary>
        private static void WaitProcessClose(string processName)
        {
            string name = Path.GetFileNameWithoutExtension(processName);
            while (true)
            {
                try
                {
                    Process[] running = Process.GetProcessesByName(name);
                    if (running.Length == 0) return;
                    foreach (Process process in running) process.Dispose();
                }
                catch
                {
                    return;
                }
                Thread.Sleep(200);
            }
        }

        /// <summary>
        /// FileOpenDialog(..., 1)：flag 1 = 文件必须存在；取消返回 null（对应 @error）。
        /// 故意不设 RestoreDirectory：AutoIt 的文件对话框会把进程工作目录换到所选文件所在目录，
        /// 而 Select 的 Folder / App 模式紧接着用**相对 CWD** 的 ConfigPath 写 ini，必须同一语义。
        /// </summary>
        private static string OpenFile(string title, string initialDirectory, string autoItFilter)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.InitialDirectory = initialDirectory ?? string.Empty;
                dialog.Filter = ToWinFormsFilter(autoItFilter);
                dialog.CheckFileExists = true;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }

        /// <summary>FileSelectFolder("…", "", 1)：flag 1 = 保留「新建文件夹」按钮；取消返回 null。</summary>
        private static string PickFolder(string description, string initialDirectory)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(initialDirectory)) dialog.SelectedPath = initialDirectory;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        /// <summary>把 AutoIt 的过滤串 "描述 (*.a;*.b)" 转成 WinForms 的 "描述|*.a;*.b"。</summary>
        private static string ToWinFormsFilter(string autoItFilter)
        {
            int open = autoItFilter.LastIndexOf('(');
            int close = autoItFilter.LastIndexOf(')');
            if (open < 0 || close <= open) return autoItFilter + "|*.*";
            string description = autoItFilter.Substring(0, open).Trim();
            string pattern = autoItFilter.Substring(open + 1, close - open - 1).Trim();
            return description + "|" + pattern;
        }

        /// <summary>
        /// _ScreenCapture_Capture($file, 0, 0, @DesktopWidth, @DesktopHeight, $cursor)：
        /// 抓主屏整屏存盘。扩展名不认识时原版找不到编码器、不写文件，此处同样放弃。
        /// </summary>
        private static void CaptureScreen(string path, bool captureCursor)
        {
            ImageFormat format = FormatFromExtension(Path.GetExtension(path));
            if (format == null) return;

            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    if (captureCursor) DrawCursor(graphics);
                }
                bitmap.Save(path, format);
            }
        }

        /// <summary>按扩展名选 GDI+ 编码器（对应 _ScreenCapture_SaveImage 的扩展名分派）。</summary>
        private static ImageFormat FormatFromExtension(string extension)
        {
            switch ((extension ?? string.Empty).TrimStart('.').ToLowerInvariant())
            {
                case "jpg":
                case "jpeg":
                case "jpe": return ImageFormat.Jpeg;
                case "png": return ImageFormat.Png;
                case "bmp": return ImageFormat.Bmp;
                case "gif": return ImageFormat.Gif;
                case "tif":
                case "tiff": return ImageFormat.Tiff;
                default: return null;
            }
        }

        /// <summary>
        /// 把当前光标画进位图（AutoIt 的 _ScreenCapture_Capture 在 $bCursor=True 时同款处理）：
        /// GetCursorInfo 取位置与句柄、GetIconInfo 取热点偏移，再 DrawIconEx 按原始尺寸绘制。
        /// </summary>
        private static void DrawCursor(Graphics graphics)
        {
            try
            {
                var info = new CURSORINFO();
                info.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                if (!GetCursorInfo(ref info)) return;
                if (info.flags != CURSOR_SHOWING || info.hCursor == IntPtr.Zero) return;

                ICONINFO icon;
                if (!GetIconInfo(info.hCursor, out icon)) return;
                try
                {
                    IntPtr hdc = graphics.GetHdc();
                    try
                    {
                        DrawIconEx(hdc, info.ptScreenPos.X - icon.xHotspot, info.ptScreenPos.Y - icon.yHotspot,
                            info.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                    }
                }
                finally
                {
                    if (icon.hbmColor != IntPtr.Zero) DeleteObject(icon.hbmColor);
                    if (icon.hbmMask != IntPtr.Zero) DeleteObject(icon.hbmMask);
                }
            }
            catch
            {
                // 画光标失败不影响截图本身
            }
        }

        // ---------------------------------------------------------------- Win32 互操作

        private const byte VK_LWIN = 0x5B;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int WM_SYSCOMMAND = 274;
        private const int SC_MONITORPOWER = 61808;
        private const int CURSOR_SHOWING = 0x0001;
        private const int DI_NORMAL = 0x0003;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;

        private struct POINT
        {
            public int X;
            public int Y;
        }

        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(ref CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
            int cxWidth, int cyWidth, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);
    }
}
