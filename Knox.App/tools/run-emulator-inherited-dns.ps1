<#
.SYNOPSIS
    Launches the Knox_Pixel Android emulator using the host's inherited DNS.

.DESCRIPTION
    Preferred way to run the emulator. Run this with NordVPN OFF: by not
    overriding the DNS, the emulator inherits the host resolver, which gives
    reliable DNS resolution and is how sign-in is expected to work.

    (The alternative, run-emulator-google-dns.ps1, forces 8.8.8.8 but is
    intermittently flaky and is not preferred.)

    Cold-boot only: fully stop any running emulator first, then run this. The
    emulator captures network state at launch, so toggling the VPN after boot
    has no effect on the running guest.

    Use -WipeData to factory-wipe the AVD's user data on this boot (clears all
    remembered state, including sign-in cookies and the MSAL token cache).
#>
[CmdletBinding()]
param(
    [string]$Avd = "Knox_Pixel",
    [switch]$WipeData
)

. (Join-Path $PSScriptRoot "inc\Start-KnoxEmulator.ps1")
Start-KnoxEmulator -Avd $Avd -WipeData:$WipeData
