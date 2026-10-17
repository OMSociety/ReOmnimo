// ReOmnimo 工具共享层（第三部分）：IconImage.au3（Ward 的 IconImage UDF）的等价物。
// 按源码链接进各工具 exe，不产出独立 DLL。
//
// 与原 UDF 的对应关系：
//   _IconImage_FromPath    → FromPath        从任意路径取图标（.lnk/.exe/.ico/文件夹/关联图标）
//   _IconImage_FromPEEx    → FromPeEx        从 PE 文件按 "文件,索引" 取图标
//   _IconImage_FromPE      → FromPe          从 PE 文件的资源组按序号/资源 ID 取图标
//   _IconImage_FromIcoFile → FromIcoFile     从 .ico 文件取图标
//   _IconImage_FromHandle  → FromHandle      从 HICON 取图标
//   _IconImage_ToImageFile → SavePng         把图标写成 PNG
//
// 原 UDF 内部用「DIB 字节串或 PNG 原始字节」当中间表示（因为要跟 HICON/资源字节互转），
// C# 侧直接用 System.Drawing.Bitmap 承载同样的内容：Bitmap 就是 32bpp 位图，
// 中间表示上没有可观测差异，而 .NET 已有成熟的位图运算，重复造字节串只会引入新错误。
// 只有「从资源字节构造位图」这一段保留原 UDF 的做法（32bpp 直接绑 Scan0 + 垂直翻转），
// 因为 GDI+ 对 HICON 的支持在原作者注释里被明确否掉了。

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OmnimoTools
{
    internal static class IconExtractor
    {
        // ---- _IconImage_Merge 的对齐位掩码（IconImage.au3 第 68-77 行）----
        private const int AlignTop = 1;
        private const int AlignMiddle = 2;
        private const int AlignBottom = 4;
        private const int AlignLeft = 8;
        private const int AlignCenter = 16;
        private const int AlignRight = 32;
        private const int AlignBottomLeft = AlignBottom | AlignLeft;

        // ---- _IconImage_Scale 的插值模式（$GDIP_ModeHighQuality = 2 = GDI+ InterpolationModeHighQuality）----
        private const int InterpolationHighQuality = 2;

        private const int RtIcon = 3;
        private const int RtGroupIcon = 14;
        private const uint LoadLibraryAsDataFile = 0x00000002;

        private const int AssocStrExecutable = 2;
        private const int AssocStrDefaultIcon = 15;

        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiIconLocation = 0x000000010;
        private const uint ShgfiShellIconSize = 0x000000004;

        /// <summary>
        /// 等价于 _IconImage_FromPath($Path, $Width)：先解析 .lnk 目标，再按「文件夹 / 普通文件」两条路
        /// 找图标来源，最后按 PE → ICO → 关联 exe → Unknown 默认图标 的顺序逐个兜底。
        /// 失败返回 null（对应原 UDF 的 @error=1 与空 Binary）。
        /// </summary>
        /// <param name="isLink">输出：来源是 .lnk（需要叠快捷方式角标）</param>
        public static Bitmap FromPath(string path, int width, int height, out bool isLink)
        {
            isLink = false;
            if (string.IsNullOrEmpty(path)) return null;

            string iconFile = null;
            int? iconIndex = null;
            bool isSelf = false;
            string ext = ExtensionOf(path);

            // 1) .lnk：换成目标路径。失败时保持原路径继续往下走（原 UDF 的 If Not @Error 分支）
            if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string[] link = ShellLink.Resolve(path);
                if (link != null)
                {
                    path = link[0];
                    ext = ExtensionOf(path);
                    isLink = true;
                }
            }

            Bitmap result = null;
            if (Directory.Exists(path))
            {
                // 2) 文件夹：SHGFI_ICONLOCATION 一般能直接给出 (图标文件, 索引)
                var info = new SHFILEINFO();
                IntPtr ok = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), ShgfiIconLocation);
                if (ok != IntPtr.Zero)
                {
                    iconFile = info.szDisplayName;
                    iconIndex = info.iIcon;
                }

                if (string.IsNullOrEmpty(iconFile))
                {
                    // 取不到图标位置（例如字体文件夹）：退回 SHGFI_ICON 拿 HICON 再转位图
                    info = new SHFILEINFO();
                    ok = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), ShgfiIcon | ShgfiShellIconSize);
                    if (ok != IntPtr.Zero && info.hIcon != IntPtr.Zero)
                    {
                        result = FromHandle(info.hIcon);
                        NativeDestroyIcon(info.hIcon);
                    }
                    // 再不行就用 shell32.dll 的文件夹图标兜底
                    iconFile = "shell32.dll";
                    iconIndex = 3;
                }
            }
            else
            {
                // 3) 普通文件：从扩展名关联里找默认图标，其次找关联的可执行文件
                string iconPath = AssocQueryString(ext, AssocStrDefaultIcon);
                if (string.IsNullOrEmpty(iconPath)) iconPath = AssocQueryString(ext, AssocStrExecutable);

                if (iconPath == "%1" || iconPath == "\"%1\"")
                {
                    // 关联自己就是该文件（exe/msc/xml 等），索引 0
                    iconFile = path;
                    iconIndex = 0;
                    isSelf = true;
                }
                else
                {
                    iconFile = iconPath;
                    iconIndex = null;
                }
            }

            bool vista = VistaOrLater;

            // 4) shell32.dll 里的经典图标在 Vista 之后被搬到 imageres.dll，按原 UDF 的替换表处理
            if (result == null && vista && !string.IsNullOrEmpty(iconFile))
            {
                string fileName = FileNamePart(iconFile);
                if (fileName == "shell32.dll,2" || (fileName == "shell32.dll" && iconIndex == 2))
                    result = FromPe("imageres.dll", 11, width, height);
                else if (fileName == "shell32.dll,0" || (fileName == "shell32.dll" && iconIndex == 0))
                    result = FromPe("imageres.dll", 2, width, height);
                else if (fileName == "shell32.dll,3" || (fileName == "shell32.dll" && iconIndex == 3))
                    result = FromPe("imageres.dll", 3, width, height);
            }

            // 5) 先当 PE 资源取；取不到再当 .ico 文件取
            if (result == null && !string.IsNullOrEmpty(iconFile))
            {
                result = iconIndex.HasValue
                    ? FromPe(iconFile, iconIndex.Value, width, height)
                    : FromPeEx(iconFile, width, height);
            }
            if (result == null && !string.IsNullOrEmpty(iconFile))
            {
                result = FromIcoFile(iconFile, width, height, null);
            }

            // 6) 图标来源是文件自身却取不到（关联 exe 没有图标资源）：退到关联的可执行文件
            if (result == null && isSelf && ext != ".ico")
            {
                string exeFile = AssocQueryString(ext, AssocStrExecutable);
                if (exeFile == "%1" || exeFile == "\"%1\"")
                {
                    if (vista) result = FromPe("imageres.dll", 11, width, height);
                    if (result == null) result = FromPe("shell32.dll", 2, width, height);
                }
                else if (!string.IsNullOrEmpty(exeFile))
                {
                    result = FromPe(exeFile, 0, width, height);
                }
            }

            // 7) 彻底取不到：Unknown 类型的默认图标
            if (result == null)
            {
                iconFile = AssocQueryString("Unknown", AssocStrDefaultIcon);
                if (vista && FileNamePart(iconFile) == "shell32.dll,0")
                    result = FromPe("imageres.dll", 2, width, height);
                if (result == null && !string.IsNullOrEmpty(iconFile)) result = FromPeEx(iconFile, width, height);
                if (result == null) result = FromPe("shell32.dll", 0, width, height);
            }

            // 8) 快捷方式角标：imageres.dll,154 缩到 (W/1.5, H/1.5) 后贴到左下角
            if (result != null && isLink && vista)
            {
                int linkW = (int)(width / 1.5);
                int linkH = (int)(height / 1.5);
                Bitmap mark = FromPeEx("imageres.dll,154", linkW, linkH);
                if (mark != null)
                {
                    Bitmap scaled = Scale(mark, linkW, linkH, InterpolationHighQuality);
                    mark.Dispose();
                    if (scaled != null)
                    {
                        Bitmap merged = Merge(result, scaled, AlignBottomLeft);
                        scaled.Dispose();
                        if (merged != null)
                        {
                            result.Dispose();
                            result = merged;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 等价于 _IconImage_FromPEEx($IconFile, $Width)：支持 "文件,索引" 形式，
        /// 没有索引时用 0；解析失败也按 0 处理（与原 UDF 的 @error 分支一致）。
        /// </summary>
        public static Bitmap FromPeEx(string iconFile, int width, int height)
        {
            string file;
            int index;
            if (ParseIconLocation(iconFile, out file, out index)) return FromPe(file, index, width, height);
            return FromPe(iconFile, 0, width, height);
        }

        /// <summary>
        /// 等价于 _IconImage_FromPE($IconPE, $Index, $Width, $Height)：
        /// 以 LOAD_LIBRARY_AS_DATAFILE 加载模块，$Index >= 0 取第 $Index+1 个 RT_GROUP_ICON（按枚举顺序），
        /// $Index &lt; 0 取资源 ID = -$Index 的那一组；随后用 LookupIconIdFromDirectoryEx 挑最合适的一帧。
        /// </summary>
        public static Bitmap FromPe(string peFile, int index, int width, int height)
        {
            if (string.IsNullOrEmpty(peFile)) return null;

            IntPtr module = LoadLibraryEx(peFile, IntPtr.Zero, LoadLibraryAsDataFile);
            if (module == IntPtr.Zero) return null;
            try
            {
                IntPtr groupName;
                if (index >= 0)
                {
                    List<IntPtr> groups = EnumResourceIds(module, RtGroupIcon);
                    if (groups.Count < index + 1) return null;
                    groupName = groups[index];
                }
                else
                {
                    groupName = (IntPtr)(-index);
                }

                byte[] bytes = LoadIconBytes(module, groupName, width, height);
                return bytes == null ? null : DecodeIconBytes(bytes);
            }
            finally
            {
                FreeLibrary(module);
            }
        }

        /// <summary>
        /// 等价于 _IconImage_FromIcoFile($IconFile, $Width, $Index)：
        /// 支持 "xxx.ico,index" 形式的文件名；$Index 为空时用 LookupIconIdFromDirectoryEx 自动挑帧。
        /// </summary>
        public static Bitmap FromIcoFile(string iconFile, int width, int height, int? index)
        {
            if (string.IsNullOrEmpty(iconFile)) return null;

            if (!File.Exists(iconFile))
            {
                string file;
                int parsed;
                if (ParseIconLocation(iconFile, out file, out parsed))
                {
                    iconFile = file;
                    index = parsed;
                }
            }

            byte[] data;
            try
            {
                data = File.ReadAllBytes(iconFile);
            }
            catch
            {
                return null;
            }
            return DecodeIcoData(data, width, height, index);
        }

        /// <summary>
        /// 等价于 _IconImage_FromHandle($Handle)：从 HICON 取彩色位与掩码位，
        /// 有 alpha 通道就用 alpha，没有（高字节全 0）才用 1bpp 掩码补透明。
        /// </summary>
        public static Bitmap FromHandle(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;

            ICONINFO info;
            if (!GetIconInfo(hIcon, out info)) return null;
            try
            {
                Bitmap color = ReadHBitmap(info.hbmColor, false);
                if (color == null) return null;
                if (!HasAlpha(color))
                {
                    Bitmap mask = ReadHBitmap(info.hbmMask, true);
                    if (mask != null)
                    {
                        ApplyMask(color, mask);
                        mask.Dispose();
                    }
                }
                return color;
            }
            finally
            {
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
            }
        }

        /// <summary>
        /// 等价于 _IconImage_FromGdipBitmap + _IconImage_ToImageFile(..., "PNG")：
        /// 把图标内容直接存成 PNG。原 UDF 走 GDI+ 编码器，这里用 .NET 的 Bitmap.Save(Png)，
        /// 输出都是同一个 GDI+ PNG 编码器的字节。
        /// </summary>
        public static void SavePng(Bitmap image, string file)
        {
            if (image == null || string.IsNullOrEmpty(file)) return;
            image.Save(file, ImageFormat.Png);
        }

        /// <summary>等价于 $_IconImage_VistaFlag（GetVersionEx 主版本 >= 6）。</summary>
        private static bool VistaOrLater
        {
            get { return Environment.OSVersion.Version.Major >= 6; }
        }

        // ------------------------------------------------------------------
        // ICO / 资源字节 → 位图
        // ------------------------------------------------------------------

        /// <summary>
        /// 等价于 _IconImage_FromIcoData：校验资源头 (Identify == 0x10000)；
        /// $Index 为空时构造「伪资源组」交给 LookupIconIdFromDirectoryEx 挑帧（原 UDF 的做法），
        /// 否则直接按 16 字节一项取第 $Index 帧（负值从末尾数）。
        /// </summary>
        private static Bitmap DecodeIcoData(byte[] data, int width, int height, int? index)
        {
            if (data == null || data.Length < 6) return null;
            if (ReadUInt16(data, 0) != 0 || ReadUInt16(data, 2) != 1) return null; // Identify != 0x10000

            int count = ReadUInt16(data, 4);
            if (count <= 0 || data.Length < 6 + 16 * count) return null;

            int[] sizes = new int[count];
            int[] offsets = new int[count];
            var entry = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                int at = 6 + 16 * i;
                sizes[i] = ReadInt32(data, at + 8);
                offsets[i] = ReadInt32(data, at + 12);
                entry[i] = new byte[12];
                Buffer.BlockCopy(data, at, entry[i], 0, 12);
            }

            int chosen;
            if (index.HasValue)
            {
                chosen = index.Value;
                if (chosen < 0) chosen = count + chosen;
                if (chosen < 0 || chosen >= count) return null;
            }
            else
            {
                // 伪资源组：6 字节头 + 每帧 12 字节 + word(序号 1 基)
                var group = new byte[6 + 14 * count];
                group[2] = 1; // type = 1（icon），前两个字节保留字段保持 0
                group[0] = 0; group[1] = 0;
                group[2] = 1; group[3] = 0;
                group[4] = (byte)(count & 0xFF); group[5] = (byte)((count >> 8) & 0xFF);
                for (int i = 0; i < count; i++)
                {
                    Buffer.BlockCopy(entry[i], 0, group, 6 + 14 * i, 12);
                    group[6 + 14 * i + 12] = (byte)((i + 1) & 0xFF);
                    group[6 + 14 * i + 13] = (byte)(((i + 1) >> 8) & 0xFF);
                }

                chosen = LookupIconIdFromDirectoryExPinned(group, true, width, height, 0);
                if (chosen == 0 || chosen > count) return null;
                chosen -= 1;
            }

            if (offsets[chosen] < 0 || sizes[chosen] <= 0) return null;
            if (data.Length < offsets[chosen] + sizes[chosen]) return null;

            var slice = new byte[sizes[chosen]];
            Buffer.BlockCopy(data, offsets[chosen], slice, 0, slice.Length);
            return DecodeIconBytes(slice);
        }

        /// <summary>把一段图标资源字节转成位图：PNG 帧直接解码，32bpp DIB 绑 Scan0 后垂直翻转，其余套一层 .ico 交给 GDI+。</summary>
        private static Bitmap DecodeIconBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return null;

            if (IsPng(bytes))
            {
                try
                {
                    using (var stream = new MemoryStream(bytes))
                    using (Image image = Image.FromStream(stream))
                    {
                        return new Bitmap(image);
                    }
                }
                catch
                {
                    return null;
                }
            }

            if (bytes.Length < 40) return null;
            int bitCount = ReadUInt16(bytes, 14);
            int w = ReadInt32(bytes, 4);
            int h = ReadInt32(bytes, 8) / 2; // 图标 DIB 的高度含掩码位，是真实高度的两倍
            if (w <= 0 || h <= 0) return null;

            if (bitCount == 32)
            {
                // 对应 _IconImage_32BitAlphaToGdipBitmap：从 header 之后的 Scan0 建位图，
                // 再用 GdipImageRotateFlip(6) 翻正（GDI+ 的 6 = Rotate180FlipX = 垂直翻转）
                int stride = 4 * w;
                if (bytes.Length < 40 + stride * h) return null;
                var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy(bytes, 40 + y * stride, data.Scan0 + y * data.Stride, stride);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipX);
                return bitmap;
            }

            // 非 32bpp：原 UDF 会套回 .ico 容器再交给 GDI+ 解码（_IconImage_ToIcoData 兜底），这里同样处理
            return DecodeIcoContainer(WrapAsIco(bytes, w, h, bitCount));
        }

        /// <summary>用 .NET 的 Icon 解码器读单帧 .ico 容器（GDI+ 对 1/4/8/24bpp 图标的解码路径）。</summary>
        private static Bitmap DecodeIcoContainer(byte[] icoBytes)
        {
            try
            {
                using (var stream = new MemoryStream(icoBytes))
                using (var icon = new Icon(stream))
                {
                    return icon.ToBitmap();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>等价于 _IconImage_ToIcoData：把单独的图标 DIB 包成只有一个条目的 .ico 字节流。</summary>
        private static byte[] WrapAsIco(byte[] dib, int width, int height, int bitCount)
        {
            var ico = new byte[6 + 16 + dib.Length];
            ico[2] = 1; ico[3] = 0;   // type = 1 (icon)
            ico[4] = 1; ico[5] = 0;   // count = 1
            ico[6] = (byte)(width >= 256 ? 0 : width);
            ico[7] = (byte)(height >= 256 ? 0 : height);
            ico[8] = 0;               // 调色板数（>=8bpp 恒为 0）
            ico[9] = 0;
            ico[10] = 1; ico[11] = 0; // planes
            ico[12] = (byte)(bitCount & 0xFF); ico[13] = (byte)((bitCount >> 8) & 0xFF);
            WriteInt32(ico, 14, dib.Length);
            WriteInt32(ico, 18, 22);
            Buffer.BlockCopy(dib, 0, ico, 22, dib.Length);
            return ico;
        }

        private static bool IsPng(byte[] bytes)
        {
            return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                   && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        }

        // ------------------------------------------------------------------
        // PE 资源
        // ------------------------------------------------------------------

        /// <summary>枚举模块里某个资源类型下的整数 ID（RT_GROUP_ICON 都是整数 ID；命名资源原 UDF 也处理不了索引访问）。</summary>
        private static List<IntPtr> EnumResourceIds(IntPtr module, int type)
        {
            var ids = new List<IntPtr>();
            EnumResourceNamesProc callback = delegate (IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam)
            {
                if (lpName.ToInt64() >= 0 && lpName.ToInt64() <= 0xFFFF) ids.Add(lpName);
                return true;
            };
            EnumResourceNames(module, (IntPtr)type, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return ids;
        }

        /// <summary>等价于 _IconImage_FromGroupId：取资源组 → LookupIconIdFromDirectoryEx → 取 RT_ICON 原始字节。</summary>
        private static byte[] LoadIconBytes(IntPtr module, IntPtr groupName, int width, int height)
        {
            IntPtr resource = FindResource(module, groupName, (IntPtr)RtGroupIcon);
            if (resource == IntPtr.Zero) return null;
            IntPtr handle = LoadResource(module, resource);
            if (handle == IntPtr.Zero) return null;
            IntPtr pointer = LockResource(handle);
            if (pointer == IntPtr.Zero) return null;

            int iconId = LookupIconIdFromDirectoryEx(pointer, true, width, height, 0);
            if (iconId == 0) return null;

            IntPtr iconResource = FindResource(module, (IntPtr)iconId, (IntPtr)RtIcon);
            if (iconResource == IntPtr.Zero) return null;
            uint size = SizeofResource(module, iconResource);
            IntPtr iconHandle = LoadResource(module, iconResource);
            IntPtr iconPointer = iconHandle == IntPtr.Zero ? IntPtr.Zero : LockResource(iconHandle);
            if (size == 0 || iconPointer == IntPtr.Zero) return null;

            var bytes = new byte[size];
            Marshal.Copy(iconPointer, bytes, 0, (int)size);
            return bytes;
        }

        // ------------------------------------------------------------------
        // 位图工具（对应 _IconImage_Scale / _IconImage_Merge）
        // ------------------------------------------------------------------

        /// <summary>等价于 _IconImage_Scale($img, $W, $H, $GDIP_ModeHighQuality)。</summary>
        private static Bitmap Scale(Bitmap source, int width, int height, int quality)
        {
            if (source == null || width <= 0 || height <= 0) return null;
            var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(target))
            {
                graphics.InterpolationMode = (System.Drawing.Drawing2D.InterpolationMode)quality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, width, height));
            }
            return target;
        }

        /// <summary>
        /// 等价于 _IconImage_Merge($a, $b, $Align)：按对齐位算 (X, Y)，把 $b 叠加到 $a 上（GDI+ 默认 alpha 混合）。
        /// 返回新位图，$a 不被修改。
        /// </summary>
        private static Bitmap Merge(Bitmap under, Bitmap over, int align)
        {
            if (under == null || over == null) return null;
            int x = 0, y = 0;
            if ((align & AlignLeft) != 0) x = 0;
            else if ((align & AlignRight) != 0) x = under.Width - over.Width;
            else if ((align & AlignCenter) != 0) x = (under.Width - over.Width) / 2;

            if ((align & AlignTop) != 0) y = 0;
            else if ((align & AlignBottom) != 0) y = under.Height - over.Height;
            else if ((align & AlignMiddle) != 0) y = (under.Height - over.Height) / 2;

            var target = new Bitmap(under.Width, under.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(target))
            {
                graphics.DrawImage(under, 0, 0);
                graphics.DrawImage(over, x, y);
            }
            return target;
        }

        private static bool HasAlpha(Bitmap bitmap)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    IntPtr row = data.Scan0 + y * data.Stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (Marshal.ReadByte(row, x * 4 + 3) != 0) return true;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return false;
        }

        /// <summary>掩码位为 1 处（GetDIBits 出来是白色）视为透明，对应 _IconImage_CreateMaskBmp 的语义。</summary>
        private static void ApplyMask(Bitmap color, Bitmap mask)
        {
            BitmapData colorData = color.LockBits(new Rectangle(0, 0, color.Width, color.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            BitmapData maskData = mask.LockBits(new Rectangle(0, 0, mask.Width, mask.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int w = Math.Min(color.Width, mask.Width);
                int h = Math.Min(color.Height, mask.Height);
                for (int y = 0; y < h; y++)
                {
                    IntPtr colorRow = colorData.Scan0 + y * colorData.Stride;
                    IntPtr maskRow = maskData.Scan0 + y * maskData.Stride;
                    for (int x = 0; x < w; x++)
                    {
                        if (Marshal.ReadByte(maskRow, x * 4) != 0) Marshal.WriteByte(colorRow, x * 4 + 3, 0);
                        else Marshal.WriteByte(colorRow, x * 4 + 3, 255);
                    }
                }
            }
            finally
            {
                mask.UnlockBits(maskData);
                color.UnlockBits(colorData);
            }
        }

        /// <summary>把 HBITMAP 读成 32bpp 位图（GetDIBits 一律按 32bpp 请求，GDI 会做转换）。</summary>
        private static Bitmap ReadHBitmap(IntPtr hBitmap, bool isMask)
        {
            if (hBitmap == IntPtr.Zero)
            {
                if (!isMask) return null;
                return null;
            }

            var info = new BITMAP();
            if (GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref info) == 0) return null;
            int w = info.bmWidth;
            int h = info.bmHeight;
            if (w <= 0 || h <= 0) return null;

            var header = new BITMAPINFO();
            header.bmiHeader.biSize = 40;
            header.bmiHeader.biWidth = w;
            header.bmiHeader.biHeight = -h; // 负数 = 自顶向下，省一次翻转
            header.bmiHeader.biPlanes = 1;
            header.bmiHeader.biBitCount = 32;
            header.bmiHeader.biCompression = 0;

            var buffer = new byte[w * h * 4];
            IntPtr dc = GetDC(IntPtr.Zero);
            try
            {
                if (GetDIBits(dc, hBitmap, 0, (uint)h, buffer, ref header, 0) == 0) return null;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }

            var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++) Marshal.Copy(buffer, y * w * 4, data.Scan0 + y * data.Stride, w * 4);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            if (isMask) return bitmap;

            // GetDIBits 的 32bpp 输出是 BGRA，与 Format32bppArgb 的内存布局一致
            return bitmap;
        }

        // ------------------------------------------------------------------
        // 路径与关联查询
        // ------------------------------------------------------------------

        /// <summary>等价于 _WinAPI_PathParseIconLocation：解析 "文件,索引"；没有索引时返回 false。</summary>
        private static bool ParseIconLocation(string value, out string file, out int index)
        {
            file = null;
            index = 0;
            if (string.IsNullOrEmpty(value)) return false;

            string text = value.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"') text = text.Substring(1, text.Length - 2);

            int comma = text.LastIndexOf(',');
            if (comma <= 0) return false;

            string tail = text.Substring(comma + 1).Trim();
            int parsed;
            if (!int.TryParse(tail, System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed)) return false;

            file = text.Substring(0, comma).Trim();
            index = parsed;
            return file.Length > 0;
        }

        /// <summary>等价于 _WinAPI_PathFindFileName：只按反斜杠取文件名部分（形如 "shell32.dll,2" 的原样返回）。</summary>
        private static string FileNamePart(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int slash = path.LastIndexOf('\\');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        private static string ExtensionOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOfAny(new[] { '\\', '/' });
            if (dot <= slash) return string.Empty;
            return path.Substring(dot).ToLowerInvariant();
        }

        /// <summary>等价于 _WinAPI_AssocQueryString($assoc, $type)，取不到返回空串。</summary>
        private static string AssocQueryString(string assoc, int type)
        {
            if (string.IsNullOrEmpty(assoc)) return string.Empty;
            int length = 1024;
            var buffer = new StringBuilder(length);
            int hr = AssocQueryStringW(0, type, assoc, null, buffer, ref length);
            if (hr != 0) return string.Empty;
            return buffer.ToString();
        }

        private static int LookupIconIdFromDirectoryExPinned(byte[] data, bool isIcon, int width, int height, uint flags)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return LookupIconIdFromDirectoryEx(handle.AddrOfPinnedObject(), isIcon, width, height, flags);
            }
            finally
            {
                handle.Free();
            }
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        // ------------------------------------------------------------------
        // Win32 互操作
        // ------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            // 只按 32bpp 请求，调色板不参与，故不声明 RGBQUAD 数组
        }

        private delegate bool EnumResourceNamesProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResourceNamesProc callback, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr data);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr module, IntPtr resource);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int LookupIconIdFromDirectoryEx(IntPtr directory, bool isIcon, int cx, int cy, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static void NativeDestroyIcon(IntPtr hIcon)
        {
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetObject(IntPtr handle, int size, ref BITMAP data);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFO info, uint usage);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int AssocQueryStringW(int flags, int str, string assoc, string extra, StringBuilder outBuffer, ref int outBufferSize);
    }
}
