# Knox.App dev tools

## run-emulator.ps1 — boot the emulator with working DNS

Visual Studio can't pass custom flags (like `-dns-server`) to the Android
emulator when it deploys a MAUI app, so the emulator it auto-boots may have no
working DNS and report **"You're offline."** Knox needs internet, so boot the
emulator with this script first, then deploy from VS.

### One-time setup: add it as a Visual Studio launch option

Wire the script into VS as a clickable item under the **Tools** menu:

1. In Visual Studio: **Tools → External Tools… → Add**
2. Fill in:
   - **Title:** `Launch Knox Emulator (DNS fix)`
   - **Command:** `powershell.exe`
   - **Arguments:** `-ExecutionPolicy Bypass -File "$(SolutionDir)Knox.App\tools\run-emulator.ps1"`
   - **Initial directory:** `$(SolutionDir)`
   - (optional) check **Use Output window** to see progress inside VS
3. **OK.**

It now appears as **Tools → Launch Knox Emulator (DNS fix)**. (You can also
assign a keyboard shortcut via *Tools → Options → Environment → Keyboard*,
searching for `Tools.ExternalCommand`.)

### Daily workflow

1. Click **Tools → Launch Knox Emulator (DNS fix)** — boots Knox_Pixel with DNS.
   Wait until it reports *booted and ready*. (Do this once per session.)
2. In the VS run dropdown, select the **Knox_Pixel** Android device and press
   **F5**. VS attaches to the already-running emulator and deploys Knox — with
   internet.

### Run it from a terminal instead

```powershell
powershell -ExecutionPolicy Bypass -File .\Knox.App\tools\run-emulator.ps1
```

Optional parameters: `-Avd <name>` and `-Dns <servers>` (defaults:
`Knox_Pixel` and `8.8.8.8,8.8.4.4`).
