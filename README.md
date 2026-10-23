# Monitor Control

Switch between monitor setups without digging through Windows Settings.

Set up your screens once (which ones are on, which is the main one, and where they sit) and save that as a preset. After that, switching takes one click, a right-click on the tray icon, or a keyboard shortcut.

It's handy if you share a monitor between two computers, plug a laptop into a desk setup, or turn on the TV for movie night.

- **Portable:** no installer, administrator access, account, or network connection. It runs on Windows 10/11 x64 with the .NET Framework 4.x that Windows already includes.
- **Safe by design:** every change comes with a countdown. If you don't confirm it, the previous layout is restored automatically.
- **Private:** the app never connects to the internet. Your settings stay on your PC.

## Download and run

1. Download **MonitorControl-portable.zip** from the [latest release](https://github.com/coffeerunsmycode/monitor-control/releases/latest).
2. Check that the download is genuine. In PowerShell, run `Get-FileHash "$HOME\Downloads\MonitorControl-portable.zip"` and compare the result with the **SHA-256** in the release notes. If they differ, delete the file.
3. Extract it to a folder of your own, such as `%LOCALAPPDATA%\Programs\MonitorControl`, not Downloads or a shared folder, and open **`MonitorControl.exe`**.

You do not need the source code. Windows may show a SmartScreen warning because the app is not code-signed; choose **More info → Run anyway** only if the checksum matched. The app restores your layout after an interrupted change by starting itself again from the same folder, so keep it where only your account can change it. Opening the app and refreshing are read-only. Nothing changes until you apply a layout or use a preset's shortcut. For help inside the app, click the round **?** button or press **F1**.

## Help

These guides are built into the app: press **F1** to read them offline. They are also online:

- [User guide](https://github.com/coffeerunsmycode/monitor-control/blob/main/docs/USER-GUIDE.md): every feature, step by step.
- [FAQ](https://github.com/coffeerunsmycode/monitor-control/blob/main/docs/FAQ.md): short answers to common questions.
- [Troubleshooting](https://github.com/coffeerunsmycode/monitor-control/blob/main/docs/TROUBLESHOOTING.md): error messages, fixes, and how to report a bug.

![The main window](docs/screenshots/main-window.png)

## Set up your layouts

The example below uses two layouts for a monitor shared between two computers, but you can save as many layouts as you like.

1. Use **Identify screens** to show labels on enabled displays. Click a name in the **Name** column to change it to a label such as "Left", "Center", or "TV", then **Save monitor names** (or **Undo name changes**).
2. With some monitors enabled and others off, select your preferred **Main** monitor. Click **Save as preset…**, name it (for example **Laptop**), and optionally assign Ctrl + Alt + 1.
3. Enable another monitor and select it as **Main**. Drag the display tiles into position in the preview, or use **Arrange in a row**. Save another preset (for example **Desktop**) with Ctrl + Alt + 2.
4. Choose a preset in the drop-down list to load it into the table, then click **Apply layout**. Windows does not change until you apply. The preview's heading names what the table holds and shows **· edited** once you change it.
5. Choose **Keep changes** in the confirmation window on the resulting primary display. Otherwise, the previous layout is restored after the countdown.

**Minimize to tray** keeps presets and global shortcuts available. Right-click the tray icon to select a preset. Closing the main window exits the app, unless you check **Keep running in the tray when closed**. The app does not start automatically with Windows. Shortcut conflicts are reported in a tray notification, and the preset remains available in the app and tray.

Turning a monitor off in a layout only disables its Windows display path. The app does not change the monitor's input or send a power command. If you share a monitor between computers, whether it then switches to the other computer depends on its own hardware settings and signal detection.

## What you can customize

- Enable or disconnect individual monitors, and select one enabled primary.
- Edit monitor labels and save any number of layouts with optional hotkeys.
- Select resolutions and refresh rates exposed by the graphics driver. The drop-down lists are read from the driver for each monitor (32-bit modes), not a generic list.
- Rotate displays and edit desktop positions in physical pixels. Orientation angles are clockwise.
- Drag enabled displays in the preview to position them. Edges, centers, and top/bottom alignments snap automatically, and releasing docks to the nearest valid edge without overlaps or gaps.
- Click a display to select its table row. **Esc** cancels an in-progress drag, and **Undo move** restores the last arrangement. Moving a display that connects other displays is constrained to keep the whole group connected.
- **Exact positions** reveals the optional pixel columns, which update as you drag. Off displays must be enabled before they can be dragged.
- Enabled monitors must touch along an edge and may not overlap. The primary monitor's coordinates are normalized to `(0, 0)` on apply.
- Preset names are case-insensitive, and saving the same name asks before replacing it. Labels are global across presets. **Refresh** replaces unsaved table edits with the current state.

**Not supported in this version:** HDR, Windows text/UI scaling, color profiles, brightness, physical power, monitor input switching, and duplicate/mirror mode. Unsupported driver modes produce an error and trigger recovery if a change was started.

## How monitors are identified

Presets store the monitor's EDID manufacturer, product, and serial identity, not `DISPLAY1`, `DISPLAY2`, and so on. Each time you apply a layout, the app finds the current physical target and a valid graphics source with `QueryDisplayConfig`. Multiple inactive routes to the same physical monitor appear as one entry. Duplicate serial identities and cloned sources are blocked rather than guessed.

If a monitor supplies no useful serial, identity falls back to the Windows device interface path, which depends on the connection. Moving that monitor to another port may require naming it again and rebuilding its presets. There is no manual remapping in this version. Software cannot reconnect a physically unavailable display: reconnect its cable or make its input visible to the PC, then refresh.

## Safety and recovery

Before applying a layout, the app checks for a valid primary, missing monitors, duplicate identities, overlaps, gaps, and newly attached active monitors that an old preset doesn't include. It validates the layout through `SetDisplayConfig`, applies it, and then rereads the displays to confirm Windows honored the request.

A separate recovery process starts before every change and must report ready. The confirmation deadline is 35 seconds from the start of the change, so the visible countdown can be shorter after a slow driver operation. Keep and Revert can't both win. The recovery process survives the main app closing or crashing, and restores the previous layout if you haven't confirmed. If you lock the PC during the countdown, the restore waits until you unlock it. If Windows restarts, shuts down or signs out during the countdown, the previous layout is restored when you next sign in. Recovery can still fail if hardware disappears or the graphics driver can't apply the old layout. Such failures are logged and the recovery data is kept.

The app refuses another change while one is pending. If a pending change survives an interrupted recovery, restore your desktop through Windows Settings before removing the stale pending file. Don't remove pending files while a confirmation or recovery is still running.

## Your data

Preferences and logs are stored on your PC only, in **`%LOCALAPPDATA%\MonitorControl`**:

- `settings.json` and `.bak`: monitor names, presets, and app options.
- `last-layout.json`: the most recent pre-change layout.
- `activity.log`: changes and errors.
- `pending-*.json`: an active recovery change. `.failed` files hold unsuccessful recovery snapshots.

These files contain monitor identifiers such as serial numbers. Review or redact them before sharing them in a bug report.

## Security

To report a security problem, see [SECURITY.md](SECURITY.md). Please don't open a public issue for it.

## License

Monitor Control is licensed under the [PolyForm Noncommercial License 1.0.0](LICENSE). You may use, modify, and share it for any noncommercial purpose, provided you keep the copyright notice. Selling it or using it commercially requires separate permission from the author.

## For developers

See [CONTRIBUTING.md](https://github.com/coffeerunsmycode/monitor-control/blob/main/CONTRIBUTING.md), [docs/ARCHITECTURE.md](https://github.com/coffeerunsmycode/monitor-control/blob/main/docs/ARCHITECTURE.md), and the [changelog](https://github.com/coffeerunsmycode/monitor-control/blob/main/CHANGELOG.md).

### Build and test

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

These commands use the C# compiler bundled with Windows .NET Framework. The execution policy flag applies only to the build/test process and does not change the computer's policy. There are no package dependencies. Local build output goes to `dist\` and is ignored by Git.

To publish, run `package.ps1` to create `downloads\MonitorControl-portable.zip` (the exe, README, and license; also ignored by Git) and its SHA-256 checksum. Paste the checksum into the release notes. `build.ps1` builds the user guide, FAQ, troubleshooting page, and screenshots into the exe for the Help window, so edit them in `docs\` and rebuild, then upload it to a GitHub Release.

### Diagnostics

Read-only diagnostic commands (use absolute output paths):

```powershell
.\dist\MonitorControl.exe --diagnose "$PWD\dist\monitors.json"
.\dist\MonitorControl.exe --validate-layouts "$PWD\dist\native-validation.txt"
.\dist\MonitorControl.exe --render "$PWD\dist\preview.png"
```

The GUI executable may return control to your shell before it finishes; `Start-Process -Wait` waits for output. `--validate-layouts` calls only Windows' validation API and never applies a layout. It needs access to the local desktop and may fail inside a sandbox or remote session. `--render` uses an isolated QA profile beside the output image and exercises the editable UI without applying changes. `--render-demo` does the same with made-up generic monitors, for screenshots that are safe to publish (see [docs/screenshots](https://github.com/coffeerunsmycode/monitor-control/blob/main/docs/screenshots/README.md)). **Diagnostic output includes hardware identifiers, so keep it private or redact it before sharing a report.**

### Tests

Automated checks cover layout normalization, rejecting unsafe layouts, stable identity matching, source assignment, native struct sizes, settings persistence, recovery-process cancellation, and drag snapping with negative coordinates, different sizes, overlaps, and three-monitor connectivity. The render smoke test exercises the actual mouse handlers, verifies that dropping updates the table, and checks Escape and Undo. Disconnect/reconnect and timed restoration must still be tested by hand on real hardware, because validation alone can't prove a physical monitor handoff between computers.

### Windows API references

- [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)
- [SetDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig)
- [Display source positions and primary selection](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_source_mode)
