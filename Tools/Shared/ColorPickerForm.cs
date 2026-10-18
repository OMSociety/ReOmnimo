// ReOmnimo 工具共享层（第三部分）：取色对话框
//
// 移植自 AutoIT\Includes\ColorChooser.au3（Yashied，Color Chooser Dialog UDF 1.1）。
// 调用点是 ColorChanger（_ColorChooserDialog(0x000000, $hGUI)）与 ConfigBackground
// （三处 _ColorChooserDialog($ColorXValue, $Gui)），两者都用默认参数：
//   iRefType=0、iReturnType=0、iFlags=-1 → CC_FLAG_DEFAULT = SOLIDCOLOR|CAPTURECOLOR = 0x03、
//   title='Color Picker'。因此 USERCOLOR 形态（用户色格子 + 注册表读写）在本仓库无触发路径，
//   不予实现；对话框按 SOLIDCOLOR|CAPTURECOLOR 定形（$H1 = 69、$H2 = 0）。
//
// 语义要点（逐条对齐源码，不是"大致像"）：
//   · 返回值：OK → 0xRRGGBB 整数；取消/关窗 → -1。
//   · OK 时把内置色板 $_Colors 写回 '..\colors.txt'（**相对当前工作目录**，与原版 File* 语义一致）。
//   · 菜单 Default 读 '..\defaultcolors.txt'（同样相对 CWD）。
//   · 色相条箭头 y 坐标用分母 369（源码原样 (359-H)/369*238，非 359）。
//   · 截图取色时把对话框自身色块区域涂黑，避免自反馈。
//   · HSL 走 shlwapi 的 ColorHLSToRGB/ColorRGBToHLS（0..240 量程），保证与 AutoIt 逐值一致。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OmnimoTools
{
    /// <summary>取色对话框（等价于 ColorChooser.au3 的 _ColorChooserDialog）。</summary>
    internal sealed class ColorPickerForm : Form
    {
        // ---- 常量（ColorChooser.au3:97-104）----
        private const int CC_FLAG_SOLIDCOLOR = 0x01;
        private const int CC_FLAG_CAPTURECOLOR = 0x02;
        private const int CC_FLAG_USERCOLOR = 0x40;
        private const int CC_FLAG_DEFAULT = CC_FLAG_SOLIDCOLOR | CC_FLAG_CAPTURECOLOR;

        private static readonly Color BgColor = Color.FromArgb(0xFF, 0xFF, 0xFF);
        private static readonly Color TextColor = Color.FromArgb(0x88, 0x88, 0x88);

        // ---- 状态 ----
        private readonly int _flags;
        private int _rgb;               // 0xRRGGBB 当前色
        private int _initialRgb;        // 双击 [6] 复位用
        private readonly int[] _hsl = new int[3];   // 0..240 量程
        private readonly int[] _hsb = new int[3];   // H 0..359, S/B 0..100
        private readonly int[] _colors = new int[17];  // 内置色板 [0]=count（不使用），[1..16] 用
        private bool _return = false;
        private readonly ModernUi.Theme _t;             // 深色主题（调用方传入，见 Show）
        private readonly System.Collections.Generic.Dictionary<string, string> _lang;   // 语言包（同上）

        // ---- 控件 ----
        private PictureBox _main;       // [1] 主色区 243×243
        private PictureBox _arrowL;     // [2] 左箭头条 8×220
        private PictureBox _hue;        // [4] 色相条 22×243
        private PictureBox _swatchA;    // [5] 当前色 41×24
        private PictureBox _swatchB;    // [6] 初始色 41×24
        private PictureBox _picker;     // [7] 吸管 19×19
        private PictureBox _gear;       // [29] 齿轮 19×19
        private Button _ok;
        private TextBox _txtR, _txtG, _txtB, _txtHex;
        private readonly PictureBox[] _palette = new PictureBox[17];

        private bool _updating;         // 对应 ccData[22]：屏蔽输入框回写引发的重入

        /// <summary>
        /// 弹出取色对话框；返回 0xRRGGBB，取消返回 -1。
        /// 等价于 _ColorChooserDialog($iColor, $hParent)。
        /// </summary>
        public static int Show(IWin32Window parent, int initialColor, ModernUi.Theme theme,
                               System.Collections.Generic.Dictionary<string, string> lang)
        {
            using (var form = new ColorPickerForm(initialColor, theme, lang))
            {
                form.StartPosition = FormStartPosition.CenterParent;
                DialogResult result = form.ShowDialog(parent);
                if (!form._return) return -1;
                form.WriteColors();
                return form._rgb;
            }
        }

        private ColorPickerForm(int initialColor, ModernUi.Theme theme,
                                System.Collections.Generic.Dictionary<string, string> lang)
        {
            // 与其余工具窗一致：不做 WinForms 自动缩放（本窗布局是 96-DPI 写死的设计坐标）
            AutoScaleMode = AutoScaleMode.None;
            _t = theme;                     // 深色主题由调用方传入，与主窗口一致
            _lang = lang;                   // 语言包同样由调用方传入：exe 与 Language\ 不同级，别在这里猜路径
            _flags = CC_FLAG_DEFAULT;
            int h1 = ((_flags & CC_FLAG_SOLIDCOLOR) == CC_FLAG_SOLIDCOLOR) ? 69 : 0;
            int h2 = ((_flags & CC_FLAG_USERCOLOR) == CC_FLAG_USERCOLOR) ? 56 : 0;

            // 窗口：WS_CAPTION|WS_POPUP|WS_SYSMENU + WS_EX_DLGMODALFRAME（固定边框、不可调大小）
            Text = "Color Picker";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(315, 422);
            BackColor = theme.Bg;
            Font = ModernUi.UiFont(9f);
            KeyPreview = true;
            DarkTitleBar();   // 无边框系统标题栏也要跟深色，否则白条压在深色窗体上

            LoadColors();

            // 主色区
            _main = new PictureBox { Left = 20, Top = 20, Width = 243, Height = 243, SizeMode = PictureBoxSizeMode.Normal, BackColor = theme.Bg };
            Controls.Add(_main);

            // 左侧箭头条（8×220）与色相条（22×243）
            _arrowL = new PictureBox { Left = 264, Top = 20, Width = 8, Height = 220, SizeMode = PictureBoxSizeMode.Normal, BackColor = theme.Bg };
            Controls.Add(_arrowL);
            _hue = new PictureBox { Left = 273, Top = 20, Width = 22, Height = 243, SizeMode = PictureBoxSizeMode.Normal, BackColor = theme.Bg };
            Controls.Add(_hue);

            // SOLIDCOLOR：两个对比色块
            _swatchA = new PictureBox { Left = 211, Top = 354, Width = 41, Height = 24, BorderStyle = BorderStyle.FixedSingle };
            _swatchB = new PictureBox { Left = 252, Top = 354, Width = 41, Height = 24, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(_swatchA);
            Controls.Add(_swatchB);

            // CAPTURECOLOR：吸管
            if ((_flags & CC_FLAG_CAPTURECOLOR) != 0)
            {
                _picker = new PictureBox { Left = 275, Top = 275, Width = 19, Height = 19, SizeMode = PictureBoxSizeMode.Normal, Cursor = Cursors.Cross, BackColor = theme.Bg };
                _picker.Image = GlyphEyedropper(_picker.Size);
                Controls.Add(_picker);
                _picker.MouseDown += PickerMouseDown;
            }

            // 分隔线 + 齿轮
            var sep = new Panel { Left = 19, Top = 342, Width = 273, Height = 1, BackColor = theme.Border };
            Controls.Add(sep);
            _gear = new PictureBox { Left = 275, Top = 307, Width = 19, Height = 19, SizeMode = PictureBoxSizeMode.Normal, BackColor = theme.Bg };
            _gear.Image = GlyphWheel(_gear.Size);
            Controls.Add(_gear);
            _gear.Click += (s, e) => ShowPaletteMenu();

            // R / G / B 输入：标签 18px 宽才放得下「R:」，输入框必须自己塞进 Controls，
            // 否则屏幕上只剩三个孤零零的字母（Box 从未加入窗体，永远不显示）。
            Controls.Add(MakeLabel("R:", 18, 363, 17));
            _txtR = MakeNumberBox(35, 360, 34);
            Controls.Add(_txtR);
            Controls.Add(MakeLabel("G:", 82, 363, 17));
            _txtG = MakeNumberBox(99, 360, 34);
            Controls.Add(_txtG);
            Controls.Add(MakeLabel("B:", 146, 363, 17));
            _txtB = MakeNumberBox(163, 360, 34);
            Controls.Add(_txtB);

            // '#' 十六进制输入（限 6 字符）
            Controls.Add(MakeLabel("#", 20, 389, 10));
            _txtHex = new TextBox
            {
                Left = 33, Top = 389, Width = 61, Height = 19, MaxLength = 6,
                BackColor = theme.Input, ForeColor = theme.Fg, BorderStyle = BorderStyle.FixedSingle
            };
            _txtHex.TextChanged += (s, e) => OnHexChanged();
            Controls.Add(_txtHex);

            // 内置色板 16 格：x = i*30+24 (i=0..7)，y = j*32+272 (j=0..1)
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int index = i * 2 + j + 1;   // 1..16
                    var box = new PictureBox { Left = i * 30 + 24, Top = j * 32 + 272, Width = 24, Height = 24, BackColor = theme.Card };
                    int captured = index;
                    box.Click += (s, e) => { _rgb = _colors[captured]; UpdateFromRgb(); };
                    _palette[index] = box;
                    Controls.Add(box);
                }
            }

            // OK 按钮（DEFBUTTON）：y = 316 + H1 + H2
            _ok = new Button
            {
                Left = 210, Top = 316 + h1 + h2, Width = 85, Height = 25, Text = "OK",
                Font = ModernUi.UiFont(9.5f),
                FlatStyle = FlatStyle.Flat, BackColor = theme.Accent, ForeColor = Color.White,
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false
            };
            _ok.FlatAppearance.BorderSize = 0;
            _ok.FlatAppearance.BorderColor = theme.Accent;
            _ok.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.Accent, 0.1f);
            _ok.Click += (s, e) => { _return = true; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(_ok);
            AcceptButton = _ok;

            // 主色区拖动：HSB 的 S/B 平面
            _main.MouseDown += MainMouseDown;
            // 色相条 / 左箭头条拖动：HSB 的 H
            _hue.MouseDown += HueMouseDown;
            _arrowL.MouseDown += HueMouseDown;
            // 双击对比色块：左键 [6] 复位到初始色
            _swatchB.DoubleClick += (s, e) => { _rgb = _initialRgb; UpdateFromRgb(); };

            // 初始化
            _initialRgb = initialColor & 0xFFFFFF;
            _rgb = _initialRgb;
            _hsl[0] = 160;
            UpdateFromRgb();
            SetPaletteImages();
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar();
        }

        private void DarkTitleBar()
        {
            if (Handle == IntPtr.Zero) return;
            int on = 1;
            try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)); } catch { }
        }

        private Label MakeLabel(string text, int x, int y, int w)
        {
            return new Label { Text = text, Left = x, Top = y, Width = w, Height = 14, ForeColor = _t.Sub, BackColor = Color.Transparent };
        }

        private TextBox MakeNumberBox(int x, int y, int w)
        {
            var box = new TextBox
            {
                Left = x, Top = y, Width = w, Height = 19, MaxLength = 3,
                BackColor = _t.Input, ForeColor = _t.Fg, BorderStyle = BorderStyle.FixedSingle
            };
            box.TextChanged += (s, e) => OnNumberChanged((TextBox)s);
            box.Leave += (s, e) => WriteBack((TextBox)s);
            return box;
        }

        // ---------- 色板文件（相对 CWD，与 AutoIt File* 一致）----------

        private void LoadColors()
        {
            for (int i = 0; i < _colors.Length; i++) _colors[i] = -1;
            // 等价于 _FileReadToArray('..\colors.txt', $_Colors)；读失败时数组保持空（源码行为）
            int[] loaded;
            if (PaletteFile.TryRead(@"..\colors.txt", 16, out loaded))
                Array.Copy(loaded, 0, _colors, 0, Math.Min(loaded.Length, _colors.Length));
        }

        private void WriteColors()
        {
            PaletteFile.Write(@"..\colors.txt", _colors);
        }

        private void SetPaletteImages()
        {
            for (int i = 1; i <= 16; i++)
            {
                if (_palette[i] == null) continue;
                _palette[i].BackColor = _colors[i] < 0 ? _t.Card : Color.FromArgb(_colors[i]);
            }
        }

        // ---------- 配色生成（ColorGenerator.au3）----------

        private void GenerateColorful() { ColorGenerator.GenerateColorful(_colors); }

        private void GenerateContigious() { ColorGenerator.GenerateContigious(_colors); }

        private void GenerateContrasting() { ColorGenerator.GenerateContrasting(_colors); }

        private void LoadDefaultColors()
        {
            int[] loaded;
            if (PaletteFile.TryRead(@"..\defaultcolors.txt", 16, out loaded))
            {
                for (int i = 0; i < loaded.Length && i < _colors.Length; i++)
                    if (loaded[i] != 0) _colors[i] = loaded[i];
            }
        }

        /// <summary>
        /// 等价于 ColorGenerator.au3 的 ApplyColors($_Colors)：给所有活动且可着色面板随机上一色。
        /// 与 ColorChanger 的 ReadActiveSkins 不同，这里的 Colorizable 检查**带** SkinPath 前缀，
        /// 且 Active 用的是 `== "0"`（区分大小写的字符串比较）。
        /// </summary>
        private void ApplyColorsToAll()
        {
            try
            {
                string settings = Paths.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Rainmeter\Rainmeter.ini");
                IniDoc live = IniDoc.TryLoad(settings);
                if (live == null) return;

                string skinPath = live.Get("Rainmeter", "SkinPath",
                    Paths.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Documents\Rainmeter\Skins\"));

                int count = _colors[0];
                if (count < 2) return;

                foreach (string section in live.SectionNames())
                {
                    if (section.IndexOf("Panels", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (string.Equals(live.Get(section, "Active", "0"), "0", StringComparison.Ordinal)) continue;
                    // StringTrimLeft(段名, 4) 去掉 "REO\" 前缀
                    string config = Paths.Combine(skinPath,
                        @"REO\@Resources\Config\" + (section.Length > 4 ? section.Substring(4) : section) + @"\RainConfigure.cfg");
                    IniDoc cfg = IniDoc.TryLoad(config);
                    if (cfg != null && cfg.Get("Options", "Colorizable", "1") == "0") continue;

                    string[] inis;
                    try { inis = Directory.GetFiles(Paths.Combine(skinPath, section), "*.ini"); }
                    catch { continue; }

                    int active = (int)AutoIt.ToNumber(live.Get(section, "Active", "0"));
                    if (inis.Length == 0 || active > inis.Length) continue;

                    int pick = _colors[SharedRandom.Next(1, count)];
                    IniDoc target = IniDoc.LoadOrCreate(inis[active - 1]);
                    target.Set("Variables", "ColorSkin", ColorText.HexToRGB(pick));
                    target.Save();
                }
                Rainmeter.SendBang("!Refresh *");
            }
            catch
            {
                // 原版此处无错误处理
            }
        }

        private void ShowPaletteMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Renderer = new ModernUi.MenuTheme(_t);   // 深色渲染：底色/边框/选中态都跟主题走
            menu.BackColor = _t.Card;
            menu.ForeColor = _t.Fg;
            menu.ShowImageMargin = false;
            menu.Items.Add(MenuItem(L("PaletteDefault"), (s, e) => { LoadDefaultColors(); SetPaletteImages(); }));
            menu.Items.Add(MenuItem(L("PaletteColorful"), (s, e) => { GenerateColorful(); SetPaletteImages(); }));
            menu.Items.Add(MenuItem(L("PaletteContiguous"), (s, e) => { GenerateContigious(); SetPaletteImages(); }));
            menu.Items.Add(MenuItem(L("PaletteContrasting"), (s, e) => { GenerateContrasting(); SetPaletteImages(); }));
            menu.Items.Add(new ToolStripSeparator { BackColor = _t.Card, Margin = new Padding(6, 4, 6, 4) });
            menu.Items.Add(MenuItem(L("PaletteApplyAll"), (s, e) => ApplyColorsToAll()));
            // 齿轮贴在右下角，菜单往下弹会压住 OK 一行；改向左上方弹，并夹进窗体里
            var size = menu.GetPreferredSize(Size.Empty);
            int x = _gear.Left - size.Width + _gear.Width;
            int y = _gear.Top - size.Height - 2;
            x = Math.Max(4, Math.Min(x, ClientSize.Width - size.Width - 4));
            y = Math.Max(4, y);
            menu.Show(this, new Point(x, y));
        }

        /// <summary>菜单项统一走这里：专业渲染器按 item.ForeColor 画文字，不设就是系统菜单字色。</summary>
        private ToolStripMenuItem MenuItem(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.ForeColor = _t.Fg;
            item.BackColor = _t.Card;
            item.Padding = new Padding(10, 5, 18, 5);   // 左侧留出文字起笔、右侧留出点击余量，否则字贴着边框
            item.Click += onClick;
            return item;
        }

        private string L(string key)
        {
            return ModernUi.Lang.T(_lang, key);
        }

        // ---------- 交互 ----------

        private void MainMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            DragSb(e.X, e.Y);
        }

        private void DragSb(int startX, int startY)
        {
            while (Control.MouseButtons == MouseButtons.Left)
            {
                Point p = _main.PointToClient(Cursor.Position);
                int x = Clamp(p.X, 0, 240);
                int y = Clamp(p.Y, 0, 240);
                _hsb[1] = Clamp((int)Math.Round(x / 240.0 * 100), 0, 100);
                _hsb[2] = 100 - Clamp((int)Math.Round(y / 240.0 * 100), 0, 100);
                UpdateFromHsb(updateInputs: false);
                Application.DoEvents();
            }
        }

        private void HueMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            while (Control.MouseButtons == MouseButtons.Left)
            {
                Point p = _hue.PointToClient(Cursor.Position);
                _hsb[0] = Clamp((int)Math.Round((240 - p.Y + 4) / 240.0 * 359), 0, 359);
                UpdateFromHsb(updateInputs: false);
                Application.DoEvents();
            }
        }

        private void PickerMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            while (Control.MouseButtons == MouseButtons.Left)
            {
                Point screen = Cursor.Position;
                using (Bitmap shot = CaptureAround(screen))
                {
                    if (shot != null)
                    {
                        _picker.Image = DrawCrosshair(shot, screen);
                        // 等价于 __GDIPlus_BitmapGetPixel($hImage, w/2, h/2)
                        int centerX = shot.Width / 2, centerY = shot.Height / 2;
                        if (centerX < shot.Width && centerY < shot.Height)
                            _rgb = shot.GetPixel(centerX, centerY).ToArgb() & 0xFFFFFF;
                    }
                }
                UpdateFromRgb();
                Application.DoEvents();
            }
            _picker.Image = GlyphEyedropper(_picker.Size);
        }

        // ---------- 颜色换算 ----------

        private void OnNumberChanged(TextBox box)
        {
            if (_updating) return;
            string digits = "";
            foreach (char c in box.Text) if (char.IsDigit(c)) digits += c;
            if (digits != box.Text)
            {
                box.Text = digits;
                MessageBeep(0);
                return;
            }
            int v = digits.Length == 0 ? 0 : int.Parse(digits);
            if (box == _txtR) { _rgb = (_rgb & 0x00FFFF) | (Clamp(v, 0, 255) << 16); }
            else if (box == _txtG) { _rgb = (_rgb & 0xFF00FF) | (Clamp(v, 0, 255) << 8); }
            else if (box == _txtB) { _rgb = (_rgb & 0xFFFF00) | Clamp(v, 0, 255); }
            else return;
            UpdateFromRgb(skip: box);
        }

        private void OnHexChanged()
        {
            if (_updating) return;
            string hex = "";
            foreach (char c in _txtHex.Text) if (Uri.IsHexDigit(c)) hex += c;
            if (hex != _txtHex.Text)
            {
                _txtHex.Text = hex;
                MessageBeep(0);
                return;
            }
            if (hex.Length == 0) return;
            int v;
            if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out v)) return;
            _rgb = v & 0xFFFFFF;
            UpdateFromRgb(skip: _txtHex);
        }

        private void WriteBack(TextBox box)
        {
            // EN_KILLFOCUS：回写规范值
            _updating = true;
            if (box == _txtR) box.Text = ((_rgb >> 16) & 0xFF).ToString();
            else if (box == _txtG) box.Text = ((_rgb >> 8) & 0xFF).ToString();
            else if (box == _txtB) box.Text = (_rgb & 0xFF).ToString();
            else if (box == _txtHex) box.Text = (_rgb & 0xFFFFFF).ToString("X6");
            _updating = false;
        }

        /// <summary>由当前 _rgb 推 HSL/HSB 并全量刷新（CC_Update(1)）。</summary>
        private void UpdateFromRgb(TextBox skip = null)
        {
            int[] rgb = { (_rgb >> 16) & 0xFF, (_rgb >> 8) & 0xFF, _rgb & 0xFF };
            int[] hsl = RgbToHsl(rgb);
            int[] hsb = RgbToHsb(rgb);
            Array.Copy(hsl, _hsl, 3);
            Array.Copy(hsb, _hsb, 3);
            RefreshUi(skip);
        }

        /// <summary>由当前 _hsb 推 RGB_HSL 并全量刷新（CC_Update(3)）。</summary>
        private void UpdateFromHsb(bool updateInputs)
        {
            int[] rgb = HsbToRgb(_hsb);
            _rgb = (rgb[0] << 16) | (rgb[1] << 8) | rgb[2];
            Array.Copy(RgbToHsl(rgb), _hsl, 3);
            RefreshUi(updateInputs ? null : _txtHex);
        }

        private void RefreshUi(TextBox skip)
        {
            _updating = true;
            try
            {
                _main.Image = RenderMainSwatch();
                _hue.Image = RenderHueBar();
                _arrowL.Image = RenderArrow();
                _swatchA.BackColor = Color.FromArgb(_rgb);
                _swatchB.BackColor = Color.FromArgb(_initialRgb);
                if (skip != _txtR) _txtR.Text = ((_rgb >> 16) & 0xFF).ToString();
                if (skip != _txtG) _txtG.Text = ((_rgb >> 8) & 0xFF).ToString();
                if (skip != _txtB) _txtB.Text = (_rgb & 0xFF).ToString();
                if (skip != _txtHex) _txtHex.Text = (_rgb & 0xFFFFFF).ToString("X6");
            }
            finally
            {
                _updating = false;
            }
        }

        /// <summary>
        /// 主色区 243×241：横向白→纯色的线性渐变，叠加纵向透明→黑渐变（等价于 CC_Update 里
        /// 两次 __GDIPlus_LineBrushCreate + FillRect），再画十字光标。
        /// </summary>
        private Bitmap RenderMainSwatch()
        {
            int[] hs = { _hsb[0], 100, 100 };
            int[] pure = HsbToRgb(hs);
            var bmp = new Bitmap(243, 241);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                var rect = new Rectangle(0, 0, 243, 241);
                using (var brush = new LinearGradientBrush(rect, Color.White, Color.FromArgb(pure[0], pure[1], pure[2]), 0f))
                    g.FillRectangle(brush, rect);
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(0, 0, 0, 0), Color.Black, 90f))
                    g.FillRectangle(brush, rect);

                int x = (int)Math.Round(_hsb[1] / 100.0 * 241);
                int y = (int)Math.Round((1 - _hsb[2] / 100.0) * 241);
                Color penColor = IsDark(_rgb) ? Color.White : Color.Black;
                using (var pen = new Pen(penColor, 2))
                {
                    g.DrawLine(pen, x - 7, y, x - 3, y);
                    g.DrawLine(pen, x + 3, y, x + 7, y);
                    g.DrawLine(pen, x, y - 7, x, y - 3);
                    g.DrawLine(pen, x, y + 3, x, y + 7);
                }
            }
            return bmp;
        }

        /// <summary>色相条 20×241：每行 HSB[0]=(240-i)*359/240、S=B=100 的纯色（CC_SetPalette）。</summary>
        private static Bitmap RenderHueBar()
        {
            var bmp = new Bitmap(20, 241);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                for (int i = 0; i <= 240; i++)
                {
                    int h = (int)((240 - i) * 359 / 240.0);
                    int[] rgb = HsbToRgb(new[] { h, 100, 100 });
                    using (var pen = new Pen(Color.FromArgb(rgb[0], rgb[1], rgb[2]), 1))
                        g.DrawLine(pen, 0, i, 19, i);
                }
            }
            return bmp;
        }

        /// <summary>
        /// 左箭头条 8×241：系统 3DFACE 底色 + 箭头画在 y = Round((359-H)/369*238)。
        /// 注意分母 369 是源码原样（ColorChooser.au3:993）。
        /// </summary>
        /// <summary>
        /// 吸管图标：按控件尺寸现画（Fluent 线性风格、圆头描边），不再贴 2013 年那张 19×19 位图，
        /// 这样 100%/125%/150% 下都是清晰的。
        /// </summary>
        private Bitmap GlyphEyedropper(Size size)
        {
            int w = Math.Max(10, size.Width), h = Math.Max(10, size.Height);
            var bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(_t.Bg);
                float u = w / 19f;
                var state = g.Save();
                g.TranslateTransform(w / 2f, h / 2f);
                g.RotateTransform(45f);   // 管身斜置，管尖指向左下
                using (var pen = new Pen(_t.Sub, 1.4f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var brush = new SolidBrush(_t.Sub))
                {
                    g.DrawEllipse(pen, -2.3f * u, -8.4f * u, 4.6f * u, 4.6f * u);       // 顶部捏囊
                    g.DrawLines(pen, new[]                                               // 管身
                    {
                        new PointF(-1.5f * u, -4.2f * u),
                        new PointF(-1.5f * u, 2.2f * u),
                        new PointF(1.5f * u, 2.2f * u),
                        new PointF(1.5f * u, -4.2f * u),
                    });
                    g.FillPolygon(brush, new[]                                           // 管尖
                    {
                        new PointF(-1.5f * u, 2.6f * u),
                        new PointF(1.5f * u, 2.6f * u),
                        new PointF(0f, 6.4f * u),
                    });
                }
                g.Restore(state);
            }
            return bmp;
        }

        /// <summary>取色盘图标：六段色相环 + 中心孔 + 描边，同样按尺寸现画。</summary>
        private Bitmap GlyphWheel(Size size)
        {
            int w = Math.Max(10, size.Width), h = Math.Max(10, size.Height);
            var bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(_t.Bg);
                float u = w / 19f;
                float x = 1.7f * u, y = 1.7f * u, d = 15.6f * u;
                for (int i = 0; i < 6; i++)
                {
                    int[] rgb = HsbToRgb(new[] { i * 60, 100, 100 });
                    using (var b = new SolidBrush(Color.FromArgb(rgb[0], rgb[1], rgb[2])))
                        g.FillPie(b, x, y, d, d, -15 + i * 60, 60);
                }
                using (var hole = new SolidBrush(_t.Bg))
                    g.FillEllipse(hole, x + d / 2 - 2.4f * u, y + d / 2 - 2.4f * u, 4.8f * u, 4.8f * u);
                using (var pen = new Pen(_t.Sub, 1f * u))
                    g.DrawEllipse(pen, x, y, d, d);
            }
            return bmp;
        }

        private Bitmap RenderArrow()
        {
            var bmp = new Bitmap(8, 241);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(_t.Bg);   // 深色底，否则箭头条是白底压在深色窗体上
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int y = (int)Math.Round((359 - _hsb[0]) / 369.0 * 238);
                using (var pen = new Pen(_t.Fg, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLines(pen, new[]
                    {
                        new PointF(1.5f, y + 1f),
                        new PointF(6f, y + 4.5f),
                        new PointF(1.5f, y + 8f),
                    });
                }
            }
            return bmp;
        }

        /// <summary>截取以屏幕点为中心的 41×19 区域（等价于 CC_Capture(...,41,19,41,24) 的取色部分）。</summary>
        private Bitmap CaptureAround(Point screen)
        {
            const int srcW = 41, srcH = 19;
            var bmp = new Bitmap(srcW, srcH);
            using (Graphics g = Graphics.FromImage(bmp))
                g.CopyFromScreen(screen.X - 20, screen.Y - 9, 0, 0, new Size(srcW, srcH), CopyPixelOperation.SourceCopy);
            return bmp;
        }

        /// <summary>放大到 41×24 并画准星（等价于 CC_Capture 里的十字线绘制）。</summary>
        private static Bitmap DrawCrosshair(Bitmap src, Point screen)
        {
            const int dstW = 41, dstH = 24;
            var bmp = new Bitmap(dstW, dstH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(src, new Rectangle(0, 0, dstW, dstH));
                using (var pen = new Pen(Color.FromArgb(0x60, 0xFF, 0x00, 0x00), 1))
                {
                    int cy = dstH / 2, cx = dstW / 2;
                    g.DrawLine(pen, 0, cy, cx - 4, cy);
                    g.DrawLine(pen, cx + 5, cy, dstW, cy);
                    g.DrawLine(pen, cx, 0, cx, cy - 4);
                    g.DrawLine(pen, cx, cy + 5, cx, dstH);
                }
            }
            return bmp;
        }

        // ---------- 换算函数（ColorChooser.au3:1124-1245）----------

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int ColorHLSToRGB(int h, int l, int s);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern void ColorRGBToHLS(int rgb, out int h, out int l, out int s);

        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>等价于 CC_SwitchColor：R↔B 交换。</summary>
        private static int SwitchColor(int c)
        {
            return ((c & 0x00FF00) | ((c & 0x0000FF) << 16) | ((c & 0xFF0000) >> 16)) & 0xFFFFFF;
        }

        private static int[] HslToRgb(int[] hsl)
        {
            if (hsl[1] == 0) hsl[0] = 160;
            int ret = ColorHLSToRGB(hsl[0], hsl[2], hsl[1]);
            int sw = SwitchColor(ret & 0xFFFFFF);
            return new[] { (sw >> 16) & 0xFF, (sw >> 8) & 0xFF, sw & 0xFF };
        }

        private static int[] RgbToHsl(int[] rgb)
        {
            int packed = (rgb[0] << 16) | (rgb[1] << 8) | rgb[2];
            int h, l, s;
            ColorRGBToHLS(SwitchColor(packed), out h, out l, out s);
            return new[] { h, s, l };   // 源码按 $Ret[2]=H, $Ret[4]=S, $Ret[3]=L 组装
        }

        private static int[] HsbToRgb(int[] hsb)
        {
            double s = hsb[1] / 100.0, v = hsb[2] / 100.0;
            double[] rgb = new double[3];
            if (s == 0)
            {
                for (int i = 0; i < 3; i++) rgb[i] = v;
            }
            else
            {
                double h = hsb[0] / 60.0;
                int l = (int)Math.Floor(h);
                double f = h - l;
                double p = v * (1 - s);
                double q = v * (1 - s * f);
                double t = v * (1 - s * (1 - f));
                switch (l)
                {
                    case 1: rgb[0] = q; rgb[1] = v; rgb[2] = p; break;
                    case 2: rgb[0] = p; rgb[1] = v; rgb[2] = t; break;
                    case 3: rgb[0] = p; rgb[1] = q; rgb[2] = v; break;
                    case 4: rgb[0] = t; rgb[1] = p; rgb[2] = v; break;
                    case 5: rgb[0] = v; rgb[1] = p; rgb[2] = q; break;
                    default: rgb[0] = v; rgb[1] = t; rgb[2] = p; break;
                }
            }
            return new[]
            {
                (int)Math.Round(rgb[0] * 255),
                (int)Math.Round(rgb[1] * 255),
                (int)Math.Round(rgb[2] * 255)
            };
        }

        private static int[] RgbToHsb(int[] rgb)
        {
            int min = 255, max = 0;
            for (int i = 0; i < 3; i++)
            {
                if (rgb[i] > max) max = rgb[i];
                if (rgb[i] < min) min = rgb[i];
            }
            double h, s, v;
            if (min == max)
            {
                h = 0; s = 0; v = max / 255.0;
            }
            else
            {
                int d;
                int hh;
                if (rgb[0] == min) { d = rgb[1] - rgb[2]; hh = 3; }
                else if (rgb[1] == min) { d = rgb[2] - rgb[0]; hh = 5; }
                else { d = rgb[0] - rgb[1]; hh = 1; }
                h = (hh - (d / (double)(max - min))) / 6;
                s = (max - min) / (double)max;
                v = max / 255.0;
            }
            int hi = (int)Math.Round(h * 360);
            if (hi == 360) hi = 0;
            return new[] { hi, (int)Math.Round(s * 100), (int)Math.Round(v * 100) };
        }

        /// <summary>等价于 CC_IsDark：R+G+B &lt; 3*255/2。</summary>
        private static bool IsDark(int rgb)
        {
            int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            return r + g + b < 3 * 255 / 2;
        }
    }
}
