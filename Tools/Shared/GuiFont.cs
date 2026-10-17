// ReOmnimo 工具共享层（第六部分）：AutoIt 默认 GUI 字体
//
// AutoIt 的 GUICreate 不给字体参数时用的是系统 GUI 字体，也就是 GDI 的库存对象 DEFAULT_GUI_FONT
// （简中系统解析成「宋体 9pt / lfHeight=-12 / charset=134」）。WinForms 里看起来等价的两个东西都不是它：
//   · SystemFonts.MessageBoxFont → 本机是 Microsoft YaHei UI（字面更高更宽，同样宽度的标签会多截掉一个字）
//   · Control.DefaultFont       → 同上
// 所以直接取同一个库存对象，让 7 个工具的文字外观与原版 AutoIt 界面对齐（LOGFONT 实测一致：
// face=宋体 height=-12 weight=400 charset=134 quality=0）。
//
// 注意：换字体时务必把容器的 AutoScaleMode 设成 None——WinForms 默认按 AutoScaleMode.Font 走，
// 设置 Font 会触发一次「按新旧字体高度比」的整表缩放（实测把 340x438 的窗口缩成 283x365）。
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OmnimoTools
{
    /// <summary>与原版 AutoIt 界面同源的 GUI 字体。</summary>
    internal static class GuiFont
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int index);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetObjectW")]
        private static extern int GetObject(IntPtr handle, int size, ref LogFont logFont);

        private const int DefaultGuiFontStock = 17;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LogFont
        {
            public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
            public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        private static Font _cached;

        /// <summary>系统 GUI 字体（取不到时退回消息框字体，家族可能不同但不致命）。</summary>
        internal static Font Default
        {
            get
            {
                if (_cached != null) return _cached;
                try
                {
                    IntPtr handle = GetStockObject(DefaultGuiFontStock);
                    var lf = new LogFont();
                    if (handle != IntPtr.Zero && GetObject(handle, Marshal.SizeOf(typeof(LogFont)), ref lf) != 0)
                        _cached = Font.FromLogFont(lf);
                }
                catch
                {
                    // 落到下面的兜底字体
                }
                if (_cached == null) _cached = SystemFonts.MessageBoxFont;
                return _cached;
            }
        }
    }
}
