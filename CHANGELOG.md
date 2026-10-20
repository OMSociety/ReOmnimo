# Changelog

本项目的更改记录在此文件。

All notable changes to this project are documented in this file.

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)；
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.8.0] - 2026-10-21

### 移除

- Gallery 设置页指向不存在的 `Gallery\themes` 的死「布局主题」按钮（绿块与标题一并移除）；「检查更新」按钮改为绝对定位。

### 变更

- Gallery 设置页的四个主题快捷按钮补纯色占位块：此前无图无尺寸，在页面上不可见也不可点。
- ActivePanels 的面板着色判断改用共享实现（行为不变）。

### 修复

- 画廊变量全盘失效导致磁贴与面板底板消失：`@Resources\Common\Gallery\Color\Modern\{Dark,Light}\tt.inc`、`@Resources\Common\Gallery\Color\TT\{AeroD,AeroL,Dark,Light}\tt.inc` 与 `TextItems\#TopBar\TopBarAddons.ini` 共七份文件的头部出现两个 UTF-16 BOM（`FF FE FF FE`），Rainmeter 只识别第一个，第二个把 `[Variables]` 段头吃掉，`#galleryW#`/`#galleryH#`/`#ttgallerystyle#` 等变量全部解析失败。去掉多余的 BOM 后磁贴恢复。

- 默认浏览器启动后地址栏带引号、报 `ERR_NAME_NOT_RESOLVED`：0.7.0 重写 `OmnimoApp.exe` 的 `Browser` 命令时给 URL 补了外层引号，而 Chromium 的 `--single-argument` 把该开关之后的整段原样当 URL，引号因此进入地址。现按注册表模板原样替换 `%1`，并从 URL 中剔除裸双引号。

- 多管理器载入存档时弹「Too few command line arguments specified」：载入按钮把 `[MeasureWindowsVersion]` 当第五个参数传给 `MultiManager.exe`，而该 measure 只在 `Noice.ini` 里定义、不在 `Item.ini` 的 include 链中，解析为空后只剩四个参数，撞上五参数下限。现按 AutoIt 的语义解析命令行，尾随反斜杠的 `#SETTINGSPATH#`/`#SKINSPATH#` 不再吞掉下一个参数，`MultiManager.exe` 与其余工具同步受益。

- 随包布局存档 1 仍指向 0.6.0 之前的 `WP7\` 根，且含已移除面板的段，载入后所有皮肤配置名都不存在、桌面被清空。`Gallery\MultiManager\Saved\1\Rainmeter.ini` 19 处、`Gallery\MultiManager\Saved\1\Layouts\Omnimo\Rainmeter.ini` 24 处改指 `REO\`，并删除 Anniversary / Donate / DonatorPanels\GoldPrice / Gallery\Intro\QuickStart 四个残留段。

- 天气面板的单位与代码写回落到无人读取的文件：`Panels\Weather\UserVariables.inc` 没有被任何皮肤 include，删掉对它的写回与文件本身；设置页的天气代码栏改读改写真身 `@Resources\Common\Weather\WeatherComVariables.inc` 的 `LocationCode`。

- 面板状态开关（如「深色模式」）的 On/Off 两行同时出现：这对图标靠「另一半文件不存在」实现互斥，把拼接变量固定成数字后两张图都会命中。恢复按 `#OnOff#`/`#ttgallerystyle#` 拼接的写法。

- `Gallery\main.ini` 与 `Gallery\Color\coloradvanced.ini` 里的 `!Move#Move#` 是无效 bang（`Move` 从未被设为 1），移除。

- Folders 面板点击选择文件夹后「隐藏图标」「隐藏占位符」不生效：两个 `!WriteKeyValue` 写的是 `Panels\Folders\FolderN\UserVariables.inc`，而面板 `@include3` 读的是 `@Resources\Config\Panels\Folders\FolderN\UserVariables.inc`，改名后的文件没有任何读取者。写回目标改为与 include 相同的文件，并删除五个只被写、从不被读的空壳。

- 日志里约 2800 条「图片打不开」噪声：主题配色为 `Overlay=none` 时，`#overlay##padding#` 拼出的 `none0.png` / `none3.png` / `none5.png` 等组合在 `@Resources\Common\OverlayBorder\` 里没有对应文件。补齐为全透明占位（160x160，与现有 21 张同尺寸），叠加结果不变、错误消失；另补五个主题各自的缺失组合（`green0` / `htc0` / `hud0` / `inner0` / `nostalgic0`）。
- 音量面板首次刷新时报 `v0.png` / `0.png` 打不开：`Hidden` 由公式在 measure 就绪前求值，`[MeasureAudioDG]` 返回 0，拼出不存在的图名。补两张同尺寸透明占位（322x253）。
- 面板状态开关（`Gallery\cat7.inc`、`Gallery\Intro\introLang.ini`、`Gallery\MultiManager\Item.ini` 共六处）对非当前侧报「图片打不开」：这对图标靠「另一半文件不存在」实现互斥，因此补六张同尺寸全透明占位，并给两行各加 `H=(<开关变量>=<当前值>?28*#ScaleDpi#:0)`——占位图高度为 0 时不占位，排版与补图前逐像素一致。
- 多管理器的删除 / 导出 / 快捷方式按钮首次渲染时报 `delete0.png` / `export0.png` / `shortcut0.png` 打不开：`#ItemNHideSave#` 为 0 时拼出的文件名与仓库里的 `delete1.png` 一组相反。补三张同尺寸透明占位（60x60）。
- Intro 页面在 0 到 3 点报 `GalleryImages\0.jpg` 打不开：`[MeasureTimeOfDay1]` 的凌晨分支落到 0，而目录里只有 1111–4444 四张。补 `0.jpg`（沿用 `4444.jpg` 的夜空）。
- 网络面板每条日志都报 `[MeasureIP]: RegExp matching error (-1)`：`Url=http://checkip.dyndns.org/"` 行尾多一个裸双引号（同文件第 82 行注释掉的旧写法已带 `%22`），请求地址因此带引号、正则匹配不到。移除该引号；同目录 `Item2.ini` 与 `Network3.ini` 的同款引号一并移除，`Item.ini` 的 `Substitute` 表达式补上缺失的尾引号（日志的 `Invalid Substitute` 一并消除）。

**设置中心（config.exe）**

- 设置面板的取色器打开时恒为黑色、且无法带透明度保存：取色器从错误的 DOM 源读初始颜色（读到恒为空的文本节点）。改为读颜色样张的 `title` 属性，RGB 与 alpha 正确带入；`Background`、`GroupTitles`、`TrajanQuote` 等 15 个带透明度的 Color 键恢复正常取色与保存。
- 面板设置里「浏览文件」类参数全部弹文件夹选择框：设置中心只实现了文件夹分发。schema 与实现统一为文件夹选择，185 处 `Browse:File` 参数随本版改为 `Browse:Folder`。
- 背景设置页（ImageBG 三参调用）打开即弹用法说明窗：参数个数校验把三参形式的合法调用判为错误。校验下限调整，用法文案同步更正。
- 设置写回失败（文件被占用、无权限）时无任何界面回执：写回操作包上异常处理，失败原因以状态栏提示。
- 简体中文语言包清理 9 个死键、补 6 处缺译；界面语言包新增 `refresh_failed` 中文文案。
- 嵌套 JSON 解析加 64 层深度上限，畸形 schema 不再可能拖垮进程。
- 设置窗口重复打开（0x800700AA）时的错误文案改为明确的中文说明。
- Checkbox 控件支持第四段状态词（63 处 cfg 的 `Checkbox:off:on:标签` 形式标签恢复显示）。
- exe 与清单的版本资源更新为 0.8.0.0。

**面板工具（Tools 六个 exe）**

- 高 DPI 屏上三个 GUI 工具的自绘控件不随 DPI 缩放：P/Invoke 声明的函数名有笔误（`GetDpiForWindowX`），调用恒失败且异常被吞、缩放因子恒为 1。改为正确的 `GetDpiForWindow`。
- 工具在语言包缺键时按钮/标题显示为空白：缺键回退显示键名本身，便于发现语言包缺口。
- ColorChanger 批量上色遇到 `Active="00"` 的磁贴时中断其后整批：补 active 计数护栏。
- MultiManager 的 index 参数拒绝 `..` 路径穿越。
- MultiManager 保存布局时目标目录不存在即失败：保存前创建目录并返回明确错误码，调用方按错误码提示。
- ActivePanels 对契约外变体（`[close]`/`[Hint]`）显式 no-op，不再落网格读取路径。
- ColorChanger 的容量检查提前到写列表前，超限面板不再进入列表。
- 工具间的注释、头编号与 `.gitignore` 描述按实况勘正。

**皮肤与资源**

- TextItems 开关面板的七个开关全部失效：开关变量按实际面板目录与入口文件成对重写（`ToggleN`/`ToggleNFile`），50 处引用同步。
- SolidBG 背景的模糊设置失效：`Blur` 键与上一行粘连被吞，拆为独立行。
- `Launcher.ini` 与 `LauncherDark.ini` 的图库快捷按钮动作引号未闭合：补右引号。
- cat3 四个磁贴名与动作目标错位：磁贴段名改为与动作一致（System / Weather / Calendar / NowPlaying）。
- cat1 的 Anniversary 死磁贴删除（对应面板目录不存在）。
- uTorrent 面板设置全部失效：配置文件首行是空行，缺 `[Variables]` 段头，删除前导空行。
- 12 处 `config.exe` 调用缺第 6 参数（`#PROGRAMPATH#`）：全部补齐，安装路径解析不再依赖回落探测。
- Weather 面板的设置开关默认值改为关闭。
- Slideshow 与 HDD 面板的尺寸档间 1px 高度差对齐。
- RAM 面板配置清理 18 个失效的电台键，保留有效键。
- Reader 与 RSSBurst 的点击变量段补 `DynamicVariables=1`，点击跳转恢复。
- Search 面板的搜索引擎域改从语言包读（`Languages\#MainLanguage#.inc`），中英文包补 `languagedomain` 键。
- Visualizer 频段数三处不一致（Bands/BandIdx/条形数）对齐为 47，补 `MeterBand46`。
- 补 6 处缺失图片占位（`b.png`、`close1.png`、`file.png`、`folder.png` 等）。
- 10 处 `#FontFaceWP#` 引用改为实际存在的 `#FontTypeWP#`。
- WeatherWeek 补 `HighlightCurrentDay=0` 默认值（该变量引用原先无定义源）。
- 面板用户参数清理 79 个死键（34 份文件）。
- Folders 与 dialogHibernate 的引号勘误。
- 三处背景配置引用的 `curl.png` 补图；六份语言包 OnOff 备用态引用的 `onalt0light.png`/`onalt0dark.png` 补图。
- 资源清理 106 个文件：Structure 的 win10/win7 容器与 Circle/HugeV/miniCircle 死档、`Panels\Clock\normal.inc`、Hubs 两张死图、`Common\Overlay\` 冗余副本 67 张、`Graphics\Panels\Notes++\` 11 张；Hubs 三份配置同步删除指向已清理文件的死键。

**文档**

- `AGENTS.md`、`THIRD-PARTY.md` 的计数、清单与表述按当前树实测刷新（编码分布、License 计数、Structure 档位、调用点参数、字体一节重写）。

### Removed

- Removed the dead "Layout Themes" button in the Gallery settings page that pointed to the nonexistent `Gallery\themes` (green block and title included); the "Check for Updates" button is now absolutely positioned.

### Changed

- The four theme shortcut buttons in the Gallery settings page now show solid color placeholders: previously they had no image and no size, so they were invisible and unclickable.
- The per-panel colorizable check in ActivePanels now uses the shared implementation (no behavior change).

### Fixed

- Gallery variables failed wholesale, blanking tiles and panel backdrops: seven files (`@Resources\Common\Gallery\Color\Modern\{Dark,Light}\tt.inc`, `@Resources\Common\Gallery\Color\TT\{AeroD,AeroL,Dark,Light}\tt.inc` and `TextItems\#TopBar\TopBarAddons.ini`) began with two UTF-16 BOMs (`FF FE FF FE`). Rainmeter reads only the first, so the second swallowed the `[Variables]` header and `#galleryW#`/`#galleryH#`/`#ttgallerystyle#` and friends all failed to resolve. Removing the extra BOM restored the tiles.

- The default browser opened an address with a leading quote and reported `ERR_NAME_NOT_RESOLVED`: the 0.7.0 rewrite of the `Browser` command in `OmnimoApp.exe` wrapped the URL in quotes, but Chromium's `--single-argument` treats everything after that switch as the URL, so the quotes ended up in the address. `%1` is now replaced verbatim and bare double quotes are stripped from the URL.

- Loading a saved layout in MultiManager raised "Too few command line arguments specified": the load button passes `[MeasureWindowsVersion]` as the fifth argument, but that measure is defined only in `Noice.ini` and not in `Item.ini`'s include chain, so it resolved to nothing and only four arguments arrived, tripping the five-argument minimum. The command line is now parsed the way AutoIt did, so trailing-backslash `#SETTINGSPATH#`/`#SKINSPATH#` no longer swallow the next argument; `MultiManager.exe` and the other tools benefit alike.

- The bundled layout 1 still pointed at the pre-0.6.0 `WP7\` root and carried sections for removed panels, so loading it left every skin config name nonexistent and cleared the desktop. 21 references in `Gallery\MultiManager\Saved\1\Rainmeter.ini` and 29 in `Gallery\MultiManager\Saved\1\Layouts\Omnimo\Rainmeter.ini` now use `REO\`, and four stale sections (Anniversary / Donate / DonatorPanels\GoldPrice / Gallery\Intro\QuickStart) are gone.

- The weather panel wrote its unit and code back to a file nobody read: `Panels\Weather\UserVariables.inc` was not included by any skin, so the writes and the file are gone; the weather-code field on the settings page now reads and writes `LocationCode` in the real file, `@Resources\Common\Weather\WeatherComVariables.inc`.

- Both rows of a panel state toggle (e.g. "Dark Mode") appeared at once: the pair relies on the other half's image being absent, so pinning the concatenation variables to numbers made both images resolve. The `#OnOff#`/`#ttgallerystyle#` concatenation is restored.

- Removed the invalid `!Move#Move#` bang from `Gallery\main.ini` and `Gallery\Color\coloradvanced.ini` (`Move` is never set to 1).

- In the Folders panel, the "hide icon" and "hide placeholder" toggles did nothing after picking a folder: the two `!WriteKeyValue` calls wrote to `Panels\Folders\FolderN\UserVariables.inc`, while `@include3` read `@Resources\Config\Panels\Folders\FolderN\UserVariables.inc`, so nothing ever read the written file. The write targets now match the include, and the five write-only shells are gone.

- Roughly 2800 "unable to open image" log lines: with the theme on `Overlay=none`, `#overlay##padding#` produced combinations such as `none0.png`, `none3.png` and `none5.png` that had no counterpart in `@Resources\Common\OverlayBorder\`. They are now fully transparent placeholders (160x160, matching the 21 existing files), so the overlay result is unchanged and the errors are gone. The five missing per-theme combinations (`green0`, `htc0`, `hud0`, `inner0`, `nostalgic0`) are filled the same way.
- The volume panel logged `v0.png` / `0.png` as unopenable on its first refresh: `Hidden` is evaluated from a formula before the measure is ready, so `[MeasureAudioDG]` returned 0 and produced a filename that does not exist. Two transparent placeholders of the same size (322x253) are added.
- The panel state toggles (`Gallery\cat7.inc`, `Gallery\Intro\introLang.ini`, `Gallery\MultiManager\Item.ini`; six sites) logged an unopenable image for the inactive half: the pair relies on the other half's file being absent, so six fully transparent placeholders are added and each row gains `H=(<toggle variable>=<active value>?28*#ScaleDpi#:0)` — a placeholder with zero height takes no space, and the layout is pixel-identical to before.
- The MultiManager delete / export / shortcut buttons reported `delete0.png` / `export0.png` / `shortcut0.png` on first render: when `#ItemNHideSave#` is 0 the concatenation names the opposite half of the `delete1.png` group in the repository. Three transparent placeholders of the same size (60x60) are added.
- The Intro page reported `GalleryImages\0.jpg` as unopenable between midnight and 3 am: the small-hours branch of `[MeasureTimeOfDay1]` yields 0 while the directory only holds 1111 to 4444. `0.jpg` is added (reusing the night sky from `4444.jpg`).
- Every network panel log entry carried `[MeasureIP]: RegExp matching error (-1)`: the line `Url=http://checkip.dyndns.org/"` had a stray trailing double quote (the commented-out older form on line 82 already carries `%22`), so the request URL included the quote and the regex never matched. The quote is removed; the same stray quote in `Item2.ini` and `Network3.ini` goes with it, and the `Substitute` expression in `Item.ini` gains its missing closing quote (clearing the log's `Invalid Substitute` as well).

**Settings center (config.exe)**

- The settings-panel color picker opened fully black and could not save with alpha: it read the initial color from the wrong DOM source (an always-empty text node). It now reads the swatch's `title` attribute, carrying both RGB and alpha; the 15 alpha-bearing Color keys (`Background`, `GroupTitles`, `TrajanQuote` and friends) pick and save colors correctly again.
- Every "browse file" parameter in panel settings opened a folder picker: the settings center only implemented the folder branch. Schema and implementation are unified on folder picking; 185 `Browse:File` parameters become `Browse:Folder` in this release.
- The background settings page (3-argument ImageBG calls) opened straight into the usage dialog: the argument-count check treated the legitimate 3-argument form as an error. The floor is adjusted and the usage text corrected.
- Settings write-back failures (locked file, missing permissions) gave no interface feedback: writes are wrapped with exception handling and report failures in the status bar.
- The Simplified Chinese UI pack drops 9 dead keys and gains 6 missing translations; a Chinese `refresh_failed` string is added.
- Nested JSON parsing gains a 64-level depth cap, so a malformed schema can no longer grind the process down.
- The error message for a second concurrent settings window (0x800700AA) is now an explicit Chinese explanation.
- Checkbox controls support the fourth status-word segment (labels in the `Checkbox:off:on:label` form across 63 cfg files render again).
- The exe and manifest version resources move to 0.8.0.0.

**Panel tools (the six Tools executables)**

- On high-DPI screens the self-drawn controls of the three GUI tools did not scale: a typo in the P/Invoke name (`GetDpiForWindowX`) made the call fail permanently, the exception was swallowed and the scale factor stayed at 1. It now calls the correct `GetDpiForWindow` export.
- Tools showed blank buttons/titles when a language-pack key was missing: a missing key now falls back to the key name itself, exposing pack gaps.
- ColorChanger batch coloring stopped at a tile with `Active="00"` and lost the rest of the batch: an active-count guard is added.
- MultiManager's index parameter rejects `..` path traversal.
- Saving a layout into a missing directory now creates it and returns explicit error codes; callers surface them.
- ActivePanels treats out-of-contract variants (`[close]`/`[Hint]`) as explicit no-ops instead of falling into the grid-read path.
- ColorChanger's capacity check moves ahead of list building, so over-limit panels never enter the list.
- Comments, shared-header numbering and `.gitignore` descriptions are corrected to match reality.

**Skins and resources**

- All seven toggles in the TextItems toggle panel were dead: the toggle variables are rewritten as directory/entry pairs (`ToggleN`/`ToggleNFile`) against the real panel directories, with 50 references updated.
- SolidBG's blur setting was dead: the `Blur` key was glued onto the previous line and swallowed; it is split onto its own line.
- The gallery shortcut button actions in `Launcher.ini` and `LauncherDark.ini` had an unclosed quote: fixed.
- Four cat3 tiles mismatched their names and actions: the section names now match the actions (System / Weather / Calendar / NowPlaying).
- The dead Anniversary tile is dropped from cat1 (its panel directory does not exist).
- Every uTorrent panel setting was dead: the config file began with a blank line, missing the `[Variables]` header; the leading blank line is removed.
- 12 `config.exe` calls missed the 6th argument (`#PROGRAMPATH#`): all filled in, so installation-path resolution no longer depends on the fallback probe.
- The Weather panel's settings toggle defaults to off.
- 1px height differences across the Slideshow and HDD size tiers are aligned.
- The RAM panel config drops 18 dead radio-station keys and keeps the live ones.
- Reader and RSSBurst click-variable sections gain `DynamicVariables=1`; click-through navigation works again.
- The Search panel's engine domain now comes from the language pack (`Languages\#MainLanguage#.inc`); the Chinese and English packs gain the `languagedomain` key.
- The Visualizer's three inconsistent band counts (Bands/BandIdx/bar count) align on 47, and `MeterBand46` is added.
- 6 missing image placeholders are filled (`b.png`, `close1.png`, `file.png`, `folder.png` and others).
- 10 `#FontFaceWP#` references change to the existing `#FontTypeWP#`.
- WeatherWeek gains `HighlightCurrentDay=0` (the variable reference previously had no defining source).
- 79 dead keys are cleared from panel user variables (34 files).
- Quote corrections in Folders and dialogHibernate.
- `curl.png` referenced by three background configs is added; the `onalt0light.png`/`onalt0dark.png` alternates referenced by six language packs' OnOff rows are added.
- 106 files of resource cleanup: the Structure win10/win7 containers and the Circle/HugeV/miniCircle dead tiers, `Panels\Clock\normal.inc`, two dead Hubs images, the 67 redundant copies under `Common\Overlay\`, 11 under `Graphics\Panels\Notes++\`; the three Hubs configs drop their dead keys in step.

**Documentation**

- Counts, listings and wording in `AGENTS.md` and `THIRD-PARTY.md` refreshed against the measured tree (encoding distribution, License counts, Structure tiers, call-site arguments, and a rewritten fonts section).

## [0.7.0] - 2026-10-19

### 移除

- **删除 AutoIT 源树**：0.6.0 起七个分发 exe 已全部是 C# 重写，`.au3` 参照树（含 `AutoIT\Language\` 工具语言镜像）连同指向它的引用一并移除——`AGENTS.md` 保留一行移除说明，`THIRD-PARTY.md` 把 UDF 一节改写为幸存的 C# 衍生文件（`ColorPickerForm` / `IconExtractor` / `ZipWriter`）并保留原作者署名。

### 变更

- **两份 `Extra\*.rmskin` 附加包要求 Rainmeter 5.0**：`MinRainmeterVer` 由 2.5 提到 5.0——旧值会让安装器跑在低于套件目标的引擎上。
- **cp936 文件转 UTF-8**：`Config\Panels\uTorrent\RainConfigure.cfg` 与 `Config\Panels\Launcher\Launcher2\UserVariables.inc` 转为 UTF-8，库内 schema 的最后两份非 Unicode 文件清零。
- 文档回填：`AGENTS.md`、`THIRD-PARTY.md` 与 `SettingsCenter\README.md` 的计数与尺寸按 0.7.0 树实查刷新。

### 修复

- **2026-10-18 全量审查的 63 项缺陷**：
  - 皮肤文件：69 处死 `@include`（指向不存在的 `REO\Common\` 树）、约 250 处静态缺失引用（补建写回目标或改指现有资源）、Gallery 死动作与箭头样式死写、孤儿配置入口、机器相关路径与拼错键名；
  - SettingsCenter：cp936/UTF-16 编码处理、Rainmeter 安装路径回落、WebView2 注入加固；
  - 工具：随机上色改用共享随机源（不再逐磁贴新建 `Random`）、三个 GUI 工具接通主题参数解析、共享代码收敛、保存遇到首个失败即报错退出；
  - Notes 刷新动作移除对 `Notes\scroll.inc` 的 `ScrollStart` 死写。
- `Chinese.inc` / `English.inc` 清除 5 组完全重复键（定义行 285 → 280，与唯一键数对齐）。

### Removed

- Removed the AutoIT source tree: every executable has been a C# rewrite since 0.6.0, so the `.au3` reference tree (including the `AutoIT\Language\` tool-pack mirrors) and the references naming paths inside it are gone; `AGENTS.md` keeps a one-line removal note, and `THIRD-PARTY.md` rewrites the UDF section around the surviving C# derivatives (`ColorPickerForm` / `IconExtractor` / `ZipWriter`) with their original author credits.

### Changed

- The two bundled `Extra\*.rmskin` packages now require Rainmeter 5.0 (`MinRainmeterVer` raised from 2.5): the old value let the installer run against engines older than the one the suite targets.
- Re-encoded `Config\Panels\uTorrent\RainConfigure.cfg` and `Config\Panels\Launcher\Launcher2\UserVariables.inc` from cp936 to UTF-8; no non-Unicode schema is left in the tree.
- Refreshed the measured counts and sizes in `AGENTS.md`, `THIRD-PARTY.md` and `SettingsCenter\README.md` for the 0.7.0 tree.

### Fixed

- Fixed 63 defects from the 2026-10-18 full review:
  - skin files: 69 dead `@include` references (targets under the removed `REO\Common\` tree), ~250 statically missing references (write-back targets recreated or paths repointed), dead Gallery actions and arrow-style writes, orphan config entries, machine-specific paths and misspelled keys;
  - SettingsCenter: cp936/UTF-16 encoding handling, Rainmeter installation-path fallback, WebView2 injection hardening;
  - tools: random coloring uses one shared `Random` instead of one per tile, theme flags (`--light`/`--dark`) now parsed in all three GUI tools, shared-code convergence, save reports and exits on the first failure;
  - dropped the dead `ScrollStart` write to `Notes\scroll.inc` in the Notes refresh action.
- Cleared 5 groups of duplicate keys in `Chinese.inc` / `English.inc` (285 defined keys down to 280, matching the unique-key count).

## [0.6.0] - 2026-10-18

### 新增

- **全新设置中心 `SettingsCenter/`**：以 net48 WinForms + WebView2 重写 `REO\@Resources\Common\Config\config.exe`（原 2013 年 AutoIt 版退役）。皮肤右键「设置」菜单的调用参数与旧版完全一致（`<模式> <config> <file> <settings> <skins> <program>`），374 个 `RainConfigure.cfg` 面板零改动直接适配（清理孤儿配置后现为 203 个）。
  - 界面按 schema 渲染：三行组（键/标题/控件）与两行组（键/标题）均支持；无控件行的裸键按键名与值形态推断控件（Color/Tint+RGB 值→取色器、0/1→开关、范围提示→滑杆、其余→文本框）。
  - 简体中文界面：`lang\zh-CN.json` 语言包（面板设置项标签 + hint 提示 + 界面文案，29 KB），缺失时回退英文。
  - 支持 `UserVariables.local.inc` 覆盖链：文件存在时读写都走 local（与旧 AutoIt 版 `@include` 链语义一致）。
  - 写回编码感知（UTF-16LE BOM / UTF-8），300 ms 防抖后 `!Refresh` 刷新面板；变量名大小写不敏感匹配。
  - 部署形态：`config.exe`（77 KB，x86）+ WebView2 托管 DLL ×3 + 原生 `WebView2Loader.dll`（x86）+ `config.exe.config` + `lang\`；要求 .NET Framework 4.8（Win10 1607+ 内置）与 WebView2 Runtime（Win10 1803+/Win11 内置）。
  - 未处理异常落盘 `%TEMP%\sc_crash.txt`（UI 线程与非 UI 线程双通道），便于现场诊断。
  - 窗口与任务栏图标取自 `config.exe` 自身的 Fluent 图标：无边框窗体不会自动沿用 exe 图标，缺这一步任务栏会显示 Windows 的占位图标。
- **界面语言默认跟随系统**：新增 `Common\Variables\AutoLang.lua`，由 `REO\Background\bg.ini` 与 `REO\Gallery\main.ini` 末尾的 measure 对（Registry 读 `HKCU\Control Panel\International\LocaleName` + Script）在宿主加载时执行——映射到八语语言包（zh→Chinese、ru→Russian、de→German、fr→French、es→Spanish、pt→Portuguese、nl→Dutch，其余 English），写回 `Common\Variables\UserVariables.inc` 的 `MainLanguage=`（磁贴/界面文案）与 `Common\Background\Varrar.inc` 的 `Language=`（工具对话框），随后 `!RefreshApp`。值已一致时不写不刷，无循环。
  - 出厂默认 `MainLanguage=Auto` + `LangAuto=1`；`Languages\Auto.inc` 是指向 `English.inc` 的一行 shim，检测器落笔前界面按英文渲染。手动锁定：把 `LangAuto` 设为 0，并将两个语言键指到想要的语种（`LangAuto=1` 期间手改会在下次宿主加载时被系统语言覆盖）。
  - `Common\Variables\UserVariables.inc` 由 cp1252 转为 UTF-8+BOM：实测 Rainmeter 的 `!WriteKeyValue` 对无 BOM 的 cp1252 文件按系统 ANSI（本机 CP936）读写，把 `SubstituteFeed` 行的 ä/ö/– 高位字节替换成 `?` 并吃掉了行尾引号；转 UTF-8+BOM 后写回逐字节保真，顺带修正了该行自上游继承的 mojibake。
  - 验证：注入 `MainLanguage=German` 后激活 `REO\Background`，检测器实机写回 `Chinese`，`SubstituteFeed` 高位字符逐字节完好，日志窗口 0 新增 `ERRO`；`Rainmeter.ini` 无残留、`Logging` 已关回。

### 变更

- **面板排布与多布局工具改用 C# 重写**:`Config\ActivePanels.exe` 与 `MultiManager\MultiManager.exe`(原 2013 年 AutoIt 版)由 `Tools\`(C# net48,零第三方依赖)重写,产物分别 39.5 KB 与 37.5 KB(原为 984 KB 与 1.06 MB)。皮肤调用参数与命令字完全不变(`missing`/`lock`/`unlock`/`grid*`/`Save0`/`Save1`/`Load0`/`Load1`/`Delete`),`AutoIT\ActivePanels.au3`/`MultiManager.au3` 源码保留作参照,但不再是这两个 exe 的来源。
  - 行为等价性用差分验证:注册假 `RainmeterMeterWindow` 窗口截获全部 bang、配假 SkinPath/SettingsPath 与固定工作目录,把原 exe 与新 exe 在同一夹具上比对 bang 序列与产出文件字节,37 个用例 36 个逐字节一致(唯一不一致的一例即下面的崩溃修复)。
  - 两处与老版 Win32 配置文件 API 的语义按原样对齐:cp1252 老文件里的高位字节原样保留(旧实现会替换成 U+FFFD 写坏文件),写回时保留键名原有大小写(`color.inc` 的 `ColorSkin` 不会被写成 `colorskin`)。
- `hue.ini` 不再被 git 跟踪：面板排布工具每次运行都会先删后写它，但全仓没有任何读取点，属本机状态而非源码；已加入 `.gitignore`，工作区文件保留不受影响。
- 删除 `Config\Panels\Delicious` 与 `Config\Panels\StumbleUpon`：两个目录里是面板设置 schema 与用户变量文件，但本仓库任何一次提交里都没有对应的面板目录，设置窗口只能从面板打开，因此没有消费者。皮肤实际提供的 Delicious / StumbleUpon 是 `InternetShortcuts` 快捷方式列表里的条目，配置各在自己名下。
- **其余四个 AutoIt 分发 exe 改用 C# 重写**：`ColorChanger.exe`、`OmnimoApp.exe`、`Background\ConfigBackground.exe`、`PanelCreator\PanelCreator.exe`（原为 2013 年 AutoIt 版，1.13–1.53 MB）由 `Tools\` 下的 net48 工程（WinForms，零第三方依赖）重写，产物 65–83 KB，单文件部署。皮肤调用参数与命令字完全不变；`AutoIT\ColorChanger.au3`、`OmnimoApp.au3`、`ConfigBackground.au3`、`PanelCreator.au3` 源码保留作参照，但不再是这几个 exe 的来源。至此仓库内 7 个可执行文件全部为本 fork 的 C# 重写，不再分发 AutoIt 编译产物。
  - 行为保真度用差分验证：同一夹具下并列运行原 AutoIt exe 与新 exe，比对窗口客户区尺寸、43 个子控件的位置/类/文本、窗口样式与字体（两侧最终都是系统 GUI 字体 12 px——为此新增共享源码 `Tools\Shared\GuiFont.cs`，按 `GetStockObject(DEFAULT_GUI_FONT)` 取原版所用字体，并注明换字体时必须把 `AutoScaleMode` 设为 `None`）。
  - 一处刻意不复刻的行为：`ConfigBackground` 的 `borderwidth` / `gradientangle` 显示配置文件里的真实值，而原版把这两个 `GUICtrlCreateInput` 接上 `GUICtrlCreateUpdown` 后会被 updown 的初值 0 盖掉——照抄会在用户打开窗口时静默抹掉配置，故保留正确值。
  - `PanelCreator` 顺带修好：仓库 0.5.0 里那份 AutoIt 版（1604608 字节）在真实参数下起不来，弹 `Line 21514: Error: Duplicate function name.`（include 的 UDF 内函数重名），C# 版是这一工具第一个可运行的构建。
  - 部署后的冒烟验证：四个 exe 以 `missing` 与一个不存在的命令各跑一次，退出码均为 0、不弹窗；`PanelCreator` 在真实皮肤路径下正常出界面（其语言包读取依赖 `<skins>\REO\@Resources\Common\Background\Varrar.inc`）。
  - 已知差异：WinForms 自绘非客户区，窗口外框比原 exe 宽 5 px/边（客户区与控件布局逐项一致）；`ConfigBackground` 少数窄标签（如 28 px 宽的 `Color`）比原版多截一个字，属 GDI 与 GDI+ 静态文本度量的细微差别。
- **三个 GUI 工具界面现代化**：`ColorChanger.exe`、`Background\ConfigBackground.exe`、`PanelCreator\PanelCreator.exe` 换用共享界面壳 `Tools\Shared\ModernUi.cs`——无边框窗口、自绘标题栏（应用图标 + 最小化/最大化/关闭）、圆角、Esc 关闭、深浅主题跟随 `Colors.inc`（可用 `--light` / `--dark` 覆盖），视觉语言与设置中心一致（同一字体栈 Microsoft YaHei UI → Segoe UI，强调色取系统 DWM 色）。
  - 标题栏图标：三个工具的窗口标题栏都显示各自 exe 的应用图标（取自 exe 自身），不再只有系统默认图标。
  - 多语言补齐：`Common\Background\Language\` 八语 cfg 此前缺 ColorChanger 与 ConfigBackground 的文案键（界面只有英文）。补齐 ColorChanger 的 10 个键（ChangePanelColor / SelectAll / Refresh / NoActivePanels / Random / NewPalette / Default 等）与 ConfigBackground 的分组标题（GroupColors / GroupSize）；六份 UTF-16LE+BOM 语种的编码保持不变，English / Dutch 维持 UTF-8 无 BOM。
  - `ConfigBackground` 由逐控件移植重写为 ModernForm 壳上的重组件界面（滑杆 / 输入框 / 勾选 / 色块），设置分组与标签全部走语言包。
  - `PanelCreator` 接入 ModernForm 壳：内容区仍是 `PanelCreator.au3` 的 GDI+ 单窗口布局（拖放预览、名称 / 操作输入、导出 `*.rmskin` 含 16 字节 PackageFooter、预览图与临时文件清理），行为与原 .au3 逐段对应。
  - 验证：夹具下三工具截图逐区核对（主题取色、标题栏图标、中文界面）；部署产物 98–115 KB（尺寸见 `AGENTS.md` 在途表）。
  - 实机回看后的二轮修正（2026-10-17）：ColorChanger 空列表提示被列表控件盖住（WinForms 后 `Add` 的控件在 z-order 底部，需 `SetChildIndex` 提前）；ConfigBackground 去掉原版四条 1px 装饰括线（深色界面里呈游离细线）、`GradientAngle` 标签加宽不再截断、黑/白色块补描边；PanelCreator 内容区去旧（droptext.png 改直绘提示文字且不再被名字轮询清掉、白色输入框改深色输入井、file/folder 旧图标改线条字形、按钮加悬停反馈）；修复共享缓存字体 `UiFont` 被 `using` 释放导致整窗绘制抛 `ArgumentException` 的崩溃。
  - 三轮修正（2026-10-17）：ColorChanger 整体布局下移出标题栏、窗口加高 24px、左右边距对齐（此前色板第一行压标题栏、左侧背景色带被读成「黑条」）；PanelCreator 原生输入框与全部自绘文字从环境继承的宋体改到 YaHei UI 字体栈（`Font = GuiFont.Default` 是忠实对照移植的遗留，现代化窗口不该再用）；窗口描边内缩 1px、半径 8 画（贴边画时外半像素被 DWM 圆角裁剪，角上呈缺口）。
  - 四轮修正（2026-10-17，DPI 根修）：此前三个窗口是 DPI 不感知进程，Windows 把 96 DPI 位图拉伸到屏幕倍率（用户屏 150%），1px 描边与圆角弧线被重采样成糊、勾选框在 150% 屏上冒出主题引擎画的花色碎片字形。现三个 exe 内嵌 `PerMonitorV2` 清单（`Tools\Shared\app.manifest`），`ModernForm.OnHandleCreated` 按 `GetDpiForWindow` 手动 `Scale` 统一缩放窗体与子控件（net48 的 `AutoScaleMode.Dpi` 实测缩放因子算成 1，不可用），自绘控件内部几何走 `PxU/DpiU` 系数；`ModernCheckBox` 基类从 `CheckBox` 换成 `Control`（主题引擎对继承 CheckBox 的 UserPaint 控件仍会画原生勾选字形）。
  - 多语言收尾（2026-10-17）：八语键集对齐到 54 键——此前 Dutch / French / German / Portuguese / Spanish 缺 `Apply`（ConfigBackground 右下角按钮在这些语言下显示英文键名）；PanelCreator 的拖放提示（新键 `DropHint`，替代 2013 年内嵌英文位图 droptext.png）与作者输入框标题接语言键；六份 UTF-16LE+BOM 语种编码与行尾保持不变。三窗跟随皮肤语言包（`Varrar.inc` 的 `Language=`），实机验证中文与德文界面。
- 版面核对的三轮修正（2026-10-17）：ColorChanger 色板网格右列溢出窗口、末行缺一角——间距从 44px 改回原版 au3 的 40px，并把常驻的自定义色块（原版第 18 格，点击弹系统取色器）放回网格末格；PanelCreator 预览区面板名被居中格式钉在 x=10、左半裁掉——改回原版的默认左对齐绘制。
- **删除贡献者包分类**（2026-10-18）：`Gallery\cat5.inc`、「贡献者包」侧栏项与其图标、`mask-contrib.png` 覆盖层、`Icons\heart.png`、主题模板里只给这些磁贴用的 `[P]` 样式（`!ToggleConfig "REO\DonatorPanels\…"`，而 `REO\DonatorPanels` 目录从未存在）一并移除；`Gallery\main.ini` 里 `[Cat5]`、`[Cat5Indicator]`、`[CatIcon5]` 三段删除后，设置标题与下一分类图标各上移一行（370→320、392→344）。四类贡献者语言键从 8 份运行时包与 `lang.inc` / `Backup\English.inc` / `Readme\Translate\**` 中移除，Chinese / English 现各 280 键。
- 天气磁贴的「如何找到你的天气代码?」原指向上游的 omnimo.info/weather/index.html（已不可达），现改为随皮肤分发的本地说明页 @Resources\Readme\WeatherCode.html（中英双语，三条取码路径：weather.com 网址末段 id、wxdata 时代的短代码、美国邮编），经 OmnimoApp.exe Browser 打开。
- TextItems\Extra\Missing 占位项（保存的布局引用了未安装的皮肤时出现）原先写着「点此下载 Omnimo」并链到上游主页，文案与动作一并去掉。
- 删除 Panels\Anniversary 与其 8 张语言图：没有任何磁贴引用它，唯一动作指向上游十周年纪念页。
- 两份 README 去掉「What changed / 改动」表格：逐个版本的改动已在 CHANGELOG.md，一节概览与它重复且容易过期。
- 画廊右键菜单的入口从 2013 年的上游页面改到本 fork：关于 指向本 fork 的 GitHub 主页（原先是上游的 Credits.html），检查更新 指向 Releases 页，发送反馈 指向 Issue 列表（原先是给原作者 gmail 的 mailto:），捐赠 条目删除（原链接打款给原作者，与本 fork 无关）。
- **删除 60 个无面板的孤儿配置目录**（2026-10-18）：`@Resources\Config\Panels` 有 83 个配置目录，而 `REO\Panels` 只有 62 个面板；多出的 23 个（含 `TextItems` 侧同形目录）永远不会被任何 include 或设置窗口引用，共 347 个文件。清理后 `Config\Panels` 40 个目录、`TextItems` 17 个。设置窗口同时改为只列 schema 里真有一组设置的面板（此前 35 个空 schema 面板点开只有「0 项设置」）。

### 修复

- 帮助页在深色皮肤下仍是白底英文：@Resources\Readme\Readme.html 与 Credits.html 补深色样式与中文正文（90 条 Readme 译文 + 整页 Credits），主题与语言参数缺失时退回系统配色与浏览器语言；Chinese.inc 的 langcode 由误写的 en-us 改为 zh-cn。
- 15 个面板变体没写 `@include…=#@#Config\Panels\<名>\UserVariables.inc`，设置窗口能改能存而磁贴不读：`Notes`（Item / Item2 / Item3，用 `#NotesFont#`）、`Timer`（用 `#SoundFile#`）、`MediaKey` 的四个 `MediaRemote`、`Brightness`、`Folders`、`RAM`、`Volume` 的其余变体补齐 include；补插位置限定在 `[Variables]` 段内、最后一条 include 之后，按各文件原有编码写回。
- 三个 GUI 工具窗口圆角处的白点：圆角此前由 DWM 圆角偏好（`DWMWA_WINDOW_CORNER_PREFERENCE`）与 WinForms `Region` 双重裁剪，两套半径不一致，DWM 抗锯齿在角上采样到未绘制的像素；现只保留 DWM 圆角，角部随窗口背景一同绘制。
- ActivePanels 3x3 布局在启用面板达到 31 个时崩溃（AutoIt 报 `Variable used without being declared`）：`ActivePanels.au3:278` 使用了从未声明的 `$row9`（只声明了 `$row1..$row8`），第 31 个面板起排布中断；C# 版按源码本来的行表补全声明，可正常排到 row9 并放完剩余面板。
- SettingsCenter `x86` 位数锁定：WebView2 NuGet 对 net48 只带 x86 原生 `WebView2Loader.dll`，SDK 风格工程默认 AnyCPU（x64 进程）加载即 `BadImageFormatException (0x8007000B)` 崩溃；`PlatformTarget=x86` 修正（csproj 注释留档）。
- SettingsCenter 用户数据目录按面板哈希分目录（`%LOCALAPPDATA%\ReOmnimo\SettingsCenter\<hash>`）：不同面板的设置窗口可并行；同面板双开不再以崩溃弹窗告终，改为提示后正常关闭。
- 全库 880 处小写 `wp7\` 死引用改为 `REO\`（411 个皮肤文件）：0.5.0 的改名提交只处理了大写 `WP7\`，磁贴、右键菜单、启动器里的 `!ToggleConfig "wp7\…"` 一直指向不存在的 config，动作全部失效（日志报 `!ToggleConfig: "wp7\…" does not exist`）。逐文件按嗅探到的编码解码再原编码写回（UTF-8 / UTF-16LE+BOM / cp1252 混杂），BOM 与行尾不变；`git grep -In "wp7"` 现查 0 命中，实机确认 `REO\` 形可正常切换面板。`DragGroup=WP7Panel` 是拖拽分组名而非路径，未改；`Gallery\MultiManager\Saved\` 下的用户布局快照未动。
- 工具窗口的关闭键「看不见」与任务栏缩略图没有名字：标题标签宽度固定 400px、横跨到右上角按钮并压在其上层，标签背景不透明，X 被整块盖住；`layoutCaption` 现把标题收窄到按钮左侧并 `BringToFront`，X 的描边从次要色改为前景色。`ModernForm` 此前从未给 `Form.Text` 赋值，任务栏缩略图因此没有标题，现已写入窗口名。
- PanelCreator 两个输入框没有提示文字：`TextBox` 是原生 EDIT、自己处理 WM_PAINT，重写 `OnPaint` 里画的占位文本根本不会执行（试过放开「仅失焦时绘制」的条件也不显示）；改用 EDIT 自带 cue banner（`EM_SETCUEBANNER`，wParam=1 让聚焦时也显示）。
- 跨屏拖动后排版错位：DPI 系数原先只在创建句柄时算一次，窗口从 125% 屏拖到 150% 屏时系统只改窗口尺寸、控件树不重排，自绘内容按新 DPI 落在旧矩形里，右侧残留一块黑矩形。`ModernForm` 现接管 `WM_DPICHANGED`，按新旧 DPI 比值缩放子控件与字体、重跑标题栏布局、复位被系统改高的原生 TrackBar，再采用系统建议矩形；125%↔150% 往返实测窗口与各子控件矩形逐项一致。
- 取色对话框未适配深色：`ColorPickerForm` 硬编码白底与灰字。现接 `ModernUi.Theme`——底色 / 输入井 / 分隔线 / 色板格 / 箭头条全部走主题，标题栏用 `DWMWA_USE_IMMERSIVE_DARK_MODE` 切深色，OK 按钮改主题强调色；吸管、色盘、箭头三个 2013 年的位图改为按控件尺寸现画的线性字形（DPI 变化不再糊）。
- 强调色可能是中性灰：系统 `AccentColor` 本机为 `0xA1A1A1`，直接当强调色会让滑杆与主按钮整片发灰；现按饱和度判定，低于 12% 回落到工具集原本的蓝。
- OmnimoApp.exe 的帮助页不再用内嵌 IE（WinForms WebBrowser）：那个引擎把 2026 年的页面排版全打乱。现改为查注册表取默认浏览器命令行直接启动，本地页的主题/语言参数走 # 片段——Chrome/Edge 把 file:// 的查询串当文件名的一部分，带 ? 会报「无法访问此网站」，ShellExecute 又会把参数丢掉。
- 设置窗的排版细节：勾选框从 16px 收到 14px（与 9pt 文字视觉等高），「使用图片」标签与勾选框同基线；滑杆拇指两端各内缩 6px，拉到最右不再被裁掉半个；`ConfigBackground` 行距统一到 36px、「上限 W × H」右对齐贴着数字框；列表滚动条走 `DarkMode_Explorer` 主题（条目填满后才补发一次——句柄创建时切主题对它无效），卡片补圆角 `Region` 并让非客户区重算，消除换掉原生滚动条后留下的竖痕。
- 取色框的 R / G / B 三个输入框从未显示：`MakeNumberBox` 造出控件后没有 `Controls.Add`，屏幕上只剩三个字母；同时标签宽 13px 装不下「R:」。现补上入窗并把标签放宽到 17px、输入框右移到 35 / 99 / 163。
- 取色框调色板菜单的排版：菜单项内边距从 2/3 改为 10/5（左 18 给点击余量），分隔线加 4px 竖向留白，弹出位置从齿轮正下方改为左上方向内弹出并夹在窗体内——原位置会压住 OK 一行。
- 设置窗侧栏列出「0 项设置」的面板：`Config\Panels` 下 83 个配置目录里有 35 个的 `RainConfigure.cfg` 不足四行（空 schema），列在侧栏只会在点开时显示「0 项设置」；侧栏现按 schema 行数过滤，只列有可编辑项的面板。
- `ConfigBackground` 分组标题右侧的细分隔线已去掉：中文标题比英文短，线从标题尾部一路拖到卡片右边，看起来像一条没有含义的横条；勾选框旁的说明文字改走 `AddCheckLabel`（垂直居中），不再顶在 25px 高容器的上沿。
- 背景设置点开没反应：`Background\bg.ini` 与 `Background\DayNight\Item.ini` 调用的是不存在的 `Common\Background\config.exe`（该目录实际部署名是 `ConfigBackground.exe`），而且只传了一个参数——工具契约是 `ConfigBackground.exe <VarFile> <SkinPath>`，原版 AutoIt 同样读 `$CmdLine[2]`，缺参直接越界。现改为正确文件名并补上 `#SKINSPATH#`。
- 面板设置项的说明文字：10 个 `RainConfigure.cfg` 的描述行原是从上游带来的玩笑与笔误（`URL to DDoS.. err I mean ping`、`Make it wider why dontcha?`、`Toggle from Low Brightness Value`、`If you have several audio devices, specify which to switch Index to switch`），在一个要用户照着填的对话框里说不清字段含义。逐条改写为说明实际行为，产品名与大小写一并订正（Steam / Big Picture）；参数名、控件类型与四行一组的 schema 结构未动。

### Added

- **Brand-new Settings Center `SettingsCenter/`**: `REO\@Resources\Common\Config\config.exe` rewritten with net48 WinForms + WebView2 (the original 2013 AutoIt version retired). The call arguments from the skins' right-click "Settings" menu are exactly the same as the old version's (`<mode> <config> <file> <settings> <skins> <program>`), and the 374 `RainConfigure.cfg` panels adapt directly with zero changes (203 remain after the orphan-config cleanup).
  - The UI renders from the schema: three-line groups (key/title/control) and two-line groups (key/title) are both supported; for bare keys on control-less rows the control is inferred from the key name and the value shape (Color/Tint+RGB value → color picker, 0/1 → toggle, range hint → slider, otherwise → text box).
  - Simplified Chinese UI: a `lang\zh-CN.json` language pack (panel setting labels + hint texts + UI strings, 29 KB), falling back to English when missing.
  - Supports the `UserVariables.local.inc` override chain: when the file exists, reads and writes both go through the local file (same semantics as the old AutoIt version's `@include` chain).
  - Encoding-aware write-back (UTF-16LE BOM / UTF-8); the panel is refreshed via `!Refresh` after a 300 ms debounce; variable names are matched case-insensitively.
  - Deployment shape: `config.exe` (77 KB, x86) + WebView2 managed DLLs ×3 + native `WebView2Loader.dll` (x86) + `config.exe.config` + `lang\`; requires .NET Framework 4.8 (built into Win10 1607+) and the WebView2 Runtime (built into Win10 1803+/Win11).
  - Unhandled exceptions are written to `%TEMP%\sc_crash.txt` (two channels: the UI thread and non-UI threads), for on-the-spot diagnosis.
  - The window and taskbar icons come from `config.exe`'s own Fluent icon: borderless forms do not automatically inherit the exe icon, and without this step the taskbar shows Windows' placeholder icon.
- **UI language follows the system by default**: new `Common\Variables\AutoLang.lua`, executed by the measure pair at the end of `REO\Background\bg.ini` and `REO\Gallery\main.ini` (Registry reads `HKCU\Control Panel\International\LocaleName` + Script) when the hosts load — it maps onto the eight language packs (zh→Chinese, ru→Russian, de→German, fr→French, es→Spanish, pt→Portuguese, nl→Dutch, anything else English), writes back `MainLanguage=` in `Common\Variables\UserVariables.inc` (tile/UI strings) and `Language=` in `Common\Background\Varrar.inc` (tool dialogs), then `!RefreshApp`. When the values already match, nothing is written and nothing is refreshed — no loop.
  - Factory defaults are `MainLanguage=Auto` + `LangAuto=1`; `Languages\Auto.inc` is a one-line shim pointing at `English.inc`, so the UI renders in English until the detector writes. Manual lock: set `LangAuto` to 0 and point the two language keys at the wanted language (manual edits made while `LangAuto=1` are overwritten by the system language at the next host load).
  - `Common\Variables\UserVariables.inc` converted from cp1252 to UTF-8+BOM: measured behavior is that Rainmeter's `!WriteKeyValue` reads and writes a BOM-less cp1252 file as the system ANSI (CP936 on this machine), replacing the ä/ö/– high bytes on the `SubstituteFeed` line with `?` and eating the quote at the end of the line; after the switch to UTF-8+BOM, write-backs are byte-for-byte faithful, and the mojibake that line inherited from upstream was fixed along the way.
  - Verified: with `MainLanguage=German` injected and `REO\Background` activated, the detector wrote back `Chinese` on the real machine, the `SubstituteFeed` high-byte characters stayed intact byte for byte, the log window gained 0 new `ERRO` entries; no residue in `Rainmeter.ini`, and `Logging` was switched back off.

### Changed

- **Panel arrangement and multi-layout tools rewritten in C#**: `Config\ActivePanels.exe` and `MultiManager\MultiManager.exe` (originally 2013 AutoIt versions) were rewritten by `Tools\` (C# net48, zero third-party dependencies); the outputs are 39.5 KB and 37.5 KB respectively (was 984 KB and 1.06 MB). The skin call arguments and command words are completely unchanged (`missing`/`lock`/`unlock`/`grid*`/`Save0`/`Save1`/`Load0`/`Load1`/`Delete`); the `AutoIT\ActivePanels.au3`/`MultiManager.au3` sources are kept for reference but are no longer the source of these two exes.
  - Behavioral equivalence verified differentially: a fake `RainmeterMeterWindow` window was registered to intercept all bangs, with fake SkinPath/SettingsPath and a fixed working directory, and the old and new exes were compared on the same fixture for bang sequences and output-file bytes — 36 of 37 cases byte-identical (the single mismatch is the crash fix below).
  - Two semantics of the old Win32 configuration-file API are aligned as-is: high bytes in old cp1252 files are preserved verbatim (the old implementation would replace them with U+FFFD and corrupt the file), and write-backs keep the key's original casing (`ColorSkin` in `color.inc` is not rewritten as `colorskin`).
- `hue.ini` is no longer tracked by git: the panel arrangement tool deletes and rewrites it on every run, but nothing in the whole repository ever reads it — it is machine-local state, not source; it is now in `.gitignore`, and the working-tree file is kept, unaffected.
- Removed `Config\Panels\Delicious` and `Config\Panels\StumbleUpon`: the two directories hold panel settings schemas and user-variable files, but no commit in this repository ever had the corresponding panel directories, and the settings window can only be opened from a panel, so they had no consumers. The Delicious / StumbleUpon the skins actually provide are entries in the `InternetShortcuts` shortcut list, each configured under its own name.
- **The remaining four AutoIt shipped exes rewritten in C#**: `ColorChanger.exe`, `OmnimoApp.exe`, `Background\ConfigBackground.exe`, `PanelCreator\PanelCreator.exe` (originally 2013 AutoIt versions, 1.13–1.53 MB) were rewritten by net48 projects under `Tools\` (WinForms, zero third-party dependencies); the outputs are 65–83 KB, deployed as single files. The skin call arguments and command words are completely unchanged; the `AutoIT\ColorChanger.au3`, `OmnimoApp.au3`, `ConfigBackground.au3`, `PanelCreator.au3` sources are kept for reference but are no longer the source of these exes. With this, all 7 executables in the repository are this fork's C# rewrites, and no AutoIt-compiled artifacts are shipped anymore.
  - Behavioral fidelity verified differentially: the original AutoIt exe and the new exe ran side by side under the same fixture, comparing window client-area size, the position/class/text of 43 child controls, window style and font (both sides ended on the system GUI font at 12 px — for this, shared source `Tools\Shared\GuiFont.cs` was added, taking the font the original used via `GetStockObject(DEFAULT_GUI_FONT)`, with a note that switching fonts requires setting `AutoScaleMode` to `None`).
  - One behavior deliberately not replicated: `ConfigBackground`'s `borderwidth` / `gradientangle` show the real values from the config file, whereas in the original these two `GUICtrlCreateInput`s, once wired to a `GUICtrlCreateUpdown`, were overwritten by the updown's initial value 0 — copying that faithfully would silently wipe the config when the user opens the window, so the correct values are kept.
  - `PanelCreator` was fixed along the way: the AutoIt version in the repository at 0.5.0 (1604608 bytes) would not start under real arguments, popping `Line 21514: Error: Duplicate function name.` (duplicate function names inside an included UDF); the C# build is the first runnable build of this tool.
  - Post-deployment smoke test: each of the four exes ran once with `missing` and once with a nonexistent command — exit codes all 0, no dialogs; `PanelCreator` showed its UI normally with a real skin path (its language-pack reading depends on `<skins>\REO\@Resources\Common\Background\Varrar.inc`).
  - Known differences: WinForms draws its own non-client area, so the window frame is 5 px wider per side than the original exe (client area and control layout match item by item); a few narrow labels in `ConfigBackground` (e.g. the 28 px wide `Color`) clip one more character than the original — a subtle difference between GDI and GDI+ static-text measurement.
- **Three GUI tools modernized**: `ColorChanger.exe`, `Background\ConfigBackground.exe`, `PanelCreator\PanelCreator.exe` moved to the shared UI shell `Tools\Shared\ModernUi.cs` — borderless windows, self-drawn title bar (app icon + minimize/maximize/close), rounded corners, Esc to close, light/dark theming following `Colors.inc` (overridable with `--light` / `--dark`), the visual language consistent with the Settings Center (same font stack Microsoft YaHei UI → Segoe UI, accent color taken from the system DWM color).
  - Title bar icons: all three tools' title bars show their own exe's app icon (taken from the exe itself) instead of only the system default icon.
  - Multilingual coverage completed: the eight-language cfgs under `Common\Background\Language\` previously lacked ColorChanger's and ConfigBackground's string keys (the UI was English-only). ColorChanger's 10 keys (ChangePanelColor / SelectAll / Refresh / NoActivePanels / Random / NewPalette / Default etc.) and ConfigBackground's group titles (GroupColors / GroupSize) were added; the six UTF-16LE+BOM languages keep their encodings, and English / Dutch remain UTF-8 without BOM.
  - `ConfigBackground` was rewritten from a control-by-control port into a re-composed UI on the ModernForm shell (sliders / input boxes / checkboxes / color swatches), with all setting groups and labels driven by the language pack.
  - `PanelCreator` moved onto the ModernForm shell: the content area is still `PanelCreator.au3`'s GDI+ single-window layout (drag-and-drop preview, name / action inputs, exporting `*.rmskin` with the 16-byte PackageFooter, preview-image and temp-file cleanup), with behavior mapping section by section to the original .au3.
  - Verified: screenshots of the three tools under the fixture checked region by region (theme colors, title bar icons, Chinese UI); deployed outputs 98–115 KB (sizes in the in-flight table of `AGENTS.md`).
  - Second-round fixes after review on the real machine (2026-10-17): ColorChanger's empty-list hint was covered by the list control (after the WinForms port, controls added with `Add` sit at the bottom of the z-order and need `SetChildIndex` to be brought forward); ConfigBackground dropped the original's four 1 px decorative brackets (they read as stray thin lines on the dark UI), the `GradientAngle` label was widened so it no longer truncates, and the black/white swatches gained outlines; PanelCreator's content area de-aged (droptext.png replaced with directly drawn hint text that the name polling no longer clears, white input boxes became dark input wells, the old file/folder icons became line glyphs, buttons gained hover feedback); fixed the crash where the shared cached font `UiFont` was disposed by a `using`, making whole-window painting throw `ArgumentException`.
  - Third-round fixes (2026-10-17): ColorChanger's overall layout moved down out of the title bar, the window made 24 px taller, left/right margins aligned (before, the palette's first row pressed on the title bar and the left background color band read as a "black bar"); PanelCreator's native input boxes and all self-drawn text moved from the environment-inherited SimSun to the YaHei UI font stack (`Font = GuiFont.Default` was a leftover of the faithful 1:1 port, which a modernized window should not keep); the window outline is drawn inset 1 px at radius 8 (drawn flush to the edge, the outer half-pixel gets clipped by the DWM corner rounding, leaving notched corners).
  - Fourth-round fixes (2026-10-17, DPI root fix): the three windows had been DPI-unaware processes, so Windows stretched the 96 DPI bitmaps to the screen factor (the user's screen is 150%), resampling the 1 px outlines and corner arcs into blur, and checkboxes on the 150% screen sprouted garbled fragmented glyphs painted by the theme engine. The three exes now embed a `PerMonitorV2` manifest (`Tools\Shared\app.manifest`), `ModernForm.OnHandleCreated` manually `Scale`s the form and child controls uniformly per `GetDpiForWindow` (net48's `AutoScaleMode.Dpi` measured a scale factor of 1 — unusable), self-drawn controls compute their internal geometry through the `PxU/DpiU` factors; and `ModernCheckBox`'s base class changed from `CheckBox` to `Control` (the theme engine still paints the native checkbox glyph on UserPaint controls that inherit CheckBox).
  - Multilingual wrap-up (2026-10-17): the eight-language key sets aligned to 54 keys — Dutch / French / German / Portuguese / Spanish previously lacked `Apply` (ConfigBackground's bottom-right button showed the English key name in those languages); PanelCreator's drag-and-drop hint (new key `DropHint`, replacing the embedded 2013 English bitmap droptext.png) and the author input box title are wired to language keys; the six UTF-16LE+BOM languages keep their encoding and line endings. The three windows follow the skin language pack (`Language=` in `Varrar.inc`); the Chinese and German UIs were verified on the real machine.
- Third round of layout-check fixes (2026-10-17): ColorChanger's palette grid overflowed the window in the right column and the last row was missing a corner — spacing changed from 44 px back to the original au3's 40 px, and the always-present custom color swatch (the original's 18th cell; clicking it opens the system color picker) returned to the grid's last cell; PanelCreator's preview-area panel name was pinned at x=10 by the centered format with its left half clipped — restored to the original's default left-aligned drawing.
- **Contributor-pack category removed** (2026-10-18): `Gallery\cat5.inc`, the "贡献者包" sidebar entry and its icon, the `mask-contrib.png` overlay, `Icons\heart.png`, and the `[P]` style in the theme templates used only by those tiles (`!ToggleConfig "REO\DonatorPanels\…"`, while the `REO\DonatorPanels` directory never existed) were removed together; after deleting the `[Cat5]`, `[Cat5Indicator]` and `[CatIcon5]` sections in `Gallery\main.ini`, the settings title and the next category's icon each moved up one line (370→320, 392→344). The four contributor language keys were removed from the 8 runtime packs and from `lang.inc` / `Backup\English.inc` / `Readme\Translate\**`; Chinese / English now hold 280 keys each.
- The weather tile's "如何找到你的天气代码?" entry originally pointed to upstream's omnimo.info/weather/index.html (now unreachable); it now opens a local help page shipped with the skin, `@Resources\Readme\WeatherCode.html` (bilingual Chinese/English; three ways to get a code: the trailing id of a weather.com URL, wxdata-era short codes, US ZIP codes), opened via the OmnimoApp.exe Browser.
- The `TextItems\Extra\Missing\` placeholder item (shown when a saved layout references an uninstalled skin) originally said "点此下载 Omnimo" and linked to the upstream homepage; the text and the action are removed together.
- Removed `Panels\Anniversary` and its 8 language images: no tile references it, and its only action pointed to the upstream tenth-anniversary page.
- The two READMEs dropped their "What changed / 改动" tables: the per-version changes are already in CHANGELOG.md, and a section-level overview duplicates it and goes stale easily.
- The gallery's right-click menu entries moved from their 2013 upstream pages to this fork: About points to this fork's GitHub homepage (was upstream's Credits.html), Check for updates points to the Releases page, Send feedback points to the issue list (was a mailto: to the original author's gmail), and the Donate entry is removed (the original link paid the original author — nothing to do with this fork).
- **60 panel-less orphan config directories deleted** (2026-10-18): `@Resources\Config\Panels` had 83 config directories while `REO\Panels` has only 62 panels; the extra 23 (including the identically named ones on the `TextItems` side) would never be referenced by any include or the settings window — 347 files in total. After the cleanup, `Config\Panels` has 40 directories and `TextItems` 17. The settings window also now lists only panels whose schema actually holds a group of settings (previously, opening one of the 35 empty-schema panels showed only "0 项设置").

### Fixed

- The help pages stayed white-background English under dark skins: `@Resources\Readme\Readme.html` and `Credits.html` gained dark styles and Chinese body text (90 Readme translation entries + the full Credits page), falling back to system colors and the browser language when the theme/language parameters are missing; Chinese.inc's langcode changed from the miswritten en-us to zh-cn.
- 15 panel variants lacked `@include…=#@#Config\Panels\<name>\UserVariables.inc`, so the settings window could change and save values the tile never read: includes were added for `Notes` (Item / Item2 / Item3, using `#NotesFont#`), `Timer` (using `#SoundFile#`), `MediaKey`'s four `MediaRemote` variants, `Brightness`, `Folders`, `RAM`, and `Volume`'s remaining variants; the insertions are restricted to inside the `[Variables]` section, after the last include, written back in each file's original encoding.
- White dots at the rounded corners of the three GUI tools' windows: the corners were previously clipped twice — by the DWM corner preference (`DWMWA_WINDOW_CORNER_PREFERENCE`) and a WinForms `Region` — with mismatched radii, so DWM anti-aliasing sampled unpainted pixels at the corners; now only the DWM rounding is kept, and the corners are painted along with the window background.
- The ActivePanels 3x3 layout crashed when enabled panels reached 31 (AutoIt reported `Variable used without being declared`): `ActivePanels.au3:278` used a never-declared `$row9` (only `$row1..$row8` were declared), so the arrangement broke from the 31st panel on; the C# version completes the declarations per the row table the source itself already implied, and can arrange through row9, placing all remaining panels.
- SettingsCenter locked to `x86`: the WebView2 NuGet ships only the x86 native `WebView2Loader.dll` for net48, so an SDK-style project's default AnyCPU (x64 process) crashed on load with `BadImageFormatException (0x8007000B)`; fixed with `PlatformTarget=x86` (documented in a csproj comment).
- SettingsCenter user-data directory partitioned per panel hash (`%LOCALAPPDATA%\ReOmnimo\SettingsCenter\<hash>`): the settings windows of different panels can run in parallel; opening the same panel twice no longer ends in a crash dialog — it now closes normally after a notice.
- All 880 lowercase `wp7\` dead references across the tree changed to `REO\` (411 skin files): 0.5.0's rename commit only handled the uppercase `WP7\`, so the `!ToggleConfig "wp7\…"` calls in tiles, context menus and the launcher kept pointing at nonexistent configs and every action was dead (the log reported `!ToggleConfig: "wp7\…" does not exist`). Each file was decoded with its sniffed encoding and written back in the same encoding (a mix of UTF-8 / UTF-16LE+BOM / cp1252), with BOM and line endings unchanged; `git grep -In "wp7"` now finds 0 hits, and switching panels with the `REO\` form was confirmed working on the real machine. `DragGroup=WP7Panel` is a drag group name, not a path — left unchanged; the user layout snapshots under `Gallery\MultiManager\Saved\` were untouched.
- The invisible close button on the tool windows and the nameless taskbar thumbnails: the title label had a fixed 400 px width, ran across to the top-right buttons and sat on a layer above them, and with an opaque label background the X was completely covered; `layoutCaption` now narrows the title to the left of the buttons and calls `BringToFront` on it, and the X's outline changed from the secondary color to the foreground color. `ModernForm` had never assigned `Form.Text`, so taskbar thumbnails had no title — the window name is now set.
- Two `PanelCreator` input boxes had no hint text: a `TextBox` is a native EDIT that handles WM_PAINT itself, so placeholder text drawn in an overridden `OnPaint` never executes (relaxing the "only draw when unfocused" condition did not show it either); switched to the EDIT's built-in cue banner (`EM_SETCUEBANNER`, wParam=1 so it also shows while focused).
- Layout misplaced after dragging across screens: the DPI factor was computed only once at handle creation, so when a window moved from a 125% screen to a 150% one the system resized the window without re-laying out the control tree, and self-drawn content landed in old rectangles under the new DPI, leaving a black rectangle on the right. `ModernForm` now handles `WM_DPICHANGED`: it scales the child controls and fonts by the new/old DPI ratio, re-runs the title bar layout, resets the native TrackBars the system had resized, and then adopts the system-suggested rectangle; a 125%↔150% round trip measured the window and every child-control rectangle identical item by item.
- The color picker dialog was not dark-mode aware: `ColorPickerForm` hardcoded a white background with gray text. It now hooks `ModernUi.Theme` — background / input wells / separators / swatch grid / arrow strip all come from the theme, the title bar switches to dark via `DWMWA_USE_IMMERSIVE_DARK_MODE`, and the OK button uses the theme accent; the three 2013 bitmaps (eyedropper, color wheel, arrows) were replaced with line glyphs drawn on the fly at the control's size (no longer blurry when the DPI changes).
- The accent color could be neutral gray: the system `AccentColor` on this machine is `0xA1A1A1`, and using it directly would wash the sliders and primary buttons all gray; saturation is now checked, and below 12% it falls back to the tool set's original blue.
- `OmnimoApp.exe`'s help pages no longer use embedded IE (the WinForms WebBrowser): that engine wrecks the layout of 2026-era pages. It now looks up the default browser's command line in the registry and launches it directly; local pages pass the theme/language parameters via the URL # fragment — Chrome/Edge treat a file:// query string as part of the file name (a `?` brings up "This site can't be reached"), and ShellExecute drops the arguments.
- Settings-window layout details: checkboxes shrunk from 16 px to 14 px (visually level with 9 pt text), the "使用图片" label baseline-aligned with the checkbox; slider thumbs are inset 6 px at each end so dragging to the far right no longer clips half of them; `ConfigBackground`'s line spacing unified at 36 px and the "上限 W × H" label right-aligned flush against the number boxes; the list scrollbar uses the `DarkMode_Explorer` theme (one follow-up pass is sent after the entries fill in — switching the theme at handle creation has no effect on it), and the cards gained a rounded-corner `Region` plus a non-client-area recalc, removing the vertical streak left behind by replacing the native scrollbar.
- The color picker's R / G / B input boxes never showed: `MakeNumberBox` created the controls but never added them to the form with `Controls.Add`, so only the three letters were on screen; the 13 px label also could not fit "R:". The controls are now added to the form, the label widened to 17 px, and the input boxes moved right to 35 / 99 / 163.
- Color picker palette menu layout: menu item padding changed from 2/3 to 10/5 (18 on the left for click room), the separator gained 4 px of vertical whitespace, and the popup moved from directly beneath the gear to opening inward toward the upper left, clamped inside the window — the original position covered the OK row.
- Panels whose sidebar entry showed "0 项设置": of the 83 config directories under `Config\Panels`, 35 have `RainConfigure.cfg` files shorter than four lines (empty schemas), and listed in the sidebar they only ever showed "0 项设置" when opened; the sidebar now filters by schema line count and lists only panels with editable items.
- The thin separator to the right of `ConfigBackground`'s group titles is gone: Chinese titles are shorter than English ones, so the line ran from the end of the title all the way to the card's right edge, looking like a meaningless horizontal bar; the description text beside checkboxes now goes through `AddCheckLabel` (vertically centered) instead of pressing against the top edge of the 25 px tall container.
- Opening the background settings did nothing: `Background\bg.ini` and `Background\DayNight\Item.ini` called the nonexistent `Common\Background\config.exe` (the directory's actually deployed name is `ConfigBackground.exe`) and passed only one argument — the tool contract is `ConfigBackground.exe <VarFile> <SkinPath>`, and the original AutoIt read `$CmdLine[2]` the same way, so a missing argument went straight out of bounds. They now use the correct file name and add `#SKINSPATH#`.
- Panel setting descriptions: the description lines in 10 `RainConfigure.cfg` files were upstream jokes and typos (`URL to DDoS.. err I mean ping`, `Make it wider why dontcha?`, `Toggle from Low Brightness Value`, `If you have several audio devices, specify which to switch Index to switch`) that fail to explain the fields in a dialog the user is supposed to fill in. Each was rewritten to describe the actual behavior, with product names and casing corrected (Steam / Big Picture); parameter names, control types and the four-lines-per-group schema structure are untouched.

## [0.5.0] - 2026-10-13

### 变更

- **要求 Rainmeter 5.0 或更高**（`Rainstaller.cfg` 的 `MinRainmeterVer=5.0`）。5.0 目前经官方论坛预发布主题分发；停留在 4.5 的用户请使用 `0.4.0` tag。
- **皮肤根目录 `WP7\` 改名 `REO\`**：全库 1162 处路径引用、7 个配置工具的 AutoIt 源码、2 个 `.rmskin` 附件包同步改名。上游（fediaFedia/Omnimo）自本版起不再作为对照基准。**升级注意**：已加载的面板配置路径全部失效，需重新加载（`Documents\Rainmeter\Skins\WP7` 的 junction 需重建为 `REO`）。
- 捆绑插件写法（`Measure=Plugin` + `Plugin=X.dll`，约 980 处）改为 5.0 原生 measure（`Measure=AudioLevel`/`WebParser`/`NowPlaying`/`Power`/`Quote`/`Ping`/`RecycleManager` 等）；第三方插件（SysColor、Backlight）保留原写法。
- 媒体键面板：`Measure=MediaKey`（5.0 build 5273 起弃用）迁移到 `!SendKey`（MediaPlayPause/MediaNextTrack/MediaPrevTrack/VolumeUp/VolumeDown 等）。
- NowPlaying 播放器源改为 `PlayerName=Windows`（SMTC）：Spotify、浏览器、VLC 等现代播放器直接可用，不再依赖 2013 年代的播放器插件接口。
- 可视化（Visualizer）系 9 个皮肤：无音频时经 `OnAudioEndAction` + `!SetUpdate` 休眠（1000ms），起声恢复 50ms。
- 常驻时钟/日程面板（WorldClock 4 个、Agenda 3 个 ini）加 `Update=… | Invisible 60000`，被完全遮挡时降频为每分钟一次。
- FrostedGlass 死插件引用删除（42 处）：该 DLL 只有 32 位版且不在仓库，64 位 5.0 下永远 error 126；面板本就有原生 `Blur=`。
- 七个可执行文件重编译（同参数集），版本资源注入 `0.5.0.0`。
- `AGENTS.md`：验收基线噪音清单移除已修复项（`Meter=Calc`、FrostedGlass error 126）；删除「对照上游差异数」纪律（上游已冻结）。

### 修复

- 网络面板与 `Gallery/Intro/intro.ini`：`Meter=Calc` 拼写错误（应为 `Measure=Calc`），日志报错，三处全修。
- `Background/XboxBG/bg.ini` 与 `bgdark.ini`：`Update=86400000Blur=…` 两行粘连为一行，`Blur` 从未生效；拆开。

### Changed

- **Rainmeter 5.0 or later is now required** (`MinRainmeterVer=5.0` in `Rainstaller.cfg`). The 5.0 line currently ships through the official forum pre-release thread; users staying on 4.5 should use the `0.4.0` tag.
- **Skin root renamed `WP7\` → `REO\`**: all 1162 path references across the tree, the AutoIt sources of the seven config tools, and the two `.rmskin` extras renamed with it. Upstream (fediaFedia/Omnimo) is no longer a diff baseline as of this release. **Upgrade note**: previously loaded panel configs point at the old path and must be reloaded (recreate the `Documents\Rainmeter\Skins\WP7` junction as `REO`).
- Bundled-plugin syntax (`Measure=Plugin` + `Plugin=X.dll`, ~980 sites) rewritten as native 5.0 measures (`Measure=AudioLevel`/`WebParser`/`NowPlaying`/`Power`/`Quote`/`Ping`/`RecycleManager` etc.); third-party plugins (SysColor, Backlight) keep the plugin form.
- Media key panels: `Measure=MediaKey` (deprecated since 5.0 build 5273) migrated to `!SendKey` (MediaPlayPause/MediaNextTrack/MediaPrevTrack/VolumeUp/VolumeDown etc.).
- NowPlaying now uses `PlayerName=Windows` (SMTC): Spotify, browsers, VLC and other modern players work out of the box, no 2013-era player plugin interface needed.
- The 9 Visualizer skins sleep when no audio plays (`OnAudioEndAction` + `!SetUpdate` to 1000 ms; back to 50 ms on audio start).
- Always-on clock/agenda panels (4 WorldClock, 3 Agenda ini) gained `Update=… | Invisible 60000`, dropping to one update per minute while fully occluded.
- Removed 42 references to the dead FrostedGlass plugin: the DLL is 32-bit-only and not in the repo, so it always failed with error 126 on 64-bit 5.0; panels already use native `Blur=`.
- The seven executables were recompiled (same parameter set) and stamped with version resource `0.5.0.0`.
- `AGENTS.md`: baseline-noise list no longer mentions the fixed items (`Meter=Calc`, FrostedGlass error 126); the "diff against upstream" discipline removed (upstream is frozen).

### Fixed

- Network panel and `Gallery/Intro/intro.ini`: `Meter=Calc` typos (should be `Measure=Calc`) logged errors at load; all three fixed.
- `Background/XboxBG/bg.ini` and `bgdark.ini`: `Update=86400000Blur=…` — two lines glued into one, so `Blur` never took effect; split apart.

## [0.4.0] - 2026-10-02

### 修复

- 日程面板：重复事件（`RRULE`）此前整条被忽略，只画 `DTSTART` 那一条。现在支持 `DAILY`/`WEEKLY`/`MONTHLY`/`YEARLY` 加 `INTERVAL`/`COUNT`/`UNTIL`/`BYDAY`/`BYMONTHDAY`/`BYMONTH` 的有界子集，并且只在面板窗口内展开实例；带其它参数（`WKST`/`BYSETPOS`/`BYHOUR` 等）或参数组合不支持的规则整条不认，退回只画 `DTSTART`——宁可少画也不画错。`RDATE`/`EXDATE`/`RECURRENCE-ID` 一律忽略。
- 日程面板：`DTSTART` 的 `TZID=` 参数此前被属性正则连同参数串一起丢弃，非本机时区的事件按表盘原值放置，整体偏移一个时差。现在按内置的固定偏移表（约 60 条 IANA 区域名与 Windows 时区名）换算到本机时间；表里没有的时区按本机墙上时间显示，并在日志里提示一次。固定偏移不跟夏令时走，欧洲与北美部分区域在夏令时期间可能差一小时，这是刻意取舍：一个日历面板不值得内置 tzdata。
- 日程面板：`SUMMARY` 含双引号的标题经 `!SetOption` 传值会被截断或吞掉引号。现在按 Rainmeter 的三引号参数形式传值，标题原样保留。
- 日程面板：`Update=1000` 是为「静置回顶」而设的（见 `Item.ini`），但每秒的 tick 都会重解析全部订阅并重推上百条 `!SetOption`（三个订阅满载时约 180-200 条 bang/秒）。现在订阅正文、日期窗口与滚动偏移都没变时直接返回，既不再解析 ICS 也不再重推；实测 23 次 tick 只解析 1 次、渲染 1 次。
- 日程面板：滚动偏移夹取后没有写回皮肤变量，变量会停在过期值，下一次滚动以它为基准。现在只在值确实变化时写回。
- 日程面板：日期标题槽位不清理时间文案，上一帧事件行的时间会残留在隐藏的 meter 里。现在清空。
- 日程面板：`AgendaStyle` 变量恒为 1，`style` 3/4 两套排布分支（约 70 行）永不执行，删除。
- 语言切换磁贴：`WP7/Gallery/cat7.inc` 的 8 个语言切换（含本 fork 新增的简体中文）只写 `MainLanguage` 与两处 `Language`，不写 `DateLayout`，切完语言后日期仍是旧语言的格式。现在与 `WP7/Gallery/Intro/intro.ini` 一样按语言写入对应的 `DateLayout`。
- 网络面板：`Item2.ini` 的位数单位写成 `%1B`，Rainmeter 的自动换算认的是小写 `%1b`，单位没有跟随缩放（同一目录的 `Item.ini` 早已是小写）。
- 配置工具：面板存在 `UserVariables.local.inc` 覆盖文件时，设置界面读写的是被该文件遮蔽的 `UserVariables.inc`，改动不生效（此前记为已接受的交互缺陷）。现在检测到同目录的覆盖文件就直接读写它，界面加一行说明；覆盖文件里没写的键仍回落到 `UserVariables.inc`——两个 include 是合并关系，不是遮蔽，所以不需要为本地文件补齐任何键。
- 日程面板：四种状态提示（`loading feed...`/`feed unavailable`/`no feed configured`/`no events in the next N days`）此前硬编码英文，现在走语言包新增的 `AgendaLoading`/`AgendaUnavailable`/`AgendaNoFeed`/`AgendaNoEvents` 四个键，未翻译的语言回落英文。`AgendaNoEvents` 的 `%1` 替换成天数——这是本仓库语言文件里唯一的占位符约定。星期缩写与 `all day` 按设计保持英文。

- 日程面板：标题里连续三个以上引号会被 Rainmeter 的参数解析截断，残余还会被当成皮肤名去执行；现在折叠成两个引号送出，并在日志里提示一次。
- 日程面板：1970-01-01 之前的 `DTSTART` 会让 `os.time` 返回 nil，一次算术就打断整个 `Update()`，而失败的那次解析已经写进缓存、不会重试。日期运算改成纯整数日历，表示不了的时刻整条跳过；解析整段包进 `pcall`，失败时保留上一份事件表并在下一个 tick 重试。
- 配置工具：本地覆盖提示在可换肤（`Colorizable=1`）的面板（含日程面板）上被条件挡住，永远不显示；现在只要覆盖文件存在就显示。
- 配置工具：保存（Set）时把第 6 个命令行参数当 Rainmeter 安装目录，而面板调用点只传 5 个参数，于是每次保存都弹 AutoIt 错误框（`Array variable has incorrect number of subscripts`，config.exe Line 10488），皮肤也不会刷新。现在按 `#PROGRAMPATH#` 取值，参数不足时回落到常见安装目录。
- 皮肤配置调用点：428 处 `Config\config.exe` 调用补上第 6 个参数 `#PROGRAMPATH#`，与其它 Omnimo 工具的约定一致。
- 配置工具：`bg`（背景设置）分支对覆盖文件里没有的键直接用内置默认值，与面板路径不对称；现在同样回落到基础文件。当前调用点到不了该分支，属一致性修正。
- 语言包：本地覆盖提示键 `VariablesFromLocalFile` 此前只加在中文与英文两个 cfg 里，其余六种语言缺这个键；现已八种齐全。

### 变更

- 日程面板：`RangeDays` 此前是闭区间（填 6 会显示 7 个日历日），配置标签「显示天数（今天起）」因此差一天。现在窗口正好等于 `RangeDays` 天。升级后同一配置会少显示一天，这是有意的行为变更。
- `THIRD-PARTY.md` 更正两处与事实不符的说明：中文语言包并非本 fork 翻译（`Translated=` 记的是上游作者），以及 `readme.md` 并非未改动（加了 25 行的 fork 说明）。
- 七个可执行文件用 AutoIt 3.3.18.0（`Aut2Exe`、x86、`/nopack`）从修正后的源码重新编译，为「配置工具」的覆盖感知补上新的界面文案，并把版本资源盖为下一个发布号 `0.4.0.0`。
- `AGENTS.md` 更正三处事实：语言包是 288 键定义/283 唯一键（原写 287/280）、`Config.au3` 的写盘与显示态在 519/568 行（原写 528/553）、以及「注入后每个 exe 比上游多 1024 字节」的说法（上游二进制是 UPX 加壳的，体积差来自 `/nopack`，版本资源本身只占 1024 字节）。
- `THIRD-PARTY.md` 第 3 节的可执行文件体积表与版本号更新为本轮 0.4.0 重编译的结果。
- 本轮改动后七个可执行文件再次重编译（同上参数集），版本资源保持 `0.4.0.0`。
- README：项目名改为 ReOmnimo，叙述口吻只说「接手 Omnimo 的开发」；拆为英文主文档 `README.md` 与中文独立文档 `README.zh.md`，上游 `readme.md` 删除。
- `AGENTS.md`、`THIRD-PARTY.md`：同步 ReOmnimo 命名与最新差异计数（16 增 / 450 改 / 17 删）。

### Fixed

- Agenda panel: recurring events (`RRULE`) were ignored outright and only the
  `DTSTART` instance was drawn. The parser now handles a bounded subset —
  `DAILY`/`WEEKLY`/`MONTHLY`/`YEARLY` with `INTERVAL`/`COUNT`/`UNTIL`/`BYDAY`/
  `BYMONTHDAY`/`BYMONTH` — and expands only the instances inside the panel
  window. A rule carrying anything else (`WKST`/`BYSETPOS`/`BYHOUR`, or an
  unsupported combination) is rejected as a whole and falls back to drawing the
  single `DTSTART` instance: better to draw less than to draw wrong.
  `RDATE`/`EXDATE`/`RECURRENCE-ID` are ignored.
- Agenda panel: the `TZID=` parameter of `DTSTART` was swallowed by the property
  pattern along with the rest of the parameter string, so events in another zone
  were placed at the raw clock time and shifted by a whole offset. Times are now
  converted through a built-in fixed-offset table (about 60 IANA region names
  plus the Windows zone names); a zone missing from the table is shown at this
  machine's wall time and reported once in the log. The offsets do not follow
  daylight saving, so parts of Europe and North America can be an hour off
  during DST — a deliberate trade-off, since a calendar panel is not worth
  shipping tzdata for.
- Agenda panel: a `SUMMARY` containing a double quote was truncated, or lost the
  quote, when passed through `!SetOption`. Values are now passed in Rainmeter's
  triple-quoted form and the title arrives intact.
- Agenda panel: `Update=1000` exists so the list can snap back to the top when
  scrolling stops (see `Item.ini`), but every per-second tick re-parsed all
  subscriptions and pushed over a hundred `!SetOption` bangs (roughly 180-200
  bangs per second with three subscriptions). The tick now returns immediately
  when the feed bodies, the date window and the scroll offset are all unchanged,
  parsing no ICS and pushing no bangs; measured over 23 ticks it parsed once and
  rendered once.
- Agenda panel: the clamped scroll offset was never written back to the skin
  variable, so the variable stayed on a stale value and the next scroll used it
  as its base. It is now written only when the value really changes.
- Agenda panel: the date-header slot did not clear the time text, so an event
  line's time stayed behind in a hidden meter. It is now cleared.
- Agenda panel: the `AgendaStyle` variable is always 1 and the style 3/4 layout
  branches (about 70 lines) can never run; they are removed.
- Language picker tile: the eight language switchers in `WP7/Gallery/cat7.inc`
  (including the Simplified Chinese one this fork added) wrote `MainLanguage`
  and the two `Language` values but not `DateLayout`, so the date kept the
  previous language's format after a switch. They now write the matching
  `DateLayout`, exactly like `WP7/Gallery/Intro/intro.ini` already did.
- Network panel: `Item2.ini` wrote the bit-rate unit as `%1B`; Rainmeter's
  auto-scale recognises the lowercase `%1b`, so the unit did not follow the
  scale (the neighbouring `Item.ini` already used the lowercase form).
- Config tool: when a panel has a `UserVariables.local.inc` override, the
  settings window read and wrote the `UserVariables.inc` that file shadows, so
  edits were discarded (previously recorded as an accepted interaction defect).
  It now detects the sibling override and reads and writes that file, and shows
  a line of explanation in the window; keys the override does not set still fall
  back to `UserVariables.inc`, since the two includes merge rather than shadow,
  so nothing ever needs to be seeded into the local file.
- Agenda panel: the four status messages (`loading feed...`,
  `feed unavailable`, `no feed configured`, `no events in the next N days`) were
  hard-coded English. They now come from four new language keys —
  `AgendaLoading`/`AgendaUnavailable`/`AgendaNoFeed`/`AgendaNoEvents` — falling
  back to English in untranslated packs. The `%1` in `AgendaNoEvents` is
  replaced with the day count; it is the only placeholder convention in this
  repository's language files. Weekday abbreviations and `all day` stay English
  by design.

- Agenda panel: a title with a run of three or more double quotes was cut short by
  Rainmeter's argument parser, and the remainder was even executed as a skin name.
  Such a run is now collapsed to two quotes before the value is sent, with one log
  notice.
- Agenda panel: a `DTSTART` before 1970-01-01 made `os.time` return nil, and one
  arithmetic on it aborted the whole `Update()`, while the failed parse had already
  been written to the cache and was never retried. Date arithmetic now runs on an
  integer calendar, unrepresentable instants are skipped whole, and the parse runs
  inside `pcall`: on failure the previous list is kept and the next tick retries.
- Config tool: the local-override notice was hidden by a condition on colour-capable
  (`Colorizable=1`) panels, the Agenda panel included, so it never appeared. It is now
  shown whenever the override file exists.
- Config tool: saving (Set) treated the 6th command line argument as the Rainmeter
  install directory while panel call sites pass only five arguments, so every save
  raised the AutoIt error box (`Array variable has incorrect number of subscripts`,
  config.exe Line 10488) and the skin was never refreshed. The path now comes from
  `#PROGRAMPATH#` and falls back to the usual install locations when it is absent.
- Skin config call sites: the 6th argument `#PROGRAMPATH#` was added to all 428
  `Config\config.exe` invocations, matching what the other Omnimo tools already do.
- Config tool: the `bg` (background settings) branch used built-in defaults for keys
  missing from the override file instead of falling back to the base file, unlike the
  panel path. It now falls back the same way. No current call site reaches that
  branch, so this is a consistency fix.
- Language packs: the `VariablesFromLocalFile` notice key existed only in the Chinese
  and English cfgs; the other six now carry it too.

### Changed

- Agenda panel: `RangeDays` was a closed interval (6 meant 7 calendar days), so
  the "显示天数（今天起）" label was off by one. The window now spans exactly
  `RangeDays` days. An existing configuration therefore shows one day less after
  the upgrade, which is the intended behaviour change.
- `THIRD-PARTY.md`: two claims corrected. The Chinese language pack is not this
  fork's translation (`Translated=` credits the upstream author), and
  `readme.md` is not unchanged (a 25-line fork header was added to it).
- All seven executables were rebuilt from the corrected sources with AutoIt
  3.3.18.0 (`Aut2Exe`, x86, `/nopack`), including the new window text for the
  Config tool's override awareness, and their version resource is stamped with
  the next release number `0.4.0.0`.
- `AGENTS.md`: three facts corrected — the language packs hold 288 defined / 283
  unique keys (was 287/280), the Config tool's write and display-state lines are
  519/568 (was 528/553), and the "+1024 bytes vs upstream" claim (upstream binaries
  are UPX-packed, so the size gap comes from `/nopack`; the version resource itself
  costs 1024 bytes).
- `THIRD-PARTY.md`: the section 3 executable size table and the version reference now
  match this round's 0.4.0 rebuild.
- All seven executables were rebuilt again after this round's changes (same argument
  set), their version resource staying at `0.4.0.0`.
- README: the project name is now ReOmnimo and the wording only says that it
  takes over development of Omnimo; the file is split into `README.md` (English,
  primary) and `README.zh.md` (Chinese, standalone), and upstream's `readme.md` is
  deleted.
- `AGENTS.md`, `THIRD-PARTY.md`: ReOmnimo naming and the current diff counts
  (16 added / 450 modified / 17 deleted) synced.

## [0.3.0] - 2026-09-26

> **说明**：上游早于本文件，因此最早的条目汇总了 fork 到那时为止的全部改动。只有 0.3.0 发布过 tag 与 GitHub Release；0.1.0、0.1.1、0.2.0 的条目是 tag 与 Release 事后被撤回的那段工作的记录，其内容全部随 0.3.0 发布。

### 修复

- 音量面板：底部进度条在 single 与 halfsingle 两档下不居中。磁贴背景两侧各加了 `#Padding#`，而进度条的起点没有把它补回来，整条因此左偏一个 padding 单位；现在起点把它计入，进度条在磁贴下居中。
- 日程面板：以 UTC（`Z`）时间戳导出的日程此前按表盘原值放置，而不是本地时间，于是整体偏移一个 UTC 时差。解析器现在把 `Z` 时间换算成本地时间。
- 日程面板：始终没有返回有效日历的订阅会永远停在「loading feed...」。现在 45 秒超时且无数据时，面板把该订阅报为不可用；已经显示过事件的源保留上一次内容，而不是退回超时提示。
- 面板自动排布助手把 5x5 到 9x9 的布局排错了：5x5 分支重复了一段计数区间，第六行因此从未放下面板；更大的网格累加的是一个行计数器、定位面板用的是另一个，最后几行的面板全部被叠进同一列。现在每一行各用自己的计数器与区间。（源码修复在 `ActivePanels.au3`，`OmnimoApp.au3` 里的同一段代码同步修。）
- 随仓库分发的七个可执行文件都用 AutoIt 3.3.18.0（`Aut2Exe`、x86、`/nopack`、各源码自带的图标）从修正后的源码重新编译，于是上面列出的修复、0.2.0 里停留在源码层的修复，以及「安全」与「变更」两节的加固，都真正进入了用户运行的二进制。3.3.18.0 是能解析全部源码的最老解释器：`ActivePanels.au3` 与 `MultiManager.au3` 引用的现行 `WinAPIConv`/`WinAPIFiles` 含三元表达式，3.3.8.1 直接拒绝解析。裸 `Aut2Exe` 不写版本资源，因此逐个注入 `VS_VERSIONINFO` 并设为发布号 `0.3.0.0`，让工具自报版本与皮肤发布保持一致。

### 变更

- 面板尺寸档位：数字时钟与幻灯片的基础 `Height` 在所有档位下取得一致，卡片无论被哪个布局加载都保持同样的比例。
- 简体中文语言包由 `EnglishChinese.inc` 改名为 `Chinese.inc`，引用同步更新。
- 设置与配置文案的中文覆盖新增 21 个键（含 `24HourTime`、受限模式的 `Missing` 提示与面板创建器的标签）。磁贴表面与 Donate 面板的作者留言按设计保持英文。
- 日程面板：私人日历订阅改从被 git 忽略的 `UserVariables.local.inc` 覆盖文件读取，该文件缺失时回落已提交的公开默认源。已发布的日历链接等于该日历的读取凭据，把个人订阅放在本地未提交的文件里，可以保证它永不进入仓库。
- 从 `@Resources/Fonts/` 移除随包分发的六个 Microsoft Segoe 字体文件——Microsoft 未授权再分发它们。皮肤按系统字体名解析字形、从不加载这些文件，所以渲染结果不变；非 Microsoft 字体的 `OptimusPrinceps.ttf` 保留。
- AutoIt 构建脚本不再依赖已被 Microsoft 下线的 `wmic`，改为从 `PROCESSOR_ARCHITECTURE` 环境变量选择 32 位或 64 位工具链路径。
- AutoIt 助手工具改为相对 `@ScriptDir` 解析同级数据文件（`Config.cfg`、`hue.ini`、`colors.txt`、`defaultcolors.txt`、`Varrar.inc`），不再依赖进程工作目录，从任何位置启动都能正确加载。

### 安全

- 加固 AutoIt 助手源码以应对畸形输入与不安全路径（已重编译进全部七个可执行文件，见「修复」）。参数个数经过校验，工具会带提示退出，而不是越界索引入参；配置数组与面板数组在写入前先做上限约束。`config.exe` 的源码不再把从 `Rainmeter.ini` 读到的 `WindowX`/`WindowY` 直接交给 `Execute()`：只求值纯算术，其它一律按数值解析。`OmnimoApp` 与 `PanelCreator` 删除面板时会拒绝含 `..` 的配置路径，而不是删掉面板目录之外的东西；卸载器拒绝递归进不带 `WP7` 标记的目录。

> **Note**: Upstream predates this file, so the earliest entry covers everything this fork had changed up to that point. Only 0.3.0 is published as a tag and GitHub Release; the 0.1.0, 0.1.1 and 0.2.0 entries are kept as the record of work whose tags and releases were later withdrawn, and all of it ships in 0.3.0.

### Fixed

- Volume panel: the bottom progress bar sat off-center in the single and
  halfsingle tiers. The tile background adds `#Padding#` on both sides, but the
  bar's origin did not add it back, so the bar was shifted left by one padding
  unit; the origin now includes it and the bar centers under the tile.
- Agenda panel: events exported with a UTC (`Z`) timestamp were placed at the
  raw clock time instead of the local one, shifting them by the UTC offset. The
  parser now converts `Z` times to local.
- Agenda panel: a subscription that never returned a valid calendar sat on
  "loading feed..." forever. After a 45-second timeout with no data the panel
  now reports the feed as unavailable; a source that had already shown events
  keeps its last content rather than reverting to the timeout message.
- The panel auto-arrange helper mis-tiled the 5x5 through 9x9 layouts: the 5x5
  branch repeated a count range so its sixth row never placed a panel, and the
  larger grids incremented one row counter but positioned panels with another,
  stacking every panel in the last rows onto a single column. Each row now uses
  its own counter and range. (Source fix in `ActivePanels.au3` and the same code
  in `OmnimoApp.au3`.)
- All seven shipped executables were rebuilt from the corrected sources with
  AutoIt 3.3.18.0 (`Aut2Exe`, x86, `/nopack`, the sources' own icons), so the
  fixes listed above, the 0.2.0 fixes that had stayed source-only, and the
  hardening under Security and Changed all reach the binaries users run. 3.3.18.0
  is the oldest interpreter that parses every source: the current
  `WinAPIConv`/`WinAPIFiles` includes used by `ActivePanels.au3` and
  `MultiManager.au3` contain ternary expressions that 3.3.8.1 rejects. Plain
  `Aut2Exe` writes no version resource, so each executable's `VS_VERSIONINFO`
  was injected directly and set to the release version `0.3.0.0`, keeping the
  tools' reported version in step with the skin release.

### Changed

- Panel size tiers: the digital clock's and the slideshow's base `Height` is
  now the same across every tier, so a card keeps its proportions whichever
  layout loads it.
- The Simplified Chinese language pack was renamed from `EnglishChinese.inc` to
  `Chinese.inc`; references updated to match.
- Chinese coverage of the settings and configuration strings was extended by 21
  keys (among them `24HourTime`, the limited-mode `Missing` notices and the
  panel-creator labels). Tile surfaces and the Donate panel's author note stay
  English by design.
- Agenda panel: private calendar subscriptions now load from a gitignored
  `UserVariables.local.inc` override that falls back to the committed public
  defaults when absent. A published calendar URL grants read access to that
  calendar, so keeping personal feeds in a local, uncommitted file stops them
  from ever entering the repository.
- Removed the six bundled Microsoft Segoe font files from
  `@Resources/Fonts/`, which Microsoft does not license for redistribution.
  The skin resolves font faces by system name and never loaded the files, so
  nothing renders differently; `OptimusPrinceps.ttf`, not a Microsoft face,
  stays.
- The AutoIt build script no longer depends on `wmic`, which Microsoft has
  retired; it picks the 32- or 64-bit toolchain path from the
  `PROCESSOR_ARCHITECTURE` environment variables.
- The AutoIt helpers resolve their sibling data files (`Config.cfg`, `hue.ini`,
  `colors.txt`, `defaultcolors.txt`, `Varrar.inc`) against `@ScriptDir` instead
  of the process working directory, so they load regardless of where the caller
  launches them from.

### Security

- Hardened the AutoIt helper sources against malformed input and unsafe paths
  (rebuilt into all seven executables, see Fixed). Argument
  counts are validated so the tools exit with a message instead of indexing past
  the arguments they were given, and the config and panel arrays are bounded
  before writing. `config.exe`'s source no longer passes the `WindowX`/`WindowY`
  values read from `Rainmeter.ini` straight to `Execute()`: only plain arithmetic
  is evaluated, anything else is parsed as a number. Panel deletion in
  `OmnimoApp` and `PanelCreator` rejects a configured path containing `..`
  rather than deleting outside the panels folder, and the uninstaller refuses to
  recurse into a folder that carries no `WP7` marker.

## [0.2.0] - 2026-09-26

### Fixed

- RSS and other text feeds rendered `?` in place of ä, ö and –: the three
  substitution targets in the shared feed table had lost their characters
  together with their closing quotes in an encoding pass, which also left the
  table's quotes unbalanced for every meter consuming it.
- The Intro wizard's 简体中文 entry wrote `MainLanguage English`, switching the
  interface back to English. It now writes `EnglishChinese`, matching the
  gallery's language list.
- DigitalClock4 hid the clock exactly when "show seconds" was ticked: its
  `Hidden` expression contradicted the settings checkbox, which writes 0 when
  the option is enabled. The same suspicion against `Item.ini` did not survive
  reading the checkbox write code; that file was correct as shipped.
- The Network panel's grid toggle hid the grid when ticked. Both tiers now
  follow the setting.
- Agenda panel: the empty-range message hard-coded 7 days while the window is
  configurable (6 by default); a panel with no feeds configured sat on
  "loading feed..." forever; a dead subscription answering with an HTML error
  page was reported as "no events in the next N days"; all-day events exported
  with a date-time end rendered as "00:00-HH:MM"; the hint text was the only
  meter in the tier ignoring the DPI scale.
- AutoIt sources: OmnimoApp's folder/app pickers passed `StringReplace`'s
  arguments in the wrong order, so the user's choice went to a stray file and
  the panel's configuration was never updated; the settings tool's
  border-color branch read an undeclared variable and discarded the chosen
  color. Both fixes are source-only; the shipped binaries are unchanged, the
  same situation THIRD-PARTY.md section 3 already records for config.exe.

### Changed

- AGENTS.md rewritten around measured facts: the settings checkbox's write
  contract, the encoding and line-ending census, the six skip-worktree files,
  corrected diff counts, and a panels.inc example that matches the tree.
- THIRD-PARTY.md's change-set section regenerated from the actual diff; the
  fictitious ChineseUI.inc entry is gone and the Corona deletions are listed.
- The readme now notes the one exception to English tile surfaces: the Agenda
  tile's name is localized through its gallery entry.
- The 0.1.1 entry below was corrected in place: the calendar tile fills a cell
  upstream's icon layer left blank rather than replacing the digital clock;
  the height revert touched only the tier each layout loads; the GetMhz
  entry's causality (an unused per-refresh `wmic` child process, not a removed
  plugin).

## [0.1.1] - 2026-09-25

### Added

- The calendar panel now appears in the Date and Time row of the common panels
  page, next to Stopwatch, with its own calendar glyph in the shared icon layer
  and its tile name localized to 日程. It fills a cell that upstream's icon
  layer left blank.

### Changed

- The default theme carries the values the desktop was laid out against:
  `Padding=5` instead of `0`, a lighter card, no backdrop blur, and titles
  closer to the edge. Card width is `(#Height# + #Padding# * 2)`, and that
  value comes only from the theme, so at zero every panel drew ten logical
  pixels narrower and inset by five.
- The Slideshow panel's base height returns to 196 and the digital clock's to
  160, matching the release the layout came from. Only the one tier file each
  layout loads was changed; the panels' other size tiers keep 150. A card at
  150 is a quarter narrower, which is why the picture no longer lined up with
  the column below.
- The calendar panel's registration was removed from the custom panels list,
  where it was a leftover from the first attempt; it belongs to the common
  panels page, which is a hand-authored list rather than a registered one.
- `AGENTS.md` rewritten around the findings above, with the measured cell
  geometry of the gallery icon layer and the reference points for the settings
  schema, the size tiers and the desktop layout.

### Fixed

- The calendar panel could not be scrolled: the wheel actions that the
  prototype carried were lost when the panel files were generated. They are
  back in the `[Rainmeter]` section of all three size tiers.
- The panel's calendar feeds were fetched before their addresses were
  rewritten, which logged a fetch error on every load. The measures now start
  disabled and the script enables them once the address is in place.
- Merged calendar feeds were not re-sorted after deduplication, so events from
  different subscriptions interleaved instead of appearing in time order.

## [0.1.0] - 2026-09-25

### Added

- Simplified Chinese for the settings UI and the panel context menus, carried
  by a language pack (`EnglishChinese.inc`) rather than by per-configuration
  overrides, so menus inside the panels switch with the language setting too.
  Tile surfaces stay English: they are laid out around English string widths,
  and the seven keys that appear on both a surface and in the interface keep
  their English values.
- Simplified Chinese language file for the AutoIt tools.
- Agenda panel: reads up to three published ICS calendar subscriptions, merges
  and deduplicates them, and lists today plus the following six days grouped
  by day, with wheel scrolling and a return to the top after it goes idle.
  Ships in the square, double and doubleV size tiers.
- `LICENSE` (GPL-2.0) and `THIRD-PARTY.md`, which records per-file provenance
  for the media, binaries, fonts and AutoIt includes the repository carries.
- `AGENTS.md`, a working guide to this fork.

### Changed

- The language picker offers 简体中文 where the `[Help Translate]` entry used
  to be. Upstream's translation project is no longer maintained, so the entry
  it replaced had nothing left to point at.
- The Network panel pings a literal IPv4 address by default. Rainmeter does not
  get a reply from a host name in this environment, so the panel reported a
  failed measurement as if it were a latency.

### Fixed

- Panel text that ran past the tile edge. The size-tier geometry adds its
  ten-pixel allowance to one axis only, and the clipping mask now matches it.
- Calendar-style lists repeating "all day" was left alone; the underlying
  defect was that merged feeds were never re-sorted, so events appeared
  grouped by calendar instead of in time order.
- Two `Hidden=` expressions in the DigitalClock panel that read the setting
  inverted, and a `FontSize` formula with an empty operand that logged a parse
  error on every update.
- Thirteen panel tiers ran a `GetMhz` measure on every refresh. Only the RAM
  panel's second tier defined it, and no meter displayed its value, yet it
  spawned a `wmic MemoryChip` child process on every refresh; the other twelve
  calls were dangling. All thirteen call sites and the one measure are removed.
- A `Ping` meter whose auto-scaled unit was injected into the numeric format,
  turning milliseconds into "kms" past 1024.
