# -*- coding: utf-8 -*-
"""重新生成 Tools\Icons\*.ico。

图标来自微软 Fluent UI System Icons（MIT，见 Tools\Icons\LICENSE-Fluent.md）。
用官方字体版（FluentSystemIcons-Regular.ttf）按各尺寸档渲染，避免二进制图标被手改。

用法（需要一次联网下载字体，约 2.8 MB）：
    python Tools\\Icons\\make_icons.py

字体与码点表下载到 Tools\\Icons\\.cache\\，不入库（放 .gitignore）。
"""
from __future__ import print_function

import io
import json
import os
import sys
import urllib.request

BASE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(BASE, ".cache")
TTF = os.path.join(CACHE, "FluentSystemIcons-Regular.ttf")
MAPJSON = os.path.join(CACHE, "FluentSystemIcons-Regular.json")
URL = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/fonts/"

# 统一 Windows 蓝
COLOR = "#0F6CBD"
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

# 输出文件名 → (Fluent 图标主名, 说明)
ICONS = [
    ("config.ico",           "settings",         "设置中心 config.exe"),
    ("ActivePanels.ico",     "align_straighten", "面板排布 ActivePanels.exe"),
    ("MultiManager.ico",     "collections",      "多布局 MultiManager.exe"),
    ("ColorChanger.ico",     "color_fill",       "调色 ColorChanger.exe"),
    ("ConfigBackground.ico", "wallpaper",        "背景 ConfigBackground.exe"),
    ("PanelCreator.ico",     "add_square",       "面板创建 PanelCreator.exe"),
    ("OmnimoApp.ico",        "wrench",           "工具箱 OmnimoApp.exe"),
]


def fetch(name):
    path = os.path.join(CACHE, name)
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    if not os.path.isdir(CACHE):
        os.makedirs(CACHE)
    print("下载 %s ..." % name)
    urllib.request.urlretrieve(URL + name, path)
    return path


def main():
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        print("需要 Pillow：pip install pillow -i https://pypi.tuna.tsinghua.edu.cn/simple")
        return 1

    fetch("FluentSystemIcons-Regular.ttf")
    fetch("FluentSystemIcons-Regular.json")
    cmap = json.load(io.open(MAPJSON, encoding="utf-8"))

    def glyph(base, size):
        # Fluent 只提供部分光学尺寸档，缺失时按 24 → 20 → 16 → 28 → 32 → 48 回落
        for s in (size, 24, 20, 16, 28, 32, 48):
            key = "ic_fluent_%s_%d_regular" % (base, s)
            if key in cmap:
                return int(cmap[key])
        raise KeyError("字体里没有图标：%s" % base)

    def render(base, size):
        # 4 倍超采样后缩小，边缘更干净
        ss = 4
        font = ImageFont.truetype(TTF, size * ss)
        ch = chr(glyph(base, size))
        bbox = font.getbbox(ch)
        gw, gh = bbox[2] - bbox[0], bbox[3] - bbox[1]
        img = Image.new("RGBA", (size * ss, size * ss), (0, 0, 0, 0))
        ImageDraw.Draw(img).text(
            ((size * ss - gw) // 2 - bbox[0], (size * ss - gh) // 2 - bbox[1]),
            ch, font=font, fill=COLOR)
        return img.resize((size, size), Image.LANCZOS)

    for out_name, base, label in ICONS:
        frames = [render(base, s) for s in SIZES]
        path = os.path.join(BASE, out_name)
        frames[-1].save(path, format="ICO",
                        sizes=[(f.width, f.height) for f in frames],
                        append_images=frames[:-1])
        print("%-22s %-18s %6d B  %d 帧" % (out_name, label, os.path.getsize(path), len(frames)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
