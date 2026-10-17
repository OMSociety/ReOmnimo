// ReOmnimo MultiManager —— AutoIT\MultiManager.au3 的 C# 等价实现
//
// 调用契约（皮肤侧见 REO\Gallery\MultiManager\*.inc 与 Noice.ini）：
//   MultiManager.exe "#SETTINGSPATH#" "#SKINSPATH#" <命令> <序号> <壁纸路径>
// 命令：Save0（存布局）| Save1（存布局+壁纸）| Load0（载布局+快捷方式）| Load1（载布局）| Delete（删）
//       export.inc / shortcut.inc 传的 Zip 在 AutoIt 源码里没有对应分支，历史行为即空操作，
//       本次移植保持原样（见 CHANGELOG 与本工具 README）。
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace OmnimoTools.MultiManager
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 5)
                OmnimoDialogs.Error("Omnimo MultiManager", "Too few command line arguments specified.");

            string settings = args[0];
            string skins = args[1];
            string command = args[2];
            string index = args[3];
            string wallpaper = args[4];
            string saved = Paths.Combine(skins, @"REO\Gallery\MultiManager\Saved\");

            LanguagePack.Load(skins); // 与 AutoIt 版一致：仅校验语言文件可用

            string savedIni = Paths.Combine(saved, index + "\\Rainmeter.ini");

            if (Same(command, "Save0") || Same(command, "Save1"))
            {
                OmnimoDialogs.EnsureDirectory(Paths.Combine(saved, index));
                SaveScreenshot(Paths.Combine(saved, index + "\\screenshot.png"));

                if (!OmnimoDialogs.CopyFile(Paths.Combine(settings, "Rainmeter.ini"), savedIni))
                    return 0;
                if (Same(command, "Save1"))
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

                Rainmeter.SendBang(@"!Refresh REO\Gallery\MultiManager");
                return 0;
            }

            if (Same(command, "Load0") || Same(command, "Load1"))
            {
                if (Same(command, "Load0")) CopyShortcutSettings(settings, saved, index);

                string temp = Paths.Combine(settings, @"Layouts\MultiManagerTemp");
                if (!OmnimoDialogs.EnsureDirectory(temp))
                {
                    OmnimoDialogs.Message("Debug", "Creating directory failed");
                    return 0;
                }
                if (!OmnimoDialogs.CopyFile(savedIni, Paths.Combine(temp, "Rainmeter.ini")))
                {
                    OmnimoDialogs.Message("Debug", "Copying Rainmeter.ini file failed");
                    return 0;
                }
                if (!OmnimoDialogs.CopyFile(Paths.Combine(saved, index + "\\color.inc"),
                        Paths.Combine(skins, @"REO\@Resources\Common\Color\color.inc")))
                {
                    OmnimoDialogs.Message("Debug", "Copying color.inc file failed");
                    return 0;
                }

                Rainmeter.SendBang("!LoadLayout MultiManagerTemp");
                Thread.Sleep(5000); // 布局加载是异步的，临时目录必须活到加载完成
                OmnimoDialogs.RemoveDirectoryTree(temp);
                return 0;
            }

            if (Same(command, "Delete"))
            {
                OmnimoDialogs.RemoveDirectoryTree(Paths.Combine(saved, index));
                OmnimoDialogs.CopyFile(Paths.Combine(skins, @"REO\Gallery\MultiManager\screenshot.png"),
                    Paths.Combine(saved, index + "\\screenshot.png"), createDestinationDirectory: true);
                Rainmeter.SendBang(@"!Refresh REO\Gallery\MultiManager");
                return 0;
            }

            // 未识别的命令：与 AutoIt 的 Switch（无 default）一致，什么都不做
            return 0;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 等价于 Load0 里的 Shortcuts\1..3 处理：把当前 Rainmeter.ini 的 Active/WindowX/WindowY
        /// 抄进待加载的存档，让快捷方式面板跟随存档状态。
        /// </summary>
        private static void CopyShortcutSettings(string settings, string saved, string index)
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

        /// <summary>
        /// 等价于 _ScreenCapture_Capture("",0,0,-1,-1,False) + _SavehBitmapEx(..., 400, 400/(宽/高))：
        /// 抓主屏全屏，等比压到 400 像素宽的 PNG。进程保持 DPI 不感知（net48 默认），
        /// 因此 Screen.PrimaryScreen.Bounds 与 AutoIt 的 @DesktopWidth/@DesktopHeight 同源。
        /// </summary>
        private static void SaveScreenshot(string path)
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
    }
}
