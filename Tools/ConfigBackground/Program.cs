// ReOmnimo ConfigBackground（等价于 AutoIT\ConfigBackground.au3，343 行）
//
// 调用契约（不许改）：ConfigBackground.exe <VarFile> <SkinPath>
// 部署：REO\@Resources\Common\Background\ConfigBackground.exe
// 调用点：REO\Background\bg.ini:9 → ["#@#Common\Background\configbackground.exe" "#@#Config\Background\UserVariables.inc" #SKINSPATH#]
//
// 移植口径见 D:\WorkSpace\WorkBench\port_spec_4tools.md §4。
//
// 2026-10 现代化（用户批准）：界面套用 ModernUi（视觉权威=SettingsCenter：系统强调色、
// 深浅两主题、无边框自绘标题栏、圆角控件），文案走皮肤语言包（8 种语言，回退 English）。
// 行为逻辑（VarFile 读写、预览、ApplySettings、!Refresh）与 AutoIt 原版逐条等价，未动。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OmnimoTools.ConfigBackground
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // DPI 感知由 Tools\Shared\app.manifest（PerMonitorV2）声明，删清单比删这行更致命
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool forceLight, forceDark;
            args = ModernUi.ParseThemeFlags(args, out forceLight, out forceDark);
            if (args.Length < 2)
                OmnimoDialogs.Error("Omnimo Background Settings", "Too few command line arguments specified.");

            var theme = ModernUi.Theme.Detect(forceLight, forceDark);
            var lang = ModernUi.Lang.LoadFromExeDir();
            Application.Run(new ConfigBackgroundForm(args[0], args[1], theme, lang));
        }
    }

    internal sealed class ConfigBackgroundForm : ModernUi.ModernForm
    {
        private readonly string _varFile;
        private readonly double _scale;

        private readonly Dictionary<string, string> _lang;
        private string L(string key) { return ModernUi.Lang.T(_lang, key); }

        /// <summary>Global $ImageFile：未 Browse 时为空串（au3 只声明不赋值），勾了 Image 却没 Browse 会把空串写进 BackgroundImage。</summary>
        private string _imageFile = string.Empty;

        /// <summary>GDI+ 直接画在窗口上的图片预览（au3 的 $hImage）。</summary>
        private Image _preview;

        // 三个颜色值：AutoIt 变量无类型——初期是 RGBToHex 出来的字符串 '0xRRGGBB'，取色后变成整数
        private AutoItValue _color1;
        private AutoItValue _color2;
        private AutoItValue _color3;

        private ModernUi.ModernTrackBar _opacity, _width, _height;
        private ModernUi.ModernTextBox _borderWidth, _gradientAngle;
        private ModernUi.ModernTextBox _opacityInput, _widthInput, _heightInput;
        private ModernUi.ModernCheckBox _image, _leftRight, _topBottom, _aeroGlass, _gradient;
        private ModernUi.Swatch _swatch1, _swatch2, _swatch3;
        private ModernUi.ModernButton _browse;
        private ModernUi.ModernButton _cancel, _ok, _apply;
        private Timer _sync;

        internal ConfigBackgroundForm(string varFile, string skinPath, ModernUi.Theme t, Dictionary<string, string> lang)
            : base(t, ModernUi.Lang.T(lang, "BackgroundSettings"), resizable: false)
        {
            _varFile = varFile;
            _lang = lang;

            // Const $scaledpi = IniRead($skinpath & "REO\@Resources\...\UserVariables.inc", "Variables", "ScaleDpi", "1")
            _scale = 1;
            IniDoc userVars = IniDoc.TryLoad(Paths.Combine(skinPath, @"REO\@Resources\Common\Variables\UserVariables.inc"));
            if (userVars != null) _scale = AutoIt.ToNumber(userVars.Get("Variables", "ScaleDpi", "1"));

            // ---- 读 VarFile（原 au3 用 IniRead；文件读不到时每个键各自取默认值）----
            IniDoc vars = IniDoc.TryLoad(varFile);
            string color1Value = Read(vars, "Color1", "0,0,0");
            string color2Value = Read(vars, "Color2", "0,0,0");
            string color3Value = Read(vars, "ColorBorder", "0,0,0");
            string gradientAngleValue = Read(vars, "gradientangle", "0");
            double borderWidthValue = AutoIt.ToNumber(Read(vars, "borderwidth", "0"));
            double widthValue = AutoIt.ToNumber(Read(vars, "Width", Screen.PrimaryScreen.Bounds.Width.ToString(CultureInfo.InvariantCulture)));
            double heightValue = AutoIt.ToNumber(Read(vars, "Height", Screen.PrimaryScreen.Bounds.Height.ToString(CultureInfo.InvariantCulture)));
            string aeroGlassValue = Read(vars, "EnableAero", "0");
            string topBottomValue = Read(vars, "showbordertopbottom", "0");
            string leftRightValue = Read(vars, "showborderleftright", "0");
            string imageValue = Read(vars, "BackgroundImage", "");

            // $Color1Split[4]：Color1 是 "R,G,B[,A]" 四段时取不透明度，不足四段用 255
            string[] color1Parts = color1Value.Split(',');
            double opacityValue = color1Parts.Length < 4 ? 255 : AutoIt.Number(color1Parts[3]);

            // If $Color1Value Then $Color1Value = RGBToHex($Color1Value)：非空串才转换（AutoIt 字符串真值 = 非空）
            string color1Hex = ConvertToHex(color1Value);
            string color2Hex = ConvertToHex(color2Value);
            string color3Hex = ConvertToHex(color3Value);
            _color1 = AutoItValue.FromString(color1Hex);
            _color2 = AutoItValue.FromString(color2Hex);
            _color3 = AutoItValue.FromString(color3Hex);

            // ---- 窗口内容区（标题栏 32 由 ModernForm 提供）；坐标 ×ScaleDpi ----
            ClientSize = new Size(S(340), S(438));

            // Title 已由 ModernForm 的自绘标题栏承担，不再放内容区大标题（旧 au3 布局里的行头标题去掉）

            _ok = new ModernUi.ModernButton(T, "OK") { Bounds = new Rectangle(S(72), S(403), S(81), S(25)) };
            _cancel = new ModernUi.ModernButton(T, L("Cancel"), ghost: true) { Bounds = new Rectangle(S(160), S(403), S(81), S(25)) };
            _apply = new ModernUi.ModernButton(T, L("Apply"), ghost: true) { Bounds = new Rectangle(S(248), S(403), S(81), S(25)) };
            Controls.Add(_ok);
            Controls.Add(_cancel);
            Controls.Add(_apply);

            AddGroupLabel(L("Appearance"), 16, 40, 55);
            AddGroupLabel(L("Borders"), 16, 232, 234);
            AddGroupLabel(L("Advanced"), 16, 306, 302);

            // 行距统一 36（设计像素）：标签 top = 行 top + 4，文字中线正好落在滑杆中线
            AddLabel(L("Color"), 32, 72, 64, 17, heightScaled: false);   // au3 此处高度写死 17，未乘 dpi；宽度按现代字体放宽
            AddLabel(L("Opacity"), 32, 108, 55, 17);
            AddLabel(L("Width"), 32, 144, 55, 17);
            AddLabel(L("Height"), 32, 180, 55, 17);

            _swatch1 = AddSwatch(_color1, 100, 68, 30, 21);
            _swatch2 = AddSwatch(_color2, 129, 68, 30, 21);
            _swatch3 = AddSwatch(_color3, 200, 262, 30, 21);   // 右移让出「上下」标签的横向空间（原 168 会盖住标签尾字）

            _opacity = AddSlider(94, 104, 150, 25, 255, opacityValue);
            _width = AddSlider(94, 140, 150, 25, Screen.PrimaryScreen.Bounds.Width, widthValue);
            _height = AddSlider(94, 176, 150, 25, Screen.PrimaryScreen.Bounds.Height, heightValue);

            _image = AddCheckBox(170, 66, 17, 25);
            AddLabel(L("UseImage"), 188, 66, 65, 25);   // 与勾选框同 top/同高，文字与方框才垂直居中对齐
            _browse = new ModernUi.ModernButton(T, L("Browse"), ghost: true) { Bounds = new Rectangle(S(253), S(66), S(62), S(22)) };
            Controls.Add(_browse);

            AddLabel(L("Sides"), 32, 262, 50, 17);
            AddLabel(L("Width"), 234, 262, 34, 17);   // 26 宽在 150% 下会把「宽度」裁成「宽」
            AddLabel(L("GradientAngle"), 120, 370, 132, 17);  // "Gradient" 文本结束约 x=100；输入框从 255 起，标签放 120..252 不再被截断

            _leftRight = AddCheckBox(84, 262, 17, 17);
            AddLabel(L("LeftRight"), 102, 262, 34, 17);     // 原版两个勾选框没有文字，只能靠位置猜左右/上下
            _topBottom = AddCheckBox(140, 262, 17, 17);
            AddLabel(L("TopBottom"), 158, 262, 34, 17);
            _borderWidth = AddNumberInput("", 270, 260, 45, 21, center: false);   // au3:175-177 输入框 + GUICtrlCreateUpdown（现代化壳里退化为纯数字输入框）
            _borderWidth.Value = AutoIt.Num(borderWidthValue);
            if (Same(leftRightValue, "0")) _leftRight.Checked = true;     // 语义反转：存 0 表示显示左右边框 → 勾选
            if (Same(topBottomValue, "0")) _topBottom.Checked = true;

            _aeroGlass = AddCheckBox(32, 336, 17, 17);
            AddLabel(L("EnableAero"), 50, 336, 129, 17);

            _gradient = AddCheckBox(32, 370, 17, 17);
            AddLabel(L("Gradient"), 50, 370, 113, 17);
            _gradientAngle = AddNumberInput("", 255, 368, 60, 21, center: false); // au3:194-196 输入框 + GUICtrlCreateUpdown
            _gradientAngle.Value = AutoIt.Num(AutoIt.ToNumber(gradientAngleValue));
            if (Same(aeroGlassValue, "1")) _aeroGlass.Checked = true;
            if (AutoIt.StringNotEqual(color1Hex, color2Hex)) _gradient.Checked = true;   // 两个颜色串不相等 → 勾上渐变

            _opacityInput = AddNumberInput("0", 253, 104, 62, 21, center: true);
            _widthInput = AddNumberInput("0", 253, 140, 62, 21, center: true);
            _heightInput = AddNumberInput("0", 253, 176, 62, 21, center: true);

            // 宽/高最终会被夹到桌面尺寸（原版 GUICtrlSetData 的 limit 行为，见 AddSlider），
            // 把上限直接写在界面上，免得用户填了更大的数、点 Apply 后才发现值被悄悄改小。
            // 右对齐到数字输入框的右缘，读作「高度」这一行的补注，而不是另起一行。
            var maxNote = AddLabel(L("MaxSize") + " " + Screen.PrimaryScreen.Bounds.Width + " \u00d7 " + Screen.PrimaryScreen.Bounds.Height,
                      115, 204, 200, 16);   // 315 宽在 150% 下「× 1440」会被裁掉
            maxNote.TextAlign = ContentAlignment.MiddleRight;

            // au3:208-215 在原版里是围着两个边框勾选框的 1px 装饰括线（GUICtrlCreateGraphic），
            // 现代化深色界面里只剩几条游离细线、观感像残迹——有意不再复刻。

            // ---- 初始联动（au3:221-238）----
            if (AutoIt.IsTrue(imageValue))
            {
                _image.Checked = true;
                _swatch1.Enabled = false;
                if (_gradient.Checked) _swatch2.Enabled = false;
                _aeroGlass.Enabled = false;
                _gradient.Enabled = false;
                if (_gradient.Checked) _gradientAngle.Enabled = false;
            }
            else
            {
                _browse.Enabled = false;
            }

            if (!AutoIt.StringNotEqual(color1Hex, color2Hex))
            {
                _gradientAngle.Enabled = false;
                _swatch2.Enabled = false;
            }

            if (!_gradient.Checked) _swatch2.Value = _color1.ToColor();

            // ---- 事件 ----
            _cancel.Click += (s, e) => Close();                      // Case $GUI_EVENT_CLOSE, $Cancel → Exit（不保存）
            _apply.Click += (s, e) => ApplySettings();               // Case $Apply → 只写盘
            _ok.Click += (s, e) => { ApplySettings(); Close(); };    // Case $OK → 写盘后 Exit

            _swatch1.Click += (s, e) => ChooseColor(1);
            _swatch2.Click += (s, e) => ChooseColor(2);
            _swatch3.Click += (s, e) => ChooseColor(3);

            _image.CheckedChanged += (s, e) => OnImageToggled();
            _gradient.CheckedChanged += (s, e) => OnGradientToggled();
            _browse.Click += (s, e) => BrowseImage();

            // au3 主循环每 50ms 做一次滑杆⇄输入框绑定，然后再取消息
            _sync = new Timer { Interval = 50 };
            _sync.Tick += (s, e) => SyncSliders();
            _sync.Start();
        }

        // ---------- 语言与坐标 ----------

        private static string Read(IniDoc doc, string key, string defaultValue)
        {
            return doc == null ? defaultValue : doc.Get("Variables", key, defaultValue);
        }

        /// <summary>AutoIt 的 `$a = "字面量"`：两边都是字符串 → 不区分大小写比较。</summary>
        private static bool Same(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string ConvertToHex(string raw)
        {
            return AutoIt.IsTrue(raw) ? ColorText.RGBToHexString(raw) : raw;
        }

        /// <summary>
        /// GUI 坐标 ×ScaleDpi。AutoIt 把这些浮点数直接交给 Win32 的整数参数，换算即 C 的强制转换（截断），
        /// 故这里用 (int) 而不是 Math.Round。
        /// </summary>
        private int S(double value) { return (int)(value * _scale); }

        // ---------- 控件工厂 ----------

        private Label AddLabel(string text, int left, int top, int width, int height, bool heightScaled = true)
        {
            var label = new Label
            {
                Text = text,
                Left = S(left), Top = S(top), Width = S(width),
                Height = heightScaled ? S(height) : height,
                ForeColor = T.Sub,
                BackColor = Color.Transparent,
                Font = ModernUi.UiFont(8.5f)
            };
            Controls.Add(label);
            return label;
        }

        private Label AddGroupLabel(string text, int left, int top, int lineY)
        {
            var label = AddLabel(text, left, top, 200, 17);
            label.ForeColor = T.Fg;
            label.Font = ModernUi.UiFont(9.5f);
            int lineX = text.Length * 5 + 35;
            AddLine(lineX, lineY, 315 - lineX, 1);
            return label;
        }

        private void AddLine(int left, int top, int width, int height)
        {
            Controls.Add(new Panel
            {
                Left = S(left), Top = S(top), Width = Math.Max(1, S(width)), Height = Math.Max(1, S(height)),
                BackColor = T.Border
            });
        }

        private ModernUi.ModernTrackBar AddSlider(int left, int top, int width, int height, int maximum, double value)
        {
            // height 形参仅为对齐原 au3 坐标记录：ModernTrackBar 高度由控件自建（26 逻辑）
            int max = maximum < 0 ? 0 : maximum;
            int v = (int)AutoIt.ToNumber(AutoIt.Num(value));
            v = Math.Min(Math.Max(v, 0), max);                       // GUICtrlSetData 会被限制夹到 limit 内
            var slider = new ModernUi.ModernTrackBar(T, 0, max, v)
            {
                Left = S(left), Top = S(top), Width = S(width),
                SmallChange = 1,
                LargeChange = 5
            };
            Controls.Add(slider);
            return slider;
        }

        private ModernUi.ModernCheckBox AddCheckBox(int left, int top, int width, int height)
        {
            var box = new ModernUi.ModernCheckBox(T, "") { Left = S(left), Top = S(top), Width = S(width), Height = S(height) };
            Controls.Add(box);
            return box;
        }

        private ModernUi.ModernTextBox AddNumberInput(string text, int left, int top, int width, int height, bool center)
        {
            var box = new ModernUi.ModernTextBox(T, text)
            {
                Left = S(left), Top = S(top), Width = S(width), Height = S(height)
            };
            box.Inner.TextAlign = center ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            box.Inner.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            Controls.Add(box);
            return box;
        }

        private ModernUi.Swatch AddSwatch(AutoItValue value, int left, int top, int width, int height)
        {
            var swatch = new ModernUi.Swatch(T, Math.Min(width, height))
            {
                Left = S(left), Top = S(top), Width = S(width), Height = S(height)
            };
            swatch.Value = value.ToColor();
            Controls.Add(swatch);
            return swatch;
        }

        // ---------- 主循环里做的事 ----------

        /// <summary>
        /// au3 每 50ms：焦点在输入框 → 用输入框文本驱动滑杆；否则用滑杆值回写输入框。
        /// </summary>
        private void SyncSliders()
        {
            // 宽/高输入框超上限时立刻改写回上限值（slider 一直就是这么夹的），让用户当场看见数被改；
            // 不透明度同样有上限 255，但没有「桌面尺寸」那种意外感，保持原来的静默夹断。
            SyncPair(_widthInput, _width, clampInput: true);
            SyncPair(_heightInput, _height, clampInput: true);
            SyncPair(_opacityInput, _opacity, clampInput: false);
        }

        private static void SyncPair(ModernUi.ModernTextBox input, ModernUi.ModernTrackBar slider, bool clampInput)
        {
            if (input.Inner.Focused)
            {
                int v = (int)AutoIt.ToNumber(input.Value);
                int clamped = Math.Min(Math.Max(v, slider.Minimum), slider.Maximum);
                if (clampInput && clamped != v)
                {
                    input.Value = clamped.ToString(CultureInfo.InvariantCulture);
                    input.Inner.SelectionStart = input.Value.Length;
                }
                slider.Value = clamped;
            }
            else
            {
                input.Value = slider.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// au3 的 Case $Color1/$Color2/$Color3：取色框返回值（取消 = -1）照写进色块与变量，
        /// 且未勾渐变时把 Color2 的底色同步成 Color1 的值。
        /// </summary>
        private void ChooseColor(int index)
        {
            AutoItValue current = index == 1 ? _color1 : (index == 2 ? _color2 : _color3);
            int chosen = ColorPickerForm.Show(this, (int)current.Number, T, _lang);
            AutoItValue value = AutoItValue.FromNumber(chosen);

            if (index == 1)
            {
                _color1 = value;
                _swatch1.Value = value.ToColor();
                if (!_gradient.Checked) _swatch2.Value = value.ToColor();
            }
            else if (index == 2)
            {
                _color2 = value;
                _swatch2.Value = value.ToColor();
            }
            else
            {
                _color3 = value;
                _swatch3.Value = value.ToColor();
            }
        }

        /// <summary>Case $Image：勾上 → 禁用 Color1/（渐变勾选时 Color2）/AeroGlass/Gradient/（渐变勾选时 GradientAngle），启用 Browse；反之相反。</summary>
        private void OnImageToggled()
        {
            bool enable = !_image.Checked;
            _swatch1.Enabled = enable;
            if (_gradient.Checked) _swatch2.Enabled = enable;
            _aeroGlass.Enabled = enable;
            _gradient.Enabled = enable;
            if (_gradient.Checked) _gradientAngle.Enabled = enable;
            _browse.Enabled = !enable;
        }

        /// <summary>Case $Gradient：勾上 → 启用 GradientAngle 与 Color2 且 Color2 显示自身值；取消 → 禁用两者且 Color2 跟随 Color1。</summary>
        private void OnGradientToggled()
        {
            bool enable = _gradient.Checked;
            _gradientAngle.Enabled = enable;
            _swatch2.Enabled = enable;
            _swatch2.Value = (enable ? _color2 : _color1).ToColor();
        }

        /// <summary>
        /// Case $Browse：FileOpenDialog 取消时 au3 走 ContinueCase（什么都不做）；选到图后 GDI+ 直接把图画在窗口 (254,72,60,35)
        /// —— 这几个坐标**不乘** ScaleDpi（au3 原样传的常量）。
        /// </summary>
        private void BrowseImage()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Choose an image",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures"),
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                CheckFileExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _imageFile = dialog.FileName;
            }

            try
            {
                using (var loaded = Image.FromFile(_imageFile))
                {
                    Image old = _preview;
                    _preview = new Bitmap(loaded);
                    if (old != null) old.Dispose();
                }
            }
            catch
            {
                // 原版加载失败时 $hImage = 0，随后的 DrawImageRect 什么也不画
            }
            Invalidate(new Rectangle(254, 72, 60, 35));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_preview != null) e.Graphics.DrawImage(_preview, new Rectangle(254, 72, 60, 35));
        }

        /// <summary>
        /// ApplySettings()：Apply 按钮与 OK 按钮共用，逐键写回 VarFile 后 !Refresh REO\Background。
        /// 键序与取值来源严格照 au3:321-342（Width/Height 取滑杆读数，其余取输入框文本与勾选态）。
        /// </summary>
        private void ApplySettings()
        {
            IniDoc doc = IniDoc.LoadOrCreate(_varFile);
            doc.Set("Variables", "Width", AutoIt.Num(_width.Value));
            doc.Set("Variables", "Height", AutoIt.Num(_height.Value));
            doc.Set("Variables", "EnableAero", _aeroGlass.Checked ? "1" : "0");
            doc.Set("Variables", "UseWin10Blur", _aeroGlass.Checked ? "Blur" : "");
            doc.Set("Variables", "showborderleftright", _leftRight.Checked ? "0" : "1");
            doc.Set("Variables", "showbordertopbottom", _topBottom.Checked ? "0" : "1");
            doc.Set("Variables", "Color1", _color1.ToRgbText() + "," + AutoIt.Num(_opacity.Value));
            if (_gradient.Checked)
                doc.Set("Variables", "Color2", _color2.ToRgbText() + "," + AutoIt.Num(_opacity.Value));
            else
                doc.Set("Variables", "Color2", _color1.ToRgbText() + "," + AutoIt.Num(_opacity.Value));
            doc.Set("Variables", "gradientangle", _gradientAngle.Value);
            doc.Set("Variables", "ColorBorder", _color3.ToRgbText());
            doc.Set("Variables", "borderwidth", _borderWidth.Value);
            if (_image.Checked)
            {
                doc.Set("Variables", "BackgroundImage", _imageFile);
                doc.Set("Variables", "BackgroundImageHidden", "0");
            }
            else
            {
                doc.Set("Variables", "BackgroundImageHidden", "1");
            }
            doc.Save();

            Rainmeter.SendBang(@"!Refresh REO\Background");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_sync != null) _sync.Stop();
            if (_preview != null) { _preview.Dispose(); _preview = null; }
            base.OnFormClosed(e);
        }
    }
}
