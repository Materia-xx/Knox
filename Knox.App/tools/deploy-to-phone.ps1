<#
.SYNOPSIS
    Builds Knox.App for Android and side-loads it onto the phone over Wireless
    debugging (adb). A small step-by-step wizard.

.DESCRIPTION
    No cable needed -- deploys over Wi-Fi with no Visual Studio deploy step. Just
    run it and follow the prompts:

        .\deploy-to-phone.ps1

    The wizard walks four steps:
      1. Choose the build configuration (Release default; Debug also offered).
      2. Connect to the phone. On the phone, turn ON Settings -> Developer options
         -> Wireless debugging and leave that screen open. Read its "IP address &
         Port" and type it in ([C] Connect). The first time a computer connects it
         also needs a one-time pairing ([P] Pair -> tap "Pair device with pairing
         code" on the phone for its own ip:port + 6-digit code).
      3. Confirm the build + target device.
      4. Build, install, and optionally launch.

    Optional params below just prefill the wizard:
      .\deploy-to-phone.ps1 -Configuration Debug          # preselect the build
      .\deploy-to-phone.ps1 -WirelessAddress <ip:port>    # try this address first
      .\deploy-to-phone.ps1 -Launch                       # launch after install

    Signing note: Debug uses the debug key whose hash is registered in
    AndroidManifest.xml / Entra for the msauth:// redirect, so interactive MSAL
    sign-in works. Release is signed with a different key and needs its own hash
    registered, or sign-in will fail.

    DebugEmulator (which bypasses the biometric app-lock) is intentionally not
    offered here -- it's for the emulator only.
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$WirelessAddress,
    [switch]$Launch
)

. (Join-Path $PSScriptRoot "inc\Deploy-KnoxToPhone.ps1")
Deploy-KnoxToPhone -Configuration $Configuration -WirelessAddress $WirelessAddress -Launch:$Launch
