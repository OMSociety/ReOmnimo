# AGENTS.md — ReOmnimo

适用范围：本仓库全部目录（单文件，无子目录级 AGENTS.md）。
最后更新：2026-11-17。

Rainmeter 皮肤套件：WP7/Metro 磁贴布局，接手 fediaFedia/Omnimo 后独立演进。文档索引：`README.md`（英文）、`README.zh.md`（中文）、`CHANGELOG.md`、`THIRD-PARTY.md`（含差异集逐文件清单）。改用户可见行为时同步两份 README。

## 1 环境与工具

- 现代化方向：把旧实现迁到 C#、把插件改成原生 measure 是本职范围内的改造；不要以「与上游一致」为由保留过时实现，也不要以上游写法为由拒绝重构。
- 设计层不做现代化：不重画视觉、不复刻面板、不改设计语言、不换字体。磁贴表面文案保持英文并按英文宽度排版，磁贴名走语言包（`Gallery\cat*.inc` 的 `Text="#键#"`）。
- 双 remote：`origin` 是 `OMSociety/ReOmnimo`（本仓库），`upstream` 是 `fediaFedia/Omnimo`。`gh` 必须显式带 `-R OMSociety/ReOmnimo`，否则默认解析到 upstream。`upstream` 只作历史来源，不作对照基准——皮肤根 `WP7\` 改名 `REO\` 后全树分叉。

| 项 | 要求 |
|---|---|
| Rainmeter | 最低版本以 `Rainstaller.cfg` 的 `MinRainmeterVer` 为准；安装目录不在 PATH，命令里用 `"$env:ProgramFiles\Rainmeter\Rainmeter.exe"` |
| .NET | SDK 3.1–11；两个 C# 工程目标 `net48`，只用 `dotnet build -c Release` |
| CI / 测试 | 无 CI、无测试框架，验收标准即 §8 |

## 2 常用命令

| 目的 | 命令 |
|---|---|
| 核对改动面（提交前必跑） | `git status --porcelain`，逐条确认与意图一致 |
| 查 skip-worktree 标记 | `git ls-files -v`，行首 `S` 为已标记（现查 5 个：`Config\Panels\Slideshow\UserVariables.inc`、`Config\Panels\WorldClock\UserVariables.inc`、`Config\TextItems\MultiManager\UserVariables.inc`、`Gallery\MultiManager\Saved\2\screenshot.png`、`Gallery\MultiManager\TimeSettings.inc`） |
| 找残留旧路径 | `git grep -In "wp7" -- 'REO/*'` |
| 刷新全部并重扫描 | `& "$env:ProgramFiles\Rainmeter\Rainmeter.exe" !RefreshApp`，等约 9 秒再 Activate（未扫描完会找不到 config） |
| 激活单个面板 | `cmd /c '"%ProgramFiles%\Rainmeter\Rainmeter.exe" !ActivateConfig "REO\Panels\Agenda" "Item.ini"'`——pwsh 的 `&` 会吃掉带引号 bang 的内层引号；带引号参数的 bang 一律 `cmd /c` 包装，无参 bang（`!RefreshApp`/`!Quit`）`&` 直传 |
| 构建设置中心 | `dotnet build SettingsCenter\SettingsCenter.csproj -c Release` |
| 构建面板工具 | `dotnet build Tools\ActivePanels\ActivePanels.csproj -c Release`（其余五个工程同理） |
| 还原运行时写脏文件 | `git checkout -- REO\Gallery\main.ini REO\Gallery\scroll.inc` |

开日志：`%APPDATA%\Rainmeter\Rainmeter.ini` 是 UTF-16LE+BOM 且行尾可能为 LF，必须字节级只改 `(?m)^Logging=\d` 的单个数字。流程：`!Quit` → 备份 ini → 改 `Logging=1` → 手动启动 Rainmeter → 复现 → 读 `Rainmeter.log`（UTF-8+BOM，按 `utf-8-sig` 解码）抓 `ERRO` → `!Quit` → 改回 `0` → 重启。改完必须重启才生效，`!SetLogging` 实测无效；关闭 Logging 时 Rainmeter 删除日志文件，要留证据先另存。

## 3 架构

```text
REO\                              皮肤根（Rainmeter 加载此目录后即桌面布局）
├─ Launcher.ini / LauncherDark.ini
├─ Panels\【名】\Item*.ini          60 个面板目录
├─ TextItems\【名】\*.ini           17 个
├─ Hubs\【名】\                     3 个（Alarm / RSSBurst / Time）
├─ Background\  Gallery\          背景与磁贴库
└─ @Resources\
   ├─ Common\                     共用资源 + 分发 exe + Config\ 与 MultiManager\ 下的 C# 工具
   ├─ Config\Panels\【名】\         面板 UserVariables.inc 与 RainConfigure.cfg
   └─ Structure\【PanelType】\      8 档底板，各含 Main.inc
```

面板 ini 的 include 链，编号升序生效、后写者胜：

```text
@include   Common\Variables\UserVariables.inc               全局变量（MainLanguage / LangAuto）
@include1  Common\Variables\Languages\#MainLanguage#.inc    界面语言包
@include2  Common\color\color.inc                           主题（Padding / Opacity 唯一定义处）
@include3  Config\Panels\【名】\UserVariables.inc             面板参数
@include4  Structure\#PanelType#\Main.inc                   档位底板
@include5  Config\Panels\Agenda\UserVariables.local.inc     Agenda 专属，未跟踪
```

- 全局键只在 include 链的上游定义；面板 ini 不重定义 `Padding`/`Opacity` 一类主题键（另有 6 个范围外文件也定义它们，见 §9）。
- 磁贴表在 `Gallery\cat1/2/3/4/6/7.inc`：每格 = 面板目录名 + `Text="#语言键#"`，动作 `!ToggleConfig "REO\Panels\#CURRENTSECTION#" "Item.ini"`。增删或移动磁贴必须同步改图标层 `@Resources\Graphics\Gallery\mask-<类>.png`（复制相邻字形，不要自画）；跨行回流靠行锚点 `Y=(1*#ScaleDpi#)R` + `x=(360*#ScaleDpi#)`（`cat1.inc:93-94`）。
- 工具改皮肤只经 `Tools\Shared\OmnimoShared.cs`（WM_COPYDATA 发 bang、`IniDoc` 读写），不直接改动皮肤加载中的文件。

## 4 改动落在哪

| 我要改… | 改这里 |
|---|---|
| 面板参数 | `REO\@Resources\Config\Panels\【名】\UserVariables.inc`（首行必须是 `[Variables]`） |
| 界面语言 | `REO\@Resources\Common\Variables\Languages\Chinese.inc` 与 `English.inc`（UTF-16LE+BOM，按 §5 流程） |
| 工具的按钮文案 | `REO\@Resources\Common\Background\Language\*.cfg`（八份键集必须一致） |
| 面板设置界面 | `REO\@Resources\Config\Panels\【名】\RainConfigure.cfg`（4 行一组：参数名 / 标题 / 控件类型 / 空行） |
| 分发 exe | `Tools\` 六个工程或 `SettingsCenter\`，按 §6 构建并部署 |
| Agenda 的私人订阅 | `Config\Panels\Agenda\UserVariables.local.inc`（未跟踪，见 §6） |

## 5 编码契约

改皮肤文件（`ini`/`inc`/`cfg`/`lua`）必须用编码感知的补丁：二进制读入 → 按嗅探到的编码解码 → 替换 → 按同一编码写回且 `newline=""`。直接用文本工具写会把 `\r\n` 写成 `\r\r\n`。嗅探顺序：UTF-16LE+BOM → UTF-8+BOM → 会抛异常的严格 UTF-8 → cp936 → cp1252；纯 ASCII 会同时通过多种编码，另看是否含 `>= 0x80` 字节。

- 已知非 UTF-8：`Common\Variables\Languages\*.inc` 中除 `Auto.inc` 外均为 UTF-16LE+BOM（含中英两包）；`Gallery\cat7.inc`、`Config\Panels\Agenda\RainConfigure.cfg`、`Common\Background\Language\` 的六份 `.cfg` 同样是 UTF-16LE+BOM；Weather 面板 ini 与 `Common\Settings\Language\` 等为 cp1252/cp936。
- `Common\Variables\UserVariables.inc` 必须是 **UTF-8+BOM**：它是语言检测器 `!WriteKeyValue` 的写回目标，无 BOM 会被按系统 ANSI 读写、高位字节变 `?` 并可能吞行尾引号。会被运行时写回的 inc 一律带 BOM。
- `git grep` 把 UTF-16 文件当二进制跳过（`-I` 更彻底），grep 不到不等于不存在，先解码再查。
- cp936 与 cp1252 会互相误判；判不准时用 latin-1 读写（字节与码点一一对应），只替换其中的 ASCII 片段。

## 6 修改契约（按改动类型）

- **改 SettingsCenter（`config.exe` 来源）**：`dotnet build SettingsCenter\SettingsCenter.csproj -c Release`，把 `bin\Release\net48\` 的 `config.exe`、`config.exe.config`、`Microsoft.Web.WebView2.{Core,WinForms,Wpf}.dll`、`WebView2Loader.dll`、`lang\` 全量拷到 `REO\@Resources\Common\Config\`。csproj 锁死 `PlatformTarget=x86`——WebView2 的 net48 包只带 x86 loader，AnyCPU 会 `BadImageFormatException (0x8007000B)`。
  - 皮肤调用参数契约：`【模式】 【config】 【file】 【settings】 【skins】 【program】`；新增调用点必须传第 6 参 `#PROGRAMPATH#`（`ImageBG\bg.ini` 的 2 处 3 参形式是裁决保留的合法用法）。`config.exe` 解析 Rainmeter 路径的顺序：第 6 参 → `ProgramW6432`/`ProgramFilesDir`/`ProgramFiles(x86)` 下的 `Rainmeter\Rainmeter.exe` → PATH（`_RainmeterExe()`）。未处理异常落盘 `%TEMP%\sc_crash.txt`，诊断崩溃先看它；WebView2 用户数据目录按面板哈希分（`%LOCALAPPDATA%\ReOmnimo\SettingsCenter\【hash】`），不同面板可并行。
- **改 Tools（六个分发 exe）**：`dotnet build Tools\【名】\【名】.csproj -c Release`，把 `bin\Release\net48\【名】.exe` 单文件拷到部署位；`bin` 里的 `【名】.exe.config` 不部署。部署位：`ActivePanels.exe`→`Common\Config\`；`MultiManager.exe`→`Common\MultiManager\`；`ColorChanger.exe`→`Common\`；`ConfigBackground.exe`→`Common\Background\`；`PanelCreator.exe`→`Common\PanelCreator\`；`OmnimoApp.exe`→`Common\`。工程用 `<Compile Include="..\Shared\【文件】.cs" Link="…" />` 共享源码。
  - `OmnimoShared.cs` 三条硬约定：① bang 走 WM_COPYDATA 发给 `FindWindow("RainmeterMeterWindow")`，`dwData=1`、载荷 UTF-16 + 结尾 NUL、`cbData=(字符数+1)*2`；② `IniDoc` 的 UTF-8 解码必须用会抛异常的严格实例，否则 cp1252 的非法字节被静默换成 U+FFFD 写坏文件；③ 写键值时只替换 `=` 之后的值、保留键名大小写（`ColorSkin` 不能变 `colorskin`）。
  - 皮肤调用契约与命令字：`missing` / `lock` / `unlock` / `grid*` / `Save0` / `Save1` / `Load0` / `Load1` / `Delete`，其余模式静默 no-op。`ActivePanels` 每次运行先删 `hue.ini`、有活动面板时才重写；`OmnimoApp` 的 Align 是合并写；其余工具不碰。`hue.ini` 只写不读，不跟踪。`bin/`、`obj/`、`*.pdb`、`*.exe.WebView2/` 在 `.gitignore` 内，不要 `git add -f`。
- **改面板参数**：`UserVariables.inc` 首行必须是 `[Variables]`——缺段头整文件键被静默忽略，日志里只有下游异常。这些文件被跟踪，改完会脏，提交前逐个确认。
- **改私人订阅**：只写 `Config\Panels\Agenda\UserVariables.local.inc`（未跟踪，`.gitignore` 已覆盖），经 `@include5` 与 `UserVariables.inc` 合并，同名键以后者为准。`config.exe` 会自动跟随它读写，界面另显示本地覆盖提示——在设置界面改的值写进这个未跟踪文件，属有意行为。
- **改语言文案**：`Chinese.inc` 与 `English.inc` 的键集逐键对照（现查各 280 键、双向零差）。全库唯一的占位符约定是 `AgendaNoEvents` 的 `%1`（`agenda.lua` 用 `gsub('%%1', …)`），不要发明第二种写法。7 个键在中文包里故意保持英文：`Folders`/`weather`/`Humidity`/`Pressure`/`Wind`/`brightness`/`start`。磁贴名本地化在 `Gallery\cat*.inc` 覆盖 `Text="#键#"`，不要在磁贴里写死中文。其余六份语言包是遗留漂移态（缺 6–8 个现役键、含 109–166 个上游遗留键、存在大小写变体重复键），冻结不修复，完整性以中英两包为准；要动它们先单独提案。
- **改工具语言包**：八语键集必须一致，新增键同时补全 8 份（现查各 54 键、两两全等；缺键时按钮显示英文键名）。改动按 §5 流程，六份 UTF-16LE+BOM、`English`/`Dutch` 为 UTF-8 无 BOM。
- **改语言跟随**：机制在 `Common\Variables\AutoLang.lua` + `Languages\Auto.inc`，宿主是 `Background\bg.ini` 与 `Gallery\main.ini` 末尾的 measure 对（Registry 读 `LocaleName`，`UpdateDivider=-1`）。语言→包映射在 lua 顶部，改语言包集合必须同步 lua、README 与 `Auto.inc`；`Auto.inc` 保持为指向 `English.inc` 的一行 shim。两个宿主 ini 编码不同（`bg.ini` 单字节、`main.ini` UTF-16LE+BOM），追加内容按各自编码写。检测器只在 `LangAuto=1` 且系统语言与 `MainLanguage` 不同时写回；换 Windows 语言后 `Common\Variables\UserVariables.inc` 与 `Common\Background\Varrar.inc` 各脏一次，属预期。
- **改面板设置 schema**：`RainConfigure.cfg` 4 行一组，第 4 行非空且不是 `[Options]` 会弹读取失败。`Checkbox:a:b` 的契约是未勾选写 a、勾选写 b——把 `Checkbox:1:0` 读成「勾选=1」会把 `Hidden=#X#` 判反。
- **加 Agenda 渲染样式**：现只有一套排布，`AgendaStyle` 变量不存在（现查命中 0）；要加样式先单独提案。
- **改 `agenda.lua`**：Rainmeter 公式里 `**` 是幂运算符（`7**#TypeH#`）；Lua 的 `0` 是真值，判断开关要显式比较。含 `"` 的值走三引号 bang `!SetOption 【meter】 【opt】 """值"""`，写成 `""` 会报 `Skin "X" does not exist` 且值不变。Rainmeter 按系统 ANSI 读 `.lua`（中文 Windows 为 CP936），源里字符串字面量必须纯 ASCII。

## 7 禁止操作

- 禁止把私人日历订阅 URL 写进任何被跟踪文件、脚本或文档——公开订阅链接本身就是凭据。自检：对 `git rev-list --all -- REO/@Resources/Config/Panels/Agenda/UserVariables.inc` 的每个 blob 比对 Feed 行的长度 + sha256 前 16 位；不要用 `git log -S`（关键词入库后必自匹配）。
- 禁止提交会被运行时改写的文件：`Gallery\main.ini`、`Gallery\scroll.inc`、`Gallery\Intro\save.inc`、`Gallery\MultiManager\TimeSettings.inc`、`Gallery\MultiManager\Saved\*\`；`Gallery\Settings\font.ini`、`Gallery\Color\save.inc`、`Gallery\Color\color.ini`、`Gallery\MultiManager\Item.ini` 的 `Labels2/3` 也会被写。误改用 `git checkout --` 还原。
- 禁止手改分发 exe，或只改源码不重编译就发版。原因：用户运行的是二进制，源码修复不重编译等于没修。要改 exe 必须先按 §6 构建 C# 源并部署单文件产物。
- 禁止为「修语义反转」改 `Hidden=#ShowSeconds#` / `Hidden=#ShowExternalIP#` 一类写法：前者配 `Checkbox:1:0`（勾选写 0，启用即显示，正确）；后者是外网/内网 IP 同位互换设计（靠 `Formula=-1*#ShowExternalIP#+1` 反转配合）。改前先读该面板 `RainConfigure.cfg` 的 Checkbox 规约。
- 禁止「顺手修复」上游遗留死文件：`Languages\Backup\`、`Languages\lang.inc`、`Common\Settings\UserVariables.inc`（只有写入者无读取者）。清理需单独提案，混进功能改动会污染 diff。
- 禁止在文档与提交里放凭据、真实邮箱；对外文档零 emoji，提示块写 `> **注意**:`；commit message 用英文说清 Why，一个提交只做一件事。
- 面板 ini 的 `License=` 统一写 `Creative Commons Attribution-Noncommercial-Share Alike 3.0 License`；不许改动 `TextItems\Search\` 三份 NoDerivs 文件的许可声明。

## 8 验收标准

改动完成 = 下列全部通过：

1. `git status --porcelain` 改动面与意图一致，不含运行时文件与 skip-worktree 文件（`git ls-files -v` 逐条核对）。
2. 改过 C# 工程：`dotnet build -c Release` 零 error，按 §6 部署后实机验证——`VersionInfo.FileVersion` 等于本次发布号；`CreateProcess` 带 `CREATE_SUSPENDED` 映射镜像后立刻 `TerminateProcess` 过可加载性自检；命令行工具实机跑 `missing` 与一条未知命令（退出码 0、日志无新增 `ERRO`），GUI 工具用真实皮肤参数启动确认界面与控件齐全；`SettingsCenter` 另需右键皮肤实机验证设置窗口与写回。
3. 动过皮肤行为：开日志（§2）实机验证无新增 `ERRO`，验完把 `Logging` 关回。日志验收判据：出现任何 `ERRO` 都按新缺陷处理；已知噪声仅变量为空时的 `ImageName: Unable to open: …\OverlayBorder\` 一条。视觉对比用差异像素占比给阈值，不要求逐字节相同（截图前关动态壁纸、移开光标、等 WebParser 完成）。
4. 发版一次闭口：改动全提交 → `git tag -a X.Y.Z -m "…"`（不带 `v` 前缀）→ `git push origin main X.Y.Z` → `gh release create X.Y.Z -R OMSociety/ReOmnimo --title X.Y.Z -F 【正文文件】`（双 remote 下必须显式 `-R`；正文 = 一句中文摘要 + `CHANGELOG.md` 对应小节）。发布号只活在 tag + `CHANGELOG.md` 标题 + GitHub Release 三处；仓库内 `Version=` 字段（`Rainstaller.cfg` 10.0.4、`OmnimoVersion` 10.0、`Settings` 6.0.1、`RMSKIN.inc` 1.0）属上游自有体系，不要动；号码动手前先向用户确认。

## 9 已知风险区

| 位置 | 风险 | 前置动作 |
|---|---|---|
| `agenda.lua` 数据通路 | WebParser `Download=1` 时字符串值是下载文件路径而非正文（58–64 字符） | 必须 `RegExp=(?s)^(.*)$` + `StringIndex=1` 整页捕获 |
| `agenda.lua` 窗口计算 | 窗口 = 恰好 `RangeDays` 个日历日（闭区间），今天与第 `RangeDays` 天都显示 | 以闭区间为唯一定义，`RangeDays` 直接决定天数（面板参数见 `Config\Panels\Agenda\UserVariables.inc`） |
| `agenda.lua` 时区表 | 表内是标准时偏移，夏令时期间偏 1 小时；未知 TZID 按本机墙上时间并只记一次 Notice | 不要引入完整 IANA 库 |
| `agenda.lua` 每秒门控 | `Update()` 的两级缓存不能拆（旧实现每 tick 推约 120 条 bang） | 用仓库外探针皮肤验证，读数取 `SKIN:GetMeter('Row1'):GetOption('Text')` |
| `[Clip]` 裁剪 | 被指定为 Container 的表自身不绘制，拿 `[bg]` 当遮罩会让磁贴背景消失 | 另建不透明独立遮罩表 |
| `RainConfigure.cfg` | 第 4 行非空且不是 `[Options]` 会弹读取失败 | 每组后留空行；改 schema 前按 4 行一组核对 |
| 新建皮肤目录 | 不 `!RefreshApp` 直接 `!ActivateConfig` 找不到 config | 先 RefreshApp 再 Activate |
| 大网格自动排布 | 3x3 分支在启用面板 ≥31 时生效，行表必须含 `$row9` | 改排布以 `Tools\Shared\OmnimoShared.Tools.cs` 的 `Aligner.GridPosition` 为准（`Tools\ActivePanels\Program.cs:180` 调用） |
| `Tools\` 的 GUI 工具 | ModernForm 无边框壳：圆角由 DWM 裁剪，禁止再叠 WinForms `Region`（双重圆角会在角上反白）；`ConfigBackground` 滑杆上限 = 进程视角桌面尺寸，超出夹断、Apply 写回夹断值；PanelCreator 的 `OnSaveClick` 先切到 exe 目录再走相对路径；`ModernUi.UiFont()` 返回共享缓存字体，禁止 `using`/`Dispose`；DPI 靠 `app.manifest`（PerMonitorV2）+ `ModernForm.OnHandleCreated` 手动 `Scale`，别用 `AutoScaleMode.Dpi`（因子算成 1），自绘常量乘 `ModernUi.PxU/DpiU`；`ModernCheckBox` 基类是 `Control` 不是 `CheckBox` | 截图用 PrintWindow（`flags=2`）或同 DPI 下的 `CopyFromScreen`，光标钉主屏，必要时出控件转储 |
| 主题键定义 | `Padding`/`Opacity`/`Opacity2`/`Globalblurenable`/`Xposition` 除 `Common\Color\*.inc` 外，还有 6 个范围外文件定义（`Config\TextItems\Extra\{Menu,AnotherMenu,AndAnother}\UserVariables.inc`、`#Win8Sidebar\Addons\Config` 与 `G'DayMate` 系、`Hubs\RSSBurst\Hub.ini`） | 查键定义别只看 Color 目录；换主题会整体覆盖 `color.inc`，提交的默认值会被改回主题自带值，属有意边界 |
| `!WriteKeyValue` 写回 | Rainmeter 对无 BOM 的非 UTF-8 文件按系统 ANSI 读写，高位字节变 `?`、可能吞行尾引号 | 会被写回的 inc 一律带 BOM（§5） |

## 10 出错怎么办

| 症状（可检索片段） | 处理 |
|---|---|
| `12006` / `未使用可识别的协议` | 面板配置 inc 缺 `[Variables]` 段头，或 `Url` 变量未定义；查 include 顺序与变量拼写（变量名不区分大小写） |
| `Unable to read RainConfigure.cfg` | schema 坏，见 §9 |
| `tag exists locally but has not been pushed` | `gh` 解析到了 upstream；命令补 `-R OMSociety/ReOmnimo` |
| 截图每张都不同 | 半透明面板透出动态壁纸与亚像素抖动；关动态壁纸，用差异像素占比判据 |
| 写回后行尾全乱 | `\r\n` 被写成 `\r\r\n`；补丁脚本补 `newline=""` |
| 重启后 `Rainmeter.ini` 只剩一百多字节、皮肤全没 | 真 BOM 被写坏（残迹 `EF BF BD EF BF BD`），Rainmeter 按 CP936 读成空配置写回默认值。该文件是 UTF-16LE + 真 `FF FE` BOM，从备份恢复，别在缺 BOM 的文件上做字节级替换；行尾按实际处理（可能纯 LF） |
| 开日志报 `ABORT (no write): file has no CRLF line endings` | 该 ini 当前是纯 LF，改 §2 的字节级只改一个字符的做法，不做整文件重写 |

## 11 维护

- 本文件与触发它的改动进同一个提交，不攒批；发现与代码不符时先改本文件。必须同步：验收命令、include 链、Checkbox 契约、凭据规则、分发 exe 清单与重编译命令、风险区。
- 事实性数字（键数、目录数、文件大小）引用前现查，不凭记忆。
