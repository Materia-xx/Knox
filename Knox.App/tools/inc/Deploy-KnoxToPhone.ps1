<#
.SYNOPSIS
    Builds Knox.App for Android and side-loads it onto the phone over Wireless
    debugging (adb). Implemented as a small step-by-step wizard.

.DESCRIPTION
    This is NOT a top-level entry point. It lives in the inc\ subfolder and defines
    Deploy-KnoxToPhone plus a few small helpers. It is dot-sourced by:

        deploy-to-phone.ps1

    The wizard walks four steps:
      1. Choose the build configuration (Release is the default; Debug also offered).
      2. Connect to the phone. You type the "IP address & Port" shown on the phone's
         Wireless debugging screen ([C] Connect). Pairing (a one-time trust) is only
         needed the first time a computer connects to the phone ([P] Pair).
      3. Confirm the build + target device.
      4. Build and install (MSBuild "Install" target), then optionally launch.

    Wireless facts this relies on:
      - `adb pair <ip:pairPort> <code>` sets a PERSISTENT trust; it's one-time per PC.
      - `adb connect <ip:connectPort>` is per-session; the connect port changes each
        time Wireless debugging is toggled, so you re-read it off the phone screen.

    Signing note: Debug is signed with the debug keystore whose SHA-1 hash is
    registered in AndroidManifest.xml / Entra for the msauth:// redirect. Release is
    signed with a different key, so interactive MSAL sign-in will fail on a Release
    build until that key's hash is also registered.

    The DebugEmulator configuration is intentionally NOT offered here: it bypasses
    the biometric app-lock and is for the emulator only.
#>

$script:KnoxAdb = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"

# Reads a single keypress (no Enter needed), upper-cased. Returns "" for Enter so
# callers can treat Enter as "accept the default".
function Read-KnoxKey {
    param([string]$Prompt)
    Write-Host -NoNewline ($Prompt + " ")
    $key = [System.Console]::ReadKey($true)
    if ($key.Key -eq "Enter") { Write-Host ""; return "" }
    Write-Host $key.KeyChar
    return ([string]$key.KeyChar).ToUpper()
}

# Returns the serials of ready ("device") non-emulator devices currently known to adb.
function Get-KnoxReadyDevices {
    param([string]$Adb = $script:KnoxAdb)
    $ready = @()
    foreach ($line in (& $Adb devices)) {
        $text = $line.ToString().Trim()
        if ($text -eq "" -or $text -like "List of devices attached*") { continue }
        $parts = $text -split "\s+", 2
        $serial = $parts[0]
        $state = if ($parts.Count -gt 1) { $parts[1].Trim() } else { "" }
        if (-not $serial -or $serial -like "emulator-*") { continue }
        if ($state -eq "device") { $ready += $serial }
    }
    return $ready
}

# Connects to a wireless address and reports whether an authorized device resulted.
# Returns an object: Connected (reached the port), Ready (authorized 'device'),
# Address, and Message.
function Connect-KnoxWireless {
    param(
        [Parameter(Mandatory)] [string]$Address,
        [string]$Adb = $script:KnoxAdb
    )
    $out = & $Adb connect $Address 2>&1
    $connected = $out -notmatch "failed|cannot|unable|missing"
    $ready = $false
    if ($connected) {
        # Give adb a moment; an un-paired connection settles to offline/drops.
        Start-Sleep -Milliseconds 800
        $ready = (Get-KnoxReadyDevices -Adb $Adb) -contains $Address
    }
    return [pscustomobject]@{
        Connected = $connected
        Ready     = $ready
        Address   = $Address
        Message   = ($out | Out-String).Trim()
    }
}

# Runs the one-time pairing flow: prompts for the pairing dialog's ip:port + code
# and calls `adb pair`. Returns $true on success. Pairing establishes a permanent
# trust; it does NOT connect (connecting uses the main screen's port afterwards).
function Invoke-KnoxPairing {
    param([string]$Adb = $script:KnoxAdb)
    Write-Host "  On the phone, tap 'Pair device with pairing code' (NOT the main" -ForegroundColor Cyan
    Write-Host "  Wireless debugging line). That dialog shows its own IP:port and a" -ForegroundColor Cyan
    Write-Host "  6-digit code -- both are only valid while the dialog is open." -ForegroundColor Cyan
    $pairAddr = (Read-Host "    Pairing IP:port").Trim()
    $pairCode = (Read-Host "    Pairing code").Trim()
    if (-not $pairAddr -or -not $pairCode) {
        Write-Host "  Pairing needs both an address and a code; skipped." -ForegroundColor Yellow
        return $false
    }
    $pairOut = (& $Adb pair $pairAddr $pairCode 2>&1 | Out-String).Trim()
    if ($pairOut -match "Successfully paired") {
        Write-Host "  Paired successfully -- this is permanent for this computer." -ForegroundColor Green
        Write-Host "  Next, connect using the 'IP address & Port' on the MAIN Wireless" -ForegroundColor Green
        Write-Host "  debugging screen (a different port than the pairing dialog used)." -ForegroundColor Green
        return $true
    }
    Write-Host "  Pairing failed: $pairOut" -ForegroundColor Yellow
    Write-Host "  The code + port expire quickly -- reopen the dialog and try again." -ForegroundColor Yellow
    return $false
}

function Deploy-KnoxToPhone {
    [CmdletBinding()]
    param(
        # Prefill the build configuration (Release default). Debug matches the
        # registered debug-signing hash; Release needs its own hash for MSAL sign-in.
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release",

        # Prefill a wireless address (ip:port) so [C] Connect isn't needed the first pass.
        [string]$WirelessAddress,

        # Launch the app after a successful install.
        [switch]$Launch
    )

    $ErrorActionPreference = "Stop"

    $adb = $script:KnoxAdb
    $jdk = "C:\Program Files\Android\openjdk\jdk-21.0.8"
    if (Test-Path $jdk) { $env:JAVA_HOME = $jdk }
    if (-not (Test-Path $adb)) {
        throw "adb not found at '$adb'. Install platform-tools via Android SDK Manager."
    }

    $projectDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)  # ...\Knox.App
    $project = Join-Path $projectDir "Knox.App.csproj"
    $appId = "com.materia.knox"
    $framework = "net10.0-android"
    if (-not (Test-Path $project)) {
        throw "Knox.App.csproj not found at '$project'."
    }

    & $adb start-server 2>&1 | Out-Null

    Write-Host ""
    Write-Host "============= Knox.App  ->  phone deploy =============" -ForegroundColor Cyan

    # ---- Step 1: build configuration --------------------------------------
    Write-Host ""
    Write-Host "Step 1/4  Build configuration" -ForegroundColor White
    Write-Host "  [1] Debug     (signed with the debug key registered for MSAL sign-in)"
    Write-Host "  [2] Release   (default; different signing key -- MSAL sign-in needs its"
    Write-Host "                 hash registered in Entra, otherwise sign-in will fail)"
    $k = Read-KnoxKey "  Choose [1/2, Enter = Release]:"
    switch ($k) {
        "1"     { $Configuration = "Debug" }
        "2"     { $Configuration = "Release" }
        ""      { $Configuration = "Release" }
        default { Write-Host "  Unrecognized; keeping $Configuration." -ForegroundColor Yellow }
    }
    Write-Host "  -> Using $Configuration." -ForegroundColor Green

    # ---- Step 2: connect to the phone -------------------------------------
    Write-Host ""
    Write-Host "Step 2/4  Connect to the phone (Wireless debugging)" -ForegroundColor White
    Write-Host "  On the phone: Settings -> Developer options -> Wireless debugging = ON,"
    Write-Host "  and leave that screen open. That screen shows two things you'll use:"
    Write-Host "    - 'IP address & Port' (e.g. 192.168.1.50:37123) -- this is what you connect to."
    Write-Host "    - a 'Paired devices' list -- you should see THIS computer's name in it."
    Write-Host "      If this computer is NOT listed there, the phone isn't paired with it yet;"
    Write-Host "      use [P] Pair below. If it IS listed, you're paired -- just [C] Connect."
    Write-Host ""
    Write-Host "  Note: the IP:port on that screen changes every time Wireless debugging is"
    Write-Host "  toggled, but pairing is permanent, so you normally only pair once per PC."

    $target = $null
    while (-not $target) {
        # (a) Already-connected device from a previous run / earlier loop?
        $ready = Get-KnoxReadyDevices -Adb $adb
        if ($ready.Count -eq 1) {
            $target = $ready[0]
            Write-Host ""
            Write-Host "  Connected: $target" -ForegroundColor Green
            break
        }
        elseif ($ready.Count -gt 1) {
            Write-Host ""
            Write-Host "  Multiple devices are connected:" -ForegroundColor Yellow
            for ($i = 0; $i -lt $ready.Count; $i++) { Write-Host ("    [{0}] {1}" -f ($i + 1), $ready[$i]) }
            $sel = Read-KnoxKey "  Pick one [number], or [Q]uit:"
            if ($sel -eq "Q") { Write-Host "  Aborted; nothing deployed." -ForegroundColor Yellow; return }
            elseif ($sel -match "^\d$" -and [int]$sel -ge 1 -and [int]$sel -le $ready.Count) {
                $target = $ready[[int]$sel - 1]; break
            }
            else { Write-Host "  Invalid choice." -ForegroundColor Yellow; continue }
        }

        # (b) A prefilled -WirelessAddress is tried once, automatically.
        if ($WirelessAddress) {
            $addr = $WirelessAddress
            $WirelessAddress = ""
            Write-Host ""
            Write-Host "  Connecting to $addr ..." -ForegroundColor Cyan
            $c = Connect-KnoxWireless -Address $addr -Adb $adb
            if ($c.Ready) {
                $target = $addr
                Write-Host "  Connected: $target" -ForegroundColor Green
                break
            }
            elseif ($c.Connected) {
                Write-Host "  Reached $addr but adb isn't authorized -- pair first with [P]." -ForegroundColor Yellow
            }
            else {
                Write-Host "  Couldn't reach $addr. ($($c.Message))" -ForegroundColor Yellow
            }
        }

        # (c) Menu: connect by IP:port (the reliable path) or pair (one-time).
        Write-Host ""
        Write-Host "    [C] Connect by typing the IP:port shown on the phone"
        Write-Host "    [P] Pair with this computer  (only if it's not in the phone's Paired devices list)"
        Write-Host "    [Q] Quit"
        $sel = Read-KnoxKey "  Choose:"
        switch ($sel) {
            "C" {
                $manual = (Read-Host "    Enter IP:port from the Wireless debugging screen").Trim()
                if ($manual -notmatch "^\d{1,3}(\.\d{1,3}){3}:\d+$") {
                    Write-Host "  That doesn't look like ip:port (e.g. 192.168.1.50:37123)." -ForegroundColor Yellow
                }
                else {
                    Write-Host "  Connecting to $manual ..." -ForegroundColor Cyan
                    $c = Connect-KnoxWireless -Address $manual -Adb $adb
                    if ($c.Ready) {
                        $target = $manual
                        Write-Host "  Connected: $target" -ForegroundColor Green
                    }
                    elseif ($c.Connected) {
                        Write-Host "  Reached $manual but adb isn't authorized -- pair this computer" -ForegroundColor Yellow
                        Write-Host "  first with [P], then connect again." -ForegroundColor Yellow
                    }
                    else {
                        Write-Host "  Couldn't reach $manual. ($($c.Message))" -ForegroundColor Yellow
                        Write-Host "  Re-check the IP:port on the phone -- the port changes when" -ForegroundColor Yellow
                        Write-Host "  Wireless debugging is toggled." -ForegroundColor Yellow
                    }
                }
            }
            "P"     { [void](Invoke-KnoxPairing -Adb $adb) }   # loop shows the menu again afterwards
            "Q"     { Write-Host "  Aborted; nothing deployed." -ForegroundColor Yellow; return }
            default { Write-Host "  Unrecognized choice." -ForegroundColor Yellow }
        }
    }

    # ---- Step 3: confirm ---------------------------------------------------
    Write-Host ""
    Write-Host "Step 3/4  Confirm" -ForegroundColor White
    Write-Host ("  Build         : {0}" -f $Configuration)
    Write-Host ("  Target device : {0}" -f $target)
    Write-Host ("  Launch after  : {0}" -f $(if ($Launch) { "Yes" } else { "No" }))
    $go = Read-KnoxKey "  Deploy now? [Y/N]:"
    if ($go -ne "Y") { Write-Host "  Cancelled; nothing deployed." -ForegroundColor Yellow; return }

    # ---- Step 4: build + install ------------------------------------------
    Write-Host ""
    Write-Host "Step 4/4  Building and installing ($Configuration -> $target)..." -ForegroundColor Cyan

    # -t:Install builds the APK and installs it on the device named by AdbTarget.
    # AdbTarget is passed straight to adb, so "-s <serial>" pins the exact device.
    dotnet build $project `
        -f $framework `
        -c $Configuration `
        -t:Install `
        -p:AdbTarget="-s $target"

    if ($LASTEXITCODE -ne 0) {
        throw "Build/install failed (exit code $LASTEXITCODE)."
    }
    Write-Host "Install succeeded on '$target'." -ForegroundColor Green

    if ($Launch) {
        Write-Host "Launching $appId..." -ForegroundColor Cyan
        & $adb -s $target shell monkey -p $appId -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
    }
}
