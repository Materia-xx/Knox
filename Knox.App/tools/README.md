# Running Knox.App on Android

## Emulator

> **NordVPN must be off before starting the emulator.** When it is enabled,
> inherited DNS can prevent authentication sites such as
> `login.microsoftonline.com` from resolving.

Fully stop any running emulator, disable NordVPN, then run:

```powershell
.\run-emulator-inherited-dns.ps1
```

This is the only supported emulator launcher. It inherits the host's DNS and
accepts `-Avd Knox_Pixel` and `-WipeData`. The emulator captures DNS when it
starts, so toggling NordVPN afterward has no effect; stop it completely and
cold-boot it again after changing VPN state.

`-WipeData` factory-wipes the AVD at boot, clearing browser state, application
data, and the MSAL token cache. You must redeploy Knox afterward.

After the emulator reports ready, select the already-running device in Visual
Studio and deploy Knox.

Interactive system-browser authentication works in the deployed app, but a
known Visual Studio/.NET MAUI Android debugger issue can terminate the app a few
seconds after Chrome Custom Tabs sends it to the background. Test the browser
round trip with **Start Without Debugging** (`Ctrl+F5`). This is tracked by
[dotnet/maui#20980](https://github.com/dotnet/maui/issues/20980).

For Android MSAL configuration and troubleshooting, see
[`../Auth/README.md`](../Auth/README.md).

## Physical phone

Enable wireless debugging on the phone, then run:

```powershell
.\deploy-to-phone.ps1
```

The wizard supports pairing, connecting, choosing an authorized physical
device, selecting Debug or Release, building, installing, and optional launching
with `-Launch`.

Pairing is normally required only once per computer. Connecting uses the
`IP address & Port` shown on the main Wireless debugging screen; this port can
change whenever wireless debugging is restarted.

The current Entra Android redirect is registered for this computer's Debug
keystore. A Release build or a Debug build produced on another computer has a
different signing hash that must also be registered in Entra.

On Samsung phones, Auto Blocker under **Settings > Security and privacy** must be
off or it can silently block installation.
