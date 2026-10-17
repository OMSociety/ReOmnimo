# 图标来源与许可

`Tools\Icons\*.ico` 由 `make_icons.py` 生成，图形取自 **Microsoft Fluent UI System Icons**。

- 上游仓库：<https://github.com/microsoft/fluentui-system-icons>
- 许可：MIT License，Copyright (c) 2020 Microsoft Corporation
- 取用形态：官方字体发行版 `FluentSystemIcons-Regular.ttf`（与仓库同许可），按各尺寸档把字形渲染成位图后打包为多尺寸 `.ico`

## 许可原文

```text
MIT License

Copyright (c) 2020 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 图标映射

| 文件 | Fluent 图标 | 用途 |
|---|---|---|
| `config.ico` | `settings` | 设置中心 `config.exe` |
| `ActivePanels.ico` | `align_straighten` | 面板排布 `ActivePanels.exe` |
| `MultiManager.ico` | `collections` | 多布局 `MultiManager.exe` |
| `ColorChanger.ico` | `color_fill` | 调色 `ColorChanger.exe` |
| `ConfigBackground.ico` | `wallpaper` | 背景 `ConfigBackground.exe` |
| `PanelCreator.ico` | `add_square` | 面板创建 `PanelCreator.exe` |
| `OmnimoApp.ico` | `wrench` | 工具箱 `OmnimoApp.exe` |

七个图标统一用 Windows 蓝 `#0F6CBD`，每个打包 9 个尺寸档（16/20/24/32/40/48/64/128/256）。
