# Shows your public IP address as a system tray (notification area) icon.
# - The icon displays the last part of the IP; hover for the full address.
# - Left-click / "Copy IP" copies it to the clipboard.
# - Right-click for Refresh / Exit.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -Namespace Native -Name User32 -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool DestroyIcon(System.IntPtr handle);
'@

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$RefreshMinutes = 5
$Endpoints = @('https://api.ipify.org', 'https://checkip.amazonaws.com', 'https://icanhazip.com')

$script:ip = $null

function Get-PublicIp {
    foreach ($url in $Endpoints) {
        try {
            $r = (Invoke-RestMethod -Uri $url -TimeoutSec 5).ToString().Trim()
            if ($r -match '^[0-9a-fA-F\.:]+$') { return $r }
        } catch { }
    }
    return $null
}

function New-TextIcon([string]$text) {
    $bmp = New-Object System.Drawing.Bitmap 32, 32
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(30, 90, 200))
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $size = if ($text.Length -le 2) { 20 } else { 15 }
    $font = New-Object System.Drawing.Font('Segoe UI', $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString($text, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 0, 32, 32), $fmt)
    $hIcon = $bmp.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($hIcon).Clone()
    [Native.User32]::DestroyIcon($hIcon) | Out-Null
    $g.Dispose(); $font.Dispose(); $bmp.Dispose()
    return $icon
}

function Update-Tray {
    $new = Get-PublicIp
    $script:ip = $new
    if ($new) {
        $label = if ($new -match '\.') { ($new -split '\.')[-1] } else { '6' }
        $tip = "Public IP: $new"
    } else {
        $label = '?'
        $tip = 'Public IP: unavailable (no connection?)'
    }
    $old = $notify.Icon
    $notify.Icon = New-TextIcon $label
    if ($old) { $old.Dispose() }
    $notify.Text = $tip.Substring(0, [Math]::Min($tip.Length, 63))
    $copyItem.Text = if ($new) { "Copy IP ($new)" } else { 'Copy IP' }
    $copyItem.Enabled = [bool]$new
}

function Copy-Ip {
    if ($script:ip) {
        [System.Windows.Forms.Clipboard]::SetText($script:ip)
        $notify.ShowBalloonTip(1500, 'Public IP', "Copied $($script:ip)", 'Info')
    }
}

$notify = New-Object System.Windows.Forms.NotifyIcon
$menu = New-Object System.Windows.Forms.ContextMenuStrip
$copyItem = $menu.Items.Add('Copy IP')
$refreshItem = $menu.Items.Add('Refresh now')
$menu.Items.Add('-') | Out-Null
$exitItem = $menu.Items.Add('Exit')
$notify.ContextMenuStrip = $menu

$copyItem.add_Click({ Copy-Ip })
$refreshItem.add_Click({ Update-Tray })
$exitItem.add_Click({
    $timer.Stop()
    $notify.Visible = $false
    $notify.Dispose()
    [System.Windows.Forms.Application]::Exit()
})
$notify.add_MouseClick({ if ($_.Button -eq 'Left') { Copy-Ip } })

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = $RefreshMinutes * 60 * 1000
$timer.add_Tick({ Update-Tray })

$notify.Visible = $true
Update-Tray
$timer.Start()

[System.Windows.Forms.Application]::Run()
