# Public IP Tray

A tiny Windows app that shows your current public IP address in the taskbar
notification area (system tray).

## Use

Double-click `PublicIpTray.exe`. It needs .NET Framework 4, which is built in to Windows 10/11.

(Alternative, no exe: put `PublicIpTray.ps1` and `PublicIpTray.vbs` in one folder and double-click the `.vbs`.)

Rebuild the exe from `src/PublicIpTray.cs` with:
`mcs -target:winexe -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:PublicIpTray.exe src/PublicIpTray.cs`
(or `csc` on Windows, from `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`).

The icon shows the last number of your IP. Hover for the full address,
left-click to copy it, right-click for **Refresh now** / **Exit**. It refreshes
every 5 minutes (`$RefreshMinutes` in the script).

Windows may hide new tray icons under the `^` arrow. Drag the icon onto the
taskbar to keep it visible.

## Start with Windows

Press `Win+R`, run `shell:startup`, and put a shortcut to `PublicIpTray.exe` there.

## Notes

- Only one instance runs at a time.
- The IP is fetched from api.ipify.org, falling back to checkip.amazonaws.com and icanhazip.com.
