# Frequently asked questions

Short answers to common questions. For step-by-step instructions, see the [user guide](USER-GUIDE.md). For error messages and fixes, see [Troubleshooting](TROUBLESHOOTING.md).

## Safety and privacy

### Does Monitor Control change my monitor's input?

No. It does not switch inputs, such as HDMI or DisplayPort. It only changes how Windows uses your screens: which are on, where they sit, which is the main screen, and each screen's resolution, refresh rate, and orientation.

### Does it need administrator rights?

No. The app runs with your normal user rights.

### Does it need an internet connection?

No. The app never connects to the internet. The user guide, this FAQ, and Troubleshooting are built into the app's Help window, so help works offline too. Only **Report a problem** and **GitHub** in the Help window open web pages, and only when you click them.

### Is my data sent anywhere?

No. Your monitor names, presets, and logs are stored only on your PC, in `%LOCALAPPDATA%\MonitorControl`. If you share a bug report, the files you send are the only copies that leave your PC.

### How do I know my download is genuine?

The app is not code-signed, so check the file yourself. In PowerShell, run `Get-FileHash` on the zip you downloaded, for example `Get-FileHash "$HOME\Downloads\MonitorControl-portable.zip"`. The result must match the **SHA-256** in the notes of the [release](https://github.com/coffeerunsmycode/monitor-control/releases/latest) you downloaded. If it doesn't, delete the file.

Keep the extracted app in a folder of your own, such as `%LOCALAPPDATA%\Programs\MonitorControl`. If a change is interrupted, the app starts itself again from that folder to restore your layout, so no other account should be able to change the files there.

### Is it safe to try?

Opening the app and clicking **Refresh** or **Identify screens** does not change anything. A change happens only when you apply a layout. Each applied layout shows the **Keep this display layout?** window, and if you do nothing, the previous layout returns automatically.

### What if my screen goes black after applying a layout?

Wait for the countdown to finish. The previous layout returns on its own, usually within about 35 seconds. If the screen stays black after the countdown, see [The confirmation window is not visible](TROUBLESHOOTING.md#the-confirmation-window-is-not-visible) and [A previous change is awaiting recovery](TROUBLESHOOTING.md#a-previous-change-is-awaiting-recovery).

### Why does it ask me to confirm?

A layout can leave you with a screen you cannot see or use, and you may not be able to fix it from the app. The confirmation window gives you a chance to keep a layout that works. If you do not confirm, the old layout is restored.

### Can I undo a layout after I click Keep changes?

Not inside the app. Apply a preset you saved for your older setup, or change the screens in Windows Settings under **System > Display**.

## Setup and compatibility

### Does it run at startup?

No. The app does not add itself to Windows startup. Open it yourself when you need it.

### Does it work on Windows 10 and 11?

Yes, on 64-bit Windows 10 and Windows 11. It uses the .NET Framework that Windows already includes, so you do not need to install anything else.

### Does it work on laptops?

Yes. A laptop's built-in screen appears in the list like any other screen. Test a new preset while you are at the screens, and keep the confirmation window in mind.

### Can I use more than two monitors?

Yes. The app does not limit the number of screens. It has been tested with up to three. Your graphics card and Windows decide how many screens they can drive.

### Why can I not see a resolution I expect?

The app lists only the resolutions and refresh rates that your graphics driver reports for that screen. If the one you want is missing, see [A resolution or refresh rate is missing or rejected](TROUBLESHOOTING.md#a-resolution-or-refresh-rate-is-missing-or-rejected).

### Does it change text size or scaling?

No. Windows text and app scaling are not supported in this version. The app does not set them.

### Why is a monitor shown as "Unavailable"?

Windows cannot see that monitor right now. It may be disconnected, turned off at the monitor, or set to a different input. See [A monitor is missing from the list](TROUBLESHOOTING.md#a-monitor-is-missing-from-the-list).

## Using the app

### How many presets can I save?

The app does not set a limit. Only presets 1 to 9 can have a keyboard shortcut, and each shortcut belongs to one preset.

### Can I rename a preset?

Not directly. Save the preset again under the new name, then delete the old one.

### Can I change a preset's shortcut?

Yes. Save the preset again with the same name, choose the new shortcut, and click **Yes** to replace the preset.

### Do shortcuts work when the app is minimized to the tray?

Yes. They work as long as Monitor Control is running. They stop when you close the app.

### Why do I have to turn a screen on before I can drag it?

Screens that are off are not part of the layout, so the app does not show them as tiles you can move. Check **On** for the screen, then drag it.

### What does "Arrange in a row" do?

It places all the screens that are on in one line, with their tops aligned. The order follows their current order from left to right.

### Is the app free?

The app is licensed under the PolyForm Noncommercial License 1.0.0. You may use, modify, and share it for any noncommercial purpose, as long as you keep the copyright notice. You may not sell it or use it commercially without separate permission from the author. If you are not sure whether your use is noncommercial, ask the author before you use it. See the [LICENSE](../LICENSE) file.

### Where are my settings kept?

In `%LOCALAPPDATA%\MonitorControl`. See [Where your data is stored](USER-GUIDE.md#where-your-data-is-stored).

### Can I use the app over Remote Desktop?

The app needs to run on the local Windows desktop. In a remote session it reports: "No monitors are visible to this session. Run the app directly on your Windows desktop."

### Does the app need to stay open?

Yes, for shortcuts and the tray menu to work. You can minimize it to the tray with **Minimize to tray**. Closing the window quits the app, unless you check **Keep running in the tray when closed**; then use **Exit** in the tray menu to quit.
