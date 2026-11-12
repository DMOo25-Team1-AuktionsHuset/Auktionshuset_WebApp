$ErrorActionPreference = 'Stop'
$name = 'Auktionshuset USB accept backend'
if (-not (Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $name `
        -Direction Inbound -Action Allow -Protocol TCP `
        -LocalAddress '192.168.1.199' -LocalPort 55134 `
        -RemoteAddress '192.168.1.0/24' -Profile Public `
        -Description 'Temporary TLS access from the local ESP32 during USB acceptance' | Out-Null
}
Write-Host 'Den midlertidige, afgrænsede firewallregel er klar.'
Read-Host 'Tryk Enter for at lukke dette vindue' | Out-Null
