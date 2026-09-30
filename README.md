# Public IP Tray

A tiny Windows app that shows the country flag of your current public IP address
in the taskbar notification area (system tray). Hover over it to see the IP and country.

## Use

**Install:** run `PublicIpTray-Setup.exe`. It installs for your user only (no admin rights), adds a Start Menu shortcut, and by default starts the app automatically when you sign in to Windows. Uninstall from Settings > Apps.

**Portable:** or just double-click `PublicIpTray.exe`. It needs .NET Framework 4, which is built in to Windows 10/11.

Rebuild the exe and installer with `./build.sh` (needs Mono's `mcs` and `makensis`); it compiles `src/PublicIpTray.cs`
and embeds the flag images from `assets/flags`.

The icon is the flag of the country your IP is in. Hover for the full address,
left-click to copy it, right-click for **Refresh now** / **Exit**. It refreshes
every 5 seconds (`RefreshSeconds` in the source).

Windows may hide new tray icons under the `^` arrow. Drag the icon onto the
taskbar to keep it visible.

## Start with Windows

Tick/untick **Start with Windows** in the installer, or toggle it any time from the tray icon's right-click menu.

## Notes

- When your IP changes, a small popup appears in the bottom-right corner with the old and new address and country (click it to dismiss, or it closes after 8 seconds). It stays silent on startup and while you're offline. Use **Test notification** in the tray menu to preview it.
- The country is only looked up again when your IP changes, to stay within the free limits of the lookup services.
- Only one instance runs at a time.
- If a country has no flag image, the icon shows its two-letter code instead.
- Flag images are from the public-domain famfamfam set.
- The IP is fetched from api.ipify.org, falling back to checkip.amazonaws.com and icanhazip.com; the country from ipapi.co, ipinfo.io or api.country.is.
