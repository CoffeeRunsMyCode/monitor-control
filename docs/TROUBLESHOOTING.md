# Troubleshooting

Each problem below lists its likely cause and what to try. Error messages are quoted exactly as the app shows them. Some messages include the name of a monitor or a Windows error code in place of the text in angle brackets.

If none of these match, see [Other messages](#other-messages) and [Report a bug](#report-a-bug).

## A monitor is missing from the list

**Likely cause:** Windows cannot see the monitor right now. Common reasons are a loose or unplugged cable, the monitor being turned off, or the monitor showing a different input. The app can also show a message when it runs in a remote session.

**What to try:**

1. Check the cable at both ends, and make sure the monitor is turned on and showing the input your PC uses.
2. Click **Refresh** in the app.
3. Open **Settings > System > Display** and check whether Windows lists the monitor. If it does not, the problem is in the connection, not in the app.
4. If the status line says "No monitors are visible to this session. Run the app directly on your Windows desktop.", you are in a remote session. Run the app on the PC itself.

The app cannot connect a monitor that Windows cannot see. Reconnect the cable or make the input visible to the PC, then click **Refresh**.

## A shortcut does nothing

**Likely cause:** One of these:

- Another program has already registered the same **Ctrl + Alt** combination. When this happens, the app shows a balloon notice titled **Shortcut unavailable**. The app also writes a line such as `Shortcut unavailable: <preset name>` to the log file.
- Monitor Control is not running. Shortcuts only work while the app is open, even if it is minimized to the tray.
- A change is in progress, or the **Keep this display layout?** window is open. The app ignores shortcuts until that is finished.

**What to try:**

1. Check that the Monitor Control icon is in the tray.
2. Wait for any open **Keep this display layout?** window to finish.
3. If you saw the **Shortcut unavailable** notice, close the program that might be using that combination. Or save the preset again with a different shortcut.
4. The preset still works from the tray menu and the main window.

## The layout went back by itself

**Likely cause:** The **Keep this display layout?** window closed without **Keep changes** being clicked. This happens when:

- The countdown reached zero before you clicked **Keep changes**.
- You clicked **Revert**, or closed the window with its X button.
- Monitor Control was closed or crashed before you confirmed. The recovery process restores the previous layout in that case.

The countdown is about 35 seconds from the start of the change. It can be shorter than that if the change took a while to apply.

**What to try:**

1. Apply the layout again.
2. When your screens look right, click **Keep changes** straight away.
3. If you need more time to check, change one thing at a time so the screens settle quickly.

## A monitor lost its name or presets after I changed ports

**Likely cause:** The app identifies a monitor by its serial number, which is stored in the monitor's built-in data (EDID). If a monitor does not report a usable serial number, the app identifies it by the Windows connection it uses, which depends on the port. Moving that monitor to a different port makes it look like a new monitor. Its name and presets then do not match.

To see which method applies to a monitor, click the **i** icon in its **Monitor** cell. The **Monitor** column tooltip also shows it. The text is either "Hardware serial (independent of Windows display number)" or "Connection identity (keep the same port)".

**What to try:**

1. Move the monitor back to its original port, if you can, and click **Refresh**.
2. If it still has no name, click **Identify screens**, type the name in **Name**, and click **Save monitor names**.
3. Save the presets again with the monitor in place.

The app has no manual way to match a monitor to its old presets in this version.

## The confirmation window is not visible

**Likely cause:** The **Keep this display layout?** window appears in the middle of the main screen after a change. If the main screen is turned off, hidden behind another display, or not visible to you, you cannot see the window.

**What to try:**

1. Do nothing. The previous layout returns when the countdown reaches zero.
2. If you can use the keyboard, press **Esc** to revert right away. Or press **Tab** to move to **Keep changes**, then press **Enter** or the spacebar.
3. If the screen stays wrong after the countdown, check the layout in **Settings > System > Display** and see [A previous change is awaiting recovery](#a-previous-change-is-awaiting-recovery).

## A previous change is awaiting recovery

When you try to apply a layout, the app shows:

> A previous change is awaiting recovery. Wait a few seconds and try again.

**Likely cause:** A change was not finished. Monitor Control keeps a recovery file until the change is kept or reverted. After a restart or sign-out during the countdown, Monitor Control restores the previous layout when you sign in and then removes the file. That takes a few seconds. If the message is still there after that, the recovery was interrupted, and the file stays behind. The app then refuses new changes.

Opening Monitor Control does not restore the layout. In that case you need to fix the screens yourself.

**What to try:**

1. Close Monitor Control. Right-click the tray icon and choose **Exit**. If you cannot find the icon, open Task Manager and end **MonitorControl.exe**.
2. Make sure no **Keep this display layout?** window is open. Do not remove recovery files while a change is still running.
3. Set your screens the way you want in **Settings > System > Display**.
4. Open `%LOCALAPPDATA%\MonitorControl`. Move any `pending-*.json` files, and any `pending-*.json.restoring` files, to a backup folder. You can delete them if you do not need them.
5. Keep any `.failed` files. They hold recovery information and do not block new changes. Send them with a bug report if you need help.
6. Start Monitor Control and try again.

## The app says that settings are invalid

When you start the app, it shows this message and does not open:

> Settings are invalid. Your file has been preserved: `<path to settings.json>`

**Likely cause:** The `settings.json` file is damaged or missing a required part. If the file is damaged in a way the app cannot read, you may see a different message from the file parser instead.

**What to try:**

1. Make a copy of `settings.json` somewhere safe, so you have the original.
2. Try `settings.json.bak` in the same folder. It is the copy from before the last save, so it may be older. To use it, copy it over `settings.json` while the app is closed.
3. If you have no usable copy, rename or delete `settings.json`. The app then starts with no names or presets.
4. Check `activity.log` in the same folder for the full error. It is useful if you report the problem.

## A resolution or refresh rate is missing or rejected

**Likely cause:** The app can only set resolutions and refresh rates that your graphics driver reports for that monitor. If Windows rejects a combination, the app shows one of these messages:

> Windows cannot validate this layout: `<Windows message>` (`<code>`).

> Windows could not apply this layout: `<Windows message>` (`<code>`).

Either message means nothing was kept. The app restores the previous layout automatically.

Some changes can also produce:

> Invalid display size, refresh rate, rotation, or position for `<monitor name>`.

This means a value is outside the range the app accepts.

**What to try:**

1. Choose a resolution and refresh rate from the **Resolution / Hz** list. After you change the orientation, check that the swapped resolution is one your monitor supports.
2. Try a lower refresh rate, or a lower resolution.
3. Check that the cable you use can carry the signal. An older cable can limit the resolution and refresh rate.
4. Update your graphics driver from the maker of your graphics card, then restart the PC and try again.

## Windows blocked the app

**Likely cause:** Windows SmartScreen does not recognize the app because it is not code-signed. See [Run MonitorControl.exe](USER-GUIDE.md#2-run-monitorcontrolexe) in the user guide.

**What to try:**

1. Click **More info**, then click **Run anyway**.
2. If there is no **Run anyway** option, right-click the zip file before extracting it, choose **Properties**, tick **Unblock** if it is shown, and extract again.
3. If your PC is managed by a work or school, it may block unknown apps. Ask whoever manages the PC.

Only continue if you downloaded the zip from the [official release page](https://github.com/coffeerunsmycode/monitor-control/releases/latest).

## Other messages

These messages come from the app. The text in angle brackets is replaced with the monitor name or the Windows error details.

| Message | What it means | What to do |
| --- | --- | --- |
| Monitor Control is already running. Open it from the system tray. | You tried to start a second copy. | Find the tray icon and double-click it. |
| `<monitor name>` is not available to this PC. Check its cable/input and refresh. No changes were made. | A monitor in the layout is not visible to Windows. | See [A monitor is missing from the list](#a-monitor-is-missing-from-the-list). |
| Ambiguous hardware identity for `<monitor name>`. No changes were made. | Two monitors report the same identity, so the app will not guess which one to change. | Click the **i** icon in each **Monitor** cell to compare them. The app does not offer a manual fix for this in this version. |
| An additional active monitor is missing from this preset. Refresh and save an updated layout first. | A monitor is on that the preset does not include. | Click **Refresh**, set the **On** check box for each screen you want, then click **Apply layout** or save the layout as a preset. |
| Displays overlap. Use Arrange in a row or edit their positions. Mirroring is not supported in this version. | Two screens that are on are placed over each other. | Click **Arrange in a row**, or move the screens apart. |
| Enabled displays must touch along an edge. Use Arrange in a row to close gaps. | Screens that are on are not all next to each other. | Click **Arrange in a row**, then drag screens to fine-tune. |
| Choose exactly one enabled monitor as primary. | No screen, or more than one screen, is marked **Main**. | Select **Main** for one screen that is on. |
| Keep at least one monitor enabled. | Every screen is off. | Check **On** for at least one screen. |
| Monitor identities are missing or ambiguous. Refresh and inspect the hardware details. | The app cannot tell the monitors apart. | Click **Refresh**, then click the **i** icon in each **Monitor** cell. |
| Display positions must be whole numbers. | A value in **Left (px)** or **Top (px)** is not a whole number. | Type a whole number, such as `0` or `-1920`. |
| Choose a valid display mode for `<monitor name>` | A **Resolution / Hz** value is not in the expected format. | Choose a value from the list. |
| Windows has no distinct display routes for this layout. | Windows cannot assign a separate connection to each enabled screen. | Click **Refresh** and try again. If it continues, restart the PC. |
| Displays are still changing. Wait a moment and refresh. | Windows is still changing its display setup. | Wait a few seconds, then click **Refresh**. |
| Another display change is still running. | Another change is in progress. | Wait for it to finish. |
| The independent recovery process did not start. No display changes were made. | The safety process could not start, so the app stopped before changing anything. | Try again. If it keeps happening, see [Report a bug](#report-a-bug). |
| Windows did not apply the requested connection state for `<monitor name>`. | Windows did not turn the screen on or off as requested. The app restores the previous layout. | Refresh, then try again. |
| The driver changed the requested mode or position for `<monitor name>`. Restoring the previous layout. | The graphics driver did not keep the requested settings. The app restores the previous layout. | Choose a different resolution or refresh rate, then try again. |
| Automatic recovery could not fully restore the previous layout. ... Recovery data: `<path>.failed` | The app could not put the old layout back. The recovery data was kept. | Fix the screens in **Settings > System > Display**. Keep the `.failed` file and see [Report a bug](#report-a-bug). |
| Cannot inspect the Windows display topology (`<code>`). Run on the local desktop. | The app cannot read the display setup. | Run the app on the PC itself, not in a remote session. |
| Enter a name between 1 and 80 characters. | The preset name is empty or too long. | Type a shorter or longer name. |
| That shortcut is already assigned to another preset. | Another preset uses the same shortcut. | Choose a different shortcut, or set this one to **None**. |
| Settings are invalid. Your file has been preserved: `<path>` | The settings file cannot be used. | See [The app says that settings are invalid](#the-app-says-that-settings-are-invalid). |
| Choose a listed value or enter whole-number positions. | Status line message after an invalid entry in the table. | Choose a value from a list, or type a whole number. |
| Windows display configuration changed. Refresh to load its current state; your edits are preserved. | Something outside the app changed your displays, such as a cable or Windows Settings. The table still shows your edits. | Click **Refresh** to see the new state. This replaces unsaved table edits. |
| Reverting to your previous layout. Refresh if recovery is still finishing. | You clicked **Revert**, or the countdown ended. | Wait a few seconds, then click **Refresh**. |
| Windows is not accepting display changes right now. Your previous layout will be restored as soon as it does. | Windows refused the change, for example because the PC is locked. | Unlock the PC. The previous layout returns on its own. |

## Report a bug

Before you report a problem, check the list above. Then report it on GitHub:

1. Open the app's **Help & about** window, press **F1** or click the **?** button, and click **Report a problem**. This opens the issues page at `https://github.com/coffeerunsmycode/monitor-control/issues`.
2. Describe what happened, what you expected, and the steps to reproduce it.
3. Include your Windows version, the number of monitors, and your graphics card and driver.
4. If it helps, attach `activity.log` from `%LOCALAPPDATA%\MonitorControl`. First, open it in Notepad and replace every serial number and other monitor identifier with `XXXX`. The log can contain hardware identifiers.
5. Do not attach `settings.json` unless you have removed the identifiers. It contains your monitor identifiers and preset names.

Do not post the output of any command-line diagnostic option publicly unless you have removed the serial numbers first.
