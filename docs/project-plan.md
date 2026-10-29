# Taskbar Groups — Project Plan (draft)

Status: milestone 0 (scaffolding) done locally, 2026-10-28. See §7 for decisions.

## 1. What we're building

From `taskbar_groups.html`, `large-screen.png` and `small-screen.png`:

- **Groups of app shortcuts** on the Windows 11 taskbar, each with a name, a colour, and a few app icons (Work, Gaming, Browsers, Media).
- **Wide layout:** each group is a coloured pill with its label and icons, sitting after Start / Search / Task View.
- **Narrow layout:** when there isn't room, all groups collapse into one **"Groups"** button that opens a flyout listing every group.
- Clicking an icon launches the app.

Implied but not in the mockup yet:

- A settings window to create and edit groups (the mockup has CSS for an `#edit-popup` but no markup).
- Real app icons pulled from the `.exe`/`.lnk` files instead of Font Awesome placeholders.
- Running-app indicators (the mockup defines an `.active-dot` style but doesn't use it).
- Starting with Windows, plus a tray icon or right-click menu to quit and open settings.

## 2. The hard part: getting onto the taskbar

**Windows 11 has no supported way for third-party apps to add UI to the taskbar.** The old "desk band" toolbar API from Windows 10 was removed. The only new taskbar API in 2026 is for AI agents, which doesn't help here. Every product that does this uses one of the approaches below:

| | Approach | How it works | Feel | Risk |
|---|---|---|---|---|
| **A** | **Overlay window** | Our own borderless, transparent, always-on-top window placed over the empty part of the taskbar. It tracks the taskbar's position, DPI, auto-hide and Explorer restarts. | Close to native | Low. Uses only documented Win32 APIs. The taskbar can sometimes draw over us for a moment after it's clicked, so we have to put our window back on top. |
| **B** | **Parent into taskbar** | Same window, but re-parented into Explorer's taskbar window (`Shell_TrayWnd`) with `SetParent`. TrafficMonitor and SoftPerfect tools do this. | Most native (moves and hides with the taskbar) | Medium. This isn't documented, so a Windows update could break it. No code runs inside Explorer. |
| C | Explorer injection (e.g. a Windhawk mod) | Code loaded into `explorer.exe` that edits the taskbar's own XAML | Truly native | **High.** If it crashes, it takes down the shell, it's fragile across updates, and it conflicts with the "safe" goal. **Not recommended.** |
| D | Pinned launcher + flyout | One pinned shortcut per group, and clicking it opens a popup (how the original open-source "Taskbar Groups" works) | Ordinary taskbar icons, without the coloured labelled pills | None (fully supported) |

**Recommendation:** build on **A**, with the window-hosting code kept small and swappable. In the first milestone, spend about a day testing **B** to see whether it looks noticeably better on your machine. D doubles as a fallback and is basically the "Groups" flyout from the narrow layout.

**Placement (decided): the groups must never overlap Windows' own controls.** That means Start, Search, Task View, pinned and running app buttons, Widgets, and the tray/clock. Each time the taskbar changes, the app measures the free gap between the app buttons and the tray using UI Automation (a Microsoft API) and draws only inside that gap. When the gap is too small for the wide layout, it collapses to the "Groups" button. If even that won't fit, it hides and stays reachable from a tray icon. This works with both left-aligned and centred taskbars, though left alignment leaves a much bigger gap.

## 3. Technology

**C# on .NET 10 (LTS, supported until Nov 2028) with WinUI 3 (Windows App SDK 2.x).**

- All Microsoft: the SDK, the UI framework (`Microsoft.WindowsAppSDK`), the build tools (`Microsoft.Windows.SDK.BuildTools`), JSON (`System.Text.Json`, source-generated) and tests (MSTest). **No third-party packages.** Versions are pinned, and `nuget.config` only allows nuget.org.
- **Why WinUI 3 rather than WPF:**
  - It's Microsoft's current native UI stack, and the Windows shell is moving to it.
  - Mica/Acrylic and Fluent controls are built in, so the groups and flyout look like part of Windows.
  - Windows App SDK 2.0 (April 2026) officially supports fully transparent windows with `SystemBackdropElement` for "floating toolbars". That was WPF's main advantage.
  - Since May 2026, Microsoft's `dotnet new winui` templates let us build without Visual Studio.
- **Trade-offs we accept:**
  - Memory: about 107 MB for the trimmed Release build vs roughly 50–70 MB for WPF.
  - Size: the self-contained publish is about 146 MB across 335 files rather than a single `.exe`. A zip compresses well, and MSIX/Store would remove the bundled runtime.
  - The "transparent, borderless, topmost overlay" combination is less battle-tested in WinUI than in WPF. Milestone 1 is the go/no-go check. If it can't sit cleanly on the taskbar, the fallback is a WPF host for just the strip.
- **Unpackaged + self-contained:** a plain `.exe` with the Windows App SDK runtime bundled. Nothing is registered or installed during development. MSIX packaging is revisited in milestone 5 for distribution.
- Win32 calls (`SetWindowPos`, `SetParent`, `TaskbarCreated`, icon extraction) use hand-written P/Invoke. If that gets large, Microsoft's `Microsoft.Windows.CsWin32` generator is the only package I'd add.
- Light tests use **MSTest** against `TaskbarGroups.Core`, a UI-free library holding config and layout logic, so tests never load WinUI.

Alternatives considered:

- **WPF:** smaller and very proven for overlays, but it isn't where Microsoft is investing and needs more work to look like Windows 11. It stays the fallback.
- **Tauri (Rust + WebView2):** would let us reuse the HTML/Tailwind mockup almost as-is. But it needs a Rust toolchain plus Node and a large npm dependency tree, which works against both "trusted packages" and "clean machine".
- **Electron:** heavy, with the same npm concerns.

## 4. Keeping your machine clean (and the container question)

### Why containers don't fit this project

- Docker, Podman, Dev Containers and Codespaces all run **Linux** containers on Windows (via WSL2). They can't run a Windows desktop app, let alone one that touches the host's taskbar.
- Windows containers exist, but they need Docker plus Hyper-V and have **no desktop**. You'd get all of Docker's weight and none of the benefit.

So for a Windows-shell app, containers would add weight without helping.

### What I recommend instead: a self-contained project folder

Everything the build needs lives inside the repo, in a git-ignored `.tools/` folder:

| Thing | Where it goes | How |
|---|---|---|
| .NET 10 SDK | `.tools/dotnet` | Microsoft's official `dotnet-install.ps1`. No admin, no registry entries, no PATH change. |
| NuGet package cache | `.tools/nuget` | `NUGET_PACKAGES` env var |
| CLI home/state | `.tools/home` | `DOTNET_CLI_HOME` env var |
| Telemetry | off | `DOTNET_CLI_TELEMETRY_OPTOUT=1` |

- `scripts/bootstrap.ps1` downloads the SDK once.
- `scripts/env.ps1` sets those variables **for the current terminal only**.
- `build.ps1`, `run.ps1` and `test.ps1` wrap the usual commands.
- **To uninstall everything, delete the folder.**
- `nuget.config` restricts packages to nuget.org only, so packages can't come from a lookalike source.

### Windows Sandbox for clean test runs

Windows Sandbox is built into Windows 11 Pro (it's Microsoft's own and lightweight). It's a throwaway VM with **its own taskbar**, so it's a good place to try builds without risking your real Explorer session. Everything is discarded when you close it.

- A `scripts/sandbox.wsb` file would map the build output read-only and auto-launch the app.
- Enabling the feature is a one-off Windows setting that you'd turn on yourself (Turn Windows features on or off → Windows Sandbox).

### What's already on this machine

- Git, at `D:\tools\Git`.
- winget.
- .NET **8** SDK, installed globally. We won't touch it. Note that .NET 8 goes out of support on 10 Nov 2026, which is another reason to use a project-local .NET 10.
- No `gh`, Node or Rust, and none are needed. `gh` is optional. If you want it, GitHub ships a portable zip that could also live in `.tools/`.

## 5. Proposed repo layout

```
win-11-taskbar-groups/
├─ .github/workflows/build.yml     # CI on windows-latest; only GitHub-official actions
├─ design/                         # the existing mockups move here
├─ docs/                           # this plan, decisions log
├─ scripts/                        # bootstrap / env / build / run / test / sandbox.wsb
├─ src/TaskbarGroups/              # WinUI 3 app
│  ├─ Shell/                       # Win32 interop: taskbar tracking, window hosting, icons, launching
│  └─ Views/                       # GroupStrip (wide), GroupsFlyout (narrow), Settings
├─ src/TaskbarGroups.Core/         # UI-free: groups.json model + load/save, layout logic
├─ tests/TaskbarGroups.Tests/      # MSTest, light
├─ global.json                     # pins the .NET 10 SDK
├─ nuget.config
├─ .gitignore                      # includes .tools/, bin/, obj/
└─ README.md
```

## 6. Milestones

0. **Bootstrap:** git repo and GitHub remote, the `.tools` toolchain scripts, an empty WinUI 3 app that runs, and CI building on push.
1. **Taskbar spike:** a coloured box that sits reliably on the taskbar. Test A vs B, and cover DPI scaling, auto-hide, Explorer restart (`TaskbarCreated`) and the primary monitor. Then pick the approach.
2. **Groups from config:** render the wide layout from `groups.json`, show real app icons, and launch apps on click. Each group gets its own icon in place of the coloured dot (§10.1).
3. **Responsive collapse:** switch to the "Groups" button and flyout when space runs out. Test it on the portrait screen (§10.3).
4. **Settings UI:** add, rename and recolour groups, pick each group's icon, and add apps by browsing or drag-and-drop.
5. **Polish:** start with Windows, tray menu, running-app indicators, light-theme support, multi-monitor, sliding with an auto-hiding taskbar (§10.2), and a release `.exe` attached to GitHub Releases.

## 7. Decisions

| Date | Decision |
|---|---|
| 2026-10-28 | New **private** GitHub repo. |
| 2026-10-28 | Groups must **never overlap Windows' own taskbar controls** (see §2). |
| 2026-10-28 | Aim to **share with others** eventually, so distribution (MSIX/Store or signed zip) and a licence are needed before going public. |
| 2026-10-28 | **Windows Sandbox** will be used for test runs (you'll enable the feature). |
| 2026-10-28 | **WinUI 3** instead of WPF (see §3). Unpackaged and self-contained for now. |

## 8. Milestone 0 notes

- The trimmed Release publish only includes the app's `.pri`/`.xbf` files when `EnableMsixTooling=true`, even for unpackaged apps. Without it, the published `.exe` crashes on startup with `0xC000027B`.
- Trimming produces IL2104 warnings from Microsoft's own `WinRT.Runtime` and `Microsoft.Windows.SDK.NET`. The app runs fine despite them. Keep an eye on this as more WinRT APIs get used.

## 9. Milestone 1 notes (taskbar spike)

**Result: approach A (overlay window) works on WinUI 3.** Tested on the dev machine (3840×2160 at 150%, centred taskbar):

- **Placement:** the strip sits in the free gap after the app buttons. It moves when buttons are added or removed, never covers Windows' controls, and collapses to the "Groups" button when the pills don't fit.
- **Hiding:** it hides while a fullscreen app is running.
- **Release build:** trimmed, it uses about 125 MB of memory and about 1.3% of one CPU core.

**How it works:**

- **Finding the free space.** `TaskbarGroups.Shell/TaskbarProbe` reads every button on the taskbar (Start, Search, apps, tray, clock…) via UI Automation, in one cached cross-process call on a background thread. `TaskbarGroups.Core/Layout/TaskbarGapFinder` turns those into free gaps, keeping 8 DIP clear of every control. It picks the first gap after the app buttons, falling back to the widest gap.
- **Keeping it up to date.** `TaskbarHost` repeats this every second, which also covers Explorer restarts, DPI and resolution changes, and auto-hide. It also re-raises the strip whenever the foreground window changes (`SetWinEventHook`), because clicking the taskbar raises the taskbar above other topmost windows.
- **Interop.** Win32 and UI Automation bindings are generated by Microsoft's CsWin32 (`Microsoft.Windows.CsWin32`), using COM source generators so they survive trimming. They live in their own assembly because `DisableRuntimeMarshalling` applies to the whole assembly.

**Gotchas found:**

- `Windows.UI.Composition.Compositor`, needed for the transparent backdrop, throws "access denied" unless the thread has a *Windows.System* `DispatcherQueue`. WinUI only creates the Microsoft.UI one. Fix: `CreateDispatcherQueueController` (`Shell/SystemDispatcherQueue`).
- `OverlappedPresenter.SetBorderAndTitleBar(false, false)` still leaves `WS_DLGFRAME`, a visible 3px edge. WinUI re-applies the style, so clearing it with `SetWindowLong` doesn't stick. Fix: subclass the window and return 0 from `WM_NCCALCSIZE`, which gives it no frame.
- Taskbar animations mean we sometimes catch an in-between layout. It's corrected on the next pass (about 0.3–1s).

**Not done yet / to revisit:**

- Approach B (parenting into `Shell_TrayWnd`) wasn't tried. A works well enough that B's extra risk (the window is destroyed when Explorer restarts) isn't justified for now.
- Primary monitor only. Secondary taskbars (`Shell_SecondaryTrayWnd`) come later.
- Hiding for fullscreen apps uses `SHQueryUserNotificationState`, which is machine-wide. A fullscreen app on the *other* monitor also hides the strip; this should be narrowed to the taskbar's own monitor.
- Polling could be replaced with UI Automation structure-change events to cut CPU further.
- The strip uses a dark theme regardless of the system setting, and the app icons are placeholders (milestone 2).

## 9a. Milestone 2 notes (app icons and launching)

- **Icons** come from the Shell's `IShellItemImageFactory` (`Shell/ShellIcons`). That gives the same icon Windows shows for an `.exe`, `.lnk`, folder, URL or `shell:AppsFolder\<AppID>` Store app.
  - They're loaded on a dedicated STA thread, because some icon handlers need STA.
  - They're converted to a `WriteableBitmap` on the UI thread.
  - Old icons with no alpha channel come back fully transparent, so they're made opaque.
- **Sharpness:** request the icon at *exactly* the pixel size it will occupy (24 DIP × monitor scale), without `SIIGBF_BIGGERSIZEOK`, and display it at `px / scale` DIPs so XAML doesn't rescale it.
  - The first version took the Shell's nearest stock size (32 px) and let XAML shrink it to 30 px, which looked soft.
  - Icon size is computed once at load, so it won't update if the strip moves to a monitor with a different scale. Revisit with multi-monitor support.
- **Launching:** `Process.Start` with `UseShellExecute = true` is the same path as the Run box, so every kind of `path` above works. Launched apps come to the front, even though the strip itself never takes focus (`WS_EX_NOACTIVATE`).
- **Config:** `groups.json` is created from the sample on first run and reloaded about 400 ms after any save (`FileSystemWatcher`). Invalid JSON is logged and ignored.
- **Testing:** clicks were tested by invoking the buttons through UI Automation, which fires the same `Click` handler. A real mouse click still needs checking by hand.

## 10. Requested features and testing (2026-10-29)

Each feature is built on its own branch and merged through its own PR.

### 10.1 Group icons (milestone 2; picker in milestone 4). Branch: `feature/group-icons`

Each group shows an icon, e.g. a briefcase for Work, instead of (or alongside) the coloured dot.

- **Where to browse:** [Icônes](https://icones.js.org/) is a good place to look. It's a browser for the Iconify collections, which bundle 150+ open-source icon sets.
- **Licences differ per set** (MIT, Apache, CC BY…), and some need attribution. So rather than allowing any icon from the site, standardise on one set.
- **Recommended set: Microsoft's Fluent UI System Icons.** It's on Icônes as the "Fluent" collection, MIT-licensed, published by Microsoft (so it meets the trusted-supplier rule), and it's the icon style Windows 11 itself uses, so the groups will look native.
- **Bundled, not fetched.** Ship a curated subset as SVG/path data committed to the repo. Copy the files from Microsoft's GitHub repo rather than pulling them from npm or the Iconify web API. No runtime network calls, so nothing leaks and it works offline.
- **Fallback:** the *Segoe Fluent Icons* font that ships with Windows 11 needs no bundling at all, but has fewer icons.
- **Config:** `groups.json` gains an `icon` field (e.g. `"briefcase"`). The settings UI gets a searchable icon picker.

### 10.2 Slide with an auto-hiding taskbar (milestone 5). Branch: `feature/autohide-slide`

Today the strip simply hides when the taskbar auto-hides. Instead it should slide down and up with the taskbar.

- **Approach:** watch the taskbar window's position with a `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` hook, filtered to `Shell_TrayWnd`. On every move, shift the strip by the same vertical amount, keeping its horizontal spot from the last full layout pass. The 1-second re-check is far too slow for an animation.
- **To verify first:** that the Windows 11 auto-hide animation really moves the `Shell_TrayWnd` window (rather than only animating its XAML content), and how smoothly we can follow it.
- **Also:** the strip has to reappear when the mouse reveals the taskbar, and must not keep the taskbar open by itself.

### 10.3 Testing on the portrait screen (not a feature)

The second monitor is portrait, about 1080 px wide, so its taskbar has much less room. It's a real-world test of the narrow layout: the pills should collapse into the "Groups" button there.

- **How to test today:** the strip only appears on the main display's taskbar (`Shell_TrayWnd`). Temporarily make the portrait screen the main display (Settings → System → Display → "Make this my main display"), then switch back afterwards.
- **What to check:** that it collapses correctly; that DPI is right if the two screens use different scaling; and that nothing goes wrong when the main display is switched while the app is running.
- **Later:** once multi-monitor support lands (milestone 5), the same checks run with the portrait screen as a secondary display.

### 10.4 Test: groups overflowing into the system tray / clock area (not a feature)

Add two or more groups (ideally with several items each) and check what happens when the strip grows wide enough to reach the notification area and clock at the far end of the taskbar.

- **What to check:** that the strip never overlaps or covers the tray icons and clock; that it stops at the tray's left edge and falls back to the collapsed "Groups" button (§ responsive collapse) instead of spilling over; and that it recovers when groups are removed or the tray gains or loses icons.
- **Also try:** with the taskbar centred and left-aligned, and with many pinned apps, since the free space differs.
