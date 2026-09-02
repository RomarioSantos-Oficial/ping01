$ErrorActionPreference = "Stop"

$policyName = "SectorFlow LMU"

Write-Host "Installing Windows QoS policy for Le Mans Ultimate..." -ForegroundColor Cyan

Get-NetQosPolicy -Name $policyName -ErrorAction SilentlyContinue |
    Remove-NetQosPolicy -Confirm:$false -ErrorAction SilentlyContinue

New-NetQosPolicy -Name $policyName `
    -AppPathNameMatchCondition "Le Mans Ultimate.exe" `
    -DSCPAction 46 | Out-Null

Write-Host ""
Write-Host "QoS policy installed." -ForegroundColor Green
Write-Host "This prioritizes LMU locally when Windows/network equipment honors DSCP."
Write-Host "It does NOT change Starlink's BGP/international route."
Write-Host ""
Read-Host "Press Enter to close"
