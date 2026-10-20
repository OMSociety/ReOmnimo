// ReOmnimo ColorChanger（原 AutoIt 版的等价实现；AutoIT\ 源树已于 0.7.0 移除）
//
// 调用契约（不许改）：ColorChanger.exe <SettingsPath> [--light|--dark]
// 部署：REO\@Resources\Common\ColorChanger.exe
// 调用点：REO\Gallery\Color\coloradvanced.ini
//
// 移植口径见 D:\WorkSpace\WorkBench\port_spec_4tools.md §1。
// 注意本工具是 4 个工具里唯一用 @ScriptDir（=exe 所在目录）读 colors.txt 的。
//
// 2026-10 现代化（用户批准）：界面套用 ModernUi（视觉权威=SettingsCenter：系统强调色、
// 深浅两主题、无边框自绘标题栏、圆角控件），文案走皮肤语言包（8 种语言，回退 English）。
// 行为逻辑（色板读写、面板枚举、ApplyColor、!Refresh）与 AutoIt 原版逐条等价，未动。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace OmnimoTools.ColorChanger
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 参数取自原始命令行（CmdLine），不用 Main 的 args：
            // Rainmeter 传进来的路径以反斜杠结尾，CRT 会把它与后一个参数的引号
            // 解释成转义引号并合并两个参数（AutoIt 的 $CmdLine 无此行为）。
            args = CmdLine.Get();

            // DPI 感知由 Tools\Shared\app.manifest（PerMonitorV2）声明，删清单比删这行更致命
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool forceLight, forceDark;
            args = ModernUi.ParseThemeFlags(args, out forceLight, out forceDark);
            if (args.Length < 1)
                OmnimoDialogs.Error("Individual Panel Color", "Too few command line arguments specified.");

            var theme = ModernUi.Theme.Detect(forceLight, forceDark);
            Application.Run(new ColorChangerForm(args[0], theme));
        }
    }

    internal sealed class ColorChangerForm : ModernUi.ModernForm
    {
        private readonly string _settingsPath;
        private readonly string _skinPath;
        private readonly string _opacity;

        private readonly Dictionary<string, string> _lang;
        private string L(string key) { return ModernUi.Lang.T(_lang, key); }

        private readonly int[] _colors = new int[19];   // [0]=行数，[1..18]=0xRRGGBB
        private readonly string[] _panels = new string[100];
        private int _numPanels;

        private readonly ModernUi.Swatch[] _swatches = new ModernUi.Swatch[18];
        private int _prev;
        private bool _actionsHidden = true;

        private ModernUi.ListBoxM _panelList;
        private Label _emptyHint;
        private ModernUi.ModernButton _random, _reset, _newPalette;
        private ModernUi.ModernButton _selectAll, _refresh;
        private RainbowSwatch _custom;

        internal ColorChangerForm(string settingsPath, ModernUi.Theme t)
            : base(t, "", resizable: false)
        {
            _settingsPath = settingsPath;

            IniDoc settings = IniDoc.TryLoad(Paths.Combine(settingsPath, "Rainmeter.ini"));
            _skinPath = settings != null
                ? settings.Get("Rainmeter", "SkinPath",
                    Paths.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Documents\Rainmeter\Skins\"))
                : Paths.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Documents\Rainmeter\Skins\");

            _lang = ModernUi.Lang.Load(_skinPath);
            TitleText = L("ChangePanelColor");

            _opacity = ReadOrDefault(Paths.Combine(_skinPath, @"REO\@Resources\Common\Color\color.inc"), "Opacity", "255");

            // ---- 窗口内容区（标题栏 32 由 ModernForm 提供）；设计坐标 96dpi 逻辑像素
            //（DPI 感知后由 ModernForm.OnHandleCreated 的手动 Scale 放大；AutoScaleMode=None，不用 AutoScaleMode.Dpi）----
            ClientSize = new Size(464, 248);   // 标题栏 32 + 内容 192 + 边距

            // 左卡：面板列表（左右边距 22，与右侧色板对齐；内容整体在标题栏之下）
            var card = new ModernUi.CardPanel(t) { Bounds = new Rectangle(22, 42, 172, 192) };
            Controls.Add(card);

            _panelList = new ModernUi.ListBoxM(t) { Bounds = new Rectangle(8, 6, 156, 146) };
            _panelList.Click += (s, e) => ShowElements();
            // 单选某个面板时把它的现有 ColorSkin 标在色板上（重开窗口也能看出起点）
            _panelList.SelectedIndexChanged += (s, e) => SyncCurrentColor();
            card.Controls.Add(_panelList);

            _emptyHint = new Label
            {
                Bounds = new Rectangle(8, 6, 156, 146),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = t.Sub,
                Font = ModernUi.UiFont(8.5f),
                Text = L("NoActivePanels"),
                Visible = false
            };
            card.Controls.Add(_emptyHint);
            card.Controls.SetChildIndex(_emptyHint, 0);   // WinForms 后 Add 的控件在 z-order 底部，不提到最前会被列表盖住

            _selectAll = new ModernUi.ModernButton(t, L("SelectAll"), ghost: true) { Bounds = new Rectangle(8, 156, 76, 26) };
            _selectAll.Click += (s, e) =>
            {
                for (int i = 0; i < _panelList.Items.Count; i++) _panelList.SetSelected(i, true);
                ShowElements();
            };
            card.Controls.Add(_selectAll);

            _refresh = new ModernUi.ModernButton(t, L("Refresh"), ghost: true) { Bounds = new Rectangle(88, 156, 76, 26) };
            _refresh.Click += (s, e) => ReadActiveSkins();
            card.Controls.Add(_refresh);

            // 右侧：色板 6×3（17 色 + 1 自定义）
            CreateColorButtons();

            // 底部动作行（选中面板后才出现，与原版一致）
            _random = new ModernUi.ModernButton(t, L("Random")) { Bounds = new Rectangle(206, 196, 72, 30), Visible = false };
            _random.Click += (s, e) => ForEachSelected(p => ApplyColor(p, ColorText.HexToRGB(RandomColor()) + "," + _opacity));
            Controls.Add(_random);

            _newPalette = new ModernUi.ModernButton(t, L("NewPalette"), ghost: true) { Bounds = new Rectangle(286, 196, 72, 30), Visible = false };
            Controls.Add(_newPalette);

            _reset = new ModernUi.ModernButton(t, L("Reset"), ghost: true) { Bounds = new Rectangle(366, 196, 72, 30), Visible = false };
            _reset.Click += (s, e) => ForEachSelected(p => ApplyColor(p, null));
            Controls.Add(_reset);

            _custom = new RainbowSwatch(t) { Bounds = new Rectangle(406, 122, 36, 36) }; // 第 18 格（i=5,j=2），与 au3 一致常驻显示
            _custom.Click += (s, e) => ChooseCustomColor();
            Controls.Add(_custom);

            // ---- colors.txt：读自 exe 所在目录，读失败直接报错退出 ----
            LoadPalette();
            SetColorButtons();
            BuildPaletteMenu();
            ReadActiveSkins();
        }

        // ---------- 状态 ----------

        private string ReadOrDefault(string file, string key, string defaultValue)
        {
            IniDoc doc = IniDoc.TryLoad(file);
            return doc == null ? defaultValue : doc.Get("Variables", key, defaultValue);
        }

        private void LoadPalette()
        {
            Array.Clear(_colors, 0, _colors.Length);
            if (!PaletteFile.TryRead(Paths.Combine(Paths.ScriptDirectory(), "colors.txt"), 18, out int[] loaded))
                OmnimoDialogs.Error("Individual Panel Color", "Unable to read colors from colors.txt.");
            Array.Copy(loaded, _colors, Math.Min(loaded.Length, _colors.Length));
        }

        private int RandomColor()
        {
            return _colors[SharedRandom.Next(1, 18)];
        }

        // ---------- 色块 ----------

        private void CreateColorButtons()
        {
            for (int i = 0; i <= 5; i++)
            {
                for (int j = 0; j <= 2; j++)
                {
                    int k = i * 3 + j;
                    if (k > 16) break; // 第 18 格是自定义色（RainbowSwatch）
                    var sw = new ModernUi.Swatch(T, 36) { Bounds = new Rectangle(206 + i * 40, 42 + j * 40, 36, 36) };
                    int captured = k;
                    sw.Click += (s, e) => ChooseColor(captured);
                    _swatches[k] = sw;
                    Controls.Add(sw);
                }
            }
        }

        private void SetColorButtons()
        {
            for (int i = 0; i <= 16; i++)
                // colors.txt 存 0xRRGGBB；FromArgb(int) 会把 0x00RRGGBB 当 ARGB（alpha=0=全透明），必须补 FF
                _swatches[i].Value = Color.FromArgb(unchecked((int)(0xFF000000u | (uint)(_colors[i + 1] & 0xFFFFFF))));
        }

        private void ShowElements()
        {
            if (!_actionsHidden) return;
            _random.Visible = true;
            _reset.Visible = true;
            _newPalette.Visible = true;
            _actionsHidden = false;
        }

        private void BuildPaletteMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Renderer = new ModernUi.MenuTheme(T);
            menu.ForeColor = T.Fg;
            ToolStripItem it;
            it = menu.Items.Add(L("Default"), null, (s, e) =>
            {
                if (PaletteFile.TryRead(Paths.Combine(Paths.ScriptDirectory(), "defaultcolors.txt"), 18, out int[] loaded))
                {
                    Array.Clear(_colors, 0, _colors.Length);
                    Array.Copy(loaded, _colors, Math.Min(loaded.Length, _colors.Length));
                }
                SetColorButtons();
            });
            it.ForeColor = T.Fg;
            it = menu.Items.Add(L("Colorful"), null, (s, e) => { ColorGenerator.GenerateColorful(_colors); SetColorButtons(); });
            it.ForeColor = T.Fg;
            it = menu.Items.Add(L("Contigious"), null, (s, e) => { ColorGenerator.GenerateContigious(_colors); SetColorButtons(); });
            it.ForeColor = T.Fg;
            it = menu.Items.Add(L("Contrasting"), null, (s, e) => { ColorGenerator.GenerateContrasting(_colors); SetColorButtons(); });
            it.ForeColor = T.Fg;
            _newPalette.Click += (s, e) => menu.Show(_newPalette, new Point(0, _newPalette.Height));
        }

        // ---------- 面板列表 ----------

        private void ReadActiveSkins()
        {
            _numPanels = 0;
            _panelList.Items.Clear();

            IniDoc doc = IniDoc.TryLoad(Paths.Combine(_settingsPath, "Rainmeter.ini"));
            if (doc == null)
                OmnimoDialogs.Error("Individual Panel Color", "Unable to open Rainmeter.ini for reading.");

            foreach (string section in doc.SectionNames())
            {
                if (!Aligner.MatchesPanelSection(section)) continue;
                if (!AutoIt.IsNonZeroString(doc.Get(section, "Active", "0"))) continue;
                if (!Directory.Exists(Paths.Combine(_skinPath, section))) continue;

                // 与 AutoIt 一致：配置路径相对当前工作目录（读不到就不过滤）
                string config = section.Replace("REO\\", "REO\\@Resources\\Config\\") + "\\RainConfigure.cfg";
                IniDoc cfg = IniDoc.TryLoad(config);
                if (cfg != null && cfg.Get("Options", "Colorizable", "1") == "0") continue;

                if (_numPanels >= 100) break;   // 先断容量再进列表：列表与 _panels 数组必须同步（RO-29）
                _panelList.Items.Add(Aligner.PanelDisplayName(section));
                _panels[_numPanels++] = section;
            }
            _emptyHint.Visible = _panelList.Items.Count == 0;
            // 条目填满后滚动条才出现，此时再切一次深色主题（句柄创建时它还不在）
            _panelList.RefreshScrollbarTheme();
        }

        private void ForEachSelected(Action<string> action)
        {
            foreach (int index in _panelList.SelectedIndices)
            {
                if (index >= 0 && index < _numPanels) action(_panels[index]);
            }
        }

        private void ChooseColor(int index)
        {
            if (_prev >= 0 && _prev <= 16 && _swatches[_prev] != null) _swatches[_prev].Selected = false;
            _swatches[index].Selected = true;
            _prev = index;
            ForEachSelected(p => ApplyColor(p, ColorText.HexToRGB(_colors[index + 1]) + "," + _opacity));
        }

        /// <summary>
        /// 把选中面板现有的 ColorSkin 标到色板上：恰好选中一个面板时读它，多选或读不到就清掉标记。
        /// 颜色不在调色板里时标在第 18 格（自定义取色入口），表示这个颜色不来自色板。
        /// </summary>
        private void SyncCurrentColor()
        {
            ClearSwatchSelection();
            if (_panelList.SelectedIndices.Count != 1) return;
            int index = _panelList.SelectedIndices[0];
            if (index < 0 || index >= _numPanels) return;

            int rgb;
            if (!TryReadPanelColor(_panels[index], out rgb)) return;

            for (int i = 0; i < 17; i++)
            {
                if ((_colors[i + 1] & 0xFFFFFF) != (rgb & 0xFFFFFF)) continue;
                if (_swatches[i] != null)
                {
                    _swatches[i].Selected = true;
                    _prev = i;
                }
                return;
            }
            if (_custom != null) _custom.Selected = true;
            _prev = -1;
        }

        private void ClearSwatchSelection()
        {
            for (int i = 0; i < 17; i++)
                if (_swatches[i] != null) _swatches[i].Selected = false;
            if (_custom != null) _custom.Selected = false;
            _prev = -1;
        }

        /// <summary>读面板 ini 里第一个 ColorSkin（形如 "r,g,b,opacity"），成功则给出其 RGB。</summary>
        private bool TryReadPanelColor(string panel, out int rgb)
        {
            rgb = 0;
            string[] files;
            try { files = Directory.GetFiles(Paths.Combine(_skinPath, panel), "*.ini"); }
            catch { return false; }

            foreach (string file in files)
            {
                IniDoc doc;
                try { doc = IniDoc.LoadOrCreate(file); }
                catch { continue; }
                string value = doc.Get("Variables", "ColorSkin", "");
                if (string.IsNullOrEmpty(value)) continue;
                string[] parts = value.Split(',');
                if (parts.Length < 3) continue;
                int r, g, b;
                if (!int.TryParse(parts[0].Trim(), out r)) continue;
                if (!int.TryParse(parts[1].Trim(), out g)) continue;
                if (!int.TryParse(parts[2].Trim(), out b)) continue;
                rgb = ((r & 0xFF) << 16) | ((g & 0xFF) << 8) | (b & 0xFF);
                return true;
            }
            return false;
        }

        private void ChooseCustomColor()
        {            int data = ColorPickerForm.Show(this, 0x000000, T, _lang);
            if (data == -1) return;   // 取消：AutoIt 走 ContinueCase，等价于什么都不做
            string selected = ColorText.HexToRGB(data) + "," + _opacity;
            ForEachSelected(p => ApplyColor(p, selected));
        }

        /// <summary>
        /// 等价于 ApplyColor($Panel, $NewColor)：写该面板目录下全部 *.ini，然后 Refresh 该面板。
        /// newColor 为 null 表示 AutoIt 的 $NewColor=-1（IniDelete ColorSkin）。
        /// </summary>
        private void ApplyColor(string panel, string newColor)
        {
            string[] files;
            try { files = Directory.GetFiles(Paths.Combine(_skinPath, panel), "*.ini"); }
            catch { return; }
            try
            {
                foreach (string file in files)
                {
                    IniDoc doc = IniDoc.LoadOrCreate(file);
                    if (newColor == null) doc.DeleteKey("Variables", "ColorSkin");
                    else doc.Set("Variables", "ColorSkin", newColor);
                    doc.Save();
                }
            }
            catch (IOException ex)
            {
                // IniDoc.LoadOrCreate 在目标文件被占用/读失败时抛出（不回落空文档、不覆写）：
                // 中止本次 ApplyColor 并提示用户，面板保持原色、不发 !Refresh
                MessageBox.Show(this,
                    string.Format("Unable to update panel colors for {0}:\r\n{1}", panel, ex.Message),
                    "Individual Panel Color", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Refresh panel
            Rainmeter.SendBang("!Refresh " + panel);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // GUI_EVENT_CLOSE：把色板写回 exe 目录下的 colors.txt
            PaletteFile.Write(Paths.Combine(Paths.ScriptDirectory(), "colors.txt"), _colors);
            base.OnFormClosed(e);
        }

        /// <summary>自定义色块：彩虹渐变 + 白十字，对应原版的 custom color 图 + plus.jpg。</summary>
        private sealed class RainbowSwatch : ModernUi.Swatch
        {
            public RainbowSwatch(ModernUi.Theme t) : base(t, 36) { }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = ModernUi.Rounded(r, 6))
                {
                    using (var brush = new LinearGradientBrush(r, Color.Red, Color.Magenta, 0f))
                    {
                        var blend = new ColorBlend(6)
                        {
                            Colors = new[] { Color.FromArgb(0xE8, 0x11, 0x23), Color.FromArgb(0xF7, 0xB5, 0x00), Color.FromArgb(0x10, 0x7C, 0x10), Color.FromArgb(0x00, 0x78, 0xD4), Color.FromArgb(0x4D, 0x7C, 0xFE), Color.FromArgb(0xB1, 0x4E, 0xB1) },
                            Positions = new[] { 0f, 0.25f, 0.5f, 0.7f, 0.85f, 1f }
                        };
                        brush.InterpolationColors = blend;
                        g.FillPath(brush, path);
                    }
                    using (var p = new Pen(T.Border)) g.DrawPath(p, path);
                }
                using (var p = new Pen(Color.White, 2f))
                {
                    int cx = Width / 2, cy = Height / 2, d = 6;
                    g.DrawLine(p, cx - d, cy, cx + d, cy);
                    g.DrawLine(p, cx, cy - d, cx, cy + d);
                }
                if (Selected)
                {
                    // 选中环：基类 Swatch 的画法这里被整体覆写，需要自己补（面板颜色不在色板里时标在这一格）
                    var ring = new Rectangle(1, 1, Width - 3, Height - 3);
                    using (var path = ModernUi.Rounded(ring, 6))
                    using (var p = new Pen(T.Accent, 2f)) g.DrawPath(p, path);
                }
            }
        }
    }
}
