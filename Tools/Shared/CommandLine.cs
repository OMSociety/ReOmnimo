// ReOmnimo 工具共享层：命令行拆分
// 与 OmnimoShared.cs 一样按源码链接（<Compile Include>）编进每个工具 exe，不产出独立 DLL。
//
// 为什么需要这个文件：
//   Rainmeter 展开 #SETTINGSPATH# / #SKINSPATH# / #PROGRAMPATH# 后，值都以反斜杠结尾，
//   皮肤侧又一律写成 "变量" 形式，于是命令行里出现 `"C:\...\Rainmeter\"`。
//   C 运行库（CommandLineToArgvW / .NET 的 string[] args）把 `\"` 解释成**字面双引号**，
//   于是这个引号不再闭合，后续参数被并进同一项——6 参调用点只解析出 4 个。
//   AutoIt 的 $CmdLine 不做这层转义：双引号只切换「在引号内」状态，反斜杠原样保留。
//   实测（同一命令行、尾反斜杠）：AutoIt $CmdLine 得 5 项且保留尾反斜杠，C# args 得 4 项。
//   0.7.0 把工具从 AutoIt 迁到 C# 后，全部 6 参调用点因此失效（弹「参数不足」或静默走错分支）。
//
// 因此这里按 AutoIt 的规则重新拆分原始命令行，而不是使用 string[] args。
// 规则（逐条实测于 AutoIt3）：
//   · 双引号只切换「在引号内」状态，自身不进入结果；引号内的一切字符（含空格）原样保留。
//   · 反斜杠**没有**转义含义：`"C:\a\"` 是 `C:\a\`，不是 `C:\a"`。
//   · 引号外遇空格即结束当前参数；引号外遇到非空格字符继续累加（`x"y z"w` → `xy zw`）。
//   · `""` 产生一个空参数。
using System;

namespace OmnimoTools
{
    internal static class CmdLine
    {
        /// <summary>
        /// 取当前进程的命令行参数，语义等价于 AutoIt 的 `$CmdLine[1..$CmdLine[0]]`。
        /// 替代 `Main(string[] args)` 收到的 args：后者按 C 运行库规则拆分，
        /// 会把以反斜杠结尾的带引号路径与其后的参数合并。
        /// </summary>
        public static string[] Get()
        {
            return Get(Environment.CommandLine);
        }

        /// <summary>拆分一条完整命令行（首项为程序自身路径，会被丢弃）。</summary>
        public static string[] Get(string rawCommandLine)
        {
            string[] all = Split(rawCommandLine);
            int first = 0;
            // 正常情况下首项就是程序路径。若启动方没有给含空格的程序路径加引号
            // （首项被空格切断，不以 .exe 结尾），则跳到首个以 .exe 结尾的片段之后。
            if (all.Length > 0 && !EndsWithExe(all[0]))
            {
                for (int i = 1; i < all.Length; i++)
                {
                    if (EndsWithExe(all[i])) { first = i; break; }
                }
            }
            if (all.Length <= first + 1) return new string[0];
            var rest = new string[all.Length - first - 1];
            Array.Copy(all, first + 1, rest, 0, rest.Length);
            return rest;
        }

        private static bool EndsWithExe(string s)
        {
            return s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>按 AutoIt 的 `$CmdLine` 规则拆分命令行（含首项）。</summary>
        public static string[] Split(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new string[0];

            var result = new System.Collections.Generic.List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            bool started = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '"')
                {
                    // 双引号只切换状态，自身不入结果（不做 \" 转义）
                    inQuotes = !inQuotes;
                    started = true;
                    continue;
                }
                if (!inQuotes && (c == ' ' || c == '\t'))
                {
                    if (started)
                    {
                        result.Add(current.ToString());
                        current.Length = 0;
                        started = false;
                    }
                    continue;
                }
                current.Append(c);
                started = true;
            }
            if (started) result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
