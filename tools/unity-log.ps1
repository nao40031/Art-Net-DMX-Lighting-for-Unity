param(
    [string]$ProjectPath,
    [ValidateSet("Check", "EditMode", "PlayMode")]
    [string]$Type = "Check",
    [int]$Context = 2
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity-common.ps1")

$project = Get-UnityProjectPath -ProjectPath $ProjectPath
$logDir = Get-UnityAiLogDirectory -ProjectPath $project

switch ($Type) {
    "Check"    { $logFile = Join-Path $logDir "unity-check.log" }
    "EditMode" { $logFile = Join-Path $logDir "unity-test-editmode.log" }
    "PlayMode" { $logFile = Join-Path $logDir "unity-test-playmode.log" }
}

if (!(Test-Path $logFile)) {
    throw "Log not found: $logFile"
}

Write-Host "=== Important Unity Log Lines ==="
Write-Host "Log: $logFile"
Write-Host ""
Write-ImportantUnityLogLines -LogFile $logFile -Context $Context
