<#
.SYNOPSIS
    Launches the Knox_Pixel Android emulator with a working DNS server.

.DESCRIPTION
    Visual Studio cannot pass custom flags (such as -dns-server) to the Android
    emulator when it deploys a MAUI app, so the emulator it boots can end up with
    no working DNS ("You're offline"). Knox needs internet, so use this script to
    boot the emulator with explicit public DNS servers first, then deploy/debug
    from Visual Studio (F5) onto the already-running emulator.

    Wire it into Visual Studio via Tools > External Tools (see tools/README.md).

.PARAMETER Avd
    Name of the AVD to launch. Defaults to Knox_Pixel.

.PARAMETER Dns
    DNS server(s) to use. Defaults to Google public DNS.
#>
[CmdletBinding()]
param(
    [string]$Avd = "Knox_Pixel",
    [string]$Dns = "8.8.8.8,8.8.4.4"
)

$ErrorActionPreference = "Stop"

$sdk = "C:\Program Files (x86)\Android\android-sdk"
$emulator = Join-Path $sdk "emulator\emulator.exe"
$adb = Join-Path $sdk "platform-tools\adb.exe"
$jdk = "C:\Program Files\Android\openjdk\jdk-21.0.8"

if (Test-Path $jdk) { $env:JAVA_HOME = $jdk }

if (-not (Test-Path $emulator)) {
    throw "Emulator not found at '$emulator'. Install it via Android SDK Manager."
}

# If an emulator is already running, don't start a second one.
$running = & $adb devices | Select-String "emulator-\d+\s+device"
if ($running) {
    Write-Host "An emulator is already running:" -ForegroundColor Yellow
    & $adb devices
    Write-Host "Skipping launch. (Stop it first if you want a clean DNS boot.)"
    return
}

Write-Host "Launching AVD '$Avd' with DNS '$Dns'..." -ForegroundColor Cyan
Start-Process -FilePath $emulator `
    -ArgumentList '-avd', $Avd, '-dns-server', $Dns, '-no-snapshot-load'

Write-Host "Waiting for device to connect..."
& $adb wait-for-device

Write-Host "Waiting for full boot..."
for ($i = 0; $i -lt 90; $i++) {
    $booted = (& $adb shell getprop sys.boot_completed 2>$null)
    if ("$booted".Trim() -eq "1") {
        Write-Host "Emulator '$Avd' is booted and ready." -ForegroundColor Green
        & $adb devices
        return
    }
    Start-Sleep -Seconds 5
}

Write-Warning "Emulator did not report boot_completed in time; check the emulator window."
