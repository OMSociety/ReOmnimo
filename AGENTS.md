# AGENTS.md — ReOmnimo(接手 Omnimo 开发)Agent 宪法

适用范围:本仓库全部目录(单文件,无子目录级 AGENTS.md)。
最后更新:2026-10-02

## 项目概览

- 一句话定位:Rainmeter 磁贴面板集 ReOmnimo,接手 fediaFedia/Omnimo 的开发,保留上游设计,只做四件事:设置/保存面板中文化、公开 ICS 日程订阅、修既有 bug、微小工作。
- 技术栈:Rainmeter 皮肤(ini/inc/cfg/lua)+ AutoIt 3.3.18.0 配置工具(9 个 .au3,其中 7 个编译为分发 exe;`miniShell.au3`/`Uninstall.au3` 源码保留但当前不产出分发 exe)。无 CI、无测试框架,验收靠本文件的命令与实机判据。
- 环境前提(本机):Aut2Exe 在 `C:\Program Files (x86)\AutoIt3\Aut2Exe\Aut2exe.exe`;`git`/`gh` 在 PATH,`Rainmeter.exe` 不在 PATH(在 `C:\Program Files\Rainmeter\Rainmeter.exe`,当前实测 5.0.0.5285,调用一律用完整路径);所有 `git` 命令在仓库根执行;构建/验收助手脚本(`_omni_build.ps1`、`_omni_vsver.py`、`_omni_loadcheck.ps1`、`_omni_set_logging2.py` 等)在 `D:\WorkSpace\`(与仓库同级,**不随仓库分发**);该目录可能已被清理(2026-10-02 实测已无 `_omni_*`),用到时按本文件「常用命令」「修改契约」描述的写入规则重建,不要跳过其中自检。
- 文档索引:
  - 逐版本变更:`CHANGELOG.md`
  - 许可与来源:`LICENSE`、`THIRD-PARTY.md`(含差异集逐文件清单)
  - 用户说明:`README.md`(英文)、`README.zh.md`(中文)
- 基准:与 `upstream/master`(fediaFedia/Omnimo)的差异数为 16 增 / 513 改 / 17 删(0.3.0 时为 14/60/16;0.4.0 把 428 处 config.exe 调用点补上了第 6 个参数 `#PROGRAMPATH#`,这是“改”从 61 涨到 450 量级的主因;README 双文档化后上游 `readme.md` 记删、`README.md`/`README.zh.md` 记增;本轮 Rainmeter 5.0 现代化——捆绑插件转原生 measure、FrostedGlass 死段删除、MediaKey 转 `!SendKey`、NowPlaying 转 SMTC——把“改”推到 513),勿凭记忆引用旧数字。

## 产品边界

- 接到需求先归类:属于四件事之一才做;"待办同步"用户已决定不做,不要实现。
- 明确不做:重画视觉、复刻面板、自建控件层、改设计语言、换字体。磁贴表面保持英文(按英文宽度排版)。
- 已发布 Release:`0.4.0` 与历史遗留的 `v0.3.0`(v0.1.0-0.2.0 的 tag 与 Release 已按要求删除,CHANGELOG 历史条目保留);**tag 名不带 `v` 前缀**(`v0.3.0` 是历史遗留,不要沿用);已发布的 tag 不移动。
- GitHub 仓库:`OMSociety/ReOmnimo`(2026-10-02 由 `OMSociety/Omnimo` 改名,GitHub 对旧全名与 `raw.githubusercontent.com` 旧路径都仍重定向;`origin` 已更新为新名,`upstream` 保持 `fediaFedia/Omnimo`)。
- 桌面映射:`Documents\Rainmeter\Skins\WP7` 是指向本仓库 `WP7\` 的 junction,改仓库即改桌面。

## 常用命令

| 目的 | 命令 |
|---|---|
| 核对改动面(提交前必跑) | `git diff --name-status upstream/master -- .` |
| 查 skip-worktree 标记 | `git ls-files -v` 中找行首 `S` 的条目 |
| 凭据入库自检(指纹法) | 对 `git rev-list --all -- "WP7/@Resources/Config/Panels/Agenda/UserVariables.inc"` 的每个 blob,比对 Feed 行的"长度 + sha256 前 16 位" |
| 刷新 Rainmeter 并重发现 | `Rainmeter.exe !RefreshApp`(等约 9 秒) |
| 激活单个面板 | `Rainmeter.exe !ActivateConfig "WP7\Panels\<Name>" "Item.ini"` |
| 开日志 | 完整闭环:`Rainmeter.exe !Quit` → 备份 `%APPDATA%\Rainmeter\Rainmeter.ini` → `python D:\WorkSpace\_omni_set_logging2.py 1 --apply`(只改 ini、**不重启 Rainmeter**)→ 手动启动 Rainmeter → 复现操作 → 读 `Rainmeter.log` 抓 `ERRO` → `!Quit` → `python D:\WorkSpace\_omni_set_logging2.py 0 --apply` 关回 → 重启。该文件是 **UTF-16LE + 真 `FF FE` BOM + CRLF**,Rainmeter 只认这个 BOM;BOM 被写坏(残迹形如 `EF BF BD EF BF BD`)时它按 CP936 读成空配置,**随即把整份文件重写成默认值,所有 section 全丢**。脚本会自检 BOM/行尾/section 数,不满足直接 assert 退出不写盘 |
| 还原被运行时写脏的文件 | `git checkout -- WP7\Gallery\main.ini WP7\Gallery\scroll.inc` |

改皮肤文件必须用 Python 补丁脚本:二进制读入 → 按嗅探编码解码 → 替换 → 按同一编码写回且 `newline=""`。直接用文本工具写会把 `\r\n` 写成 `\r\r\n`(实测过);控制台是 GBK,`print` 中文会抛 UnicodeEncodeError,结果写 UTF-8 文件再看。编码嗅探顺序:UTF-16LE+BOM → UTF-8+BOM → UTF-8 → cp936 → cp1252;纯 ASCII 文件会同时通过多种编码,另看"是否含 >=0x80 字节"。

## 架构边界

```text
面板 ini(WP7\Panels, 62 目录, 一档一文件)
  ├─ @include   Common\Variables\UserVariables.inc          全局变量(MainLanguage 在此)
  ├─ @include1  Common\Variables\Languages\#MainLanguage#.inc   界面语言包
  ├─ @include2  Common\color\color.inc                主题(Padding/Opacity 唯一定义处)
  ├─ @include3  Config\Panels\<Name>\UserVariables.inc    面板用户参数
  ├─ @include4  Structure\#PanelType#\Main.inc        档位底板(13 档)
  └─ @include5  Agenda 专属:UserVariables.local.inc   私人订阅覆盖(未跟踪)
AutoIt 工具(AutoIT\*.au3 → 7 个 exe)
  └─ 读 Common\Background\Varrar.inc 的 Language
     → 读 Common\Background\Language\<Language>.cfg
```

- include 按编号升序生效,变量**后写者胜**;让覆盖生效就放进编号更大的 include。config.exe 在 `UserVariables.local.inc` 存在时把读写都指向该文件(面板缺的键仍回落到 `UserVariables.inc`),见“修改契约”。
- 界面文案只走 `Languages\*.inc`;AutoIt 工具文案只走 `Background\Language\*.cfg`。`AutoIT\Language\*.cfg` 是源码侧镜像,**运行时无任何读取点**——只改它不会改变任何界面。
- `Padding`/`Opacity`/`Opacity2`/`Globalblurenable`/`Xposition` 只由 `Common\Color\*.inc` 定义;面板库换主题会用主题文件**整体覆盖** color.inc(OmnimoApp.au3 的 FileCopy),本仓库提交的默认值(Padding=5 等)会被改回该主题自带值——这是有意保留的边界。
- 磁贴表名 = 面板目录名(`[EssentialPanel]` 定义于 `Gallery\Color\Modern\<Dark|Light>\tt.inc:232`,动作是 `!ToggleConfig "WP7\Panels\#CURRENTSECTION#" "Item.ini"`)。
- 面板库图标层是 `mask-<类>.png` 按格烤好的 2 倍图,与 cat1..7 磁贴一一对应;列中心 `57,178,300,421,543,664,786,907`,格距 121.43 图内像素 = 磁贴间距 61。

## 修改契约(按改动类型)

- **改 AutoIt 源码**:必须用 `Aut2Exe`(AutoIt 3.3.18.0)重编译**全部 7 个分发 exe**,再注入版本资源;否则修复只停在源码。禁止用 `build.bat`(依赖已下线的 wmic)。Git-Bash 调 Aut2Exe 必须带 `MSYS_NO_PATHCONV=1` `MSYS2_ARG_CONV_EXCL='*'`,且 3.3.18 下不能重定向其 stdout/stderr(会静默 exit 0 不产出);`/nopack` 不能省(默认 UPX 加壳)。
  - 7 个分发 exe ↔ 源(全部在 `WP7\@Resources\Common\` 下):`Config\config.exe`←`Config.au3`、`Config\ActivePanels.exe`←`ActivePanels.au3`(该源无 Outfile 指令,编译时必须用 `/out` 指定)、`Background\ConfigBackground.exe`←`ConfigBackground.au3`(注意其 Outfile 指令写的是 `Background\Config.exe`,与实际分发名不同,以 git 跟踪的 exe 名为准)、`ColorChanger.exe`←`ColorChanger.au3`、`MultiManager\MultiManager.exe`←`MultiManager.au3`、`OmnimoApp.exe`←`OmnimoApp.au3`、`PanelCreator\PanelCreator.exe`←`PanelCreator.au3`。`miniShell.au3`/`Uninstall.au3` 的 Outfile 指向的 exe 不在仓库,当前不编译。
  - 已验证的参数集:`/in <Src>.au3 /out <绝对路径> /icon <Icons\X.ico> /x86 /nopack /companyname Omnimo /filedescription "<desc>" /internalname <Name>.exe /legalcopyright "Xyrfo 2013" /originalfilename <Name>.exe /comments "Made for Omnimo UI"`。**不要传 `/fileversion` / `/productversion`**:点分写法会弹 "Command Line Parameters" 帮助框,逗号写法只写出 `0,0,0`。
  - PowerShell 侧必须 `Start-Process -ArgumentList <单个拼接好的字符串>`(传数组报“无法将 System.Object[] 转换为参数 FilePath 所需的类型”);`-WorkingDirectory` 指到 `AutoIT\`,等约 7 秒看 `HasExited`:还活着说明弹了模态框,`Stop-Process` 并判失败。
  - **版本资源要编译后自己注入**(裸 Aut2Exe 产物没有可读版本号):重建 `VS_VERSIONINFO` 叶(8 个 string entry + `VarFileInfo\Translation`;`wLength`/`wValueLength`/`wType` 在偏移 0/2/4,key 结束补到 4 字节对齐处才是 value 起点——少这一步 Windows 读不出),**放进节表末尾新增的节**。不要搬动已有节:节表按 VirtualAddress 必须单调递增,顺序被打乱会得到 `ERROR_BAD_EXE_FORMAT` 193;改完镜像还必须重算 `OptionalHeader.CheckSum`(不重算同样 193)。注入本身只给每个 exe 增加 1024 字节(节表新增一项);本仓库的产物是 `/nopack` 重编译,整体比上游那些 UPX 加壳的二进制大得多,差别来自加壳而不是版本资源。
  - 自检:PowerShell 读 `VersionInfo.FileVersion` 应等于本次发布号;再用 `CreateProcess` 带 `CREATE_SUSPENDED` 映射镜像后立刻 `TerminateProcess`——与真实启动同一套校验、零副作用,能区分“资源读得出但加载器拒绝”。本机三个助手脚本(`_omni_build.ps1` / `_omni_vsver.py` / `_omni_loadcheck.ps1`)在仓库外,不随仓库分发(`D:\WorkSpace\` 下若无则按本文件重建)。config.exe 解析 Rainmeter 安装路径的顺序是:第 6 个命令行参数 `#PROGRAMPATH#`(仓库里 428 处 `Config\\config.exe` 调用点都已补上)→ `ProgramW6432` / `ProgramFilesDir` / `ProgramFiles(x86)` 下的 `Rainmeter\Rainmeter.exe` → PATH 中的 `Rainmeter.exe`,见 `_RainmeterExe()`(`Config.au3:75`)。
- **改面板用户参数**:`Config\Panels\<Name>\UserVariables.inc` 第一行必须是 `[Variables]`——缺段头整文件键被静默忽略,日志只有下游异常。这些文件被跟踪,改完会显示脏,提交前逐个确认。
- **改私人订阅**:只写 `Config\Panels\Agenda\UserVariables.local.inc`(未跟踪,.gitignore 已覆盖);它经 @include5 与 `UserVariables.inc` **合并**,同名键以后者为准。0.4.0 起 config.exe 自动跟随:同目录存在该文件时 `$VarFile` 指向它,界面另显示一行本地覆盖提示,面板缺的键回落到 `UserVariables.inc`(见 `Config.au3:64-70`、`:434`)——不要再按“设置界面改动不生效”的旧交互缺陷描述它。
- **改语言文案**:改 `Chinese.inc` 时与 `English.inc` 键集逐键对照(现均 288 键定义/283 唯一键,双向零差);**全库唯一的占位符约定是 `AgendaNoEvents` 的 `%1`(=显示天数),agenda.lua 用 `gsub('%%1', …)` 替换**——不要发明第二种写法;7 个表面键 `Folders` `weather` `Humidity` `Pressure` `Wind` `brightness` `start` 保持英文;右键菜单键跟随语言包,面板名本地化在 cat1..7 的磁贴表覆盖 `Text="#<语言键>#"`。
- **改面板设置 schema**:`RainConfigure.cfg` 4 行一组(参数名/标题/控件类型/空行),编码 UTF-16LE+BOM;`Checkbox:a:b` 的契约是**未勾选写 a、勾选写 b**(Config.au3:519 写、:568 显示态)——把 `Checkbox:1:0` 读成“勾选=1”会把 `Hidden=#X#` 判反,本仓库曾因此误修过 DigitalClock 两处后回退。
- **改 cat1..7 磁贴**:增删/移动一格必须同步改 `mask-<类>.png` 图标层(复制相邻字形,不要自画);跨行回流靠 `Y=(1*#ScaleDpi#)R` + `x=(360*#ScaleDpi#)` 行锚点交接。
- **改 agenda.lua**:Rainmeter 公式里 `**` 是幂运算符(如 `7**#TypeH#`),不是畸形表达式,别“修”;Lua `0` 是真值,判断开关用显式比较;含 `"` 的值必须走**三引号** bang 形式 `!SetOption <meter> <opt> """值"""`(`set()` 已如此实现;Rainmeter 只在三引号形式下保留值内引号,写成 `""` 会报 `Skin "X" does not exist` 且值不变),且 bang 值里的 `[SomeSection]` 会被当段变量替换掉(不存在的段名原样保留);**Rainmeter 按 ANSI(本机 CP936)读 .lua 源**,字符串字面量必须纯 ASCII(中文注释无害),要显示中文只能从变量/ini 取。
- **改 agenda 渲染样式**:0.4.0 起只剩一套排布——style 3/4 死分支与恒为 1 的 `AgendaStyle` 变量已删除;不要再按“未接线分支”去改 ini/RainConfigure,要加样式请单独提案。
- **改发版**:本仓库发布号只活在 tag + CHANGELOG 标题 + GitHub Release 三处(外加 exe 版本资源);仓库内 `Version=` 字段(Rainstaller.cfg 10.0.4、OmnimoVersion 10.0、Settings 6.0.1、RMSKIN.inc 1.0)属上游自有体系,不要动。"本次发布号"= 本轮用户指定的版本(如 `0.4.0.0`),必须与 tag 名(**自 `0.4.0` 起不带 `v` 前缀**,`v0.3.0` 是历史遗留)、CHANGELOG 标题、注入 exe 的 `FileVersion`(四位,tag `0.4.0` ↔ `0.4.0.0`)三者一致;动手前先向用户确认号码,不要自己编。核对改动面与凭据自检都依赖 `upstream` remote:用 `git remote -v` 确认它指向 `https://github.com/fediaFedia/Omnimo.git`,缺失则 `git remote add upstream <该地址>` 后 `git fetch upstream`。

## 禁止操作

- 禁止把私人日历订阅 URL 写进任何被跟踪文件、脚本或文档——公开订阅链接本身就是凭据。自检用 `git rev-list --all -- <路径>` 逐 blob 以"长度 + sha256 前 16 位"指纹比对;**不要**用 `git log -S'<关键词>'` 做这条自检——关键词随本文档入库后必然自匹配,永远失败(实测命中 8853d7bb)。
- 禁止提交被运行时改写的文件:`WP7\Gallery\main.ini`、`scroll.inc`、`Intro\save.inc`、`MultiManager\TimeSettings.inc`、`MultiManager\Saved\*\`。原因:Rainmeter 运行时改写,提交进去是本机状态。误改用 `git checkout --` 还原。
- 禁止手改 7 个 exe,或只改 .au3 不重编译就发版。原因:用户运行的是二进制,源码修复不重编译等于没修(上游 config.exe 曾与源码漂移两年)。
- 禁止为"修语义反转"改 `Hidden=#ShowSeconds#` / `Hidden=#ShowExternalIP#` 一类写法。原因:前者配 `Checkbox:1:0`(勾选写 0,启用即显示,正确);后者是外网/内网 IP 同位互换设计(靠 `Formula=-1*#ShowExternalIP#+1` 反转配合)。改前先读该面板 RainConfigure.cfg 的 Checkbox 规约与相邻 Calc 公式。
- 禁止"顺手修复"上游遗留死文件:`Languages\Backup\`、`lang.inc`、`Common\Settings\UserVariables.inc`(只有写入者无读取者)、`Config\Panels\Radio\`(上下游都无 Radio 面板)。原因:属上游历史形态,清理需单独提案,混进功能改动会污染 diff。
- 禁止在文档与提交里放凭据、真实邮箱;对外文档零 emoji,提示块用 `> **注意**:`;commit message 用英文说清 Why,一个提交只做一件事。
- 面板 ini 的 `License=` 统一写 `Creative Commons Attribution-Noncommercial-Share Alike 3.0 License`;不许改动 `TextItems/Search/` 三份 NoDerivs 文件的许可声明。

## 验收标准

改动完成 = 下列全部通过(无 CI,以本清单为准):

1. `git diff --name-status upstream/master -- .` 改动面与意图一致,不含运行时文件与 exclude 列出的被跟踪文件。"exclude" 指 `.git/info/exclude`(本机忽略清单,未跟踪);其头部注释列明了 419 个**已跟踪但会被运行时改写**的文件,它们不受 exclude 保护、会出现在 status 里,提交前用 `git ls-files -ci --exclude-standard` 全量枚举、逐个确认。
2. 改过 `.au3`:7 个 exe 重编译并重新注入版本资源,PowerShell 读 `VersionInfo.FileVersion` 全部等于本次发布号,且 7 个都通过 `CREATE_SUSPENDED` 可加载性自检。
3. 动过皮肤行为:开日志实机验证,无新增 `ERRO`(基线噪声只这几类——2026-10-02 全量重启实测,计数随会话长短浮动:`WP7\@Resources\Common\OverlayBorder\none5.png` 缺图、`WP7\@Resources\Graphics\Panels\Volume\` 的 `v0.png` 与 `0.png` 缺图、`FrostedGlass.dll` 找不到(error 126)、`WP7\Panels\Network\Item.ini` 的 `Meter=Calc is not valid in [MeasureNetInMbps]` 与 `[MeasureNetOutMbps]` 各一条、同文件一条 `Measure: Invalid Substitute=Current IP Address: …`,以及变量为空时的 `ImageName: Unable to open: …\OverlayBorder\`);**验完把 `Rainmeter.ini` 的 `Logging` 改回**;视觉对比用差异像素占比给阈值(本仓库实测参考:滚动生效 14.2%,静置回顶 0.04%),截图前把光标移离面板(底板 MouseOverAction 会改 tint)、等 WebParser 完成。
4. 发版一次闭口:改动全提交 → `git tag -a X.Y.Z -m "…"`(不带 `v` 前缀,见「产品边界」) → `git push origin master X.Y.Z` → `gh release create X.Y.Z -R OMSociety/ReOmnimo --title X.Y.Z -F <正文文件>`(双 remote 下 `gh` 必须显式 `-R`,否则默认解析到 upstream;正文走 `-F` 文件传,避免命令行对中文的引号与编码损耗);Release 正文 = 一句中文摘要 + CHANGELOG 对应小节原文(中英两段照抄)。

## 已知风险区

| 位置 | 风险(历史踩过) | 前置动作 |
|---|---|---|
| agenda.lua 数据通路 | WebParser `Download=1` 时字符串值是下载文件路径,不是正文(实测 58-64 字符);必须 `RegExp=(?s)^(.*)$` + `StringIndex=1` 整页捕获 | 改数据通路前读本表与"修改契约" |
| agenda.lua 窗口计算 | 0.4.0 起窗口 = **恰好 `RangeDays` 个日历日**(闭区间,`hi = addDays(today, RangeDays-1)`),不再是“从今天起 N 天之后”的定长推进;老配置 `RangeDays=6` 由 7 天变 6 天是有意的行为变更 | 改窗口逻辑先读 CHANGELOG 0.4.0 |
| agenda.lua 的 TZID 偏移表 | 表内是**标准时偏移**,夏令时期间偏 1 小时(实测 10 月的 `America/New_York` 9am 显示 22:00 而非 21:00);未知 TZID 按本机墙上时间显示并只记一次 Notice | 改表前读 CHANGELOG 0.4.0;不要引入完整 IANA 库 |
| agenda.lua 每秒门控 | `Update()` 里的 `parseKey`/`renderKey` 两级缓存不能拆(实测 23 次 tick 只解析 1 次、渲染 1 次;旧代码每 tick 推约 120 条 bang) | 验证用仓库外探针皮肤:让 `ScriptFile` 直指本文件 + 本地 .ics fixture,读数只能用 `SKIN:GetMeter('Row1'):GetOption('Text')`(`[Row1]` 在 bang/Measure 字符串里不会被替换) |
| `[Clip]` 裁剪 | 被指定为 Container 的表自身不绘制,拿 `[bg]` 当遮罩会让磁贴背景消失 | 另建不透明独立遮罩表(Agenda 的 [Clip] 即此法) |
| RainConfigure.cfg | 第 4 行非空且不是 `[Options]` 会弹 "Unable to read RainConfigure.cfg";每组后必须留空行 | 改 schema 前按 4 行一组逐行核对 |
| 新建皮肤目录 | 不 `!RefreshApp` 直接 ActivateConfig 找不到 | 先 RefreshApp 再 Activate |
| `git add -A` | exclude 清单里的 5 个被跟踪文件(Common/Variables/UserVariables.inc、WeatherCom 两件、color.inc、Colors.inc)不受 exclude 保护,运行时改写后会被暂存 | 提交前逐文件确认;exclude 只对未跟踪文件生效 |
| 大网格自动排布 | 5x5-9x9 曾因行计数器混用叠进同一列(0.3.0 已修 ActivePanels.au3 与 OmnimoApp.au3) | 改排布逻辑两处源码必须同步改 |
| 本文件引用的行号 | Config.au3 的写盘/显示态在 519/568(0.3.0 前后文档写过 458/507、471/520;本地覆盖回落读 base 在 434) | 引用行号前现查,不凭记忆 |

## 出错怎么办

| 症状(可检索片段) | 处理 |
|---|---|
| `12006` / `未使用可识别的协议` | 面板配置 inc 缺 `[Variables]` 段头,或 Url 变量未定义;查 include 顺序与变量拼写(变量名不区分大小写) |
| `Unable to parse line`(报错行落在 Include) | 解释器低于 3.3.10:现行 WinAPI UDF 含三元运算符;换 3.3.18.0,别去改自己的代码 |
| `Unable to read RainConfigure.cfg` | schema 格式坏,见已知风险区 |
| Aut2Exe 弹模态错误框 | 源码或参数错;逐源独立调用修参数 |
| Aut2Exe 静默 exit 0 且无产物 | 重定向了 stdout/stderr;去掉 `>log 2>&1` 重跑 |
| `not a valid application for this OS platform` / 193 | 注入版本资源时搬动了已有节(节表 VirtualAddress 不再单调递增),或改完镜像没重算 `CheckSum`;改为在节表末尾追加新节 + 重算校验和 |
| Aut2Exe 弹 "Command Line Parameters" 帮助框 | 传了 `/fileversion 6.0.0.0` 这类点分写法;去掉版本参数,版本号编译后注入。`Start-Process -ArgumentList` 传数组也会报类型错,必须传单个拼接字符串 |
| `tag exists locally but has not been pushed` | `gh` 解析到了 upstream;命令补 `-R OMSociety/ReOmnimo` |
| 截图每张都不同 | 半透明面板透出动态壁纸/亚像素抖动;关动态壁纸,用差异像素占比判据,别要求逐字节相同 |
| Python 写回后文件行尾全乱 | `\r\n` 被写成 `\r\r\n`;补丁脚本加 `newline=""` 重写 |
| 重启后 `Rainmeter.ini` 只剩一百多字节、桌面皮肤全没了 | 文件头真 BOM 被写坏(残迹 `EF BF BD EF BF BD`),Rainmeter 按 CP936 读成空配置后写回默认值;从备份恢复,按 `FF FE` + UTF-16LE + CRLF 写回(`_omni_rm_ini_repair.py`),别在缺 BOM 的文件上做字节级替换 |

## 维护

- 本文件与触发它的代码改动进同一个提交,不攒批;发现本文件与代码不符时,先改本文件再继续改代码。
- 改动以下内容必须同步本文件:验收命令、include 链、Checkbox 契约、凭据规则、分发 exe 清单与重编译命令、产品边界。
- 事实性数字(键数、目录数、差异计数)引用前现查,不凭记忆。
- 交付本文件的改动前跑 `python <技能目录>\agent-md-creator\scripts\check_agents_md.py --root <仓库根>`(在 agent-md-creator 技能目录里,不在仓库内;error 必须为 0),并人工核对:链接目标说的是否同一回事、每条规则触发条件是否明确、有没有把"我们用了 X"写成规则。
