# Changelog

All notable changes to this project are recorded here. Format loosely follows [Keep a Changelog](https://keepachangelog.com/).

## [1.0.0] - 2026-10-24

- Draggable display arrangement with edge snapping.
- Named presets, global shortcuts, and tray switching.
- Keep/Revert confirmation backed by an independent recovery process.
- Flat drop-down lists, drawn app icon, and in-app Help window.
- Buttons and drop-down fields have rounded corners (4 px, as in Windows 11). The main window has no slogan header; Help is at the right end of the preset row.
- Choosing a preset in the drop-down list opens it in the table for editing. The **Load preset** button is gone. Windows changes only when you apply.
- Monitor names look editable: each is a text box with a pencil, and one click starts typing. Unsaved names show a blue border. **Undo name changes** goes back to the saved names. Names are now saved only by **Save monitor names**, not by applying a layout or saving a preset.
- One **Apply layout** button replaces **Apply preset** and **Apply edited layout**. It always applies the table, so edits to a chosen preset are no longer skipped.
- The preview's heading names what the table holds (**Current Windows layout**, a preset, or **Unsaved layout**) and shows **· edited** after a change. **Save as preset…** starts with the current preset's name and shortcut.
- The preset list shows **Choose a preset…** or **No presets yet**, and **Delete** is disabled until a preset is chosen.
- The preview counts every monitor Windows can see, not only the ones that are on.
- **Main** is a radio button, since only one screen can be main.
- **Identify screens** and **Refresh** moved to the top row; the tool row keeps only arrangement tools. Hardware details open from an **i** icon in each **Monitor** cell.
- Messages appear once, in a status bar along the bottom edge. The **Your name** column is now **Name**.
- New **Keep running in the tray when closed** option (off by default). When it is on, the X button hides the window; **Exit** in the tray menu quits. The "still running in the tray" notice is shown only the first time.
- New Help window, laid out like a help centre: a sidebar of short topics, plus the full user guide, FAQ, and troubleshooting pages built into the app, so help works without internet. Text re-wraps as the window is resized, and links jump between sections. The user guide has screenshots.

### Security

- Links in the Help window open only `https://` web pages. Other kinds of link are refused.
- Each release lists a SHA-256 checksum, and the README and FAQ explain how to check a download and where to keep the app.
- New `SECURITY.md` explains how to report a security problem privately.

### Fixed

- The Keep/Revert window appeared in the top-left corner when a change made a different display the main screen. It now opens in the middle of the new main screen.
- Restarting or signing out during the Keep/Revert countdown left the new layout in place and marked recovery as failed. A shutdown no longer counts as Revert. The previous layout is restored at the next sign-in. A locked screen delays the restore until you unlock it.
- Turning off the main display left no display marked as main. The current main is kept while it is on; otherwise the first display that is on in the table becomes main.
