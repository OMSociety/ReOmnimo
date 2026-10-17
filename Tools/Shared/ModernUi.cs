// ReOmnimo 现代工具窗共享 UI 层（ModernUi）
// 视觉权威 = SettingsCenter（SettingsCenter\Program.cs 内嵌模板）：同一套色板（深浅两主题，
// 强调色取 Windows 系统 DWM 强调色）、同一字体栈（Microsoft YaHei UI → Segoe UI）、
// 同一圆角语言（窗口 12 / 卡 10 / 输入 8 / 开关 12 / 色块 6）。
// 2026-10 用户批准的现代化方向：ColorChanger / ConfigBackground / PanelCreator 三窗统一走本层；
// 只影响工具窗口，不触碰磁贴视觉（AGENTS.md 设计层四禁仍只约束磁贴）。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ---------- 主题 ----------

namespace OmnimoTools
{

internal static class ModernUi
{
    public static bool IsSystemLight()
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 0);
            return v is int i && i == 1;
        }
        catch { return false; } // 与 SettingsCenter 一致：读不到按深色
    }

    /// <summary>Windows 系统强调色（个性化→颜色）；读不到回退靛蓝 #4d7cfe（与 SettingsCenter 一致）。</summary>
    public static Color SystemAccent()
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null);
            if (v is int abgr) // DWM 存 AABBGGRR
            {
                Color accent = Color.FromArgb(0xFF, abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                // 系统强调色可以是中性灰（本机 AccentColor=0xA1A1A1）；拿它画按钮和滑杆会整片发灰，
                // 饱和度过低就回落到本工具集的蓝，保持配色一致。
                int max = Math.Max(accent.R, Math.Max(accent.G, accent.B));
                int min = Math.Min(accent.R, Math.Min(accent.G, accent.B));
                if (max > 0 && (max - min) * 100 / max >= 12) return accent;
            }
        }
        catch { }
        return Color.FromArgb(0x4D, 0x7C, 0xFE);
    }

    public sealed class Theme
    {
        public bool Light;
        public Color Bg, Fg, Sub, Card, Rail, Input, Accent, Border;

        public static Theme Detect(bool forceLight = false, bool forceDark = false)
        {
            bool light = forceLight || (!forceDark && IsSystemLight());
            var t = new Theme { Light = light };
            t.Bg = ColorFrom(light ? "#f6f7f9" : "#131417");
            t.Fg = ColorFrom(light ? "#191b1f" : "#e8eaed");
            t.Card = ColorFrom(light ? "#ffffff" : "#1b1d21");
            t.Rail = ColorFrom(light ? "#eef0f3" : "#17181c");
            t.Border = ColorFrom(light ? "#00000012" : "#ffffff10");
            t.Sub = ColorFrom(light ? "#5c6370" : "#8a919d");
            t.Input = ColorFrom(light ? "#f2f4f7" : "#24262c");
            t.Accent = SystemAccent();
            return t;
        }

        private static Color ColorFrom(string hex)
        {
            // 约定：6 位 = RRGGBB；8 位 = RRGGBBAA（alpha 在最后，同 SettingsCenter 的 XAML 习惯）
            int a = 0xFF, r, g, b;
            string h = hex.TrimStart('#');
            if (h.Length == 8) { a = Convert.ToInt32(h.Substring(6, 2), 16); h = h.Substring(0, 6); }
            r = Convert.ToInt32(h.Substring(0, 2), 16);
            g = Convert.ToInt32(h.Substring(2, 2), 16);
            b = Convert.ToInt32(h.Substring(4, 2), 16);
            return Color.FromArgb(a, r, g, b);
        }

        public Color HoverOn(Color baseColor) => Light ? Darken(baseColor, 0.10) : Lighten(baseColor, 0.10);
        public Color PressOn(Color baseColor) => Light ? Darken(baseColor, 0.18) : Lighten(baseColor, 0.18);
        public Color AccentSoft => Color.FromArgb(Light ? 0x20 : 0x2E, Accent.R, Accent.G, Accent.B);

        public static Color Lighten(Color c, double f) => Shade(c, f, 255);
        public static Color Darken(Color c, double f) => Shade(c, f, 0);
        private static Color Shade(Color c, double f, int target)
        {
            return Color.FromArgb(c.A,
                (int)(c.R + (target - c.R) * f),
                (int)(c.G + (target - c.G) * f),
                (int)(c.B + (target - c.B) * f));
        }
    }

    private static readonly Dictionary<float, Font> FontCache = new Dictionary<float, Font>();
    /// <summary>UI 字体栈：Microsoft YaHei UI → Segoe UI → 系统默认（GDI+ 无 600 字重，标题用 Bold 近似）。</summary>
    public static Font UiFont(float size = 9f, bool bold = false)
    {
        float key = size * 2 + (bold ? 1 : 0);
        Font f;
        if (FontCache.TryGetValue(key, out f)) return f;
        string[] faces = { "Microsoft YaHei UI", "Segoe UI" };
        foreach (string face in faces)
        {
            try
            {
                f = new Font(face, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
                FontCache[key] = f;
                return f;
            }
            catch { }
        }
        f = new Font(SystemFonts.DefaultFont.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular);
        FontCache[key] = f;
        return f;
    }

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindowX(IntPtr hwnd);

    /// <summary>控件所在窗口的 DPI 单位系数（96 DPI = 1）。Bounds/Font 已由 ModernForm.OnHandleCreated
    /// 的 Scale 统一缩放，自绘控件内部写死的设计像素常量（勾形、箭头、圆角半径、内边距）要乘它才同步。</summary>
    internal static float DpiU(Control c)
    {
        try
        {
            IntPtr h = (c.TopLevelControl != null && c.TopLevelControl.IsHandleCreated) ? c.TopLevelControl.Handle : c.Handle;
            int d = GetDpiForWindowX(h);
            if (d > 0) return d / 96f;
        }
        catch { }
        return 1f;
    }
    internal static int PxU(float design, Control c) { return Math.Max(1, (int)Math.Round(design * DpiU(c))); }

    // ---------- 无边框窗口基类 ----------

    /// <summary>
    /// 无边框 + 自绘标题栏（左：应用图标+标题，右：最小化/关闭），圆角 12，可拖拽，
    /// resizable=true 时四边可拖放缩放、双击标题栏最大化。Esc 关闭（等于点 X，不保存）。
    /// </summary>
    public class ModernForm : Form
    {
        protected readonly Theme T;
        private readonly bool _resizable;
        private Label _title;
        private Action _layoutCaption;
        private int _appliedDpi = 96;
        private const int TitleH = 32;
        private const int Radius = 12;

        public ModernForm(Theme t, string title, bool resizable = false)
        {
            T = t;
            _resizable = resizable;
            // DPI 缩放不用 AutoScaleMode.Dpi：net48 的自动缩放会在 handle 创建时用系统 DPI 提前把
            // AutoScaleDimensions 改写成当前值、缩放因子算成 1（实测 DeviceDpi=96、字被放大而坐标不动），
            // 黑箱且不可控。这里 AutoScaleMode=None，改为 OnHandleCreated 里用 GetDpiForWindow 手动
            // Scale（见下方注释）。GuiFont 那条「None」契约与本约定一致。
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = t.Bg;
            Font = UiFont(9f);
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            MinimumSize = new Size(180, TitleH + 40); // 96-DPI 设计值；DPI 缩放在 OnHandleCreated 手动做
            if (resizable) MaximizeBox = true;

            var bar = new Panel { Dock = DockStyle.Top, Height = TitleH, BackColor = t.Card };
            Controls.Add(bar);

            var pic = new PictureBox
            {
                Bounds = new Rectangle(10, (TitleH - 16) / 2, 16, 16),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Image = Icon == null ? null : Icon.ToBitmap()
            };
            bar.Controls.Add(pic);

            _title = new Label
            {
                Text = title,
                AutoSize = false,
                Bounds = new Rectangle(32, 0, 400, TitleH),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = t.Fg,
                Font = UiFont(9f)
            };
            bar.Controls.Add(_title);
            // 任务栏缩略图与提示只读 Form.Text，之前从不赋值，卡片上因此没有窗口名。
            Text = title;

            // 标题栏右侧按钮一律按「控件自身宽度」从右往左排，不能用写死的设计像素偏移：
            // 40 宽的按钮在 150% DPI 下是 60，而写死的 41 只让出 41，按钮会探出右边缘
            // （实测 ColorChanger / ConfigBackground 在笔记本屏上关闭键被裁掉 19px，看着像没有关闭键）。
            var minBtn = (TitleButton)null;
            var maxBtn = (TitleButton)null;
            if (resizable)
            {
                minBtn = new TitleButton(t, TitleGlyph.Minimize);
                minBtn.Bounds = new Rectangle(0, 0, 40, TitleH - 1);
                minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
                bar.Controls.Add(minBtn);

                maxBtn = new TitleButton(t, TitleGlyph.MaxRestore);
                maxBtn.Bounds = new Rectangle(0, 0, 40, TitleH - 1);
                maxBtn.Click += (s, e) => ToggleMaximize();
                bar.Controls.Add(maxBtn);
            }

            var close = new TitleButton(t, TitleGlyph.Close) { IsClose = true };
            close.Bounds = new Rectangle(0, 0, 40, TitleH - 1);
            close.Click += (s, e) => Close();
            bar.Controls.Add(close);

            var captionOrder = new List<TitleButton> { close };
            if (maxBtn != null) captionOrder.Add(maxBtn);
            if (minBtn != null) captionOrder.Add(minBtn);
            Action layoutCaption = () =>
            {
                int right = bar.Width;
                foreach (var b in captionOrder)
                {
                    if (b.Width <= 0) continue;
                    b.Location = new Point(right - b.Width, 0);
                    right -= b.Width;
                }
                // 标题标签原宽写死 400，会横跨到右侧按钮下面；Label 的背景色取自父容器（不透明），
                // 而 Controls.Add 的顺序让后加的按钮落在它下层，于是 X 被整块盖掉（实测三窗都看不见关闭键）。
                // 这里把标题收到按钮左侧，并把按钮提到最前。
                _title.Width = Math.Max(0, right - _title.Left - 4);
                foreach (var b in captionOrder) b.BringToFront();
            };
            _layoutCaption = layoutCaption;
            layoutCaption();
            bar.Resize += (s, e) => layoutCaption();

            bar.MouseDown += DragStart;
            _title.MouseDown += DragStart;
            pic.MouseDown += DragStart;
            bar.DoubleClick += (s, e) => { if (_resizable) ToggleMaximize(); };
            _title.DoubleClick += (s, e) => { if (_resizable) ToggleMaximize(); };

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); } };
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        /// <summary>本窗所在显示器的 DPI（Win10 1607+）。DeviceDpi/DpiHelper 缓存的是进程启动时的系统
        /// DPI，实测与窗口实际所在屏不符（96 vs 144），一律以此为准。</summary>
        protected int WinDpi
        {
            get
            {
                try { int d = GetDpiForWindow(Handle); if (d > 0) return d; } catch { }
                return 96;
            }
        }

        public string TitleText { get { return _title.Text; } set { _title.Text = value; } }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        private void DragStart(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && WindowState != FormWindowState.Maximized)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 手动 DPI 缩放：net48 的 AutoScaleMode.Dpi 在本工程实测缩放因子算成 1（坐标不动、
            // 只放大了字），黑箱不可控；改为按窗口所在屏的 GetDpiForWindow 明确缩放。
            // Control.Scale 会同时缩放窗体自身尺寸与全部子控件（Bounds+Font 递归），不要
            // 再手动预乘 ClientSize（叠加后窗体尺寸会超放）。派生类的 OnPaint 手设坐标需自行
            // 乘 WinDpi/96 或加 ScaleTransform（PanelCreator 即如此）。
            int dpi = WinDpi;
            _appliedDpi = dpi;
            if (dpi != 96)
                Scale(new SizeF(dpi / 96f, dpi / 96f));
            try
            {
                int pref = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref pref, 4); // DWMWA_WINDOW_CORNER_PREFERENCE
                int dark = T.Light ? 0 : 1;
                if (DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0) // Win11
                    DwmSetWindowAttribute(Handle, 19, ref dark, 4);       // 旧 build
            }
            catch { }
        }

        private const int WM_DPICHANGED = 0x02E0;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        /// <summary>DPI 变化后让派生类更新自己的缩放快照（各工具的 <c>_scale</c> 来自构造时的屏幕 DPI，
        /// 不更新的话新建/重画的控件仍按旧比例算）。</summary>
        protected virtual void OnDpiFactorChanged(float factor) { }

        private void RescaleForDpi(int newDpi, RECT suggested)
        {
            if (newDpi <= 0) newDpi = 96;
            float factor = newDpi / (float)_appliedDpi;
            if (Math.Abs(factor - 1f) < 0.01f) return;
            _appliedDpi = newDpi;
            SuspendLayout();
            RescaleChildren(this, factor);
            OnDpiFactorChanged(factor);
            if (_layoutCaption != null) _layoutCaption();
            ResumeLayout(true);
            if (suggested.Right > suggested.Left && suggested.Bottom > suggested.Top)
                SetBounds(suggested.Left, suggested.Top, suggested.Right - suggested.Left, suggested.Bottom - suggested.Top);
            PerformLayout();
            // 原生 TrackBar 会在 WM_DPICHANGED 之后按新 DPI 重算自己的高度（实测 150% 下从 31 涨到 56，
            // 压住下面一行的文字），ModernTrackBar 的设计高度是固定的，这里重新钉一次。
            foreach (Control c in Descendants(this))
            {
                var bar = c as ModernTrackBar;
                if (bar != null) bar.ForceHeight();
            }
            Invalidate(true);
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                yield return c;
                foreach (Control d in Descendants(c)) yield return d;
            }
        }

        private static void RescaleChildren(Control parent, float factor)
        {
            foreach (Control c in parent.Controls)
            {
                c.Bounds = new Rectangle(
                    (int)Math.Round(c.Left * factor),
                    (int)Math.Round(c.Top * factor),
                    Math.Max(1, (int)Math.Round(c.Width * factor)),
                    Math.Max(1, (int)Math.Round(c.Height * factor)));
                Font f = c.Font;
                if (f != null)
                {
                    float size = f.SizeInPoints * factor;
                    if (size >= 1f)
                        c.Font = new Font(f.FontFamily, size, f.Style, GraphicsUnit.Point);
                }
                if (c.HasChildren) RescaleChildren(c, factor);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED)
            {
                // 拖到另一块 DPI 不同的屏幕：系统只按 SuggestedRectangle 改窗口尺寸，
                // 而本窗是 AutoScaleMode=None + OnHandleCreated 里手动 Scale 一次，子控件的
                // 坐标与字体仍是旧 DPI 的值，自定义绘制又按新的 DeviceDpi 画，于是错位、
                // 留下残余黑块（实测把面板创建器从 125% 屏拖到 150% 屏即出现）。
                // 这里自己接管：按新旧 DPI 比值重排整棵控件树，再采用系统给的建议矩形。
                int newDpi = (int)(m.WParam.ToInt64() & 0xFFFF);
                var rect = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                RescaleForDpi(newDpi, rect);
                return;
            }
            if (m.Msg == WM_NCHITTEST && _resizable && WindowState != FormWindowState.Maximized)
            {
                Point p = PointToClient(new Point((int)m.LParam & 0xFFFF, ((int)m.LParam >> 16) & 0xFFFF));
                // 鼠标坐标是设备像素，握边阈值也要按 DPI 放大
                int grip = 5 * WinDpi / 96;
                bool l = p.X < grip, r = p.X >= ClientSize.Width - grip;
                bool t2 = p.Y < grip, b = p.Y >= ClientSize.Height - grip;
                if (l && t2) { m.Result = (IntPtr)HT_TOPLEFT; return; }
                if (r && t2) { m.Result = (IntPtr)HT_TOPRIGHT; return; }
                if (l && b) { m.Result = (IntPtr)HT_BOTTOMLEFT; return; }
                if (r && b) { m.Result = (IntPtr)HT_BOTTOMRIGHT; return; }
                if (l) { m.Result = (IntPtr)HT_LEFT; return; }
                if (r) { m.Result = (IntPtr)HT_RIGHT; return; }
                if (t2) { m.Result = (IntPtr)HT_TOP; return; }
                if (b) { m.Result = (IntPtr)HT_BOTTOM; return; }
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 1px 描边：内缩 1px、半径 8（≈DWM ROUND 在 96dpi 的基础半径）——整条路径落在 DWM
            // 圆角裁剪之内，角上不会被裁断（贴边画时外半像素被裁、弧线呈缺口）。不要再叠 WinForms Region。
            // 半径随 DPI 放大，保证缩放屏上圆角视觉大小一致。
            using (var pen = new Pen(T.Border))
                e.Graphics.DrawPath(pen, Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 8 * WinDpi / 96));
        }

        private enum TitleGlyph { Close, Minimize, MaxRestore }

        private class TitleButton : Control
        {
            private readonly Theme _t;
            private readonly TitleGlyph _glyph;
            public bool IsClose;
            private bool _hover;

            public TitleButton(Theme t, TitleGlyph g)
            {
                _t = t; _glyph = g;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Default;
                MouseEnter += (s, e) => { _hover = true; Invalidate(); };
                MouseLeave += (s, e) => { _hover = false; Invalidate(); };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_hover)
                {
                    using (var b = new SolidBrush(IsClose ? Color.FromArgb(0xE8, 0x11, 0x23) : _t.HoverOn(_t.Card)))
                        g.FillRectangle(b, new Rectangle(0, 0, Width, Height));
                }
                if (IsClose && _hover)
                {
                    using (var p = new Pen(Color.White, 1.4f * DpiU(this)))
                    {
                        int cx = Width / 2, cy = Height / 2, d = PxU(5, this);
                        g.DrawLine(p, cx - d, cy - d, cx + d, cy + d);
                        g.DrawLine(p, cx + d, cy - d, cx - d, cy + d);
                    }
                    return;
                }
                // 非 hover 时原来用 T.Sub（次要文字的灰）+ 1.2px 线宽：40px 按钮里的细叉在深色标题栏上
                // 几乎看不出来，用户实测三个工具「看不见关闭键」。改用前景色，线宽与叉口尺寸都随 DPI 放大。
                using (var p = new Pen(_t.Fg, 1.4f * DpiU(this)))
                {
                    int cx = Width / 2, cy = Height / 2;
                    switch (_glyph)
                    {
                        case TitleGlyph.Close:
                            {
                                int d = PxU(6, this);
                                g.DrawLine(p, cx - d, cy - d, cx + d, cy + d);
                                g.DrawLine(p, cx + d, cy - d, cx - d, cy + d);
                                break;
                            }
                        case TitleGlyph.Minimize:
                            g.DrawLine(p, cx - PxU(5, this), cy + PxU(3, this), cx + PxU(5, this), cy + PxU(3, this));
                            break;
                        case TitleGlyph.MaxRestore:
                            int mw = PxU(9, this), mh = PxU(8, this);
                            g.DrawRectangle(p, cx - PxU(5, this), cy - PxU(4, this), mw, mh);
                            break;
                    }
                }
            }
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 2;
        public const int WM_NCHITTEST = 0x84;
        public const int HT_LEFT = 10, HT_RIGHT = 11, HT_TOP = 12, HT_BOTTOM = 15,
                         HT_TOPLEFT = 13, HT_TOPRIGHT = 14, HT_BOTTOMLEFT = 16, HT_BOTTOMRIGHT = 17;

        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    }

    // ---------- 控件 ----------

    /// <summary>主按钮：实心强调色；Ghost=true 时变描边弱按钮。圆角 8。</summary>
    public class ModernButton : Button
    {
        public bool Ghost;
        public readonly Theme T;

        public ModernButton(Theme t, string text, bool ghost = false)
        {
            T = t; Ghost = ghost;
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            // 必须 UserPaint：否则原生 Flat 按钮会先画一遍底+字+焦点框，与 OnPaint 叠成双份
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = UiFont(9f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            // UserPaint 下背景不会自动擦除：必须先用父底色盖掉上一帧，否则 ghost 的透明底
            // 会让每次重绘的文字叠成双份（实心按钮底不透明所以看不出）
            Color erase = T.Bg;
            if (Parent != null && Parent.BackColor.A >= 0xFF) erase = Parent.BackColor;
            using (var b0 = new SolidBrush(erase)) g.FillRectangle(b0, r);
            Color bg, fg, edge = T.Border;
            if (!Enabled)
            {
                bg = erase;
                fg = T.Sub;
            }
            else if (Ghost)
            {
                bool hov = ClientRectangle.Contains(PointToClient(MousePosition));
                bg = hov ? T.Input : erase;
                fg = T.Fg;
                edge = T.Border;
            }
            else
            {
                bool hov = ClientRectangle.Contains(PointToClient(MousePosition));
                bool press = hov && (MouseButtons & MouseButtons.Left) != 0;
                bg = press ? T.PressOn(T.Accent) : hov ? T.HoverOn(T.Accent) : T.Accent;
                fg = PerceivedLight(bg) ? Color.FromArgb(0x19, 0x1B, 0x1F) : Color.White;
            }
            using (var path = Rounded(r, PxU(8, this)))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                if (Ghost || !Enabled)
                    using (var p = new Pen(edge)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && Enabled)
                using (var p = new Pen(T.Accent, 1.5f))
                using (var path = Rounded(new Rectangle(1, 1, Width - 3, Height - 3), PxU(7, this)))
                    g.DrawPath(p, path);
        }

        private static bool PerceivedLight(Color c) => (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 160;
    }

    /// <summary>输入框容器：Input 底色圆角 8 + 内部无边框 TextBox。公开 Inner。</summary>
    public class ModernTextBox : Panel
    {
        public readonly TextBox Inner;
        public readonly Theme T;

        public ModernTextBox(Theme t, string text = "")
        {
            T = t;
            Height = 30;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = t.Input,
                ForeColor = t.Fg,
                Text = text,
                Font = UiFont(9f)
            };
            Inner.Resize += (s, e) => Invalidate();
            Controls.Add(Inner);
        }

        public string Value { get { return Inner.Text; } set { Inner.Text = value; } }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Inner == null) return;   // 构造函数里 Height=30 会先触发一次 OnLayout
            int pad = PxU(9, this);
            Inner.Bounds = new Rectangle(pad, (Height - Inner.PreferredHeight) / 2, Width - 2 * pad, Inner.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), PxU(8, this)))
            using (var b = new SolidBrush(T.Input))
                g.FillPath(b, path);
            bool focus = Inner.Focused;
            if (focus)
                using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), PxU(8, this)))
                using (var p = new Pen(T.Accent, 1.5f))
                    g.DrawPath(p, path);
        }
    }

    /// <summary>复选框：16×16 圆角 4，勾选=强调色+白勾。文案 Fg。基类用 Control 而不是 CheckBox——
    /// 继承 CheckBox 时即使 UserPaint，主题引擎仍会在非 96 DPI 下把原生勾选字形画进客户区
    /// （2026-10-17 实测 150% 屏：勾选状态的框右侧冒出彩色碎片字形），换成无原生部分的
    /// Control 后主题完全不参与绘制。自带 Checked 与点击翻转（等价 CheckBox 的 AutoCheck）。</summary>
    public class ModernCheckBox : Control
    {
        public readonly Theme T;
        private bool _checked;
        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }
        public event EventHandler CheckedChanged;

        public ModernCheckBox(Theme t, string text)
        {
            T = t; Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);
            Font = UiFont(9f);
            Cursor = Cursors.Hand;
            Click += (s, e) => { Checked = !Checked; };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int bs = PxU(14, this);                       // 勾选框 14px（设计值，随 DPI）——与 9pt 文字视觉等高
            var box = new Rectangle(0, (Height - bs) / 2, bs, bs);
            using (var path = Rounded(box, PxU(3, this)))
            {
                if (Checked)
                {
                    using (var b = new SolidBrush(T.Accent)) g.FillPath(b, path);
                    using (var p = new Pen(Color.White, 1.6f * DpiU(this)))
                    {
                        int x = box.X, y = box.Y;
                        g.DrawLine(p, x + PxU(3, this), y + PxU(7, this), x + PxU(6, this), y + PxU(10, this));
                        g.DrawLine(p, x + PxU(6, this), y + PxU(10, this), x + PxU(11, this), y + PxU(4, this));
                    }
                }
                else
                {
                    using (var b = new SolidBrush(T.Input)) g.FillPath(b, path);
                    using (var p = new Pen(T.Sub)) g.DrawPath(p, path);
                }
            }
            int tx = PxU(22, this);
            var tr = new Rectangle(tx, 0, Width - tx, Height);
            TextRenderer.DrawText(g, Text, Font, tr, Enabled ? T.Fg : T.Sub,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused)
            {
                int fw = Math.Min(PxU(20, this), Height);
                using (var p = new Pen(T.Accent))
                    g.DrawRectangle(p, new Rectangle(PxU(20, this), (Height - fw) / 2, fw, fw));
            }
        }
    }

    /// <summary>滑杆：TrackBar OwnerDraw。轨道 Input 圆角，已填充段强调色，拇指圆角块。</summary>
    public class ModernTrackBar : TrackBar
    {
        public readonly Theme T;
        public ModernTrackBar(Theme t, int min, int max, int value)
        {
            T = t; Minimum = min; Maximum = max; Value = value;
            TickStyle = TickStyle.None;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            // 设计高度 26；WinForms 会顶 TrackBar 高度，建句柄后强制一次（ConfigBackground 的老问题）
            Height = 26;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ForceHeight();
        }
        public void ForceHeight()
        {
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, Width, PxU(26, this), 0x0016); // NOSIZE 之外：x,y,cx,cy 全给
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int m = PxU(2, this), th = PxU(4, this), tr = Math.Max(1, PxU(2, this));
            int cy = Height / 2;
            var track = new Rectangle(m, cy - th / 2, Width - 2 * m, th);
            using (var path = Rounded(track, tr))
            using (var b = new SolidBrush(T.Input)) g.FillPath(b, path);
            if (Value > Minimum)
            {
                int w = (int)((long)(Width - 2 * m) * (Value - Minimum) / Math.Max(1, Maximum - Minimum));
                if (w < th) w = th;
                var fill = new Rectangle(m, cy - th / 2, w, th);
                using (var path = Rounded(fill, tr))
                using (var b = new SolidBrush(T.Accent)) g.FillPath(b, path);
            }
            double frac = Maximum == Minimum ? 0 : (double)(Value - Minimum) / (Maximum - Minimum);
            int half = PxU(6, this);
            int tx = m + (int)((Width - 2 * m) * frac);
            // 拇指在两端时要整体留在控件内，否则最右/最左会被裁掉半个
            tx = Math.Min(Math.Max(tx, half), Math.Max(half, Width - half));
            var thumb = new Rectangle(tx - half, cy - PxU(9, this), PxU(12, this), PxU(18, this));
            using (var path = Rounded(thumb, PxU(5, this)))
            {
                using (var b = new SolidBrush(T.Accent)) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(0x80, 255, 255, 255))) g.DrawPath(p, path);
            }
        }

        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }

    /// <summary>分组标题 + 分隔线（ConfigBackground 的 Appearance/Borders/Advanced）。</summary>
    public class SectionHeader : Control
    {
        public readonly Theme T;
        public SectionHeader(Theme t, string text)
        {
            T = t; Text = text;
            Height = 22;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = UiFont(8.5f);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Size ts = TextRenderer.MeasureText(Text, Font);
            var tr = new Rectangle(0, 0, ts.Width + 4, Height);
            TextRenderer.DrawText(g, Text, Font, tr, T.Sub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            int y = Height / 2;
            using (var p = new Pen(T.Border))
                g.DrawLine(p, ts.Width + PxU(12, this), y, Width - 1, y);
        }
    }

    /// <summary>颜色色块：圆角 6，选中时 2px 强调色外环。</summary>
    public class Swatch : Control
    {
        public readonly Theme T;
        public Color Value = Color.Black;
        public bool Selected { get; set; }
        public Swatch(Theme t, int size = 24)
        {
            T = t; Size = new Size(size, size);
            SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int inset = Selected ? PxU(2, this) : 0;
            var r = new Rectangle(inset, inset, Width - 1 - 2 * inset, Height - 1 - 2 * inset);
            using (var path = Rounded(r, PxU(6, this)))
            {
                using (var b = new SolidBrush(Value)) g.FillPath(b, path);
                using (var p = new Pen(T.Border)) g.DrawPath(p, path);   // 黑/白色块在深色主题里不描边会融进背景
            }
            if (Selected)
                using (var p = new Pen(T.Accent, PxU(2, this)))
                using (var path = Rounded(new Rectangle(1, 1, Width - 3, Height - 3), PxU(7, this)))
                    g.DrawPath(p, path);
        }
    }

    /// <summary>数字微调：Input 容器 + TextBox + 右侧 ▲▼。</summary>
    public class SpinBox : Panel
    {
        public readonly Theme T;
        public readonly TextBox Inner;
        public int Minimum = 0, Maximum = 100;
        public SpinBox(Theme t, int value)
        {
            T = t; Height = 30;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Inner = new TextBox { BorderStyle = BorderStyle.None, BackColor = t.Input, ForeColor = t.Fg, Font = UiFont(9f), Text = value.ToString() };
            Inner.KeyPress += (s, e) => { if (!char.IsDigit(e.KeyChar) && e.KeyChar != '\b') e.Handled = true; };
            Controls.Add(Inner);
        }
        public int Value
        {
            get { int v; if (int.TryParse(Inner.Text, out v)) return Math.Max(Minimum, Math.Min(Maximum, v)); return Minimum; }
            set { Inner.Text = Math.Max(Minimum, Math.Min(Maximum, value)).ToString(); }
        }
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Inner == null) return;   // 构造函数里 Height=30 会先触发一次 OnLayout
            int pad = PxU(9, this);
            Inner.Bounds = new Rectangle(pad, (Height - Inner.PreferredHeight) / 2, Width - PxU(34, this), Inner.PreferredHeight);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), PxU(8, this)))
            using (var b = new SolidBrush(T.Input)) g.FillPath(b, path);
            int bw = PxU(22, this), bx = Width - bw - 1, bh = (Height - 2) / 2;
            DrawSpin(g, new Rectangle(bx, 1, bw, bh), true);
            DrawSpin(g, new Rectangle(bx, 1 + bh, bw, Height - 2 - bh), false);
        }
        private void DrawSpin(Graphics g, Rectangle r, bool up)
        {
            bool hov = r.Contains(PointToClient(MousePosition));
            using (var b = new SolidBrush(hov ? T.HoverOn(T.Input) : T.Input)) g.FillRectangle(b, r);
            using (var p = new Pen(T.Sub, 1.2f))
            {
                int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                int d = PxU(3, this), o = PxU(2, this);
                if (up) { g.DrawLine(p, cx - d, cy + o, cx, cy - o); g.DrawLine(p, cx, cy - o, cx + d, cy + o); }
                else { g.DrawLine(p, cx - d, cy - o, cx, cy + o); g.DrawLine(p, cx, cy + o, cx + d, cy - o); }
            }
            MouseClick += SpinBox_MouseClick;
        }
        private void SpinBox_MouseClick(object s, MouseEventArgs e)
        {
            int bw = PxU(22, this), bx = Width - bw - 1, bh = (Height - 2) / 2;
            if (e.X >= bx)
            {
                Value += (e.Y <= 1 + bh) ? 1 : -1;
                OnValueChanged(EventArgs.Empty);
            }
        }
        public event EventHandler ValueChanged;
        protected virtual void OnValueChanged(EventArgs e) { var h = ValueChanged; if (h != null) h(this, e); }
    }

    /// <summary>卡片容器：Card 底色 + 1px 描边，圆角 10。</summary>
    public class CardPanel : Panel
    {
        public readonly Theme T;
        public CardPanel(Theme t)
        {
            T = t;
            BackColor = t.Card;   // 圆角外角落差靠底色兜底；ghost 按钮也用 Parent.BackColor 擦底
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>按圆角裁掉四角：不然卡片底色的直角会露在窗底色上，角上出现「双层圆角」的色块缺口。</summary>
        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (var path = Rounded(new Rectangle(0, 0, Width, Height), PxU(10, this)))
            {
                var next = new Region(path);
                var prev = Region;
                Region = next;
                if (prev != null) prev.Dispose();
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), PxU(10, this)))
            {
                using (var b = new SolidBrush(T.Card)) g.FillPath(b, path);
                using (var p = new Pen(T.Border)) g.DrawPath(p, path);
            }
        }
    }

    /// <summary>列表面板（ColorChanger 面板列表）：无框、选中=强调色 20% 底+强调色字。</summary>
    /// <summary>列表面板（ColorChanger 面板列表）：无框、选中=强调色 20% 底+强调色字。
    /// 滚动条用系统那条，但通过 SetWindowTheme("DarkMode_Explorer") 让它跟随深色，
    /// 不再自绘——自绘版本要接管列表重绘时机，反而把行文字画丢了。</summary>
    public class ListBoxM : ListBox
    {
        public readonly Theme T;

        public ListBoxM(Theme t)
        {
            T = t;
            BorderStyle = BorderStyle.None;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 26;   // 设计值；句柄创建后按 DPI 重算（见 OnHandleCreated）
            BackColor = t.Card;
            ForeColor = t.Fg;
            Font = UiFont(9f);
            IntegralHeight = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ItemHeight = PxU(26, this);   // 行高是设计像素，Bounds/Font 已由 Scale 放大，行高要跟上
            FitToParent();
            ApplyDarkScrollbar();
        }

        /// <summary>列表的设计宽度比卡片大，会被父容器裁掉右缘（文字被切）；收进卡片内。</summary>
        private void FitToParent()
        {
            if (Parent == null) return;
            int maxW = Parent.ClientSize.Width - Left - PxU(4, this);
            if (maxW > PxU(40, this) && Width > maxW) Width = maxW;
        }

        /// <summary>系统滚动条默认跟浅色主题走，在深色卡片上是一条白条；这里切到深色变体。
        /// 滚动条要等条目填满、内容超高之后才出现，句柄创建时切主题对它还不生效，
        /// 所以调用方填完 Items 之后要再调一次 <see cref="RefreshScrollbarTheme"/>。</summary>
        private void ApplyDarkScrollbar()
        {
            try { SetWindowTheme(Handle, "DarkMode_Explorer", null); SetWindowTheme(Handle, "DarkMode_Explorer", "ScrollBar"); } catch { }
        }

        /// <summary>填完 Items / 内容高度变化后调用，让滚动条按深色主题重新绘制。</summary>
        public void RefreshScrollbarTheme()
        {
            ApplyDarkScrollbar();
            Invalidate();
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subApp, string subIdList);

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? T.AccentSoft : T.Card))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index >= 0 && e.Index < Items.Count)
            {
                var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, r,
                    sel ? T.Accent : T.Fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
    // ---------- 语言与主题参数 ----------

    /// <summary>
    /// 工具窗 i18n：读 REO\@Resources\Common\Background\Varrar.inc 指定的语言文件，
    /// 键缺失时回退 English，再缺回退键名（皮肤语言包共 8 种，工具共用同一目录）。
    /// </summary>
    public static class Lang
    {
        public static Dictionary<string, string> Load(string skinPath)
        {
            string dir = Paths.Combine(skinPath, @"REO\@Resources\Common\Background\");
            IniDoc varrar = IniDoc.TryLoad(Paths.Combine(dir, "Varrar.inc"));
            string language = varrar != null ? varrar.Get("Variables", "Language", "English") : "English";
            IniDoc doc = IniDoc.TryLoad(Paths.Combine(dir, @"Language\" + language + ".cfg"));
            if (doc == null) doc = IniDoc.TryLoad(Paths.Combine(dir, @"Language\English.cfg"));
            if (doc == null)
                OmnimoDialogs.Error("Error loading language", "Unable to load language file for " + language);
            return doc.Section("Variables");
        }

        public static Dictionary<string, string> LoadFromExeDir()
        {
            IniDoc varrar = IniDoc.TryLoad(Paths.Combine(Paths.ScriptDirectory(), "Varrar.inc"));
            string language = varrar != null ? varrar.Get("Variables", "Language", "English") : "English";
            IniDoc doc = IniDoc.TryLoad(Paths.Combine(Paths.ScriptDirectory(), @"Language\" + language + ".cfg"));
            if (doc == null) doc = IniDoc.TryLoad(Paths.Combine(Paths.ScriptDirectory(), @"Language\English.cfg"));
            if (doc == null)
                OmnimoDialogs.Error("Error loading language", "Unable to load language file for " + language);
            return doc.Section("Variables");
        }

        public static string T(Dictionary<string, string> lang, string key)
        {
            string v;
            if (lang.TryGetValue(key, out v) && v.Length > 0) return v;
            return key;
        }
    }

    /// <summary>从 args 里剥掉 --light/--dark（SettingsCenter 同款开关），返回剩余参数。</summary>
    public static string[] ParseThemeFlags(string[] args, out bool forceLight, out bool forceDark)
    {
        forceLight = false; forceDark = false;
        var rest = new List<string>();
        foreach (string a in args)
        {
            if (a == "--light") forceLight = true;
            else if (a == "--dark") forceDark = true;
            else rest.Add(a);
        }
        return rest.ToArray();
    }

    /// <summary>菜单（New palette 等 ContextMenuStrip）主题化渲染。</summary>
    public class MenuTheme : ToolStripProfessionalRenderer
    {
        private readonly Theme _t;
        public MenuTheme(Theme t) : base(new MenuTable(t)) { _t = t; }
        private class MenuTable : ProfessionalColorTable
        {
            private readonly Theme _t;
            public MenuTable(Theme t) { _t = t; }
            public override Color MenuItemSelected => _t.AccentSoft;
            public override Color ToolStripDropDownBackground => _t.Card;
            public override Color ImageMarginGradientBegin => _t.Card;
            public override Color ImageMarginGradientMiddle => _t.Card;
            public override Color ImageMarginGradientEnd => _t.Card;
            public override Color MenuBorder => _t.Border;
        }
    }
}
}