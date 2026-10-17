# AGENTS.md — ReOmnimo

适用范围：本仓库全部目录（单文件，无子目录级 AGENTS.md）。
最后更新：2026-10-18。

一句话定位：Rainmeter 皮肤套装 ReOmnimo，起于接手 fediaFedia/Omnimo，现已脱离上游独立演进，持续做现代化。

## 1 产品边界

- **乐见对旧代码的大幅度现代化改动。** 把 2013 年的 AutoIt 工具迁到 C#、把插件改成 Rainmeter 5.0 原生 measure、换用 SMTC 这类改造即此方向，属本职范围内的工作；不要为了「与上游保持一致」而保留过时实现，也不要以「上游就是这么写的」为由拒绝重构。判断标准是当前代码的正确性与可维护性，不是与 upstream 的差异大小。
- 设计层是**用户的既定设计理念，不是待还的技术债**：不重画视觉、不复刻面板、不改设计语言、不换字体。磁贴表面（面板内容）保持英文并按英文宽度排版；磁贴名走语言包（`Gallery\cat1..7.inc` 的 `Text="#键#"`）。不要以「现代化」为由改这四样。
- 「待办同步」用户已决定不做，不要实现。
- 发版：已发布 `0.5.0`、`0.4.0` 与历史遗留 `v0.3.0`（`v0.1.0` / `v0.2.0` 的 tag 与 Release 已删除，`CHANGELOG.md` 历史条目保留——**看到没有对应 tag 的旧条目是正常的**，不要补 tag）。**tag 名自 `0.4.0` 起不带 `v` 前缀**，已发布的 tag 不移动。发布号只活在 tag + `CHANGELOG.md` 标题 + GitHub Release 三处（外加 exe 版本资源）；仓库内 `Version=` 字段（`Rainstaller.cfg` 的 10.0.4、`OmnimoVersion` 10.0、`Settings` 6.0.1、`RMSKIN.inc` 1.0）属上游自有体系，不要动。动手前先向用户确认号码。
- 文档索引：`README.md`（英文）与 `README.zh.md`（中文）、`CHANGELOG.md`、`LICENSE`、`THIRD-PARTY.md`（含差异集逐文件清单）。改用户可见行为时同步两份 README。
- 仓库 `OMSociety/ReOmnimo`（2026-10-02 由 `OMSociety/Omnimo` 改名）。双 remote：`origin` = 本 fork，`upstream` = `fediaFedia/Omnimo`；`gh` 命令必须显式带 `-R OMSociety/ReOmnimo`，否则默认解析到 upstream。旧全名与 `raw.githubusercontent.com` 旧路径仍重定向（`gh api repos/OMSociety/Omnimo` 现查解析为 `OMSociety/ReOmnimo`），历史链接无需批量改写。`upstream` 仅作历史来源保留、**不作对照基准**——皮肤根 `WP7\` 改名 `REO\` 后全树分叉，与上游的 diff 计数、与上游的实现方式都不构成理由。
- 桌面映射：`%USERPROFILE%\Documents\Rainmeter\Skins\REO` 是指向本仓库 `REO\` 的 junction（现查已确认）。改仓库即改用户的桌面皮肤。

## 2 环境实况（本机）

| 项 | 实况 |
|---|---|
| 宿主 | Windows + `pwsh`；仓库根 `D:\WorkSpace\Github\ReOmnimo`，`git` 命令一律在此执行 |
| Rainmeter | 5.0，`C:\Program Files\Rainmeter\Rainmeter.exe`（**不在 PATH**，调用一律用完整路径）。5.0 测试版会自己跟进官方新 build，**版本号现查**：`(Get-Item 'C:\Program Files\Rainmeter\Rainmeter.dll').VersionInfo.FileVersion`（2026-10-16 现查 `5.0.0.5293`） |
| AutoIt | 3.3.18.0，`C:\Program Files (x86)\AutoIt3\Aut2Exe\Aut2exe.exe`；AutoIt3Wrapper 未安装 |
| .NET | `dotnet` SDK 3.1–11 已装；两个 C# 工程目标 `net48`，只用 `dotnet build -c Release` |
| CI / 测试 | **无 CI、无测试框架**，验收标准即 §8 |
| 暂存区 | 过程产物与助手脚本放 `D:\WorkSpace\WorkBench`，**不进仓库**。现存 ReOmnimo 相关：`_omni_set_logging2.py`（切 Rainmeter 日志开关）、`_omni_inventory.py`、`_reo_enc_patterns.py` / `_reo_lang.py`（§3 编码表与 §6 语言键数的重扫脚本）、`port_spec_4tools.md`（4 个工具的移植口径）、`omniapp_callsites.txt`、`cb_probe\` / `rt_cb\` / `rt_cb_new\` / `orig_tools\`（ConfigBackground 与 PanelCreator 的原版对照夹具、golden 截图与控件转储）、`_tool_shot.ps1` / `_win_probe.ps1` / `_dump_diff.ps1` / `_measure_window.au3` / `_frame_probe.au3` / `_updown_probe.au3` / `_font_probe.au3` / `_exe_loadcheck.ps1`（窗口截图与控件转储、外框/样式/字体/updown 探针、exe 可加载性自检）、`_omni_logcheck.ps1`（字节级开关 `Logging` 并比对开日志前后的 ERRO 形态）、`Rainmeter.ini.bak`（§10 BOM 事故的恢复资产）、`_reo_autolang_patch.py` / `_reo_autolang_fix.py` / `_reo_autolang_docs.py` / `_reo_autolang_roundtrip.ps1`（语言自动跟随的落地补丁、编码修复与实机验收，§6）。早期文档提到的 `_omni_build.ps1`、`_omni_vsver.py`、`_omni_loadcheck.ps1`、`_omni_tools_diff.ps1`、`_omni_fakerm\`、`_omni_bangtest\` **现已不存在**，用到时按本文描述重建，不要跳过自检 |
| 本机 git 附加物 | `.git/info/exclude`（本机忽略清单，未跟踪）、`.git/hooks/post-commit` 与 `post-checkout`（Qoder AI tracker，非仓库源码） |

## 3 常用命令

| 目的 | 命令 |
|---|---|
| 核对改动面（提交前必跑） | `git status --porcelain` 逐条确认与意图一致 |
| 枚举被 exclude 命中的已跟踪文件 | `git ls-files -ci --exclude-standard`（2026-10-16 现查 419 条） |
| 查 skip-worktree 标记 | `git ls-files -v` 行首 `S`（现查 5 条：`Config\Panels\Slideshow\UserVariables.inc`、`Config\Panels\WorldClock\UserVariables.inc`、`Config\TextItems\MultiManager\UserVariables.inc`、`Gallery\MultiManager\Saved\2\screenshot.png`、`Gallery\MultiManager\TimeSettings.inc`） |
| 找残留旧路径 | `git grep -In "wp7" -- 'REO/*'` |
| 刷新全部并重扫描 config | `& 'C:\Program Files\Rainmeter\Rainmeter.exe' !RefreshApp`（**等约 9 秒**再继续，重扫描未完成就 Activate 会找不到 config） |
| 激活单个面板 | `cmd /c '"C:\Program Files\Rainmeter\Rainmeter.exe" !ActivateConfig "REO\Panels\Agenda" "Item.ini"'`——pwsh 的 `&` 调用会吃掉带引号 bang 的内层引号（静默失效，2026-10-17 实测）；带引号参数的 bang 一律 `cmd /c` 包装，无参 bang（`!RefreshApp` / `!Quit`）`&` 直传即可 |
| 开日志 | 完整闭环：`!Quit` → **备份** `%APPDATA%\Rainmeter\Rainmeter.ini` → `python D:\WorkSpace\WorkBench\_omni_set_logging2.py 1 --apply` → 手动启动 Rainmeter → 复现 → 读日志抓 `ERRO` → `!Quit` → `0 --apply` 关回 → 重启。脚本自检 BOM / CRLF / section 数，不满足直接退出不写盘；**改完要手动重启 Rainmeter 才生效**。该 ini 是 UTF-16LE+BOM，但**行尾实测出现过纯 LF**，此时脚本会拒写——改用 `D:\WorkSpace\WorkBench\_omni_logcheck.ps1`（字节级只改 `Logging=` 后的那一个字符，BOM 与行尾原样保留，并自动开→跑工具→抓 ERRO→关回） |
| 构建设置中心 | `dotnet build SettingsCenter\SettingsCenter.csproj -c Release` |
| 构建面板工具 | `dotnet build Tools\ActivePanels\ActivePanels.csproj -c Release`（MultiManager、ColorChanger、ConfigBackground、PanelCreator、OmnimoApp 同理） |
| 还原被运行时写脏的文件 | `git checkout -- REO\Gallery\main.ini REO\Gallery\scroll.inc` |

改皮肤文件（`ini` / `inc` / `cfg` / `lua`）必须用编码感知的补丁脚本：二进制读入 → 按嗅探到的编码解码 → 替换 → 按**同一编码**写回且 `newline=""`。直接用文本工具写会把 `\r\n` 写成 `\r\r\n`（实测过）。嗅探顺序：UTF-16LE+BOM → UTF-8+BOM → **会抛异常的**严格 UTF-8 → cp936 → cp1252；纯 ASCII 文件会同时通过多种编码，另看是否含 `>= 0x80` 字节。控制台是 GBK，`print` 中文会抛 `UnicodeEncodeError`，结果写 UTF-8 文件再看。

**编码不能按目录或扩展名假定，逐个嗅探。** 2026-10-17 重扫 1574 个 `ini`/`inc`/`cfg`/`lua`：1451 个 UTF-8 无 BOM、93 个 UTF-16LE+BOM、20 个 cp1252、7 个 cp936、3 个 UTF-8+BOM。按模式看主流与例外：

| 模式（文件数） | 现查编码分布 |
|---|---|
| `Common\Variables\Languages\*.inc`（9） | **全部 UTF-16LE+BOM**——中文包在此 |
| `Panels\*\Item*.ini`（110） | UTF-8 无 BOM 95、cp1252 9（Weather / Speedfan 等）、UTF-16LE+BOM 6（Network / CoreTemp 等） |
| `Config\Panels\*\UserVariables.inc`（298） | UTF-8 无 BOM 283、UTF-16LE+BOM 14、cp936 1 |
| `Config\Panels\*\RainConfigure.cfg`（374） | UTF-8 无 BOM 371、UTF-16LE+BOM 1（Agenda）、UTF-8+BOM 1、cp936 1——**不是「cfg 一律 UTF-16」** |
| `Structure\*\Main.inc`（11）、`*.lua`（10） | 全部 UTF-8 无 BOM |
| `Gallery\cat*.inc`（7） | UTF-8 无 BOM 6；`cat7.inc` 是 UTF-16LE+BOM |

`Common\Variables\UserVariables.inc` 2026-10-17 起是 **UTF-8+BOM**（此前是库内独一份的 cp1252，含 3 个 `>= 0x80` 字节）：它是语言检测器 `!WriteKeyValue` 的写回目标，无 BOM 的 cp1252 会被 Rainmeter 按系统 ANSI 读写、高位字节变 `?` 且可能吞行尾引号（实测损伤过一行，转换修复并顺带修正上游 mojibake）——**会被运行时写回的 inc 一律带 BOM**。现存 cp1252 剩 `Common\Settings\` 死文件与 9 个 `Panels\*\Item*.ini`，以 `_reo_enc_patterns.py` 重扫为准。`Common\Background\Language\` 下 6 个 `.cfg`（含 `Chinese.cfg`）是 UTF-16LE+BOM，而 `English.cfg` / `Dutch.cfg` 是 UTF-8 无 BOM。cp936 与 cp1252 会互相误判（cp936 常把 cp1252 的高位字节读成合法双字节）。判不准时改用 **latin-1** 读写：256 个字节与码点一一对应，字节级无损，只替换其中的 ASCII 片段即可。另注意 **`git grep` 会把 UTF-16 文件当二进制跳过（加 `-I` 更彻底）**——在那 93 个 UTF-16 文件里 grep 不到东西，不等于内容不存在，先解码再查。

## 4 架构

```text
REO\                              皮肤根（桌面 junction 指到这里）
├─ Launcher.ini / LauncherDark.ini
├─ Panels\【名】\Item*.ini          62 个目录，60 个有 Item.ini（Donate、SkinsTest 没有）
├─ TextItems\【名】\*.ini           17 个
├─ Hubs\【名】\                     3 个（Alarm / RSSBurst / Time）
├─ Background\  Gallery\          背景与磁贴库
└─ @Resources\
   ├─ Common\                     共用资源 + 4 个分发 exe（ColorChanger / OmnimoApp / Background\ / PanelCreator\，全为 C#）+ Config\ 与 MultiManager\ 下的 C# 工具
   ├─ Config\Panels\【名】\         各面板的 UserVariables.inc 与 RainConfigure.cfg（现查磁盘 374 个 cfg）
   └─ Structure\【PanelType】\      13 档底板（Circle / Double / … / win10 / win7），各含 Main.inc
```

面板 ini 的 include 链（**编号升序生效，变量后写者胜**）：

```text
@include   Common\Variables\UserVariables.inc                 全局变量（MainLanguage 与 LangAuto 在此；`MainLanguage=Auto` 表示跟随系统语言，由 AutoLang.lua 落笔具体语种——见 §6）
@include1  Common\Variables\Languages\#MainLanguage#.inc      界面语言包
@include2  Common\color\color.inc                             主题（Padding / Opacity 唯一定义处）
@include3  Config\Panels\【名】\UserVariables.inc               面板用户参数
@include4  Structure\#PanelType#\Main.inc                     档位底板
@include5  Config\Panels\Agenda\UserVariables.local.inc       Agenda 专属，未跟踪
```

- 界面文案只走 `Variables\Languages\*.inc`；AutoIt 工具文案只走 `Common\Background\Language\*.cfg`。`AutoIT\Language\*.cfg` 是源码侧镜像，**运行时无读取点**——只改它不会改变任何界面。
- `Padding` / `Opacity` / `Opacity2` / `Globalblurenable` / `Xposition` 只由 `Common\Color\*.inc` 定义。面板库换主题会用主题文件**整体覆盖** `color.inc`，仓库里提交的默认值会被改回主题自带值——有意保留的边界。
- 磁贴表在 `REO\Gallery\cat1..7.inc`（7 个分类，`cat7.inc` 是 UTF-16LE+BOM）。每格 = 面板目录名 + `Text="#语言键#"`，动作是 `!ToggleConfig "REO\Panels\#CURRENTSECTION#" "Item.ini"`。
- 磁贴图标层是 `@Resources\Graphics\Gallery\mask-<类>.png`（4 个：contrib / essential / shortcut / textitems），由 `cat1` / `cat2` / `cat3` / `cat5` 引用（如 `cat1.inc:338` 引 `mask-essential.png`）。**增删或移动磁贴必须同步改对应图标层**（复制相邻字形，不要自画）。跨行回流靠行锚点交接：`Y=(1*#ScaleDpi#)R` + `x=(360*#ScaleDpi#)`（`cat1.inc:93-94`）。
- 工具取语言：读 `Common\Background\Varrar.inc` 的 `Language` → `Common\Background\Language\【Language】.cfg`（`Language` 由 AutoLang.lua 与 `MainLanguage` 一并写回；手动指定要在 `LangAuto=0` 时做，见 §6）。

## 5 当前状态：在途工作与待办

> **接手先读本节。** 2026-10-18，`master` HEAD `da7387b1`，工作区有未提交改动：七个 AutoIt 分发 exe 已移植为 C#、构建、部署、实机验收通过（七份全为 0.6.0.0），`wp7\` 死引用已修，源码与文档的提交尚未做。

在途（`git status --porcelain` 现查）：

| 项 | 状态 |
|---|---|
| `SettingsCenter\`（config.exe 的 C# 源，8 个文件） | **未跟踪**（`git ls-files SettingsCenter` 现查 0 条）；2026-10-18 修掉写盘缺陷（`Program.cs:406` 原把 value 直接拼进 `Replacement` 串，value 以数字开头时被解释成 `$1` 组引用，实测把 `UseMute=1` 写成 `$10` 并毁行），csproj 补 `Product=ReOmnimo` / `Company=Omnimo` / `Copyright` 对齐其余六份 |
| `Tools\`（30 个文件：ActivePanels / MultiManager / ColorChanger / ConfigBackground / PanelCreator / OmnimoApp 六个 net48 工程 + Shared（含 ModernUi.cs 共享界面层） + Icons） | **未跟踪** |
| `REO\@Resources\Common\Config\` 的 WebView2 部署件（3 个托管 DLL、`WebView2Loader.dll`、`config.exe.config`、`lang\`） | **未跟踪**，但 `config.exe` 已替换为 0.6.0.0——不补提交，干净检出里设置中心跑不起来 |
| `REO\@Resources\Common\Config\hue.ini` | 已暂存删除，`.gitignore` 已覆盖 |
| 七份 C# 分发 exe（`ColorChanger.exe` 99328、`OmnimoApp.exe` 67072、`Background\ConfigBackground.exe` 102400、`PanelCreator\PanelCreator.exe` 116224、`Config\ActivePanels.exe` 40448、`Config\config.exe` 78848、`MultiManager\MultiManager.exe` 38400 字节，均 0.6.0.0；2026-10-18 现查） | 已替换/已部署，属已跟踪文件的修改；`config.exe` 与 `ActivePanels`/`MultiManager` 为 2026-10-18 重建（前两份此前是旧代码或缺图标）；三个 GUI 工具又经窗口打磨（关闭键、占位提示、跨屏重排、深色取色框）后重建 |
| 语言自动跟随（新增 `Common\Variables\AutoLang.lua`、`Languages\Auto.inc`；改 `Common\Variables\UserVariables.inc`、`Common\Background\Varrar.inc`、`Background\bg.ini`、`Gallery\main.ini`） | 已实机验证（德语注入→自动写回中文、0 新增 ERRO）；`UserVariables.inc` 转 UTF-8+BOM（§3）；`Varrar.inc` 现查 `Language=Chinese`（= 本机系统语言，检测器写回） |
| 文案与文档修正 | 若干 `RainConfigure.cfg` 标题、`Slideshow\Item.ini` Height 196→195、`.gitignore`、`THIRD-PARTY.md`、`CHANGELOG.md`；Delicious / StumbleUpon 的面板配置已删 |

待办：

1. `CHANGELOG.md` 的 0.6.0 小节目前只有中文段，历史各版都有英文镜像（`Changed` / `Fixed`）——发版前补齐，否则发布页与正文只有中文。
2. 把 `CHANGELOG.md` 的 `[Unreleased]` 提到发布号，并按 §1 走 tag + Release（**先向用户确认号码**）。
3. 待决（尚未处理，均属 0.5.0 之前遗留，不在本次改动范围内）：`REO/Background/DayNight/Item.ini:4` 与 `REO/Background/bg.ini` 调用 `#@#Common\Background\config.exe`，而该目录部署名是 `ConfigBackground.exe`，背景设置入口点了没反应；`REO/Panels/Notes/` 的 `miniNotes` 配置目录与 `Notes\scroll.inc` 都指向不存在的文件，激活该面板会多出两条 `ERRO`。

**`wp7\` 死引用（已修，2026-10-18）**

- 原状：`git grep -In "wp7" -- 'REO/*'` 命中 882 行、412 个文件；改名提交 `934e838f` 只处理了大写 `WP7\`，小写的漏了。该命令按二进制跳过 UTF-16 文件，实际不止这些——逐文件解码盘点得 440 个文件、1086 处。
- 修法：`D:\WorkSpace\WorkBench\_reo_wp7_fix.py` 按上文「改皮肤文件」逐个嗅探编码，正则 `(?i)wp7(?=\\)` 与 `(?i)(?<=")wp7(?=")` 替换为 `REO`，写回前断言 `raw == text.encode(enc)`，编码行尾 BOM 全部原样。改完 **433 个文件、973 处**；`Gallery\MultiManager\Saved\` 下的用户布局快照跳过（§7）。
- 保留未改：`DragGroup=WP7Panel` 是拖拽分组名而不是路径（15 个文件）。
- 验证：`git grep -In "wp7" -- 'REO/*'` 与 `-Iin` 均 0 命中；实机开日志 `!ToggleConfig "REO\Panels\Notes" "Item.ini"` 使皮肤窗口 9→10 生效，旧形 `wp7\…` 则不动作并在日志报 `!ToggleConfig: "wp7\Panels\Notes\Item.ini" does not exist`。

## 6 修改契约（按改动类型）

- **改 AutoIt 源码**（`AutoIT\*.au3`）：**0.6.0 起没有任何分发 exe 由 `.au3` 编译而来**（七份全是 `Tools\` 与 `SettingsCenter\` 的 C# 工程产物），改它只影响参照源码，用户运行的功能一律改对应 C# 工程。下面记的是 0.3.0–0.5.0 构建 AutoIt exe 时的实测形态，只在需要重建历史产物时参考：改完必须用 Aut2Exe 3.3.18.0 重编译对应分发 exe，否则修复只停在源码。2026-10-16 实测可用的调用形态：`Start-Process -FilePath 【Aut2exe.exe】 -ArgumentList "【单个拼接好的字符串】" -WorkingDirectory AutoIT\`，参数为 `/in 【源】 /out 【绝对路径】 /icon 【ico】 /x86 /nopack /companyname Omnimo /filedescription 【描述】 /internalname 【名.exe】 /legalcopyright "Xyrfo 2013" /originalfilename 【名.exe】 /comments "Made for Omnimo UI"`；约 7 秒内 `HasExited`，还活着说明弹了模态框，判失败。**不要传 `/fileversion` / `/productversion`**；`-ArgumentList` 传数组会报「无法将 System.Object[] 转换为参数 FilePath 所需的类型」。**不要用 `AutoIT\build.bat`**（它走 AutoIt3Wrapper，本机未安装）。
  - 实测：裸 Aut2Exe 产物 `OmnimoApp.exe` 为 1187328 字节，`VersionInfo` 的 FileVersion / ProductVersion / FileDescription / CompanyName / OriginalFilename **全为空**。仓库里发的 1188352 字节、版本号 0.5.0.0 的那份是编译后另外注入版本资源的结果，差值正好 1024 字节。所以重建 exe 时要一并注入版本资源，并让 `FileVersion` 等于本次发布号。注入要点：`VS_VERSIONINFO` 叶含 8 个 string entry + `VarFileInfo\Translation`；头部 `wLength` / `wValueLength` / `wType` 在偏移 0 / 2 / 4，key 之后补到 **4 字节对齐**才是 value 起点（少这一步 Windows 读不出）；**在节表末尾追加新节**、不要搬动已有节，改完重算 `OptionalHeader.CheckSum`。注入本身只给每个 exe 加 1024 字节。
  - 分发 exe ↔ 源的历史对应（0.5.0 及以前；全部在 `REO\@Resources\Common\` 下）：`OmnimoApp.exe` ← `OmnimoApp.au3`；`ColorChanger.exe` ← `ColorChanger.au3`；`Background\ConfigBackground.exe` ← `ConfigBackground.au3`（源里 Outfile 写的是 `Background\Config.exe`，与实际分发名不同，以 git 跟踪的 exe 名为准）；`PanelCreator\PanelCreator.exe` ← `PanelCreator.au3`。**0.6.0 起这四份也改由 `Tools\` 的 C# 工程构建**（见下一条），改 `.au3` 不再影响任何分发 exe。
  - `Config.au3`、`ActivePanels.au3`、`MultiManager.au3` 源码保留作参照，**都不是对应分发 exe 的来源**（`config.exe` 由 `SettingsCenter\` 构建，另三个由 `Tools\` 构建）。`miniShell.au3` 没有 Outfile，`Uninstall.au3` 的产物不在仓库，两者都不编译。
- **改 SettingsCenter（`config.exe` 的来源）**：`dotnet build SettingsCenter\SettingsCenter.csproj -c Release`，然后把 `bin\Release\net48\` 的 `config.exe`、`config.exe.config`、`Microsoft.Web.WebView2.{Core,WinForms,Wpf}.dll`、`WebView2Loader.dll`、`lang\` **全量**拷到 `REO\@Resources\Common\Config\`（部署产物入仓库，与其余分发 exe 同一形态）。
  - csproj 锁死 `PlatformTarget=x86`：WebView2 NuGet 给 net48 只带 x86 原生 loader，AnyCPU（x64 进程）加载即 `BadImageFormatException (0x8007000B)`——改前先想清楚。
  - 皮肤调用参数契约不变：`【模式】 【config】 【file】 【settings】 【skins】 【program】`——**新增调用点必须传第 6 个参数 `#PROGRAMPATH#`**（现查 434 处调用点都已带上）。`config.exe` 解析 Rainmeter 安装路径的顺序：第 6 参数 → `ProgramW6432` / `ProgramFilesDir` / `ProgramFiles(x86)` 下的 `Rainmeter\Rainmeter.exe` → PATH（`_RainmeterExe()`，`Config.au3:75`）。WebView2 用户数据目录按面板哈希分目录（`%LOCALAPPDATA%\ReOmnimo\SettingsCenter\【hash】`），不同面板可并行；未处理异常落盘 `%TEMP%\sc_crash.txt`（UI 线程与非 UI 线程双通道），诊断崩溃先看它。
- **改 Tools（六个分发 exe 的来源：`ActivePanels.exe`、`MultiManager.exe`、`ColorChanger.exe`、`Background\ConfigBackground.exe`、`PanelCreator\PanelCreator.exe`、`OmnimoApp.exe`）**：`dotnet build Tools\【名】\【名】.csproj -c Release`，然后把 `bin\Release\net48\【名】.exe` **单文件**拷到下表对应位置（无附属 DLL；`bin` 里的 `【名】.exe.config` **不部署**，只有 `SettingsCenter` 的 `config.exe.config` 有此要求）。工程用 `<Compile Include="..\Shared\【文件】.cs" Link="【文件】.cs" />` 共享源码，不产出独立 DLL。
  - 部署位一览：`ActivePanels.exe` → `Common\Config\ActivePanels.exe`；`MultiManager.exe` → `Common\MultiManager\MultiManager.exe`；`ColorChanger.exe` → `Common\ColorChanger.exe`；`ConfigBackground.exe` → `Common\Background\ConfigBackground.exe`；`PanelCreator.exe` → `Common\PanelCreator\PanelCreator.exe`；`OmnimoApp.exe` → `Common\OmnimoApp.exe`。
  - `Tools\Shared\GuiFont.cs`：AutoIt 版的控件用系统 GUI 字体（`GetStockObject(DEFAULT_GUI_FONT)`，本机实测 SimSun 12 px），WinForms 默认字体不是它——新窗口一律 `Font = GuiFont.Default`，并把容器的 **`AutoScaleMode` 设为 `None`**（写在构造函数第一行，早于 `ClientSize` / `Font` 赋值），否则换字体会按字体比例缩放整套布局。
  - `Tools\Shared\OmnimoShared.cs` 里三条与 AutoIt 对齐的硬约定：**①** bang 走 WM_COPYDATA 发给 `FindWindow("RainmeterMeterWindow")`，`dwData=1`、载荷 UTF-16 + 结尾 NUL、`cbData=(字符数+1)*2`；**②** `IniDoc` 的 UTF-8 解码必须用会抛异常的严格实例，否则 cp1252 老文件里的非法字节会被静默换成 U+FFFD 写坏文件；**③** 写键值时只替换 `=` 之后的值、保留原有键名大小写（`color.inc` 的 `ColorSkin` 不能被改写成 `colorskin`）。
  - 皮肤调用契约与命令字不变（`missing` / `lock` / `unlock` / `grid*` / `Save0` / `Save1` / `Load0` / `Load1` / `Delete`，其余模式静默 no-op）。
  - 构建产物 `bin/`、`obj/`、`*.pdb`、`*.exe.WebView2/` 已在 `.gitignore`，不要 `git add -f`。工具每次运行会先删后写 exe 同级的 `hue.ini`（只写不读的遗留产物），它已取消跟踪，同样不要加回来。
- **改面板用户参数**：`Config\Panels\【名】\UserVariables.inc` 第一行必须是 `[Variables]`——缺段头整文件键被静默忽略，日志里只有下游异常。这些文件被跟踪，改完会显示脏，提交前逐个确认。
- **改私人订阅**：只写 `Config\Panels\Agenda\UserVariables.local.inc`（未跟踪，`.gitignore` 已覆盖）。它经 `@include5` 与 `UserVariables.inc` 合并，同名键以后者为准；面板缺的键回落到 `UserVariables.inc`。`config.exe` 会**自动跟随**：同目录存在该文件时读写都指向它（`Config.au3:65` 选基文件、`:69` 切覆盖文件、`:431` 读、`:543` 写），界面另显示一行本地覆盖提示——**在设置界面改的值会写进这个未跟踪文件**，这是有意行为，不要当成「设置不生效」的缺陷去修。
- **改语言文案**：`Chinese.inc` 与 `English.inc` 键集逐键对照——现查各 **283** 个唯一键、双向零差，`Chinese.inc` 里 149 个键含中文。两个文件都是 **UTF-16LE+BOM**（§3），`git grep -I` 会把它们当二进制跳过，**必须先解码再查**。**全库唯一的占位符约定是 `AgendaNoEvents` 的 `%1`**（`agenda.lua` 用 `gsub('%%1', …)` 替换），不要发明第二种写法。7 个键在中文包里**故意保持英文**：`Folders` / `weather` / `Humidity` / `Pressure` / `Wind` / `brightness` / `start`——不要翻译。磁贴名的本地化在 `Gallery\cat1..7.inc` 覆盖 `Text="#键#"`，不要在磁贴里写死中文。
- **改工具语言包**（`Common\Background\Language\*.cfg`）：**八语键集必须一致**——新增键要同时补全 8 份（2026-10-17 实查 5 语缺 `Apply`，缺键时按钮显示英文键名）；六份 UTF-16LE+BOM、English / Dutch 为 UTF-8 无 BOM，改动走 §3 编码感知流程。键集重比对用 `D:\WorkSpace\WorkBench\_reo_lang.py`。
- **改语言跟随机制**（`Common\Variables\AutoLang.lua` + `Languages\Auto.inc`）：宿主是 `REO\Background\bg.ini` 与 `REO\Gallery\main.ini` 末尾的 measure 对（Registry 读 `LocaleName` + Script，`UpdateDivider=-1` 每次宿主加载跑一次）。语言→包映射表在 lua 顶部，**改语言包集合必须同步 lua、README 与 `Languages\Auto.inc`**。`Auto.inc` 必须保持为指向 `English.inc` 的一行 shim（嵌套 include）。`UserVariables.inc` 是 UTF-8+BOM（§3，勿转回无 BOM）。两个宿主 ini 编码不同：`bg.ini` 单字节、`main.ini` UTF-16LE+BOM——追加内容按各自编码写（2026-10-17 在 main.ini 上踩过 ASCII 直拼变乱码）。检测器写回只认 `LangAuto`（`0` = 手动锁定）与 `MainLanguage` 的比较结果。
- **改面板设置 schema**：`RainConfigure.cfg` 4 行一组（参数名 / 标题 / 控件类型 / 空行）。**编码不统一**（现查 374 个：UTF-8 无 BOM 371、Agenda 是 UTF-16LE+BOM、`TextItems\uTorrent` 是 UTF-8+BOM、`Panels\uTorrent` 是 cp936），改前按 §3 逐个嗅探。`Checkbox:a:b` 的契约是**未勾选写 a、勾选写 b**（`Config.au3:519` 写、`:568` 显示态）——把 `Checkbox:1:0` 读成「勾选=1」会把 `Hidden=#X#` 判反。
- **加 Agenda 渲染样式**：0.4.0 起只剩一套排布——style 3/4 死分支与恒为 1 的 `AgendaStyle` 变量已删除（现查 `AgendaStyle` 命中 0）。不要再按「未接线分支」去改 ini 或 `RainConfigure.cfg`；要加样式请单独提案。
- **改 `agenda.lua`**：Rainmeter 公式里 `**` 是幂运算符（如 `7**#TypeH#`），不是畸形表达式。Lua 的 `0` 是真值，判断开关要用显式比较。含 `"` 的值必须走**三引号** bang 形式 `!SetOption 【meter】 【opt】 """值"""`，写成 `""` 会报 `Skin "X" does not exist` 且值不变。Rainmeter 按 ANSI（本机 CP936）读 `.lua` 源，字符串字面量必须纯 ASCII（中文注释无害），要显示中文只能从变量/ini 取。

## 7 禁止操作

- **禁止把私人日历订阅 URL 写进任何被跟踪文件、脚本或文档**——公开订阅链接本身就是凭据。自检：对 `git rev-list --all -- REO/@Resources/Config/Panels/Agenda/UserVariables.inc` 的每个 blob，比对 Feed 行的「长度 + sha256 前 16 位」。**不要**用 `git log -S'【关键词】'` 做这条自检——关键词随文档入库后必然自匹配，永远失败。
- **禁止提交会被运行时改写的文件**：`REO\Gallery\main.ini`、`REO\Gallery\scroll.inc`、`REO\Gallery\Intro\save.inc`、`REO\Gallery\MultiManager\TimeSettings.inc`、`REO\Gallery\MultiManager\Saved\*\`。误改用 `git checkout --` 还原。语言检测器（`AutoLang.lua`，§6）在 `LangAuto=1` 且系统语言与 `MainLanguage` 不同时还会写回 `Common\Variables\UserVariables.inc` 与 `Common\Background\Varrar.inc`（值一致时不写）——换 Windows 语言后这两个文件各脏一次，属预期。注意 `.git/info/exclude` 只对**未跟踪**文件生效：`git ls-files -ci --exclude-standard` 现查仍列出 419 个已跟踪文件，它们被写脏后会直接出现在 status 里，其中 5 个带 skip-worktree 标记（见 §3），`git add -A` 会把它们一并暂存。
- **禁止手改分发 exe，或只改源码不重编译就发版**。原因：用户运行的是二进制，源码修复不重编译等于没修；上游的 `config.exe` 曾与源码漂移两年，本就是本仓库要纠正的问题。前置动作：改 `Tools\` / `SettingsCenter\` 的 C# 源后按 §6 对应条目重新构建、把单文件 exe 部署到表内位置；改 `.au3` 前先读 §6「改 AutoIt 源码」。
- **禁止为「修语义反转」改 `Hidden=#ShowSeconds#` / `Hidden=#ShowExternalIP#` 一类写法**：前者配 `Checkbox:1:0`（勾选写 0，启用即显示，正确）；后者是外网/内网 IP 同位互换设计（靠 `Formula=-1*#ShowExternalIP#+1` 反转配合）。改前先读该面板 `RainConfigure.cfg` 的 Checkbox 规约与相邻 Calc 公式。
- **禁止「顺手修复」上游遗留死文件**：`Languages\Backup\`、`Languages\lang.inc`、`Common\Settings\UserVariables.inc`（只有写入者无读取者）、`Config\Panels\Radio\`（上下游都没有 Radio 面板）。属上游历史形态，清理需单独提案，混进功能改动会污染 diff。
- 禁止在文档与提交里放凭据、真实邮箱；对外文档零 emoji，提示块写 `> **注意**:`；commit message 用英文说清 Why，一个提交只做一件事。
- 面板 ini 的 `License=` 统一写 `Creative Commons Attribution-Noncommercial-Share Alike 3.0 License`；不许改动 `TextItems\Search\` 三份 NoDerivs 文件的许可声明。

## 8 验收标准

改动完成 = 下列全部通过（无 CI，以本清单为准）：

1. `git status --porcelain` 改动面与意图一致，不含运行时文件与 exclude 列出的已跟踪文件。用 `git ls-files -ci --exclude-standard` 全量枚举、逐个确认。
2. 改过 C# 工程（`Tools\` 六个工具 / `SettingsCenter\`）：`dotnet build -c Release` 零 error，按 §6 部署到对应位置，再实机验证——PowerShell 读 `VersionInfo.FileVersion` 等于本次发布号；`CreateProcess` 带 `CREATE_SUSPENDED` 映射镜像后立刻 `TerminateProcess` 过可加载性自检（与真实启动同一套校验、零副作用，`D:\WorkSpace\WorkBench\_exe_loadcheck.ps1`）；`Tools\` 里的命令行工具实机跑 `missing` 与一条未知命令（退出码 0、日志无新增 `ERRO`），GUI 工具用真实皮肤参数启动确认界面与控件齐全。`SettingsCenter\` 另需右键皮肤实机验证（设置窗口打开、改值写回、面板刷新）。改过 `.au3`：只影响参照源码；确需重建历史 AutoIt 产物时按 §6「改 AutoIt 源码」重编译并注入版本资源。
3. 动过皮肤行为：开日志（§3）实机验证，无新增 `ERRO`；**验完把 `Rainmeter.ini` 的 `Logging` 关回**。日志基线噪声（2026-10-02 观测，计数随会话长短浮动，**不算新增**）：`@Resources\Common\OverlayBorder\none5.png` 缺图、`@Resources\Graphics\Panels\Volume\` 的 `v0.png` 与 `0.png` 缺图（现查这三个文件确实不存在）、`Panels\Network\Item.ini:86` 的 `Substitute` 一条 `Invalid Substitute`、变量为空时的 `ImageName: Unable to open: …\OverlayBorder\`。视觉对比用差异像素占比给阈值，不要要求逐字节相同（半透明面板会透出动态壁纸与亚像素抖动；截图前关掉动态壁纸、把光标移离面板、等 WebParser 完成）。历史参考阈值：滚动生效 14.2%、静置回顶 0.04%（0.4.0 时期实测，未复测）。
4. 发版一次闭口：改动全提交 → `git tag -a X.Y.Z -m "…"`（不带 `v` 前缀）→ `git push origin master X.Y.Z` → `gh release create X.Y.Z -R OMSociety/ReOmnimo --title X.Y.Z -F 【正文文件】`（双 remote 下必须显式 `-R`；正文走 `-F` 文件传，避免命令行对中文的引号与编码损耗）。正文 = 一句中文摘要 + `CHANGELOG.md` 对应小节原文。

## 9 已知风险区

> 本节的坑来自既往实测记录，未在 2026-10-16 全部复测；引用前先按当前代码现查。

| 位置 | 风险 | 前置动作 |
|---|---|---|
| `agenda.lua` 数据通路 | WebParser `Download=1` 时字符串的值是下载文件路径而不是正文（实测 58–64 字符） | 必须 `RegExp=(?s)^(.*)$` + `StringIndex=1` 整页捕获；改前先读 `CHANGELOG.md` 0.4.0 |
| `agenda.lua` 窗口计算 | 0.4.0 起窗口 = 恰好 `RangeDays` 个日历日（闭区间），不再是定长推进；`RangeDays=6` 由 7 天变 6 天是有意的行为变更 | 改窗口逻辑先读 `CHANGELOG.md` 0.4.0 |
| `agenda.lua` 时区偏移表 | 表内是**标准时**偏移，夏令时期间会偏 1 小时；未知 TZID 按本机墙上时间显示并只记一次 Notice | 不要引入完整 IANA 库 |
| `agenda.lua` 每秒门控 | `Update()` 里的两级缓存不能拆（旧代码每 tick 推约 120 条 bang） | 用仓库外探针皮肤验证，读数只能取 `SKIN:GetMeter('Row1'):GetOption('Text')` |
| `[Clip]` 裁剪 | 被指定为 Container 的表自身不绘制，拿 `[bg]` 当遮罩会让磁贴背景消失 | 另建不透明独立遮罩表（Agenda 的 `[Clip]` 即此法） |
| `RainConfigure.cfg` | 第 4 行非空且不是 `[Options]` 会弹 `Unable to read RainConfigure.cfg` | 每组后必须留空行；改 schema 前按 4 行一组逐行核对 |
| 新建皮肤目录 | 不 `!RefreshApp` 直接 `!ActivateConfig` 找不到 config | 先 RefreshApp 再 Activate |
| `git add -A` | 5 个带 skip-worktree 标记的已跟踪文件不受 exclude 保护，运行时写脏后会被暂存 | 见 §7 第二条；提交前逐文件确认 |
| 大网格自动排布 | 3x3 分支的 `$row9` 在原版 `ActivePanels.au3:278` 从未声明，启用面板 ≥31 时崩溃；`Tools\ActivePanels\Program.cs` 已按源码本来的行表补上 | 改排布逻辑要同步三处：`Tools\ActivePanels\Program.cs`、`AutoIT\ActivePanels.au3`、`AutoIT\OmnimoApp.au3`（后两处仍是未修状态） |
| 本文件引用的行号 | `Config.au3` 的写盘 / 显示态在 519 / 568 | 引用行号前现查，不凭记忆 |
| `Tools\` 的 GUI 工具 | 三个工具已换 ModernForm 无边框壳：圆角由 DWM 偏好裁剪——**禁止再叠 WinForms `Region`**（双重圆角半径不一致会在角上反白，2026-10-17 修过）；`ConfigBackground` 宽 / 高滑杆上限 = 进程视角的桌面宽 / 高（与原版 au3 `@DesktopWidth` 一致，超出夹断、Apply 会写回夹断值）；PanelCreator 读工作目录相对的 `Resources\Colors.inc`（ConfigBackground 读 `..\PanelCreator\Resources\Colors.inc`；原版同此行为，工作目录不对则整体回退黑——截图自检时 `-Workdir` 必须给 exe 所在目录）；`ModernUi.UiFont()` 返回共享缓存字体——**禁止 `using`/`Dispose`**，否则后续绘制抛 `ArgumentException` 参数无效；DPI：三个 exe 靠 `Tools\Shared\app.manifest`（PerMonitorV2）感知，`ModernForm.OnHandleCreated` 手动 `Scale`，**别用 `AutoScaleMode.Dpi`**（net48 实测因子算成 1、只放大字不缩放坐标），自绘控件内写死的设计像素常量一律乘 `ModernUi.PxU/DpiU`；`ModernCheckBox` 基类是 `Control` 不是 `CheckBox`（继承 CheckBox 时主题引擎仍会在非 96 DPI 下把原生勾选字形画进客户区，呈彩色碎片） | 截图核对用 PrintWindow（`flags=2`）或同 DPI 上下文 `CopyFromScreen`；`_tool_shot.ps1` 已声明感知并把光标钉主屏（PerMonitorV2 窗口创建在光标所在屏），出控件转储 |
| `!WriteKeyValue` 写回 | Rainmeter 对无 BOM 的非 UTF-8 文件按系统 ANSI（本机 CP936）读写：高位字节变 `?`、可能吞行尾引号（2026-10-17 实测损伤 `UserVariables.inc` 一行后修复） | 会被运行时写回的 inc 一律 UTF-8+BOM / UTF-16LE+BOM（§3）；给皮肤加新的写回目标前先想编码 |

## 10 出错怎么办

> 症状行的可检索片段来自既往实测记录；先按 §3 开日志抓 `ERRO` 再定位。

| 症状（可检索片段） | 处理 |
|---|---|
| `12006` / `未使用可识别的协议` | 面板配置 inc 缺 `[Variables]` 段头，或 `Url` 变量未定义；查 include 顺序与变量拼写（变量名不区分大小写） |
| `Unable to parse line`，报错行落在 Include | 解释器低于 3.3.10：现行 WinAPI UDF 含三元运算符；换 3.3.18.0，别改自己的代码 |
| `Unable to read RainConfigure.cfg` | schema 格式坏，见 §9 |
| Aut2Exe 静默 exit 0 且无产物 | 重定向了 stdout/stderr；去掉 `>log 2>&1` 重跑 |
| `not a valid application for this OS platform` / 193 | 注入版本资源时搬动了已有节（节表按 VirtualAddress 必须单调递增），或改完镜像没重算 `OptionalHeader.CheckSum`；改为在节表末尾追加新节 + 重算校验和 |
| Aut2Exe 弹 "Command Line Parameters" 帮助框 | 传了 `/fileversion 6.0.0.0` 这类点分写法；去掉版本参数，编译后注入 |
| `tag exists locally but has not been pushed` | `gh` 解析到了 upstream；命令补 `-R OMSociety/ReOmnimo` |
| 截图每张都不同 | 半透明面板透出动态壁纸与亚像素抖动；先关动态壁纸，改用差异像素占比判据，不要要求逐字节相同 |
| Python 写回后文件行尾全乱 | `\r\n` 被写成 `\r\r\n`；补丁脚本补 `newline=""` 重写 |
| 重启后 `Rainmeter.ini` 只剩一百多字节、桌面皮肤全没了 | 文件头真 BOM 被写坏（残迹形如 `EF BF BD EF BF BD`），Rainmeter 按 CP936 读成空配置后写回默认值。该文件是 **UTF-16LE + 真 `FF FE` BOM**，只认这个 BOM；从备份恢复，别在缺 BOM 的文件上做字节级替换。行尾以现查为准（Rainmeter 自己写回时归一成 CRLF，但实测出现过纯 LF），补丁脚本按实际行尾处理，别假定 |
| 开日志脚本报 `ABORT (no write): file has no CRLF line endings` | `%APPDATA%\Rainmeter\Rainmeter.ini` 当前是纯 LF，`_omni_set_logging2.py` 的行尾自检挡住了写入；改用 `_omni_logcheck.ps1` 字节级改值，或先把该文件行尾统一成 CRLF 再跑脚本 | |

## 11 维护

- 本文件与触发它的代码改动进**同一个提交**，不攒批；发现本文件与代码不符时，先改本文件再继续改代码。改动以下内容必须同步本文件：验收命令、include 链、Checkbox 契约、凭据规则、分发 exe 清单与重编译命令、产品边界、§5 的在途状态。
- 事实性数字（键数、目录数、文件大小、计数）引用前现查（编码表用 `D:\WorkSpace\WorkBench\_reo_enc_patterns.py` 重扫，语言包键集用 `_reo_lang.py` 重比对），不凭记忆。交付本文件的改动前跑 `python C:\Users\Administrator\.dsh\skills\agent-onboarding\scripts\check_agents_md.py --root D:\WorkSpace\Github\ReOmnimo`（error 必须为 0），并人工核对：链接目标说的是不是同一回事、每条规则的触发条件是否明确、有没有把「我们用了 X」写成规则。
