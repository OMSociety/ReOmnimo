# Third-Party Notices

This repository is a fork of [fediaFedia/Omnimo](https://github.com/fediaFedia/Omnimo). It carries the
upstream tree essentially verbatim, plus a Simplified Chinese localization and a small set of source
fixes. This file records where every part comes from and under what terms it is redistributed.

The repository root [`LICENSE`](LICENSE) holds the **GNU General Public License version 2** text, which
governs the software and components, exactly as upstream declares it in its `readme.md`.

---

## 1. Upstream Omnimo

**Provenance.** The tree under ``REO/`` and ``Extra/``, plus ``Rainstaller.bmp`` and
`Rainstaller.cfg`, comes from <https://github.com/fediaFedia/Omnimo>, default branch `master`, last
commit `d6a69f1e` (2024-05-16). `upstream` is kept as a git remote so every file stays individually
traceable to its upstream author, and so upstream changes can still be merged. The bundled AutoIt sources (`AutoIT/`) came from the same upstream and are not part of the tree; the 0.6.0-and-earlier copies remain traceable in git history.

**What upstream declares.** The upstream repository has **no `LICENSE` file** — GitHub reports
`license: null`. The only repository-level statement is a section of upstream's `readme.md`:

> **LICENSE**
> Software and Components — [GPL v2](https://www.gnu.org/licenses/old-licenses/gpl-2.0.en.html)
> Images and Media — [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/)

**What the files themselves declare.** Independently of that, most `.ini` files under `REO/` carry a
`[Metadata] License=` line. Counted on this repository's tree (2026-10-20):

| Declaration | Files |
|---|---|
| `Creative Commons Attribution-Noncommercial-Share Alike 3.0 License` | 584 |
| `Creative Commons Attribution-Non-Commercial-Share Alike 3.0` (same licence, different spelling) | 2 |
| `Creative Commons Attribution-NonCommercial-NoDerivs 3.0` | 3 |
| `License=` present but empty | 2 |
| **`.ini` carrying a declaration** | **589** |
| `.ini` with no `License=` line at all | 22 |

The two statements disagree in scope: `readme.md` puts "software and components" under GPL-2.0, while
almost every individual `.ini` self-declares as CC BY-NC-SA 3.0. Where a file carries its own
declaration we treat that declaration as governing that file; where it carries none we only record
upstream's blanket statement and claim no rights of our own. The conflict is listed as an open item in
section 8.

The **3 NoDerivs files** are the only upstream files whose licence forbids modification. They are
carried unmodified:

```
REO/TextItems/Search/Item.ini
REO/TextItems/Search/ItemDark.ini
REO/TextItems/Search/ItemTransparent.ini
```

**Files with no declaration at all.** Beyond the 22 `.ini` files listed above, these formats cannot
carry a declaration or do not have one anywhere in the tree:

| Group | Count | Note |
|---|---|---|
| `.inc` / `.lua` / `.cfg` / `.js` | 347 / 11 / 223 / 4 | No `[Metadata]` section exists in these formats |
| `.png` / `.jpg` / `.gif` / `.ico` / `.bmp` / `.thm` | 1369 / 96 / 21 / 100 / 5 / 7 | No sidecar licence anywhere in the tree |
| `.txt` / `.html` / `.url` / `.lnk` / `.bat` / `.wav` / `.xml` | 60 / 4 / 2 / 12 / 2 / 1 / 1 | Various; no licence stated |
| `.dll` / `.config` / `.json` | 4 / 1 / 1 | WebView2 SDK files and the settings UI language pack (section 3) |
| `.exe` / `.ttf` | 7 / 1 | See sections 3 and 4 |

## 2. This fork's own changes

Measured against `upstream/master` with the `WP7\` -> `REO\` prefix aligned per file (byte-level
comparison of the two git trees): **84 files added, 704 modified, 425 deleted** as of the 0.8.0
working tree. A plain `git diff --name-status` without the prefix alignment reports 3314 renames
instead, which is why the alignment matters and why upstream stopped being this fork's general
baseline at 0.5.0.

Added:

| File | What it is |
|---|---|
| `LICENSE` | The GPL-2.0 text, matching the licence upstream's readme declares |
| `AGENTS.md` | A working guide to this fork: encodings, Rainmeter pitfalls, credential rules |
| `CHANGELOG.md` | Per-release notes for the fork |
| `THIRD-PARTY.md` | This file |
| `README.md` | The repository's own README, English primary, under the name ReOmnimo |
| `README.zh.md` | The same README in Chinese, standalone |
| `REO/@Resources/Common/Variables/Languages/Chinese.inc` | Simplified Chinese UI language pack, 280 unique keys, mirroring `English.inc` |
| `REO/@Resources/Common/Background/Language/Chinese.cfg` | Simplified Chinese strings for the C# tools, 54 keys |
| `REO/Panels/Agenda/` (`Item.ini`, `Item2.ini`, `Item3.ini`, `agenda.lua`, `Agenda.png`) | The Agenda calendar panel: published ICS subscriptions, merged, deduplicated and sorted |
| `REO/@Resources/Config/Panels/Agenda/` (`UserVariables.inc`, `RainConfigure.cfg`) | Its user parameters and settings schema |
| `SettingsCenter/` | This fork's C# (net48 + WebView2) rewrite of `config.exe` |
| `Tools/` | This fork's C# (net48) rewrites of all six bundled helper executables, sharing `Tools/Shared/OmnimoShared.cs` and `Tools/Shared/ModernUi.cs` |

Modified: seven language packs (a `PanelAgenda` key each), the gallery registration
(`REO/Gallery/cat1.inc`, `REO/Gallery/cat7.inc`, `REO/Gallery/Intro/intro.ini`), the shared
icon layer (`REO/@Resources/Graphics/Gallery/mask-essential.png`), three default-value files
(`REO/@Resources/Common/Variables/UserVariables.inc`, `REO/@Resources/Common/Color/color.inc`,
`REO/@Resources/Config/Panels/Network/UserVariables.inc`), 27 panel files under `REO/Panels/`
— thirteen of them (eight HDD, four RAM, one Multimeter) dropped a dangling
`!CommandMeasure GetMhz "Run"` call, only the RAM panel's second tier having defined that
measure and no meter having displayed its value, so the other twelve spawned an unused
`wmic MemoryChip` child process on every refresh; the Slideshow and DigitalClock panels now
carry the same base `Height` across every size tier, so a card no longer jumps when the
Alternative menu switches tier; the Volume panel's progress bar is recentered in the single
and halfsingle tiers, where the tile background adds `#Padding#` on both sides but the bar's
origin did not add it back; plus a `FontSize` expression with an empty operand in
DigitalClock4, a byte/bit suffix and a dropped `AutoScale` in the Network panel, and two
`Hidden` lines in the Network panel that contradicted their settings toggle — the AutoIt tool
sources, where `AutoIT/OmnimoApp.au3`'s folder/app pickers passed `StringReplace`'s arguments
in the wrong order so the choice went to a stray file and the panel config was never updated,
and `AutoIT/Config.au3`'s border-color branch read an undeclared variable instead of the color
picker's result, both fixed; a later hardening pass then touched every AutoIt source
(argument-count guards, array-bounds checks, path-traversal rejection on panel delete, an
`Execute()` restriction, a panel-layout counter fix, `@ScriptDir` path anchoring, and dropping
the `wmic` dependency from the build script). Every executable shipped is a C# rewrite built
from that corrected logic (section 3), so the fixes and the hardening reach the binaries users
run; `.gitignore` excludes the Agenda panel's per-machine feed override so a private calendar URL can
never be committed.

Deleted: the Corona panel — four files under `REO/Panels/Corona/` and six under
`REO/@Resources/Config/Panels/Corona/`, with the gallery row
and the icon layer reflowed to match; and the six Microsoft Segoe `.ttf` files under
`REO/@Resources/Fonts/`, not licensed for redistribution (see
section 4); and upstream's `readme.md`, which this repository replaces with its own `README.md` (English) and `README.zh.md` (Chinese); and the AutoIt source tree `AutoIT/` (every executable ships as the C# rewrite, section 3); plus, in the 0.8.0 pass, dead tiers of the structure presets, redundant icon copies and other files with no remaining reference in the tree.

Our changes inherit the terms of the file they touch: GPL-2.0 where upstream puts that file under
GPL-2.0, CC BY-NC-SA 3.0 where the file self-declares it. No rights are claimed over upstream
material.

## 3. Bundled executables

Seven executables ship inside the repository. All seven are this fork's own C#
(.NET Framework 4.8) rewrites of upstream's AutoIt helper GUIs, built with `dotnet build -c Release`
from `SettingsCenter/` and `Tools/`. Upstream's binaries carried no source and no declared
licence; the C# rewrites contain no third-party source - they reimplement the UDFs credited in
section 5.

Each executable embeds an application icon generated from [Microsoft's Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) (MIT, Copyright (c) 2020 Microsoft Corporation); the icons are built by `Tools/Icons/make_icons.py` from the official `FluentSystemIcons-Regular.ttf` distribution, and the full MIT text ships alongside them in `Tools/Icons/LICENSE-Fluent.md`.


```
REO/@Resources/Common/ColorChanger.exe                          95744 bytes   C# net48          (Tools/ColorChanger/)
REO/@Resources/Common/OmnimoApp.exe                             67584 bytes   C# net48          (Tools/OmnimoApp/)
REO/@Resources/Common/Background/ConfigBackground.exe           97792 bytes   C# net48          (Tools/ConfigBackground/)
REO/@Resources/Common/Config/ActivePanels.exe                   48128 bytes   C# net48          (Tools/ActivePanels/)
REO/@Resources/Common/Config/config.exe                         87552 bytes   C# net48 + WebView2 (SettingsCenter/)
REO/@Resources/Common/MultiManager/MultiManager.exe             38912 bytes   C# net48          (Tools/MultiManager/)
REO/@Resources/Common/PanelCreator/PanelCreator.exe            112128 bytes   C# net48          (Tools/PanelCreator/)
```

The sizes above are the 0.8.0.0 builds (`FileVersion` 0.8.0.0, 2026-10-20). `config.exe` also ships `config.exe.config`, the `lang/` JSON language pack, and four files taken
from Microsoft's WebView2 SDK - the managed `Microsoft.Web.WebView2.Core.dll`, `.WinForms.dll` and
`.Wpf.dll` plus the native `WebView2Loader.dll` (NuGet `Microsoft.Web.WebView2` 1.0.2903.40, not
written here); the WebView2 Runtime itself belongs to the user's system and is not bundled.

Upstream's own builds are
not uniform: `ColorChanger.exe` carries `FileVersion 6.0` and a description, so it went through
`AutoIt3Wrapper` with the source's `Res_*` directives; `config.exe`, `ConfigBackground.exe`,
`OmnimoApp.exe` and `PanelCreator.exe` embed the interpreter version `3.3.8.1`; `ActivePanels.exe`
and `MultiManager.exe` carry no version resource at all. Those last two cannot have been built with
3.3.8.1 — they include the split WinAPI UDFs (`WinAPIFiles.au3`, `GDIPlus.au3`,
`ScreenCapture.au3`) whose ternary operators 3.3.8.1 rejects with `Unable to parse line` — so
upstream compiled them against AutoIt 3.3.10 or newer. The C# builds carry their own
`Version`, `FileVersion` and `ProductVersion` from the `.csproj`.

## 4. Bundled fonts

The six Microsoft Segoe faces upstream carried are not licensed for redistribution, so the tree
bundles one font:

```
REO/@Resources/Fonts/OptimusPrinceps.ttf   41416 bytes   not a Microsoft face
```

Segoe WP is not a Windows default, so on a system without it the panels that name it
(`FontFace=Segoe WP …`, reached through the `#FontTypeWP#` variable) fall back
to whatever substitute the system provides.

## 5. AutoIt user-defined functions

The C# tools reimplement UDFs written by AutoIt community members; none of those UDFs states an
explicit licence: `ColorChooser.au3` (Yashied), `_Zip.au3`
(wraithdu), `IconImage.au3` (Ward). The 0.6.0-and-earlier `AutoIT/Includes/` copies, which also
bundled `WinAPIEx.au3`, `APIConstants.au3` (Yashied), `Binary.au3`, `DragDropEvent.au3` (Ward),
`MouseOnEvent.au3` (G.Sandler (MrCreatoR)), `_Startup.au3` (guinness), and `ColorGenerator.au3` /
`Common.au3` (author not stated), remain traceable in git history.

Three of them are reimplemented in the C# tools, which remain in the tree: `Tools/Shared/ColorPickerForm.cs`
follows `ColorChooser.au3` (Yashied), `Tools/Shared/IconExtractor.cs` follows `IconImage.au3` (Ward),
and `Tools/Shared/ZipWriter.cs` covers the two `_Zip.au3` entry points `PanelCreator` uses (wraithdu).
Those files are this fork's own code and carry the same author credit.

## 6. Rainmeter

Rainmeter and its bundled plugins are **GPL-2.0-only** and are **not** redistributed here. A skin is a
set of data files the engine loads; it is not a derivative work of the engine, so this repository's
licensing is not determined by Rainmeter's.

## 7. Attribution

- **Omnimo / fediaFedia** — the design this fork is based on and the source of almost every file.
- **Microsoft** — the Fluent UI System Icons the seven executables' application icons are drawn from (MIT; section 3).
- **Yashied, Ward, wraithdu, guinness and MrCreatoR** — the AutoIt UDFs the C# tools reimplement (section 5).
- The upstream Omnimo translators, credited inside each language pack's `Translated=` key. The
  Chinese pack carries `Translated=fediaFedia`, as upstream wrote it.

## 8. Open items

| # | Item | Status |
|---|---|---|
| 1 | Upstream's GPL-2.0 vs CC BY-NC-SA 3.0 statement conflict | Recorded in section 1; per-file declarations treated as governing that file. |
| 2 | As of 2026-10-20: 22 `.ini` plus 585 `.inc`/`.lua`/`.cfg`/`.js` and 1598 images with no declaration | Carried as-is with provenance traceable through the retained git history. No rights claimed. |

---

## Appendix — how the figures above were produced

Run from the repository root. `.ini` files under `REO/` are not all UTF-8, so the read is BOM-aware:

```powershell
function Read-Txt($p) {
  $b = [IO.File]::ReadAllBytes($p)
  if ($b.Length -ge 2 -and $b[0] -eq 0xFF -and $b[1] -eq 0xFE) { return [Text.Encoding]::Unicode.GetString($b, 2, $b.Length - 2) }
  if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { return [Text.Encoding]::UTF8.GetString($b, 3, $b.Length - 3) }
  return [Text.Encoding]::GetEncoding(1252).GetString($b)
}

$files = Get-ChildItem 'REO' -Recurse -File
$lic = @{}; $none = 0; $empty = 0
foreach ($f in ($files | Where-Object { $_.Extension -eq '.ini' })) {
  $m = [regex]::Match((Read-Txt $f.FullName), '(?im)^[ \t]*License[ \t]*=[ \t]*(.*?)[ \t]*\r?$')
  if (-not $m.Success) { $none++; continue }
  $v = $m.Groups[1].Value.Trim()
  if ($v -eq '') { $empty++ } else { $lic[$v] = 1 + ($lic[$v] -as [int]) }
}
$lic.GetEnumerator() | Sort-Object Value -Descending
"no License= line: $none   empty value: $empty"
```

> **Note**: the regex must anchor the value to its own line. A looser pattern such as
> `^\s*License\s*=\s*(.*?)\s*$` swallows the following line — the `.ini` metadata blocks put
> `Variant=` right after `License=` — which turns the two genuinely empty declarations into a bogus
> `Variant=` value and inflates the total.

The change set in section 2 comes from `git diff --name-status upstream/master -- .`.
