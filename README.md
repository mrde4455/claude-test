# Public IP Tray

A tiny Windows app that shows your current public IP address in the taskbar
notification area (system tray).

## Use

1. Download `PublicIpTray.ps1` and `PublicIpTray.vbs` into the same folder.
2. Double-click `PublicIpTray.vbs` (runs with no console window).

The icon shows the last number of your IP. Hover for the full address,
left-click to copy it, right-click for **Refresh now** / **Exit**. It refreshes
every 5 minutes (`$RefreshMinutes` in the script).

Windows may hide new tray icons under the `^` arrow. Drag the icon onto the
taskbar to keep it visible.

## Start with Windows

Press `Win+R`, run `shell:startup`, and put a shortcut to `PublicIpTray.vbs` there.

## Notes

- Requires Windows PowerShell 5.1 (built in to Windows 10/11). No install or compile step.
- The IP is fetched from api.ipify.org, falling back to checkip.amazonaws.com and icanhazip.com.
