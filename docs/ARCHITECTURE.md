# Architecture and contributor notes

Monitor Control is a native Windows tray app written in C# / Windows Forms (x64, .NET Framework 4.x). There are no NuGet packages, solution file or SDK project. It is built with the C# compiler bundled with Windows.

**Keep code compatible with C# 5.** Do not assume a modern .NET SDK is installed.

## Where to edit

| Change | File / entry point |
| --- | --- |
| Text, buttons, spacing, grid, dialogs, preset UI | `MainForm.cs` |
| Shared styling, flat drop-downs, icon | `Ui.cs` |
| Help window: sidebar, short topics, Markdown guide reader | `Help.cs` (guide text lives in `docs\*.md`, built into the exe by `build.ps1`) |
| Preview colors, monitor tiles, mouse behavior | `Preview.cs` |
| Snapping and valid drag positions | `LayoutPlacement.cs` |
| Data models, JSON storage, layout rules | `Models.cs` |
| Detect monitors, EDID identity, apply and verify | `DisplayService.cs` |
| Windows display routes and native calls | `DisplayTopology.cs`, `Native.cs` |
| Independent rollback process | `RecoveryService.cs` |
| Startup and diagnostic command-line modes | `Program.cs` |
| Automated checks and UI smoke test | `SelfTests.cs`, `MainForm.RenderForTest` |

For styling changes, start with `MainForm.cs`, `Ui.cs` or `Preview.cs`. Avoid changing the display backend, storage format or recovery behavior as a side effect.

## Build and test

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

Exit the app first (tray icon > **Exit**), because Windows locks a running executable. To render the UI without applying display changes, run `MonitorControl.exe --render <absolute path to png>`, or `--render-demo` for made-up monitors (see `docs\screenshots\README.md`). The render also saves every Help page, so you can check the guide layout after editing `docs\*.md`.

## Design constraints

- Presets store EDID manufacturer/product/serial identity, with a connection-path fallback. Never persist `DISPLAY1`-style numbers as identity.
- Windows exposes several inactive routes per physical monitor. Group by physical interface and pick distinct valid graphics sources.
- Preserve exact active/preferred signal timing; nominal integer refresh rates are not always accepted by native validation.
- Keep at least one display enabled and exactly one enabled primary. Enabled rectangles must be connected without overlaps. Normalize primary coordinates to `(0, 0)`.
- Dragging freezes the preview transform until release and works in desktop pixels. It never calls Windows.
- Recovery runs in a separate process, armed before applying, with a 35-second deadline and atomic Keep/Revert claiming. Do not reduce it to a UI timer.
- Windows refuses `SetDisplayConfig` with Access denied (5) while the desktop is locked, switching users or shutting down. That is `DesktopUnavailableException`: the change goes back to pending and is retried, not marked failed. A Windows shutdown never counts as Revert. Arming also writes an HKCU `RunOnce` entry (`--recover`), which restores a still-pending change at the next sign-in. Keep, a successful restore, or a final failure removes the entry.
- User settings live in `%LOCALAPPDATA%\MonitorControl`, never in the project.
- Open web pages only through `AppInfo.Open`, which refuses anything but `https://`. Windows runs whatever program another kind of link names.

## Verification boundary

Automated checks cover layout rules, identity matching, storage, recovery-process cancellation and drag snapping. The UI smoke test uses the real mouse handlers. Actual monitor disconnect/reconnect and timed restoration must be tested on real hardware.

Not implemented: HDR, UI/DPI scaling, brightness, power or input control, mirror mode, color profiles, manual identity remapping, automatic startup.
