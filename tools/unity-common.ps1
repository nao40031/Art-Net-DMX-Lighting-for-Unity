param()

function Get-UnityProjectPath {
    param([string]$ProjectPath)

    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    } else {
        $ProjectPath = (Resolve-Path $ProjectPath).Path
    }

    if (!(Test-Path (Join-Path $ProjectPath "Assets")) -or
        !(Test-Path (Join-Path $ProjectPath "Packages")) -or
        !(Test-Path (Join-Path $ProjectPath "ProjectSettings"))) {
        throw "Not a Unity project root: $ProjectPath"
    }

    return $ProjectPath
}

function Get-UnityExecutable {
    param([string]$UnityPath)

    if (![string]::IsNullOrWhiteSpace($UnityPath)) {
        if (!(Test-Path $UnityPath)) {
            throw "Unity executable not found: $UnityPath"
        }
        return (Resolve-Path $UnityPath).Path
    }

    $command = Get-Command Unity.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Unity.exe was not found in PATH. Pass -UnityPath explicitly or add the Unity Editor folder to PATH."
}

function Assert-UnityProjectNotOpen {
    param(
        [string]$ProjectPath,
        [switch]$AllowOpenEditor
    )

    if ($AllowOpenEditor) { return }

    $lockFile = Join-Path $ProjectPath "Temp\UnityLockfile"
    if (Test-Path $lockFile) {
        throw "Unity appears to have this project open: $ProjectPath. Close the Editor and retry. If the lock file is stale, remove Temp\UnityLockfile manually."
    }
}

function Get-UnityAiLogDirectory {
    param([string]$ProjectPath)

    $dir = Join-Path $ProjectPath "Logs\AI"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    return $dir
}

function Write-ImportantUnityLogLines {
    param(
        [string]$LogFile,
        [int]$Context = 1
    )

    if (!(Test-Path $LogFile)) {
        Write-Host "Unity log was not created: $LogFile"
        return
    }

    $patterns = @(
        'error CS\d{4}',
        'Scripts have compiler errors',
        'Compilation failed',
        'Unhandled Exception',
        'Exception:',
        'Assertion failed',
        'FAIL'
    )

    $matches = Select-String -Path $LogFile -Pattern $patterns -CaseSensitive:$false -Context $Context

    if ($matches) {
        foreach ($match in $matches) {
            if ($match.Context.PreContext) {
                $match.Context.PreContext | ForEach-Object { Write-Host $_ }
            }
            Write-Host $match.Line
            if ($match.Context.PostContext) {
                $match.Context.PostContext | ForEach-Object { Write-Host $_ }
            }
            Write-Host "---"
        }
    }
}
