<#
.SYNOPSIS
    Shared implementation that launches the Knox_Pixel Android emulator.

.DESCRIPTION
    This is NOT a top-level entry point. It lives in the inc\ subfolder to keep
    it separate from the scripts meant to be run directly. It defines the
    Start-KnoxEmulator function and is dot-sourced by the two entry scripts in
    the parent tools\ folder:

        run-emulator-google-dns.ps1     -> forces public Google DNS (8.8.8.8)
        run-emulator-inherited-dns.ps1  -> inherits the host's DNS

    Visual Studio cannot pass custom flags (such as -dns-server) to the Android
    emulator when it deploys a MAUI app, so boot the emulator with one of the
    entry scripts first, then deploy/debug from Visual Studio (F5) onto the
    already-running emulator.

    Why two DNS modes:
      - Inherited DNS (preferred): run with NordVPN off so the emulator inherits
        the host resolver. This gives reliable DNS and is how sign-in is expected
        to work.
      - Google DNS (fallback): forces 8.8.8.8. Only partially works -- DNS
        resolution is intermittently flaky (queries time out in bursts), which
        can make sign-in hang. Not preferred.

    IMPORTANT: The emulator captures its network/DNS configuration at launch.
    Toggling the VPN on/off after the emulator is already running has NO effect
    on the running guest. Always fully stop the emulator and cold-boot it with
    the appropriate entry script after changing your VPN state.
#>

function Start-KnoxEmulator {
    [CmdletBinding()]
    param(
        # Name of the AVD to launch.
        [string]$Avd = "Knox_Pixel",

        # DNS server(s) to pass via -dns-server. Pass $null or empty to inherit
        # the host's DNS (no -dns-server flag).
        [string]$Dns,

        # When set, factory-wipe the AVD's user data on this cold boot
        # (-wipe-data). This resets all remembered state, including sign-in
        # cookies and the MSAL token cache. A wipe only takes effect at boot.
        [switch]$WipeData
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
        Write-Host "Skipping launch. (Stop it first if you want a clean DNS boot or a -WipeData wipe.)"
        return
    }

    # Build the emulator argument list. -no-snapshot-load forces a cold boot so
    # the chosen DNS mode actually takes effect (snapshots restore old network
    # state). Only add -dns-server when a DNS value was supplied.
    $args = @('-avd', $Avd, '-no-snapshot-load')
    if ($WipeData) {
        $args += '-wipe-data'
        Write-Host "Factory-wiping AVD user data on this boot (-wipe-data)..." -ForegroundColor Magenta
    }
    if (-not [string]::IsNullOrWhiteSpace($Dns)) {
        $args += @('-dns-server', $Dns)
        Write-Host "Launching AVD '$Avd' with DNS override '$Dns'..." -ForegroundColor Cyan
    }
    else {
        Write-Host "Launching AVD '$Avd' with inherited (host) DNS..." -ForegroundColor Cyan
    }

    Start-Process -FilePath $emulator -ArgumentList $args

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
}
