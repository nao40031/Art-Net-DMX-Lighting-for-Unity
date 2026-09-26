param(
    [string]$ProjectPath,
    [string]$UnityPath,
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Mode = "EditMode",
    [string]$TestFilter,
    [switch]$AllowOpenEditor
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity-common.ps1")

$project = Get-UnityProjectPath -ProjectPath $ProjectPath
$unity = Get-UnityExecutable -UnityPath $UnityPath
Assert-UnityProjectNotOpen -ProjectPath $project -AllowOpenEditor:$AllowOpenEditor

$logDir = Get-UnityAiLogDirectory -ProjectPath $project
$logFile = Join-Path $logDir ("unity-test-{0}.log" -f $Mode.ToLowerInvariant())
$resultFile = Join-Path $logDir ("unity-test-{0}.xml" -f $Mode.ToLowerInvariant())
Remove-Item $logFile, $resultFile -Force -ErrorAction SilentlyContinue

$unityArgs = @(
    "-batchmode",
    "-projectPath", $project,
    "-runTests",
    "-testPlatform", $Mode,
    "-testResults", $resultFile,
    "-logFile", $logFile
)

if (![string]::IsNullOrWhiteSpace($TestFilter)) {
    $unityArgs += @("-testFilter", $TestFilter)
}

Write-Host "=== Unity $Mode Tests ==="
Write-Host "Project: $project"
Write-Host "Unity:   $unity"
if ($TestFilter) { Write-Host "Filter:  $TestFilter" }
Write-Host ""

& $unity @unityArgs
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "Unity tests: FAILED (exit code $exitCode)"
    Write-ImportantUnityLogLines -LogFile $logFile
    Write-Host "Results: $resultFile"
    Write-Host "Log:     $logFile"
    exit $exitCode
}

Write-Host ""
Write-Host "Unity tests: PASSED"
Write-Host "Results: $resultFile"
Write-Host "Log:     $logFile"
exit 0
