$ErrorActionPreference = "Stop"

$policyName = "SectorFlow LMU"

Get-NetQosPolicy -Name $policyName -ErrorAction SilentlyContinue |
    Remove-NetQosPolicy -Confirm:$false

Write-Host "SectorFlow LMU QoS policy removed." -ForegroundColor Green
Read-Host "Press Enter to close"
