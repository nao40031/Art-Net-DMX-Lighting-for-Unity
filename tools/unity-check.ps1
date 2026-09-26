param(
    [string]$ProjectPath,
    [string]$UnityPath,
    [switch]$AllowOpenEditor
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity-common.ps1")

$project = Get-UnityProjectPath -ProjectPath $ProjectPath
$unity = Get-UnityExecutable -UnityPath $UnityPath
Assert-UnityProjectNotOpen -ProjectPath $project -AllowOpenEditor:$AllowOpenEditor

$logDir = Get-UnityAiLogDirectory -ProjectPath $project
$logFile = Join-Path $logDir "unity-check.log"
Remove-Item $logFile -Force -ErrorAction SilentlyContinue

Write-Host "=== Unity Compile Check ==="
Write-Host "Project: $project"
Write-Host "Unity:   $unity"
Write-Host ""

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-accept-apiupdate",
    "-projectPath", $project,
    "-logFile", $logFile
)

& $unity @unityArgs
$exitCode = $LASTEXITCODE

$compileErrors = @()
if (Test-Path $logFile) {
    $compileErrors = Select-String -Path $logFile -Pattern @(
        'error CS\d{4}',
        'Scripts have compiler errors',
        'Compilation failed'
    ) -CaseSensitive:$false
}

if ($compileErrors.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Compiler errors ==="
    $compileErrors | Select-Object -ExpandProperty Line | Select-Object -Unique | ForEach-Object { Write-Host $_ }
    Write-Host ""
    Write-Host "Unity check: FAILED"
    Write-Host "Log: $logFile"
    exit 1
}

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "Unity exited with code: $exitCode"
    Write-ImportantUnityLogLines -LogFile $logFile
    Write-Host "Log: $logFile"
    exit $exitCode
}

Write-Host ""
Write-Host "No C# compiler errors detected."
Write-Host "Unity check: PASSED"
Write-Host "Log: $logFile"
exit 0
