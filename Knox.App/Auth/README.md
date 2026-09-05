# Knox.App authentication notes (read before touching auth)

Cross-platform MSAL auth for the MAUI app. These notes exist because several of
the Android requirements below are individually easy to miss and each one fails
the **exact same silent way**: the app sits on **"Signing in…" forever, with no
exception and no error in the logs**. Diagnosing that from scratch is painful,
so preserve all of it.

## Design model (why it's built this way)

Knox is **generic and not multi-tenant**. There is no central app registration,
no admin consent, and no invitation flow. Each end user creates **their own
single-tenant Entra app registration** and enters its client id + tenant id into
Knox at runtime. Nothing about a specific client id is baked into the build.

Consequences:
- We cannot hardcode a client id anywhere (manifest, code, config).
- The Android redirect URI must therefore be keyed to something generic. We use
  `msauth://<package>/<base64(SHA-1(signing cert))>`, which depends only on the
  **Knox app's package name + signing certificate**, never the client id. The
  same redirect works for every registration the user wires up; they just add
  that one redirect URI to each of their own registrations.

## Android: the four things that must all be present

If interactive sign-in hangs on "Signing in…", one of these is missing/wrong.

1. **System browser, NOT embedded WebView.**
   `MsalTokenCredential.AcquireInteractiveAsync` must NOT call
   `WithUseEmbeddedWebView(true)`. The embedded WebView completes auth but never
   hands the auth code back to MSAL → infinite hang. (Also Microsoft discourages
   embedded WebView.) See `MsalTokenCredential.cs`.

2. **`<queries>` block in `AndroidManifest.xml`** (Android 11+ / API 30+).
   Package-visibility filtering hides other apps' services by default. Without
   the `<queries>` entry for the browser VIEW intent + `CustomTabsService`, MSAL
   can't find a Custom-Tabs browser, logs *"Browser with custom tabs package not
   available. Launching with alternate browser."*, falls back to a plain full
   browser, and that browser's redirect does NOT return the auth code → hang.
   Refs: https://aka.ms/msal-net-system-browsers and
   https://developer.android.com/training/package-visibility

3. **`BrowserTabActivity` redirect entry in `AndroidManifest.xml`.**
   `<data android:scheme="msauth" android:host="<package>" android:path="/<hash>" />`.
   The `path` is the **DEBUG** signing-cert hash today. `MsalAndroidRedirect`
   computes the correct value at runtime (and logs it as
   `KNOXAUTH: Android redirect URI ...`) and passes it to MSAL; the manifest
   entry is static and must match. **A release-signed build has a different hash
   — update the manifest path AND register the new redirect in Entra.**

4. **`MainActivity.OnActivityResult` must forward to MSAL.**
   MSAL launches its `AuthenticationActivity` via `StartActivityForResult` from
   `MainActivity`; MAUI does NOT override `OnActivityResult` for you. Without the
   forward to
   `AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(...)`,
   the result is dropped and the awaiting `AcquireTokenInteractive` never
   completes → hang. See `Platforms/Android/MainActivity.cs`.

## End-user setup (per registration)

1. Create a single-tenant Entra app registration (public client).
2. Authentication → Add a platform → **Android**:
   - Package name: `com.materia.knox`
   - Signature hash: the Base64 SHA-1 shown in the app's log / error message
     (debug default: `i1RyQCxx81KoXGthqy+pYMvLqjs=`).
3. Enter that client id + tenant id in Knox and sign in.

## Debugging / testing gotchas

- **Don't test under the VS debugger for the browser round-trip.** VS
  force-stops the app (`am force-stop`) while it's backgrounded behind the
  browser, which cold-starts a new process on redirect and loses MSAL's
  in-memory request. Use **Start Without Debugging** (Ctrl+F5) or launch via the
  icon / `adb shell monkey -p com.materia.knox -c android.intent.category.LAUNCHER 1`.
- Emulator scripts + a `-WipeData` full reset live in `../tools/`.
- Auth trace tag is `KNOXAUTH` (see `AuthLog.cs`). Success path logs
  `Interactive: success` then `GetToken: token acquired`.

## App-lock (biometric / device credential)

Knox gates entry behind the device lock (bank-app style), controlled by
`RequireBiometricUnlock` in settings. Implementation lives in
`Services/BiometricService.cs`:

- **Windows:** `Windows.Security.Credentials.UI.UserConsentVerifier` (Microsoft,
  in-SDK). Unpackaged desktop can throw → caught, treated as *Unavailable*.
- **Android:** in-SDK `KeyguardManager.CreateConfirmDeviceCredentialIntent`
  (`Platforms/Android/AndroidDeviceCredential.cs`). We **deliberately do NOT use
  AndroidX `BiometricPrompt`** — `androidx.biometric` is a Google/Jetpack package,
  not Microsoft-authored, which violates the Knox Microsoft-only dependency
  policy. The system credential screen still offers **fingerprint when enrolled**,
  falling back to PIN/pattern/password, so the UX is close to a dedicated
  biometric prompt without the extra dependency.
  - Like MSAL, the confirm intent is launched via `StartActivityForResult`, so
    `MainActivity.OnActivityResult` must forward the result to
    `AndroidDeviceCredential.HandleResult(...)` — otherwise the awaiting unlock
    task never completes. This forward sits right next to the MSAL one.
  - If the device has **no secure lock screen**, `BiometricService` returns
    *Unavailable* and the lock gate fails open (you can't require a credential the
    device doesn't have).

Lock orchestration (show on launch/resume, relock on background, idle timeout)
lives in `Services/LockController.cs` + `App.xaml.cs`.

## Token flow (not a place that needs extra code)

We use the public-client **authorization code flow + PKCE** (same as a SPA).
MSAL does the code→token exchange internally after `OnActivityResult` delivers
the code. We request the target scope directly (e.g.
`https://management.azure.com/.default` for ARM), so no id-token→access-token
/ on-behalf-of conversion is needed here — that's a confidential-client/backend
concern, not ours.
