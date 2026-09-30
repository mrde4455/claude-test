# Public IP Tray

A tiny Windows app that shows the country flag of your current public IP address
in the taskbar notification area (system tray). Hover over it to see the IP and country.

## Use

Double-click `PublicIpTray.exe`. It needs .NET Framework 4, which is built in to Windows 10/11.

(Alternative, no exe: put `PublicIpTray.ps1` and `PublicIpTray.vbs` in one folder and double-click the `.vbs`.)

Rebuild the exe with `./build.sh` (needs Mono's `mcs`); it compiles `src/PublicIpTray.cs`
and embeds the flag images from `assets/flags`.

The icon is the flag of the country your IP is in. Hover for the full address,
left-click to copy it, right-click for **Refresh now** / **Exit**. It refreshes
every 5 minutes (`RefreshMinutes` in the source).

Windows may hide new tray icons under the `^` arrow. Drag the icon onto the
taskbar to keep it visible.

## Start with Windows

Press `Win+R`, run `shell:startup`, and put a shortcut to `PublicIpTray.exe` there.

## Notes

- Only one instance runs at a time.
- If a country has no flag image, the icon shows its two-letter code instead.
- Flag images are from the public-domain famfamfam set.
- The IP is fetched from api.ipify.org, falling back to checkip.amazonaws.com and icanhazip.com; the country from ipapi.co, ipinfo.io or api.country.is.
