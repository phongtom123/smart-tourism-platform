param(
    [string]$AdbPath = "C:\Android\Sdk\platform-tools\adb.exe",
    [string]$Destination = "$env:USERPROFILE\Downloads",
    [int]$IntervalSeconds = 3,
    [switch]$Once
)

$ErrorActionPreference = "SilentlyContinue"

if (-not (Test-Path -LiteralPath $AdbPath)) {
    Write-Host "ADB not found: $AdbPath"
    exit 1
}

if (-not (Test-Path -LiteralPath $Destination)) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
}

$seen = New-Object "System.Collections.Generic.HashSet[string]"

function Pull-BtReports {
    $remoteFiles = & $AdbPath shell "ls -1 /sdcard/Download/bt-report-*.txt 2>/dev/null"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteFiles)) {
        return
    }

    foreach ($remote in ($remoteFiles -split "`n")) {
        $remote = $remote.Trim()
        if ([string]::IsNullOrWhiteSpace($remote)) {
            continue
        }

        $name = Split-Path -Leaf $remote
        $local = Join-Path $Destination $name
        if ($seen.Contains($remote) -or (Test-Path -LiteralPath $local)) {
            $seen.Add($remote) | Out-Null
            continue
        }

        & $AdbPath pull $remote $local | Out-Null
        if (Test-Path -LiteralPath $local) {
            $seen.Add($remote) | Out-Null
            Write-Host "Pulled $name -> $local"
        }
    }
}

Write-Host "Watching emulator BT reports. Destination: $Destination"
Write-Host "Press Ctrl+C to stop."

do {
    Pull-BtReports
    if ($Once) {
        break
    }
    Start-Sleep -Seconds $IntervalSeconds
} while ($true)
