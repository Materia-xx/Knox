<#
.SYNOPSIS
    Launches the Knox_Pixel Android emulator using the host's inherited DNS.

.DESCRIPTION
    The supported emulator launcher. Run with NordVPN OFF so the emulator
    inherits working host DNS. With NordVPN enabled, authentication hosts such
    as login.microsoftonline.com may not resolve.

    Cold-boot only: fully stop any running emulator first, then run this. The
    emulator captures network state at launch, so toggling the VPN after boot
    has no effect on the running guest.

    Use -WipeData to factory-wipe the AVD on this boot. This clears application
    data, browser state, and the MSAL token cache.

    DEBUGGING LIMITATION:
    Visual Studio debugging can terminate a .NET MAUI Android app shortly after
    it loses focus. For example, Knox can be force-stopped a few seconds after
    MSAL opens Chrome Custom Tabs for authentication. This is tracked by:

      dotnet/maui#20980
      https://github.com/dotnet/maui/issues/20980

    To build, deploy, and test the newest app without attaching the debugger,
    use Visual Studio's Start Without Debugging (Ctrl+F5). Alternatively, deploy
    with Visual Studio, stop debugging, and relaunch Knox from the emulator's
    app launcher before testing authentication.
#>
[CmdletBinding()]
param(
    [string]$Avd = "Knox_Pixel",
    [switch]$WipeData
)

. (Join-Path $PSScriptRoot "inc\Start-KnoxEmulator.ps1")
Start-KnoxEmulator -Avd $Avd -WipeData:$WipeData
