# Screenshots

Images used in the user guide and README. `build.ps1` builds every `.png` here into the exe, so the guide in the Help window shows them too. Rebuild after you change one.

- `main-window.png`: the main window.
- `help-window.png`: the Help & about window.

## How to make the images

Run these commands from the repository root. Build output goes to `dist\`, which Git ignores.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
$out = Join-Path $env:TEMP 'mc-render'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Start-Process -FilePath .\dist\MonitorControl.exe -ArgumentList @('--render-demo', ('"' + (Join-Path $out 'main-window.png') + '"')) -Wait
```

`--render-demo` shows three made-up monitors (Generic 24in, Generic 27in, Generic TV, named Left, Center, and TV) instead of the monitors on your PC, so the images contain no real model names or serial numbers. It never changes a display.

It writes these images into `$out`:

- `main-window.png`: copy it here as `main-window.png`.
- `main-window.help.png`: copy it here as `help-window.png`.
- `main-window.edited.png`: the window after a simulated drag. Not used in the docs.
- `main-window.help-1.png` and up: every Help page, plus `-image`, `-table`, and `-narrow` views. Use them to check the layout after you edit a guide. Not used in the docs.

It also writes a `qa-profile` folder next to the images, so render into a folder outside the repository.

Note: the preset drop-down list appears empty in the image. In the app it says **Choose a preset…**.

## Before you add an image

Check each image before you add it:

- No serial numbers or other hardware identifiers appear.
- No personal monitor names appear.
- No brand names of the author's own hardware appear.

Plain `--render` uses your real monitors. Don't use its images here.
