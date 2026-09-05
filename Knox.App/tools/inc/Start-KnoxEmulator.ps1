<#
.SYNOPSIS
    Shared implementation that launches the Knox_Pixel Android emulator.

.DESCRIPTION
    This is not a top-level entry point. It defines Start-KnoxEmulator and is
    dot-sourced by run-emulator-inherited-dns.ps1.

    Visual Studio cannot pass the required networking choices when it starts an
    Android emulator, so launch the emulator with the entry script first and
    then select the already-running device in Visual Studio.

    NordVPN must be off before starting the emulator. With NordVPN enabled, the
    inherited resolver can prevent authentication hosts such as
    login.microsoftonline.com from resolving. The emulator captures its DNS
    configuration at launch, so changing VPN state afterward does not repair the
    running guest; fully stop it and cold-boot again.
#>

function Start-KnoxEmulator {
    [CmdletBinding()]
    param(
        [string]$Avd = "Knox_Pixel",

        # Factory-wipe user data on this cold boot. This clears browser state,
        # application data, and the MSAL token cache.
        [switch]$WipeData
    )

    $ErrorActionPreference = "Stop"

    Write-Host ""
    Write-Host "IMPORTANT: NordVPN must be OFF before launching this emulator." -ForegroundColor Yellow
    Write-Host "When NordVPN is enabled, authentication sites such as login.microsoftonline.com may not resolve." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "DEBUGGING NOTE: Visual Studio can force-stop the app after it loses focus," -ForegroundColor Yellow
    Write-Host "for example when MSAL opens Chrome for authentication." -ForegroundColor Yellow
    Write-Host "Use Start Without Debugging (Ctrl+F5) to build, deploy, and test the newest app." -ForegroundColor Yellow
    Write-Host "Tracked by dotnet/maui#20980: https://github.com/dotnet/maui/issues/20980" -ForegroundColor DarkGray
    Write-Host ""

    $sdk = "C:\Program Files (x86)\Android\android-sdk"
    $emulator = Join-Path $sdk "emulator\emulator.exe"
    $adb = Join-Path $sdk "platform-tools\adb.exe"
    $jdk = "C:\Program Files\Android\openjdk\jdk-21.0.8"

    if (Test-Path $jdk) {
        $env:JAVA_HOME = $jdk
    }

    if (-not (Test-Path $emulator)) {
        throw "Emulator not found at '$emulator'. Install it through Android SDK Manager."
    }
    if (-not (Test-Path $adb)) {
        throw "adb not found at '$adb'. Install Android platform-tools."
    }

    # Do not start a second emulator. DNS and wipe choices only apply at boot.
    $running = & $adb devices | Select-String "emulator-\d+\s+device"
    if ($running) {
        Write-Host "An emulator is already running:" -ForegroundColor Yellow
        & $adb devices
        Write-Host "Stop it first to change DNS mode or wipe its data."
        return
    }

    # Cold boot so the host's current VPN-off DNS configuration is inherited.
    $emulatorArgs = @("-avd", $Avd, "-no-snapshot-load")
    if ($WipeData) {
        $emulatorArgs += "-wipe-data"
        Write-Host "Factory-wiping AVD user data on this boot..." -ForegroundColor Magenta
    }
    Write-Host "Launching AVD '$Avd' with inherited host DNS..." -ForegroundColor Cyan

    Start-Process -FilePath $emulator -ArgumentList $emulatorArgs

    Write-Host "Waiting for the emulator to connect..."
    & $adb wait-for-device

    Write-Host "Waiting for full boot..."
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        $booted = & $adb shell getprop sys.boot_completed 2>$null
        if ("$booted".Trim() -eq "1") {
            Write-Host "Emulator '$Avd' is ready." -ForegroundColor Green
            & $adb devices
            return
        }

        Start-Sleep -Seconds 5
    }

    Write-Warning "The emulator did not report boot completion in time."
}
