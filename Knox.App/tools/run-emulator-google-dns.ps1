<#
.SYNOPSIS
    Launches the Knox_Pixel Android emulator using public Google DNS (8.8.8.8).

.DESCRIPTION
    Fallback only. Forces the emulator to resolve DNS via Google's public
    resolvers. This only partially works: DNS resolution is intermittently flaky
    (queries time out in bursts), which can make sign-in hang.

    Prefer run-emulator-inherited-dns.ps1 (run with NordVPN off) instead.

    Cold-boot only: fully stop any running emulator first, then run this.

    Use -WipeData to factory-wipe the AVD's user data on this boot (clears all
    remembered state, including sign-in cookies and the MSAL token cache).
#>
[CmdletBinding()]
param(
    [string]$Avd = "Knox_Pixel",
    [switch]$WipeData
)

. (Join-Path $PSScriptRoot "inc\Start-KnoxEmulator.ps1")
Start-KnoxEmulator -Avd $Avd -Dns "8.8.8.8,8.8.4.4" -WipeData:$WipeData
