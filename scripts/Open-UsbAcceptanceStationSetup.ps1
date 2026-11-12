$ErrorActionPreference = 'Stop'
$webRoot = Split-Path -Parent $PSScriptRoot
$embeddedRoot = 'C:\Users\proxi\source\repos\DMOo25-Team1-AuktionsHuset\Auktionshuset_Embedded'
$python = Join-Path $env:LOCALAPPDATA 'Programs\Python\Python312\python.exe'
$ca = Join-Path $env:LOCALAPPDATA 'AuktionshusetUsbAcceptance\tls\ca.pem'

Write-Host 'Indtast Wi-Fi-adgangskoden i dette vindue. Den vises ikke og sendes ikke til chatten.'
& $python (Join-Path $webRoot 'scripts\Initialize-UsbAcceptanceStation.py') `
    --embedded-root $embeddedRoot `
    --backend-host '192.168.1.199' `
    --ca-pem $ca `
    --wifi-ssid 'Zyxel_EC1F 2.4' `
    --usb-serial '5C93049695'

if ($LASTEXITCODE -ne 0) {
    Write-Host 'Stationsopsætningen blev ikke gemt. Ingen devicekonfiguration er ændret.'
} else {
    Write-Host 'Stationen er konfigureret. Luk vinduet efter at have set resultatet.'
}
Read-Host 'Tryk Enter for at lukke dette vindue' | Out-Null
