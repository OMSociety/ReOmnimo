# SettingsCenter — ReOmnimo 设置中心（.NET Framework 4.8 零依赖版）

> 替代原 AutoIt `config.exe` 的现代设置面板。右键皮肤 →「设置」即弹出。
> 目标框架 **net48**（Windows 10 1607+ 内置），分发体积 ~360 KB，无需安装运行时。

## 架构

```
config.exe                ← 本程序（net48，WinForms 无边框窗体）
├─ WebView2 (Edge Chromium) ← 渲染设置界面（HTML/CSS/JS）
├─ lang\zh-CN.json        ← 语言包（标签/hint/界面文案）
└─ REO\@Resources\Config\ ← 375 个 RainConfigure.cfg（schema）+ UserVariables.inc（值）
```

## 工作流程

1. **解析**：读 `RainConfigure.cfg`（settings 段 schema）+ `UserVariables.inc`（当前值）+ `UserVariables.local.inc`（覆盖链，存在则胜出）
2. **展示**：WebView2 渲染无边框窗体（自绘标题栏/导航/控件，暗色/亮色跟随系统）
3. **写回**：300ms 防抖 → 编码感知写回 inc → `!Refresh` 面板
4. **兼容性**：位置参数与老 config.exe 完全一致（`<模式> <config> [file] [settings] [skins] [program]`），皮肤零改动

## 构建

```powershell
cd SettingsCenter
dotnet build -c Release
# 产物：bin\Release\net48\config.exe + WebView2Loader.dll + lang\
```

## 部署

把 `bin\Release\net48\` 内容拷到 `REO\@Resources\Common\Config\`，覆盖旧 `config.exe`。
**注意**：`config.exe.config` 是运行时绑定配置，必须一起拷。

## 依赖

- .NET Framework 4.8（Win10 1607+ 预装）
- WebView2 Runtime（Win10 1803+ 预装 / Win11 内置；缺失时引导用户到 https://go.microsoft.com/fwlink/p/?LinkId=2124703）

## 文件清单

| 文件 | 作用 |
|---|---|
| `Program.cs` | 主窗体（无边框、拖拽、缩放、WebView2 宿主、消息泵） |
| `MiniJson.cs` | 迷你 JSON 解析器（替代 System.Text.Json，零依赖） |
| `Lang.cs` | 语言包加载（labels/hints/ui 三节） |
| `app.manifest` | DPI 感知 PerMonitorV2 |
| `lang\zh-CN.json` | 中文语言包（29 KB） |
