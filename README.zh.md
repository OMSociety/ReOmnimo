<p align="center"><a href="README.md">English</a> · <strong>中文</strong></p>

<div align="center">

<img src="https://raw.githubusercontent.com/OMSociety/ReOmnimo/master/Rainstaller.bmp" width="480" alt="ReOmnimo 横幅" />

# ReOmnimo

**基于 Rainmeter 的 Windows Phone 7 风格交互式桌面信息中心** —— 接手 Omnimo 的开发。

[![Version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2FOMSociety%2FReOmnimo%2Freleases%2Flatest&query=tag_name&label=version&color=blue)](https://github.com/OMSociety/ReOmnimo/releases)
[![Rainmeter](https://img.shields.io/badge/Rainmeter-%E2%89%A55.0-green.svg)](https://www.rainmeter.net/)
[![License: GPL v2](https://img.shields.io/badge/License-GPL%20v2-blue.svg)](https://www.gnu.org/licenses/old-licenses/gpl-2.0.en.html)
[![Stars](https://img.shields.io/github/stars/OMSociety/ReOmnimo)](https://github.com/OMSociety/ReOmnimo/stargazers)
[![Issues](https://img.shields.io/github/issues/OMSociety/ReOmnimo)](https://github.com/OMSociety/ReOmnimo/issues)

[ReOmnimo 是什么](#reomnimo-是什么) • [安装](#安装) • [日程面板](#日程面板) • [开发](#开发) • [更新日志](CHANGELOG.md) • [English](README.md)

</div>

---

## ReOmnimo 是什么

**ReOmnimo** 是一套基于 [Rainmeter](https://www.rainmeter.net/) 的 Windows Phone 7 风格多功能交互式桌面信息中心。它把桌面变成高效又好看的工作区,只呈现你需要的信息。每一块可交互磁贴都能一眼给出信息与设置,并可按你的需要定制 —— 与最初的 Metro 设计不同,ReOmnimo 给了你无限的定制空间。

ReOmnimo 接手 [Omnimo](https://github.com/fediaFedia/Omnimo)(fediaFedia 与 Xyrfo 作品)的开发。

## 安装

ReOmnimo 以 Rainmeter 皮肤套件分发。先安装 [Rainmeter](https://www.rainmeter.net/) 5.0 或更高版本——5.0 目前经其[预发布主题](https://forum.rainmeter.net/viewtopic.php?t=45991)分发——再经 Rainmeter 的皮肤管理加载本套件。仓库根的 `Rainstaller.cfg` 描述了该包(`MinRainmeterVer=5.0`)。

> **注意**: 桌面映射要求套件位于 `Documents\Rainmeter\Skins\REO`,工作配置见 `AGENTS.md`。

## 日程面板

日程面板订阅公开的 ICS 日历,把未来几天渲染成可滚动的列表。

- 每个面板最多三条订阅 URL,按时间合并排序。
- 重复事件(`RRULE`)按有界子集展开(`DAILY`/`WEEKLY`/`MONTHLY`/`YEARLY` 加 `INTERVAL`/`COUNT`/`UNTIL`/`BYDAY`/`BYMONTHDAY`/`BYMONTH`);其它规则退回只画 `DTSTART` 那一条。
- 事件时间经内置固定偏移表换算到本机时间;偏移不跟夏令时走,部分区域在夏令时期间可能差一小时,这是刻意取舍。
- `RangeDays` 是显示的确切日历日天数。

> **注意**: 公开的日历订阅 URL 本身就是凭据,不要提交进任何被跟踪文件,放在未跟踪的 `UserVariables.local.inc` 里。详见 `AGENTS.md`。

## 开发

先读 `AGENTS.md`,其中记录了文件编码、会踩坑的 Rainmeter 行为,以及适用于日历订阅 URL 的凭据规则。

## 更新日志

逐版本的改动见 [`CHANGELOG.md`](CHANGELOG.md)。

## 支持与致谢

Omnimo 由 [fediaFedia](https://github.com/fediaFedia) 与 Xyrfo 创作,感谢 Marko C.、Giblet、Varelse42、poiru 的贡献,以及 jsmorley、Smurfier、theAzack9 在 Rainmeter 上的工作。

## 许可证与作者

- 软件与组件:[GPL-2.0](LICENSE)
- 图像与媒体:[CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/)

ReOmnimo 由 [OMSociety](https://github.com/OMSociety/ReOmnimo) 维护。
