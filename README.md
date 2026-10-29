# Taskbar Groups

Colour-coded groups of app shortcuts on the Windows 11 taskbar. When space runs out they collapse into a single **Groups** flyout. See [`design/`](design/) for the mockups and [`docs/project-plan.md`](docs/project-plan.md) for the plan.

**Status:** milestone 2. The groups sit in the free space on the taskbar, show each app's real icon, and launch the app when you click it (or switch to it if it is already running; Shift+click or middle-click opens a new window). A small bar under an icon shows the app is running (for Store apps, `.exe` and `.lnk` shortcuts; folders and URLs have none yet), and a longer blue one marks the app you are in front of, updating within a second as apps start and close. When they don't fit, they collapse into a **Groups** button that opens a list of every group (Esc or clicking elsewhere closes it). Right-click the strip for **Settings** (add, edit and reorder groups and apps) or *Exit*. A **tray icon** (notification area, maybe under the ^ overflow) offers the same plus *Show on taskbar*, *Start with Windows* (a per-user entry under the Run registry key; no admin rights, and the tick always reflects the registry), *Edit groups file* and *Open log folder*; clicking it opens Settings. It stays available when the strip is hidden for lack of room or a fullscreen app. The log is at `%LOCALAPPDATA%\TaskbarGroups\taskbargroups.log`.

**Roadmap:** planned work is tracked in [Issues](https://github.com/trevortmorgan/win-11-taskbar-groups/issues), grouped into [milestones](https://github.com/trevortmorgan/win-11-taskbar-groups/milestones).

## Setting up your groups

Right-click the strip → **Settings**. Every change is saved as you make it and shows on the taskbar straight away.

- **Groups:** *Add group*, rename it in the **Name** box, reorder by dragging (or the ▲ ▼ buttons), and remove with *Delete group*.
- **Colour:** pick one of the eight palette colours, or the palette button for any custom colour.
- **Icon:** pick one of the bundled Fluent icons (searchable), *No icon* for the dot, or *Use my own .svg file…*.
- **Apps:** *Browse…* for `.exe`/`.lnk` files, *Installed apps…* to choose from your Start menu apps (including Store apps), or drag shortcuts, programs and folders into the list. Drag rows to reorder them (or Alt+Shift+↑/↓ on the keyboard), rename an app by editing its name, and remove one with the bin button.

Settings are stored in `%APPDATA%\TaskbarGroups\groups.json`, and you can still edit that file by hand (the strip reloads as soon as you save). Close the settings window before hand-editing, as it writes its own copy of the groups back. If the file has a mistake in it, the current groups stay on screen, the error goes in the log, and the settings window refuses to open until it's fixed. The format:

```json
{
  "groups": [
    {
      "name": "Work",
      "color": "#3B82F6",
      "icon": "briefcase",
      "apps": [
        { "name": "File Explorer", "path": "shell:AppsFolder\\Microsoft.Windows.Explorer" },
        { "name": "Reports", "path": "D:\\Reports" },
        { "name": "VS Code", "path": "%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe", "arguments": "--new-window" }
      ]
    }
  ]
}
```

- **`color`** is `#RRGGBB`.
- **`icon`** is optional. It's drawn in the group's colour; without one, the group gets a coloured dot. It can be:
  - **a built-in icon name.** These come from Microsoft's [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons): airplane, animal-cat, apps, beaker, book, briefcase, bug, building, calculator, calendar, camera, cart, chat, cloud, code, color, data-bar-vertical, database, design-ideas, desktop, document, drink-coffee, flash, folder, food, games, gift, globe, hat-graduation, headphones, heart, home, image, key, laptop, leaf-one, library, lightbulb, lock-closed, mail, mic, money, movies-and-tv, music-note-2, news, notebook, paint-brush, pen, people, person, phone, puzzle-piece, rocket, server, settings, shield, sparkle, sport, star, target, toolbox, trophy, video, wallet, window-console, wrench. You can preview them in the *Fluent UI System Icons* collection on [Icônes](https://icones.js.org/collection/fluent).
  - **your own `.svg` file**, e.g. `"icon": "icons\\rocket.svg"`. A relative path is relative to the folder `groups.json` is in. Any icon on [Icônes](https://icones.js.org/) works: open it, download the SVG and save it there. Single-colour icons take the group's colour; multi-colour ones keep theirs. Check the licence of the set you pick if you share your icons with others.
  - If the name or file isn't found, the group shows the dot and the reason goes in the log.
- **`path`** is anything you could type into the Run box (Win+R): an `.exe`, a `.lnk` shortcut, a folder, a URL, or a Store app as `shell:AppsFolder\<AppID>`. Environment variables like `%LOCALAPPDATA%` work.
- **Finding App IDs:** in PowerShell, run `Get-StartApps` to list the App ID of every app in the Start menu, including Store apps.
- **`arguments`** is optional.
- **Backslashes** must be doubled in JSON.

To try a different file without touching yours, run `TaskbarGroups.exe --config path\to\groups.json`.

## Tip: avoid seeing an app twice

A running app shows up both in its group and as its own button on the taskbar. Windows has no setting to hide a single app's button, and Taskbar Groups doesn't touch other apps' windows, so the button stays. Two Windows settings keep the duplication small:

- **Combine the buttons.** Settings → Personalization → Taskbar → Taskbar behaviors → **Combine taskbar buttons and hide labels** → **Always**. Each running app then costs one taskbar button, however many windows it has.
- **Unpin apps that are in a group.** A pinned app appears twice even when it isn't running (the pin plus the group's icon). Once it's unpinned, its taskbar button only exists while it runs: right-click the button → *Unpin from taskbar*.

## Installing a release

1. Download `TaskbarGroups-<version>-win-x64.zip` (or `win-arm64` for ARM PCs) from the [Releases](https://github.com/trevortmorgan/win-11-taskbar-groups/releases) page.
2. Right-click the zip → *Properties* → tick *Unblock* → OK (or run `Unblock-File` on it), then extract it somewhere you'll keep it, e.g. `%LOCALAPPDATA%\Programs\TaskbarGroups`.
3. Run `TaskbarGroups.exe`. Use the tray icon's *Start with Windows* to have it start at sign-in.

The build isn't code-signed, so SmartScreen may warn on first run (*More info* → *Run anyway*). `SHA256SUMS.txt` on the release lets you check the download. To uninstall, exit from the tray icon and delete the folder; settings stay in `%APPDATA%\TaskbarGroups` until you delete them too.

### Cutting a release

Maintainers: from an up-to-date `main`, push a version tag. The [release workflow](.github/workflows/release.yml) runs the tests, publishes win-x64 and win-arm64, zips them and creates the GitHub Release with generated notes. A tag with a suffix (`v0.3.0-beta.1`) is marked as a pre-release.

```powershell
git tag v0.3.0
git push origin v0.3.0
```

The tag must look like `vMAJOR.MINOR.PATCH`; the number is stamped into the executable's version.

## Stack

C# / .NET 10 / WinUI 3 (Windows App SDK). It's an unpackaged, self-contained app: a plain `.exe` with nothing to install. Every package comes from Microsoft. Requires Windows 11 22H2 or later.

## Getting started

Nothing is installed globally. The .NET SDK, NuGet cache and CLI state all live in `.tools/`, which is git-ignored. To remove everything, delete that folder.

```powershell
.\scripts\bootstrap.ps1   # one-off: downloads the pinned .NET SDK into .tools\ (checks Microsoft's signature first)
.\scripts\run.ps1         # build + run (Debug)
.\scripts\test.ps1        # run tests
.\scripts\publish.ps1     # Release build -> artifacts\publish\win-x64
.\scripts\sandbox.ps1     # publish, then try it in Windows Sandbox (needs the Windows Sandbox feature)
```

To use `dotnet` directly in a terminal, dot-source the environment first. It only affects that terminal:

```powershell
. .\scripts\env.ps1
```

## Layout

| Path | What |
|---|---|
| `src/TaskbarGroups` | WinUI 3 app |
| `src/TaskbarGroups.Core` | UI-free logic (config, taskbar gap-finding), unit-tested |
| `src/TaskbarGroups.Shell` | Win32 + UI Automation interop (reading the taskbar, window placement), generated by CsWin32 |
| `tests/TaskbarGroups.Tests` | MSTest tests (light) |
| `scripts/` | Toolchain and dev scripts |
| `design/` | HTML/PNG mockups |
| `docs/` | Project plan |

User config lives at `%APPDATA%\TaskbarGroups\groups.json`. Until that file exists, the app uses a built-in sample.

## Licence

[MIT](LICENSE). The bundled Fluent icons are © Microsoft, also MIT: see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
