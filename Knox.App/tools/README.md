# Running Knox.App in an emulator in Windows

> Auth not working / stuck on "Signing in…"? See **`../Auth/README.md`** for the
> Android MSAL requirements and debugging gotchas (incl. why not to test the
> browser round-trip under the VS debugger).

1. Run the emulator: From a powershell run one of these, and wait for the
   emulator to fully boot up:

   - `run-emulator-inherited-dns.ps1` — **preferred.** Inherits the host's DNS.
     Run this with **NordVPN off**; it gives reliable DNS and is the way sign-in
     is expected to work.
   - `run-emulator-google-dns.ps1` — fallback only. Forces public Google DNS
     (8.8.8.8). This only partially works: DNS resolution is intermittently flaky
     (queries time out in bursts), which can make sign-in hang. Not preferred.

   Both share `inc\Start-KnoxEmulator.ps1` and take an optional `-Avd <name>`
   (default `Knox_Pixel`). Cold-boot only: fully stop any running emulator first,
   and re-pick the script after changing your VPN state (the emulator captures
   its DNS at launch, so toggling the VPN on a running emulator has no effect).

2. In the VS run dropdown, select the **Knox_Pixel** Android device and press
   **F5**. VS attaches to the already-running emulator and deploys Knox.

Notes: I was unable to find a way to get first class integration with 
Visual Studio to do all the steps needed. When I let Visual Studio launch 
the emulator it would boot up in a state where DNS resolution was broken.

## Clearing remembered sign-in state

The emulator keeps its disk between restarts, so the app's WebView cookies and
MSAL token cache (the remembered account) survive an emulator restart. To force
a clean, fresh sign-in, cold-boot the emulator with the `-WipeData` switch on
either run-emulator entry script:

- `run-emulator-inherited-dns.ps1 -WipeData` (preferred, NordVPN off)
- `run-emulator-google-dns.ps1 -WipeData` (fallback)

`-WipeData` factory-wipes the AVD's user data (`-wipe-data`) on boot, removing
all remembered state including the WebView cookies and MSAL token cache. The
emulator must be stopped first, and you'll need to redeploy Knox (F5) afterwards.
This is the only supported reset — there is no partial app-data clear.

# Sideloading Knox.App to my Android phone

Deploys a Debug build straight to a physical phone over wireless ADB (no USB
cable needed). The phone needs Android 11+ and must be on the same network as
the PC, and must already be paired (see "Pairing my Android phone" below).

1. Connect to the phone: Using the connection IP:port shown on the Wireless
   debugging screen (this is a different port than the pairing one, and it
   changes every session), run `adb connect 192.168.x.x:<connectPort>`. Confirm
   it shows up with `adb devices`.

2. Deploy and launch: Set `$env:JAVA_HOME` to the OpenJDK path, then run

```powershell
$env:JAVA_HOME = "C:\Program Files\Android\openjdk\jdk-21.0.8"

dotnet build Knox.App\Knox.App.csproj -t:Run `
  -f net10.0-android `
  -p:RuntimeIdentifier=android-arm64 `
  -p:AdbTarget="-s 192.168.x.x:<connectPort>"
```

`-t:Run` builds, installs, and launches the app; `android-arm64` matches modern
phones (the emulator uses `x86_64`); `AdbTarget` pins the deploy to the phone so
it doesn't go to a running emulator.

Notes: On Samsung phones, Auto Blocker (Settings → Security and privacy) must be
off or it silently blocks the install.

# Pairing my Android phone

This only needs to be done once. The pairing persists across sessions and
reboots, and only needs redoing if the phone forgets the PC (e.g. a factory
reset or removing the paired device).

On the phone open the Wireless debugging pairing dialog to get a 6-digit code
and a pairing IP:port, then from a powershell run
`adb pair 192.168.x.x:<pairPort> <code>`.
