// ReOmnimo PanelCreator —— AutoIt 源码 AutoIT\PanelCreator.au3 的 C# 移植。
//
// 本文件按原 .au3 的语句顺序逐段对应：控件坐标与尺寸、Colors.inc 的取值方式、写文件的键与顺序、
// SendBang 字符串、错误提示的标题与正文都照原样；只有 WinForms 与 AutoIt 语义无法一一对应的地方
// 才换实现，并在对应位置用注释说明差异原因。
//
// 全局约定：AutoIt 的 File* / Dir* 函数把相对路径按「进程当前目录」解析，本文件里的相对路径
// （Resources\...、Templates、RmskinTemp）同样保持相对，不锚到 exe 所在目录。

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static OmnimoTools.PanelCreator.Program;

namespace OmnimoTools.PanelCreator
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // DPI 感知由 Tools\Shared\app.manifest（PerMonitorV2）声明，删清单比删这行更致命
            // 与 SettingsCenter 同款诊断：未处理异常落盘 %TEMP%\pc_crash.txt，弹窗只点「退出」会丢失堆栈
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pc_crash.txt"), e.ExceptionObject.ToString()); }
                catch { }
            };
            Application.ThreadException += (s, e) =>
            {
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pc_crash.txt"), e.Exception.ToString()); }
                catch { }
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // If $CmdLine[0] < 1 Then OmnimoError("Omnimo Panel Creator", "Too few command line arguments specified.")
            if (args.Length < 1)
            {
                OmnimoDialogs.Error("Omnimo Panel Creator", "Too few command line arguments specified.");
                return;
            }

            string skinPath = args[0];
            string panelsInc = Paths.Combine(skinPath, @"REO\Gallery\panels.inc");

            // 原版读的是相对当前目录的 ..\Background\Varrar.inc：只有当前目录等于 exe 所在目录时才对得上；
            // 共享层的 LanguagePack.Load 改成按 skinPath 直接定位同一份文件，内容不变但不再依赖当前目录。
            Dictionary<string, string> language = LanguagePack.Load(skinPath);

            // If $CmdLine[0] > 2 And $CmdLine[2] == "Delete"：AutoIt 的 == 区分大小写，这里也用序数比较
            if (args.Length > 2 && string.Equals(args[1], "Delete", StringComparison.Ordinal))
            {
                DeletePanel(skinPath, panelsInc, args[2]);
                return;
            }

            // Const $scaledpi = IniRead(... UserVariables.inc, "Variables", "ScaleDpi", "1")
            string userVariables = Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc");
            float scale = (float)AutoIt.Number(IniRead(userVariables, "Variables", "ScaleDpi", "1"));

            // Const $ColorVariables = IniReadSection("Resources\Colors.inc", "Variables")
            List<KeyValuePair<string, string>> colors = ReadColorVariables("Resources\\Colors.inc");

            // 原版还读了 $BrowseBG = $ColorVariables[3][1] 与 $Color1..$Color6，但只用于被注释掉的色板分支，
            // 移植时不再声明，避免出现「赋值未使用」的编译警告。
            using (var form = new PanelCreatorForm(skinPath, panelsInc, language, scale, colors))
            {
                Application.Run(form);
            }

            // 正常关闭窗口时 _Exit() 已经清理过临时文件，这里兜底（例如别的窗口把主窗体逼下线）
            DeleteTempPanelIcons();
        }

        /// <summary>对应原版行 51-63 的删除分支：删目录、清 panels.inc 的三条键、通知 Rainmeter。</summary>
        private static void DeletePanel(string skinPath, string panelsInc, string index)
        {
            IniDoc panels = IniDoc.TryLoad(panelsInc);
            string path = panels != null ? panels.Get("Variables", "Path" + index, string.Empty) : string.Empty;

            if (path.Length == 0) return;      // If $path == "" Then Exit

            // If StringInStr($path, "..") Then Exit —— 拒绝会跳出 REO\Panels 的路径
            if (path.IndexOf("..", StringComparison.Ordinal) >= 0) return;

            OmnimoDialogs.RemoveDirectoryTree(Paths.Combine(skinPath, @"REO\Panels\" + path));

            panels.DeleteKey("Variables", "Name" + index);
            panels.DeleteKey("Variables", "Path" + index);
            panels.DeleteKey("Variables", "Icon" + index);
            panels.Save();

            Rainmeter.SendBang("!DeactivateConfig REO\\Panels\\" + path);
            Rainmeter.SendBang("!Refresh REO\\Gallery");
        }

        /// <summary>AutoIt 的 IniRead：文件或键不存在时返回默认值。</summary>
        private static string IniRead(string file, string section, string key, string defaultValue)
        {
            IniDoc doc = IniDoc.TryLoad(file);
            return doc == null ? defaultValue : doc.Get(section, key, defaultValue);
        }

        /// <summary>等价于 _Exit()：删掉拖放时写下的临时图标文件（@TempDir &amp; '\*.panelc'）。</summary>
        internal static void DeleteTempPanelIcons()
        {
            try
            {
                foreach (string file in Directory.GetFiles(Path.GetTempPath(), "*.panelc"))
                {
                    try { File.Delete(file); }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>IniReadSection("Resources\Colors.inc", "Variables")：按节内行序返回键值对。</summary>
        private static List<KeyValuePair<string, string>> ReadColorVariables(string file)
        {
            IniDoc doc = IniDoc.TryLoad(file);
            // 原版在 Colors.inc 读不到时会因为拿不到数组直接报错；这里退化成空表，后续颜色按 0 处理
            return doc == null ? new List<KeyValuePair<string, string>>() : doc.SectionOrdered("Variables");
        }

        /// <summary>$ColorVariables[$index][1]：1 起的行序号，越界给空串。</summary>
        internal static string ColorVariable(List<KeyValuePair<string, string>> variables, int index)
        {
            return index >= 1 && index <= variables.Count ? variables[index - 1].Value : string.Empty;
        }

        /// <summary>
        /// 等价于 AddAlpha($hex)：长度不等于 8 就原样返回，否则或上不透明 alpha。
        /// 原版返回值可能是字符串（未加 alpha 时），但三个使用点（GUICtrlSetBkColor / GraphicsClear / BitAND）
        /// 都会先经 Number() 取前导数值，所以这里直接返回数值等价。
        /// </summary>
        internal static int AddAlpha(string value)
        {
            if (value.Length != 8) return AutoItColor(value);
            return unchecked((int)0xFF000000) | AutoItColor(value);
        }

        /// <summary>颜色字符串参与运算前都要过一遍 AutoIt 的 Number()（读前导数值，读不到得 0）。</summary>
        internal static int AutoItColor(string value)
        {
            // 0xFF1E1E1E 这类值超过 int 范围，先转 long 再截低 32 位
            return unchecked((int)(long)AutoIt.Number(value));
        }

        /// <summary>GDI+ 画笔与画刷用的 ARGB（保留 alpha）。</summary>
        internal static Color ArgbColor(int value)
        {
            return Color.FromArgb(value);
        }

        /// <summary>控件背景色与字色用的 RGB —— AutoIt 的 GUICtrlSetBkColor / GUICtrlSetColor 只认低 24 位，没有 alpha。</summary>
        internal static Color RgbColor(int value)
        {
            return Color.FromArgb(0xFF, (value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        }

        /// <summary>
        /// 等价于 DirCopy($source, $destination, 1)：把源目录的「内容」合并进目标目录（不嵌源目录名），
        /// 同名文件覆盖；目标不存在时新建。返回是否是成功路径（原版靠返回值 = 0 判断失败）。
        /// </summary>
        internal static bool CopyDirectoryContents(string source, string destination)
        {
            try
            {
                if (!Directory.Exists(source)) return false;
                Directory.CreateDirectory(destination);

                foreach (string file in Directory.GetFiles(source))
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);

                foreach (string directory in Directory.GetDirectories(source))
                    if (!CopyDirectoryContents(directory, Path.Combine(destination, Path.GetFileName(directory)))) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>等价于 FileMove($source, $destination, 1)：覆盖式移动，失败返回 false。</summary>
        internal static bool MoveFileOverwrite(string source, string destination)
        {
            try
            {
                File.Copy(source, destination, true);
                File.Delete(source);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>等价于 DirRemove($path, 1) 的返回值语义（共享层的 RemoveDirectoryTree 吞掉失败，这里要拿结果）。</summary>
        internal static bool RemoveDirectoryTreeChecked(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>FileGetShortcut($lnkPath)：失败（原版 @error = 1）返回 false。</summary>
        internal static bool TryResolveShortcut(string lnkPath, out string target, out string iconFile)
        {
            target = string.Empty;
            iconFile = string.Empty;

            string[] details = ShellLink.Resolve(lnkPath);
            // 共享层的 ShellLink.Resolve 返回 {目标, 参数, 工作目录, 图标路径, 图标索引}，
            // 图标文件在第 4 个元素；AutoIt 的 FileGetShortcut 里对应 $aDetails[4]（第 5 个）
            if (details == null || details.Length < 4) return false;

            target = details[0] ?? string.Empty;
            iconFile = details[3] ?? string.Empty;
            return true;
        }

        /// <summary>StringRight($value, 3) 的不区分大小写比较写法。</summary>
        internal static bool EndsWithIgnoreCase(string value, string suffix)
        {
            return value != null
                && value.Length >= suffix.Length
                && string.Equals(value.Substring(value.Length - suffix.Length), suffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>等价于 StringReplace($value, $find, $replace)：不区分大小写，替换全部出现。</summary>
        internal static string ReplaceAll(string value, string find, string replacement)
        {
            return Regex.Replace(value, Regex.Escape(find), replacement, RegexOptions.IgnoreCase);
        }
    }

    /// <summary>对应原版的 GUI 主体（.au3 行 86-393）。</summary>
    internal sealed class PanelCreatorForm : ModernUi.ModernForm
    {
        /// <summary>ModernForm 自绘标题栏占用的内容区高度：所有顶层控件/绘制的 Y 都要让出这段。</summary>
        private const int TitleOffset = 32;
        private readonly string _skinPath;      // $SkinPath
        private readonly string _panelsIncPath; // $PanelsInc
        private readonly Dictionary<string, string> _language;
        private readonly float _scale;          // $scaledpi

        private readonly int _panelArgb;        // $PanelBG（AddAlpha 之后）
        private readonly Color _createColor;    // $CreateBG
        private readonly Color _footerColor;    // $FooterBG
        private readonly Color _fieldsColor;    // $FieldsBG
        private readonly Color _fontColor;      // $FontColor

        private readonly FontFamily _fontFamily;
        private readonly Font _titleFont;       // 9pt，面板标题
        private readonly Font _browseFont;      // 8pt，"Export *.rmskin"
        private readonly Font _createFont;      // 14pt，"Create Panel"
        private readonly Brush _textBrush;
        private readonly StringFormat _centerFormat;

        private readonly Panel _drop;
        private readonly Label _loadE;
        private readonly Panel _savePanel;
        private readonly Panel _createPanel;
        private readonly Panel _browsePanel;
        private readonly Panel _selectPanel;
        private readonly TextBox _nameBox;
        private readonly TextBox _actionBox;
        private readonly Timer _pollTimer;

        private readonly Image _saveImage;
        private readonly Image _browseImage;
        private readonly Image _selectImage;

        private readonly Bitmap _backbuffer;    // 150x150 双缓冲，画好后再拉伸到控件
        private readonly object _bufferLock = new object();

        private string _oldName = string.Empty; // $oldname
        private string _tmpName = string.Empty; // $tmpname
        private string _oldImage;               // $oldimg
        private Image _panelImage;              // $hImage
        private float _imageWidth;              // $imagew
        private float _imageHeight;             // $imageh

        public PanelCreatorForm(string skinPath, string panelsIncPath, Dictionary<string, string> language, float scale,
            List<KeyValuePair<string, string>> colors)
            : base(BuildTheme(colors), LanguagePack.Text(language, "PanelCreator").Length > 0
                  ? LanguagePack.Text(language, "PanelCreator") : "Panel Creator", resizable: false)
        {
            _skinPath = skinPath;
            _panelsIncPath = panelsIncPath;
            _language = language;
            _scale = scale;

            _panelArgb = AddAlpha(ColorVariable(colors, 2));
            _createColor = RgbColor(AutoItColor(ColorVariable(colors, 4)));
            _footerColor = ArgbColor(AddAlpha(ColorVariable(colors, 5)));
            _fieldsColor = ArgbColor(AddAlpha(ColorVariable(colors, 6)));
            _fontColor = ArgbColor(AddAlpha(ColorVariable(colors, 7)));

            _fontFamily = ModernUi.UiFont(9f).FontFamily;   // 与 SettingsCenter 同栈（Microsoft YaHei UI → Segoe UI）
            _titleFont = new Font(_fontFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);
            _browseFont = new Font(_fontFamily, 8f, FontStyle.Regular, GraphicsUnit.Point);
            _createFont = new Font(_fontFamily, 14f, FontStyle.Regular, GraphicsUnit.Point);
            _textBrush = new SolidBrush(_fontColor);
            _centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };  // StringFormatSetAlign($hFormat, 1)

            _backbuffer = new Bitmap(150, 150);
            _saveImage = LoadImage("Resources\\save.png");
            _browseImage = LoadImage("Resources\\file.png");
            _selectImage = LoadImage("Resources\\folder.png");

            // 2026-10 现代化：无边框 + 自绘标题栏（ModernForm），视觉壳与 ColorChanger/ConfigBackground 同套；
            // 内容区仍按 Colors.inc 的调色板自绘（原版 GDI+ 直绘窗口的移植），整体随皮肤走。
            Font = ModernUi.UiFont(9f);   // 现代 UI 字体栈（YaHei UI），原生 TextBox 走环境字体继承——GuiFont/SimSun 只用于忠实对照移植，本窗已现代化
            ClientSize = new Size(Px(230), Px(420) + TitleOffset);
            BackColor = RgbColor(AutoItColor(ColorVariable(colors, 1)));   // GUISetBkColor($GuiBG)

            // Const $drop = GUICtrlCreateGraphic(40, 40, 150, 150) + $GUI_DROPACCEPTED
            _drop = new Panel();
            _drop.SetBounds(Px(40), Px(40) + TitleOffset, Px(150), Px(150));
            _drop.BackColor = RgbColor(_panelArgb);
            _drop.AllowDrop = true;
            _drop.DragEnter += OnDropDragEnter;
            _drop.DragDrop += OnDropDragDrop;
            _drop.Paint += OnDropPaint;
            _drop.Paint += (s, e) => { var p = (Panel)s; e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using (var pen = new Pen(Blend(_fontColor, RgbColor(_panelArgb), 0.25f))) e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
            Controls.Add(_drop);

            // Const $loadE = GUICtrlCreateLabel(".....", 110, 322, 70)：原版省略高度，AutoIt 的 label 默认高 20
            _loadE = new Label();
            _loadE.Text = ".....";
            _loadE.AutoSize = false;
            _loadE.SetBounds(Px(110), Px(322) + TitleOffset, Px(70), Px(20));
            _loadE.BackColor = RgbColor(_panelArgb);
            _loadE.ForeColor = _createColor;
            _loadE.Visible = false;
            Controls.Add(_loadE);

            // Const $save = GUICtrlCreateGraphic(40, 355, 150, 20)
            _savePanel = new Panel();
            _savePanel.SetBounds(Px(40), Px(355) + TitleOffset, Px(150), Px(20));
            _savePanel.BackColor = RgbColor(_panelArgb);
            _savePanel.Cursor = Cursors.Hand;
            _savePanel.Click += OnSaveClick;
            _savePanel.Paint += (s, e) => { var p = (Panel)s; using (var b = new SolidBrush(Blend(_fontColor, _footerColor, 0.6f))) e.Graphics.DrawString(LanguagePack.Text(_language, "ExportRmskin"), _browseFont, b, new RectangleF(0, 0, p.Width, p.Height), _centerFormat); };
            Controls.Add(_savePanel);

            // Const $create = GUICtrlCreateGraphic(40, 310, 150, 45)
            _createPanel = new Panel();
            _createPanel.SetBounds(Px(40), Px(310) + TitleOffset, Px(150), Px(45));
            _createPanel.BackColor = _createColor;
            _createPanel.Cursor = Cursors.Hand;
            _createPanel.Click += OnCreateClick;
            _createPanel.Paint += (s, e) =>
            {
                var p = (Panel)s;
                bool hot = p.ClientRectangle.Contains(p.PointToClient(Cursor.Position));
                using (var bg = new SolidBrush(hot ? Blend(Color.White, _createColor, 0.12f) : _createColor))
                    e.Graphics.FillRectangle(bg, p.ClientRectangle);
                e.Graphics.DrawString(LanguagePack.Text(_language, "CreatePanel"), _createFont, _textBrush, new RectangleF(0, 0, p.Width, p.Height), _centerFormat);
            };
            Controls.Add(_createPanel);

            // Const $browse = GUICtrlCreateGraphic(145, 262, 17, 17) / $select = (165, 262, 17, 17)
            _browsePanel = new Panel();
            _browsePanel.SetBounds(Px(145), Px(262) + TitleOffset, Px(17), Px(17));
            _browsePanel.BackColor = T.Input;
            _browsePanel.Cursor = Cursors.Hand;
            _browsePanel.Click += OnBrowseClick;
            _browsePanel.Paint += (s, e) => { var p = (Panel)s; PaintGlyph(e.Graphics, p.Width, p.Height, folder: false); };
            Controls.Add(_browsePanel);

            _selectPanel = new Panel();
            _selectPanel.SetBounds(Px(165), Px(262) + TitleOffset, Px(17), Px(17));
            _selectPanel.BackColor = T.Input;
            _selectPanel.Cursor = Cursors.Hand;
            _selectPanel.Click += OnSelectClick;
            _selectPanel.Paint += (s, e) => { var p = (Panel)s; PaintGlyph(e.Graphics, p.Width, p.Height, folder: true); };
            Controls.Add(_selectPanel);

            // Const $name = GUICtrlCreateInput($Language.Item("Name"), 50, 223, 130, 15, -1, $WS_EX_WINDOWEDGE)
            _nameBox = new EdgeTextBox();
            _nameBox.BorderStyle = BorderStyle.None;   // AutoIt 的 -1 是默认输入框样式，不含 WS_BORDER
            _nameBox.AutoSize = false;                 // 让高度就是原版的 15*$scaledpi，而不是字体撑出来的高度
            _nameBox.SetBounds(Px(50), Px(223) + TitleOffset, Px(130), Px(15));
            _nameBox.Text = LanguagePack.Text(_language, "Name");
            _nameBox.BackColor = T.Input;   // 现代化：皮肤调色板里的 FieldsBG 是白底（2013 遗产），输入框统一用深色井
            _nameBox.ForeColor = T.Fg;
            Controls.Add(_nameBox);

            // Const $action = GUICtrlCreateInput($Language.Item("Action"), 50, 263, 65, 15, -1, $WS_EX_WINDOWEDGE)
            _actionBox = new EdgeTextBox();
            _actionBox.BorderStyle = BorderStyle.None;
            _actionBox.AutoSize = false;
            _actionBox.SetBounds(Px(50), Px(263) + TitleOffset, Px(65), Px(15));
            _actionBox.Text = LanguagePack.Text(_language, "Action");
            _actionBox.BackColor = T.Input;
            _actionBox.ForeColor = T.Fg;
            Controls.Add(_actionBox);

            DrawDropHint();

            // While 1 / Sleep(50) / $tmpname = GUICtrlRead($name)
            _pollTimer = new Timer();
            _pollTimer.Interval = 50;
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();

            FormClosing += OnFormClosing;
        }

        /// <summary>把 AutoIt 的像素量乘以 $scaledpi 并截断取整（AutoIt 传给控件坐标的也是整数）。</summary>
        private int Px(float value)
        {
            return (int)(value * _scale);
        }
        /// <summary>内容区本来就按 Colors.inc 调色板自绘，标题栏主题直接从同一调色板构，保证壳与内容同色。</summary>
        private static ModernUi.Theme BuildTheme(List<KeyValuePair<string, string>> colors)
        {
            var t = new ModernUi.Theme { Light = false };
            t.Bg = RgbColor(AutoItColor(ColorVariable(colors, 1)));      // $GuiBG
            t.Card = RgbColor(AddAlpha(ColorVariable(colors, 2)));      // $PanelBG
            t.Fg = RgbColor(AutoItColor(ColorVariable(colors, 7)));     // $FontColor
            t.Input = Blend(RgbColor(AutoItColor(ColorVariable(colors, 1))), RgbColor(AutoItColor(ColorVariable(colors, 7))), 0.08f); // 深色输入井：FieldsBG 是白底，是 2013 年白输入框的遗产，现代化壳里不采用
            t.Rail = t.Card;
            t.Sub = Blend(t.Fg, t.Bg, 0.45f);
            t.Border = Color.FromArgb(48, t.Fg);
            t.Accent = ModernUi.SystemAccent();
            return t;
        }

        private static Color Blend(Color a, Color b, float f)
        {
            return Color.FromArgb(255,
                (int)(a.R + (b.R - a.R) * f),
                (int)(a.G + (b.G - a.G) * f),
                (int)(a.B + (b.B - a.B) * f));
        }

        /// <summary>旧 file.png / folder.png 小图换成几何线条图标：folder=文件夹，否则=文档。</summary>
        private void PaintGlyph(Graphics g, int w, int h, bool folder)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(_fontColor, 1.6f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            })
            {
                if (folder)
                {
                    g.DrawRectangle(pen, 3, 6, w - 6, h - 9);
                    g.DrawLine(pen, 3, 6, 6, 6);
                }
                else
                {
                    g.DrawRectangle(pen, 4, 3, w - 8, h - 6);
                    g.DrawLine(pen, 6, 7, w - 6, 7);
                    g.DrawLine(pen, 6, 10, w - 9, 10);
                }
            }
        }

        /// <summary>原版把 droptext.png 画进拖放区（图片内嵌英文、无 i18n 且风格陈旧）；现代化改为直接绘制提示文字。
        /// 名字轮询（OnPollTick）每次名字变化都会重画后备位图，所以提示文字也作为「无预览图时的底」在 DrawPanelImage 里复用。</summary>
        private void DrawDropHint()
        {
            lock (_bufferLock)
            {
                using (Graphics back = Graphics.FromImage(_backbuffer))
                {
                    back.Clear(Color.FromArgb(_panelArgb));
                    DrawHintLines(back);
                }
            }
            _drop.Invalidate();
        }

        private void DrawHintLines(Graphics back)
        {
            back.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // droptext.png 的内嵌英文位图改成了语言键（i18n 补齐）；缺键回退英文
            var hint = LanguagePack.Text(_language, "DropHint");
            var lines = (hint.Length == 0 ? "Drop here|Shortcuts|Folders|PNGs" : hint).Split('|');
            using (var brush = new SolidBrush(Blend(_fontColor, Color.FromArgb(_panelArgb), 0.45f)))
            {
                // UiFont 是共享层缓存实例——绝不能 using/Dispose，否则整窗后续绘制抛 ArgumentException「参数无效」
                Font font = ModernUi.UiFont(9f);
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    back.DrawString(string.Join(Environment.NewLine, lines), font, brush, new RectangleF(0, 0, _backbuffer.Width, _backbuffer.Height), fmt);
            }
        }

        /// <summary>_GDIPlus_ImageLoadFromFile 的等价物：失败返回 null（原版返回 0）。</summary>
        private static Image LoadImage(string path)
        {
            try
            {
                using (Image source = Image.FromFile(path))
                {
                    // 立刻拷进内存，避免原文件被 GDI+ 锁住（原版 UDF 也是读进内存的）
                    return new Bitmap(source);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>等价于 FileChangeDir(@ScriptDir)：Shell 对话框会改进程当前目录，原版每次都切回来。</summary>
        private static void ChangeToScriptDirectory()
        {
            try { Directory.SetCurrentDirectory(Paths.ScriptDirectory()); }
            catch { }
        }

        /// <summary>StringRegExpReplace($title, '[\s\\/\*\?\:&lt;&gt;|\"]', "")：删掉空白与非法路径字符。</summary>
        private static string SanitizeFolderName(string title)
        {
            return Regex.Replace(title ?? string.Empty, "[\\s\\\\/\\*\\?\\:<>|\"]", string.Empty);
        }

        // ------------------------------------------------------------------
        // 窗口整体绘制（原版直接在窗口 DC 上画，这里放到 Paint 里）
        // ------------------------------------------------------------------

        /// <summary>
        /// 原版用 _GDIPlus_GraphicsCreateFromHWND 在窗口 DC 上画一次：页脚、save.png、两个输入框底、
        /// file.png/folder.png、两个按钮文字。这些像素在窗口被遮挡或收到 WM_PAINT 之后就没了（实测确认），
        /// 所以移植到 OnPaint 重画，视觉结果一致且不会丢失。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // 本层绘制坐标是 96-DPI 设计值（_scale 是皮肤自己的 ScaleDpi，与屏幕 DPI 无关）；
            // DPI 缩放由 ModernForm.OnHandleCreated 的 Scale 完成，这里用同一倍率变换手绘层，
            // 缩放同时作用于几何与 GDI+ 字号，与子控件缩放结果一致。
            float dpiScale = WinDpi / 96f;
            g.ScaleTransform(dpiScale, dpiScale);

            // _GDIPlus_GraphicsFillRect($hGUIGraphic, 0, 205*$scaledpi, 230*$scaledpi, 500*$scaledpi, $FooterBG)
            using (var brush = new SolidBrush(_footerColor))
                g.FillRectangle(brush, 0f, 205f * _scale + TitleOffset, 230f * _scale, 500f * _scale);

            // 输入框底：FillRect(40, 215, 150, 30) 与 FillRect(40, 255, 150, 30)。原版用 $FieldsBG（白），
            // 现代化壳统一深色输入井 + 弱描边，让输入区看得出来
            using (var brush = new SolidBrush(T.Input))
            using (var pen = new Pen(Blend(T.Fg, T.Bg, 0.30f)))
            {
                g.FillRectangle(brush, 40f * _scale, 215f * _scale + TitleOffset, 150f * _scale, 30f * _scale);
                g.FillRectangle(brush, 40f * _scale, 255f * _scale + TitleOffset, 150f * _scale, 30f * _scale);
                g.DrawRectangle(pen, 40f * _scale, 215f * _scale + TitleOffset, 150f * _scale - 1f, 30f * _scale - 1f);
                g.DrawRectangle(pen, 40f * _scale, 255f * _scale + TitleOffset, 150f * _scale - 1f, 30f * _scale - 1f);
            }

            // _GDIPlus_GraphicsDrawImage($hGUIGraphic, $hSaveImage, 350, 147)
            // 这个坐标在原版里没乘 $scaledpi，落在 230 宽的客户区之外，永远看不到，照原样画
            if (_saveImage != null) g.DrawImage(_saveImage, 350f, 147f);

            // 按钮文字与 file/folder 图标在各自子面板的 Paint 里画：子控件（不透明底）会盖住
            // Form 背景层，原版 AutoIt 的 GUICtrlCreateGraphic 没有独立 HWND 才直接画窗口 DC
        }

        /// <summary>拖放区重画：把 150x150 的后备位图拉伸到控件尺寸（原版 GraphicsDrawImageRect(..., 150*$scaledpi)）。</summary>
        private void OnDropPaint(object sender, PaintEventArgs e)
        {
            lock (_bufferLock)
            {
                if (_backbuffer == null) return;
                e.Graphics.DrawImage(_backbuffer, new Rectangle(0, 0, _drop.Width, _drop.Height));
            }
        }

        // ------------------------------------------------------------------
        // 面板预览（Func DrawPanelImage，.au3 行 400-428）
        // ------------------------------------------------------------------

        private void DrawPanelImage(string imagePath)
        {
            lock (_bufferLock)
            {
                using (Graphics back = Graphics.FromImage(_backbuffer))
                {
                    // _GDIPlus_GraphicsSetSmoothingMode($hBackbuffer, 2) = 高质量平滑
                    back.SmoothingMode = SmoothingMode.HighQuality;
                    // _GDIPlus_GraphicsClear($hBackbuffer, $PanelBG)
                    back.Clear(ArgbColor(_panelArgb));

                    // If $img <> $oldimg Then：AutoIt 的字符串比较不区分大小写
                    if (string.Compare(imagePath, _oldImage, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        _oldImage = imagePath;
                        _panelImage = LoadImage(imagePath);
                        if (_panelImage != null)
                        {
                            _imageWidth = _panelImage.Width;
                            _imageHeight = _panelImage.Height;
                        }
                    }

                    // 等比缩放到长边 100
                    if (_imageWidth > _imageHeight)
                    {
                        _imageHeight = 100f * _imageHeight / _imageWidth;
                        _imageWidth = 100f;
                    }
                    else if (_imageHeight > 0f)
                    {
                        // 原版此处是 100*0/0：图片加载失败（宽高都是 0）时会除零报错，这里跳过绘制
                        _imageWidth = 100f * _imageWidth / _imageHeight;
                        _imageHeight = 100f;
                    }

                    if (_panelImage != null && _imageWidth > 0f && _imageHeight > 0f)
                    {
                        float x = 74f - (_imageWidth / 2f);
                        float y = 71f - (_imageHeight / 2f);
                        back.DrawImage(_panelImage, x, y, _imageWidth, _imageHeight);
                    }
                    else
                    {
                        DrawHintLines(back);   // 还没有拖入预览图：提示文字作为底（原版 droptext.png 的替代），名字轮询清图后仍保留
                    }

                    // 标题：_GDIPlus_GraphicsDrawStringEx($hBackbuffer, $tmpname, $hFont, RectFCreate(10,125), ...)
                    // au3 用默认左对齐格式（.au3:167-173 画完按钮文字后已把对齐复位为 0）；不能用居中文本格式——零宽矩形会把文字中心钉在 x=10，左半被后备位图裁掉
                    back.DrawString(_tmpName ?? string.Empty, _titleFont, _textBrush,
                        new RectangleF(10f, 125f, 0f, 0f), StringFormat.GenericDefault);
                }

                _drop.Invalidate();
            }
        }

        /// <summary>_GDIPlus_ImageSaveToFile($hBitmap, $preview)：150x150 后备位图原样存 PNG。</summary>
        private void SaveBackbuffer(string file)
        {
            lock (_bufferLock)
            {
                _backbuffer.Save(file, ImageFormat.Png);
            }
        }

        /// <summary>主循环里每 50ms 检查一次名字是否变化（名字变了才重画标题）。</summary>
        private void OnPollTick(object sender, EventArgs e)
        {
            _tmpName = _nameBox.Text;
            // If $tmpname <> $oldname Then：AutoIt 的 &lt;&gt; 对字符串同样不区分大小写
            if (string.Compare(_tmpName, _oldName, StringComparison.OrdinalIgnoreCase) != 0)
            {
                DrawPanelImage(_oldImage);
                _oldName = _tmpName;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _pollTimer.Stop();
            // 原版 $GUI_EVENT_CLOSE 走 _Exit()：清临时文件再退出
            Program.DeleteTempPanelIcons();
        }

        // ------------------------------------------------------------------
        // 拖放（Case $GUI_EVENT_DROPPED，.au3 行 202-256）
        // ------------------------------------------------------------------

        private void OnDropDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDropDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            // AutoIt 的 @GUI_DragFile 只会给出一个文件
            string file = files[0];
            // _PathSplit 的 $szExt（含点）再 StringLower；$szFName 是不含扩展名的文件名
            string extension = Path.GetExtension(file).ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(file);
            bool iconToFile = true;                 // $IcoToFile
            Bitmap iconImage = null;                // $IconImage

            switch (extension)
            {
                case ".lnk":
                    {
                        string targetRaw;
                        string iconFile;
                        if (!TryResolveShortcut(file, out targetRaw, out iconFile))
                        {
                            // If @error = 1 Then MsgBox(16, "Error", "Unable to load shortcut.") + ContinueCase
                            MessageBox.Show(this, "Unable to load shortcut.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            goto case ".exe";
                        }

                        string target = targetRaw;
                        if (!File.Exists(target)) target = ReplaceAll(target, "Program Files (x86)", "Program Files");

                        _nameBox.Text = fileName;
                        _actionBox.Text = target;

                        string icon;
                        if (iconFile.Length != 0 && !EndsWithIgnoreCase(iconFile, "dll"))
                        {
                            icon = iconFile;
                        }
                        else
                        {
                            // 原版这里用的是 $aDetails[0] 原值，不是上面修正过的 $target
                            icon = targetRaw;
                            if (!File.Exists(icon)) icon = ReplaceAll(icon, "Program Files (x86)", "Program Files");
                        }

                        iconImage = IconExtractor.FromPath(icon, 256, 256, out _);
                        break;
                    }

                case ".ico":
                    iconImage = IconExtractor.FromIcoFile(file, 256, 256, null);
                    break;

                case ".exe":
                    _nameBox.Text = fileName;
                    _actionBox.Text = file;
                    iconImage = IconExtractor.FromPeEx(file, 256, 256);
                    break;

                default:
                    iconToFile = false;
                    break;
            }

            if (iconToFile)
            {
                // $filen = _TempFile(Default, Default, ".panelc")：@TempDir 下 "~" 前缀的临时名，此时文件还不存在
                file = Path.Combine(Path.GetTempPath(), "~" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".panelc");
                if (iconImage != null) IconExtractor.SavePng(iconImage, file);
                // _GDIPlus_ImageDispose($hImage)
                if (_panelImage != null)
                {
                    _panelImage.Dispose();
                    _panelImage = null;
                }
            }

            DrawPanelImage(file);
        }

        // ------------------------------------------------------------------
        // 浏览 / 选择目录（Case $browse / $select）
        // ------------------------------------------------------------------

        private void OnBrowseClick(object sender, EventArgs e)
        {
            // FileOpenDialog("Choose a file", @UserProfileDir, "All (*.*)", 1)：flag 1 = 文件必须存在
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose a file";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                dialog.Filter = "All (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) _actionBox.Text = dialog.FileName;
            }
            ChangeToScriptDirectory();
        }

        private void OnSelectClick(object sender, EventArgs e)
        {
            // FileSelectFolder("Choose a folder", "", 1 + 2 + 4, @UserProfileDir)
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder";
                dialog.ShowNewFolderButton = true;
                dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (dialog.ShowDialog(this) == DialogResult.OK) _actionBox.Text = dialog.SelectedPath;
            }
            ChangeToScriptDirectory();
        }

        // ------------------------------------------------------------------
        // 创建面板（Case $create，.au3 行 268-308）
        // ------------------------------------------------------------------

        private void OnCreateClick(object sender, EventArgs e)
        {
            _createPanel.Visible = false;   // GUICtrlSetState($create, $GUI_HIDE)
            _loadE.Visible = true;          // GUICtrlSetState($loadE, $GUI_SHOW)

            string title = _nameBox.Text;
            string folderName = SanitizeFolderName(title);

            CreatePanel(title, folderName, Paths.Combine(_skinPath, @"REO\Panels\" + folderName));

            IniDoc panels = IniDoc.LoadOrCreate(_panelsIncPath);

            int i;
            for (i = 1; i <= 32; i++)
            {
                string name = panels.Get("Variables", "Name" + i, string.Empty);
                // If $iname = "" Or $iname = $title Then ExitLoop（AutoIt 的 = 不区分大小写）
                if (name.Length == 0 || string.Equals(name, title, StringComparison.OrdinalIgnoreCase)) break;
            }

            if (i == 33)
            {
                // MsgBox(52, ...) = 是/否 + 问号图标
                DialogResult answer = MessageBox.Show(this,
                    "The Custom Panels gallery is full. Would you like to overwrite the last panel?",
                    "Omnimo Panel Creator", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                // If $answer = 6 Then $i -= 1；答「否」时 $i 保持 33，原版会去写 Name33，这里照原样保留
                if (answer == DialogResult.Yes) i -= 1;
            }

            panels.Set("Variables", "Name" + i, title);
            panels.Set("Variables", "Path" + i, folderName);
            panels.Set("Variables", "Icon" + i, folderName + ".png");
            panels.Save();

            // Activate panel
            Rainmeter.SendBang("!RefreshApp");
            Rainmeter.SendBang("!ActivateConfig REO\\Panels\\" + folderName + " default.ini");

            _createPanel.Visible = true;
            _loadE.Visible = false;

            // 原版在这里又把 "CreatePanel" 文字重画了一遍；改成 Paint 重绘之后只需要让它失效一次
            Invalidate();
        }

        // ------------------------------------------------------------------
        // 导出 .rmskin（Case $save，.au3 行 312-366）
        // ------------------------------------------------------------------

        private void OnSaveClick(object sender, EventArgs e)
        {
            // 原版是 Do ... Until 1：每轮就是一次独立的保存尝试，「取消」等于跳出循环
            while (true)
            {
                string title = _nameBox.Text;
                string folderName = SanitizeFolderName(title);

                string destination;
                // FileSaveDialog("Destination", @DesktopDir, "Rmskin files (*.rmskin)", 16, $foldername & ".rmskin")
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Title = "Destination";
                    dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    dialog.Filter = "Rmskin files (*.rmskin)|*.rmskin";
                    dialog.FileName = folderName + ".rmskin";
                    dialog.OverwritePrompt = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK) break;
                    destination = dialog.FileName;
                }
                ChangeToScriptDirectory();

                string author;
                // InputBox("Author", $Language.Item("AuthorName"), @UserName, Default, 120, 125)
                using (var input = new AuthorInputForm(_language, LanguagePack.Text(_language, "AuthorName"), Environment.UserName))
                {
                    if (input.ShowDialog(this) != DialogResult.OK) break;
                    author = input.Value;
                }

                if (!OmnimoDialogs.EnsureDirectory("RmskinTemp"))
                    OmnimoDialogs.Error("Error", "Unable to create temporary .rmskin directory");

                OmnimoDialogs.CopyFile(Path.Combine("Resources", "RMSKIN.inc"), Path.Combine("RmskinTemp", "RMSKIN.ini"));

                IniDoc rmskin = IniDoc.LoadOrCreate(Path.Combine("RmskinTemp", "RMSKIN.ini"));
                rmskin.Set("rmskin", "Name", title + " Panel");
                rmskin.Set("rmskin", "Author", author);
                rmskin.Set("rmskin", "Load", "REO\\Panels\\" + folderName + "\\default.ini");
                rmskin.Save();

                CreatePanel(title, folderName,
                    Paths.Combine(Paths.Combine(Paths.ScriptDirectory(), @"RmskinTemp\Skins\REO\Panels"), folderName));

                // _Zip_Create("RmskinTemp\Temp.zip", 1)：原版不检查它的返回值，只看两次 AddItem 之后的 @error
                ZipWriter.Create("RmskinTemp\\Temp.zip", true);
                bool addedIni = ZipWriter.AddItem(Paths.Combine(Paths.ScriptDirectory(), @"RmskinTemp\Temp.zip"),
                    Paths.Combine(Paths.ScriptDirectory(), @"RmskinTemp\RMSKIN.ini"));
                bool addedSkins = ZipWriter.AddItem(Paths.Combine(Paths.ScriptDirectory(), @"RmskinTemp\Temp.zip"),
                    Paths.Combine(Paths.ScriptDirectory(), @"RmskinTemp\Skins"));
                if (!addedIni || !addedSkins)
                    OmnimoDialogs.Error("Error", "Unable to create a temporary zip file");

                if (!MoveFileOverwrite("RmskinTemp\\Temp.zip", destination))
                    OmnimoDialogs.Error("Error", "Unable to create .rmskin");

                if (!RemoveDirectoryTreeChecked("RmskinTemp"))
                    OmnimoDialogs.Error("Error", "Unable to remove temporary directory");

                // Rmskin 文件末尾是一个 16 字节的 struct PackageFooter { __int64 size; BYTE flags; char key[7]; }
                // size 是追加前的大小
                long size = new FileInfo(destination).Length;
                var footer = new byte[16];
                BitConverter.GetBytes(size).CopyTo(footer, 0);
                footer[8] = 0;
                byte[] key = Encoding.ASCII.GetBytes("RMSKIN");   // char[7]，最后一个字节保持 0
                Buffer.BlockCopy(key, 0, footer, 9, key.Length);

                using (var stream = new FileStream(destination, FileMode.Append, FileAccess.Write))
                {
                    stream.Write(footer, 0, footer.Length);
                }

                // StringReplace($dest, ".rmskin", "") 默认不区分大小写且替换全部出现
                string preview = Regex.Replace(destination, Regex.Escape(".rmskin"), string.Empty, RegexOptions.IgnoreCase)
                    + "_preview.png";
                SaveBackbuffer(preview);

                // MsgBox(64, $Language.Item("Success"), ...)
                MessageBox.Show(this,
                    LanguagePack.Text(_language, "SuccessfullyCreated") + destination + "\r\n"
                        + LanguagePack.Text(_language, "PreviewGeneratedIn") + preview,
                    LanguagePack.Text(_language, "Success"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            }
        }

        // ------------------------------------------------------------------
        // Func CreatePanel（.au3 行 430-458）
        // ------------------------------------------------------------------

        private void CreatePanel(string title, string folderName, string folderPath)
        {
            string execute = _actionBox.Text;   // Local $execute = GUICtrlRead($action)

            // Check if panel already exists（FileExists 对目录同样为真）
            if (File.Exists(folderPath) || Directory.Exists(folderPath))
            {
                DialogResult answer = MessageBox.Show(this, "A panel with the selected name already exists. Overwrite?",
                    "PanelCreator", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.No) return;   // If $answer = 7 Then Return
            }

            // DirCopy("Templates", $folderpath, 1)
            if (!CopyDirectoryContents("Templates", folderPath))
            {
                MessageBox.Show(this, "The program was unable to create a panel with the selected name.",
                    "Unable to create panel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // FileCopy($oldimg, $folderpath & '\' & $foldername & '.png', 1 + 8)
            if (!string.IsNullOrEmpty(_oldImage))
                OmnimoDialogs.CopyFile(_oldImage, Path.Combine(folderPath, folderName + ".png"), true);

            string userVariables = Path.Combine(folderPath, "UserVariables.inc");
            IniDoc variables = IniDoc.LoadOrCreate(userVariables);
            variables.Set("Variables", "PanelName", title);
            variables.Set("Variables", "PanelAction", execute);
            variables.Set("Variables", "IconLocation", folderName + ".png");
            variables.Save();

            // 面板底色写进该目录下的每个 .ini
            string colorSkin = ColorText.HexToRGB(_panelArgb & 0x00FFFFFF);
            foreach (string name in ListIniEntries(folderPath))
            {
                IniDoc ini = IniDoc.LoadOrCreate(Path.Combine(folderPath, name));
                ini.Set("Variables", "ColorSkin", colorSkin);
                ini.Save();
            }
        }

        /// <summary>
        /// _FileListToArray($folderPath, "*.ini")：只列顶层。原版 $iFlag 用默认值 0，文件和目录都算进来，
        /// 而实际 Templates 里不会有名为 *.ini 的目录，这里只取文件；掩码按 AutoIt 的正则语义匹配，
        /// 避开 Win32 通配符会顺带命中 8.3 短名的问题。
        /// </summary>
        private static List<string> ListIniEntries(string folderPath)
        {
            var result = new List<string>();
            if (!Directory.Exists(folderPath)) return result;

            Regex mask = new Regex("^" + Regex.Escape("*.ini").Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase);
            foreach (string file in Directory.GetFiles(folderPath))
            {
                string name = Path.GetFileName(file);
                if (mask.IsMatch(name)) result.Add(name);
            }
            return result;
        }
    }

    /// <summary>
    /// 输入框：AutoIt 的 GUICtrlCreateInput(..., -1, $WS_EX_WINDOWEDGE) 里 -1 是默认输入框样式（无 WS_BORDER），
    /// WinForms 的 BorderStyle.None 对应前者，扩展样式只能在窗口创建时加，所以用 CreateParams 补。
    /// </summary>
    internal sealed class EdgeTextBox : TextBox
    {
        private const int WS_EX_WINDOWEDGE = 0x00000100;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_WINDOWEDGE;
                return parameters;
            }
        }
    }

    /// <summary>InputBox("Author", ...) 的等价物：取消或关闭窗口对应原版的 @error = 1。</summary>
    internal sealed class AuthorInputForm : ModernUi.ModernForm
    {
        private readonly ModernUi.ModernTextBox _input;

        public string Value
        {
            get { return _input.Value; }
        }

        public AuthorInputForm(Dictionary<string, string> language, string prompt, string defaultValue)
            : base(ModernUi.Theme.Detect(), LanguagePack.Text(language, "AuthorName"), resizable: false)
        {
            ShowInTaskbar = false;
            ClientSize = new Size(240, 150);
            StartPosition = FormStartPosition.CenterScreen;

            var label = new Label();
            label.AutoSize = false;
            label.SetBounds(12, 12, 216, 40);
            label.ForeColor = T.Fg;
            label.Font = ModernUi.UiFont(9f);
            label.Text = prompt;
            Controls.Add(label);

            _input = new ModernUi.ModernTextBox(T, defaultValue);
            _input.SetBounds(12, 58, 216, 30);
            Controls.Add(_input);

            var ok = new ModernUi.ModernButton(T, "OK");
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(62, 108, 80, 30);
            Controls.Add(ok);

            var cancel = new ModernUi.ModernButton(T, ModernUi.Lang.T(language, "Cancel"), ghost: true);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(150, 108, 80, 30);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
