// ReOmnimo 工具共享层（第四部分）：_Zip.au3 的等价物。
// 按源码链接进各工具 exe，不产出独立 DLL。
//
// 只实现 PanelCreator 真正用到的两个调用点，其余 _Zip_* 不搬：
//   _Zip_Create($file, $overwrite)          → Create
//   _Zip_AddItem($zip, $item, $destDir, ...) → AddItem
//
// 原 UDF 是拿 Shell.Application 的 NameSpace.CopyHere 借「压缩文件夹」写 zip；
// C# 侧改用 System.IO.Compression.ZipArchive 直接写同样结构的 zip。
// 之所以不模拟 Shell 那一套：CopyHere 是异步的，原 UDF 要轮询 ParseName 等条目出现，
// 且写出来的条目顺序不确定；.rmskin 的消费方（Rainmeter）按条目名读取，
// 因此条目名与层次一致即可，容器字节与顺序本来就没被比较（见移植规格 §7）。

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace OmnimoTools
{
    internal static class ZipWriter
    {
        /// <summary>
        /// 等价于 _Zip_Create($file, $overwrite)：目标已存在且 $overwrite 为假时失败；
        /// 否则写一个 22 字节的空 zip 尾记录（原 UDF 就是这么建的「空压缩文件夹」）。
        /// </summary>
        public static bool Create(string file, bool overwrite)
        {
            if (string.IsNullOrEmpty(file)) return false;
            if (File.Exists(file) && !overwrite) return false;

            var emptyEndOfCentralDirectory = new byte[]
            {
                0x50, 0x4B, 0x05, 0x06, // 签名 "PK\5\6"
                0x00, 0x00,             // 本磁盘号
                0x00, 0x00,             // 中央目录起始磁盘号
                0x00, 0x00,             // 本磁盘条目数
                0x00, 0x00,             // 条目总数
                0x00, 0x00, 0x00, 0x00, // 中央目录大小
                0x00, 0x00, 0x00, 0x00, // 中央目录偏移
                0x00, 0x00              // 注释长度
            };
            try
            {
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(file, emptyEndOfCentralDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 等价于 _Zip_AddItem($zip, $item, $destDir = "", $flag = 21)：
        /// 要求 zip 与 item 都是全路径（原 UDF 用字符串截取拼绝对路径，相对路径会算错），
        /// 且 item 必须存在；条目名取 item 的文件名部分（根目录），目录则整树加入。
        /// $flag 是 Shell 的 CopyHere 选项，对直接写 zip 没有对应语义，保留参数只为对齐调用点。
        /// </summary>
        public static bool AddItem(string zipFile, string item, string destDir = "", int flag = 21)
        {
            if (string.IsNullOrEmpty(zipFile) || string.IsNullOrEmpty(item)) return false;
            if (!Path.IsPathRooted(zipFile) || !Path.IsPathRooted(item)) return false;
            if (!File.Exists(zipFile)) return false;

            bool isDirectory = Directory.Exists(item);
            if (!isDirectory && !File.Exists(item)) return false;

            string root = Path.GetFileName(item);
            if (!string.IsNullOrEmpty(destDir)) root = destDir.TrimEnd('/', '\\') + "/" + root;

            try
            {
                using (var stream = new FileStream(zipFile, FileMode.Open, FileAccess.ReadWrite))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    if (isDirectory) AddDirectoryTree(archive, item, root);
                    else AddFile(archive, item, root);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 递归加入目录：目录本身也写一个以 "/" 结尾的条目（Shell 的压缩文件夹就是这么建的），
        /// 再按名字升序写子目录与文件，保证多次生成的条目顺序稳定。
        /// </summary>
        private static void AddDirectoryTree(ZipArchive archive, string directory, string entryRoot)
        {
            archive.CreateEntry(entryRoot.TrimEnd('/') + "/");

            var children = new List<string>(Directory.GetDirectories(directory));
            children.Sort(StringComparer.Ordinal);
            foreach (string child in children) AddDirectoryTree(archive, child, entryRoot.TrimEnd('/') + "/" + Path.GetFileName(child));

            var files = new List<string>(Directory.GetFiles(directory));
            files.Sort(StringComparer.Ordinal);
            foreach (string file in files) AddFile(archive, file, entryRoot.TrimEnd('/') + "/" + Path.GetFileName(file));
        }

        private static void AddFile(ZipArchive archive, string file, string entryName)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            entry.LastWriteTime = File.GetLastWriteTime(file);
            using (Stream target = entry.Open())
            using (var source = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                source.CopyTo(target);
            }
        }
    }
}
