# Changelog

本项目的更改记录在此文件。

All notable changes to this project are documented in this file.

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)；
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
  - 多语言收尾（2026-10-17）：八语键集对齐到 46 键——此前 Dutch / French / German / Portuguese / Spanish 缺 `Apply`（ConfigBackground 右下角按钮在这些语言下显示英文键名）；PanelCreator 的拖放提示（新键 `DropHint`，替代 2013 年内嵌英文位图 droptext.png）与作者输入框标题接语言键；六份 UTF-16LE+BOM 语种编码与行尾保持不变。三窗跟随皮肤语言包（`Varrar.inc` 的 `Language=`），实机验证中文与德文界面。
- 版面核对的三轮修正（2026-10-17）：ColorChanger 色板网格右列溢出窗口、末行缺一角——间距从 44px 改回原版 au3 的 40px，并把常驻的自定义色块（原版第 18 格，点击弹系统取色器）放回网格末格；PanelCreator 预览区面板名被居中格式钉在 x=10、左半裁掉——改回原版的默认左对齐绘制。
- **删除贡献者包分类**（2026-10-18）：`Gallery\cat5.inc`、「贡献者包」侧栏项与其图标、`mask-contrib.png` 覆盖层、`Icons\heart.png`、主题模板里只给这些磁贴用的 `[P]` 样式（`!ToggleConfig "REO\DonatorPanels\…"`，而 `REO\DonatorPanels` 目录从未存在）一并移除；`Gallery\main.ini` 里 `[Cat5]`、`[Cat5Indicator]`、`[CatIcon5]` 三段删除后，设置标题与下一分类图标各上移一行（370→320、392→344）。四类贡献者语言键从 8 份运行时包与 `lang.inc` / `Backup\English.inc` / `Readme\Translate\**` 中移除，Chinese / English 现各 281 键。
- 天气磁贴的「如何找到你的天气代码?」原指向上游的 omnimo.info/weather/index.html（已不可达），现改为随皮肤分发的本地说明页 @Resources\Readme\WeatherCode.html（中英双语，三条取码路径：weather.com 网址末段 id、wxdata 时代的短代码、美国邮编），经 OmnimoApp.exe Browser 打开。\n- TextItems\Extra\Missing 占位项（保存的布局引用了未安装的皮肤时出现）原先写着「点此下载 Omnimo」并链到上游主页，文案与动作一并去掉。\n- 删除 Panels\Anniversary 与其 8 张语言图：没有任何磁贴引用它，唯一动作指向上游十周年纪念页。\n- 画廊右键菜单的入口从 2013 年的上游页面改到本 fork：关于 指向本 fork 的 GitHub 主页（原先是上游的 Credits.html），检查更新 指向 Releases 页，发送反馈 指向 Issue 列表（原先是给原作者 gmail 的 mailto:），捐赠 条目删除（原链接打款给原作者，与本 fork 无关）。
- **删除 60 个无面板的孤儿配置目录**（2026-10-18）：`@Resources\Config\Panels` 有 83 个配置目录，而 `REO\Panels` 只有 62 个面板；多出的 23 个（含 `TextItems` 侧同形目录）永远不会被任何 include 或设置窗口引用，共 347 个文件。清理后 `Config\Panels` 40 个目录、`TextItems` 17 个。设置窗口同时改为只列 schema 里真有一组设置的面板（此前 35 个空 schema 面板点开只有「0 项设置」）。

### 修复

- 15 个面板变体没写 `@include…=#@#Config\Panels\<名>\UserVariables.inc`，设置窗口能改能存而磁贴不读：`Notes`（Item / Item2 / Item3，用 `#NotesFont#`）、`Timer`（用 `#SoundFile#`）、`MediaKey` 的四个 `MediaRemote`、`Brightness`、`Folders`、`RAM`、`Volume` 的其余变体补齐 include；补插位置限定在 `[Variables]` 段内、最后一条 include 之后，按各文件原有编码写回。
- 三个 GUI 工具窗口圆角处的白点：圆角此前由 DWM 圆角偏好（`DWMWA_WINDOW_CORNER_PREFERENCE`）与 WinForms `Region` 双重裁剪，两套半径不一致，DWM 抗锯齿在角上采样到未绘制的像素；现只保留 DWM 圆角，角部随窗口背景一同绘制。
- ActivePanels 3x3 布局在启用面板达到 31 个时崩溃（AutoIt 报 `Variable used without being declared`）：`ActivePanels.au3:278` 使用了从未声明的 `$row9`（只声明了 `$row1..$row8`），第 31 个面板起排布中断；C# 版按源码本来的行表补全声明，可正常排到 row9 并放完剩余面板。
- SettingsCenter `x86` 位数锁定：WebView2 NuGet 对 net48 只带 x86 原生 `WebView2Loader.dll`，SDK 风格工程默认 AnyCPU（x64 进程）加载即 `BadImageFormatException (0x8007000B)` 崩溃；`PlatformTarget=x86` 修正（csproj 注释留档）。
- SettingsCenter 用户数据目录按面板哈希分目录（`%LOCALAPPDATA%\ReOmnimo\SettingsCenter\<hash>`）：不同面板的设置窗口可并行；同面板双开不再以崩溃弹窗告终，改为提示后正常关闭。
- 全库 973 处小写 `wp7\` 死引用改为 `REO\`（433 个皮肤文件）：0.5.0 的改名提交只处理了大写 `WP7\`，磁贴、右键菜单、启动器里的 `!ToggleConfig "wp7\…"` 一直指向不存在的 config，动作全部失效（日志报 `!ToggleConfig: "wp7\…" does not exist`）。逐文件按嗅探到的编码解码再原编码写回（UTF-8 / UTF-16LE+BOM / cp1252 混杂），BOM 与行尾不变；`git grep -In "wp7"` 现查 0 命中，实机确认 `REO\` 形可正常切换面板。`DragGroup=WP7Panel` 是拖拽分组名而非路径，未改；`Gallery\MultiManager\Saved\` 下的用户布局快照未动。
- 工具窗口的关闭键「看不见」与任务栏缩略图没有名字：标题标签宽度固定 400px、横跨到右上角按钮并压在其上层，标签背景不透明，X 被整块盖住；`layoutCaption` 现把标题收窄到按钮左侧并 `BringToFront`，X 的描边从次要色改为前景色。`ModernForm` 此前从未给 `Form.Text` 赋值，任务栏缩略图因此没有标题，现已写入窗口名。
- PanelCreator 两个输入框没有提示文字：`TextBox` 是原生 EDIT、自己处理 WM_PAINT，重写 `OnPaint` 里画的占位文本根本不会执行（试过放开「仅失焦时绘制」的条件也不显示）；改用 EDIT 自带 cue banner（`EM_SETCUEBANNER`，wParam=1 让聚焦时也显示）。
- 跨屏拖动后排版错位：DPI 系数原先只在创建句柄时算一次，窗口从 125% 屏拖到 150% 屏时系统只改窗口尺寸、控件树不重排，自绘内容按新 DPI 落在旧矩形里，右侧残留一块黑矩形。`ModernForm` 现接管 `WM_DPICHANGED`，按新旧 DPI 比值缩放子控件与字体、重跑标题栏布局、复位被系统改高的原生 TrackBar，再采用系统建议矩形；125%↔150% 往返实测窗口与各子控件矩形逐项一致。
- 取色对话框未适配深色：`ColorPickerForm` 硬编码白底与灰字。现接 `ModernUi.Theme`——底色 / 输入井 / 分隔线 / 色板格 / 箭头条全部走主题，标题栏用 `DWMWA_USE_IMMERSIVE_DARK_MODE` 切深色，OK 按钮改主题强调色；吸管、色盘、箭头三个 2013 年的位图改为按控件尺寸现画的线性字形（DPI 变化不再糊）。
- 强调色可能是中性灰：系统 `AccentColor` 本机为 `0xA1A1A1`，直接当强调色会让滑杆与主按钮整片发灰；现按饱和度判定，低于 12% 回落到工具集原本的蓝。
- 设置窗的排版细节：勾选框从 16px 收到 14px（与 9pt 文字视觉等高），「使用图片」标签与勾选框同基线；滑杆拇指两端各内缩 6px，拉到最右不再被裁掉半个；`ConfigBackground` 行距统一到 36px、「上限 W × H」右对齐贴着数字框；列表滚动条走 `DarkMode_Explorer` 主题（条目填满后才补发一次——句柄创建时切主题对它无效），卡片补圆角 `Region` 并让非客户区重算，消除换掉原生滚动条后留下的竖痕。
- 取色框的 R / G / B 三个输入框从未显示：`MakeNumberBox` 造出控件后没有 `Controls.Add`，屏幕上只剩三个字母；同时标签宽 13px 装不下「R:」。现补上入窗并把标签放宽到 17px、输入框右移到 35 / 99 / 163。
- 取色框调色板菜单的排版：菜单项内边距从 2/3 改为 10/5（左 18 给点击余量），分隔线加 4px 竖向留白，弹出位置从齿轮正下方改为左上方向内弹出并夹在窗体内——原位置会压住 OK 一行。
- 设置窗侧栏列出「0 项设置」的面板：`Config\Panels` 下 83 个配置目录里有 35 个的 `RainConfigure.cfg` 不足四行（空 schema），列在侧栏只会在点开时显示「0 项设置」；侧栏现按 schema 行数过滤，只列有可编辑项的面板。
- `ConfigBackground` 分组标题右侧的细分隔线已去掉：中文标题比英文短，线从标题尾部一路拖到卡片右边，看起来像一条没有含义的横条；勾选框旁的说明文字改走 `AddCheckLabel`（垂直居中），不再顶在 25px 高容器的上沿。
- 背景设置点开没反应：`Background\bg.ini` 与 `Background\DayNight\Item.ini` 调用的是不存在的 `Common\Background\config.exe`（该目录实际部署名是 `ConfigBackground.exe`），而且只传了一个参数——工具契约是 `ConfigBackground.exe <VarFile> <SkinPath>`，原版 AutoIt 同样读 `$CmdLine[2]`，缺参直接越界。现改为正确文件名并补上 `#SKINSPATH#`。
- 面板设置项的说明文字：10 个 `RainConfigure.cfg` 的描述行原是从上游带来的玩笑与笔误（`URL to DDoS.. err I mean ping`、`Make it wider why dontcha?`、`Toggle from Low Brightness Value`、`If you have several audio devices, specify which to switch Index to switch`），在一个要用户照着填的对话框里说不清字段含义。逐条改写为说明实际行为，产品名与大小写一并订正（Steam / Big Picture）；参数名、控件类型与四行一组的 schema 结构未动。

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
