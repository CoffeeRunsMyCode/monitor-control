# Security policy

## Reporting a problem

Please report security problems privately, not in a public issue. On GitHub, open the repository's **Security** tab and choose **Report a vulnerability**, or use [this form](https://github.com/coffeerunsmycode/monitor-control/security/advisories/new). Only the maintainer can see the report.

Include the app version (Help window > sidebar header), your Windows version, and the steps to reproduce. Don't attach `settings.json`, `activity.log`, or diagnostic files without removing monitor serial numbers first.

You should get a reply within 14 days. Fixes go into the next release, and the report is credited unless you ask otherwise.

## Supported versions

Only the latest release gets fixes.

## What the app can and can't do

This helps you judge whether something is a security problem:

- It runs with your normal user rights and never asks for administrator access.
- It has no network code. The only web pages it opens are https links you click in the Help window.
- It writes only to `%LOCALAPPDATA%\MonitorControl` and, while a layout change is waiting for **Keep changes**, to one `RunOnce` value under `HKEY_CURRENT_USER` that restores your layout at the next sign-in.
- Releases are not code-signed. Check the SHA-256 checksum in the release notes before you run a download (see the README).
