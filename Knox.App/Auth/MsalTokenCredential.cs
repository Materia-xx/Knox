// MSAL-based token provider for the cross-platform (MAUI) app.
//
// Lives in Knox.App (not Knox.Core) on purpose: Knox.Core targets netstandard2.0
// only so the .NET Framework 4.7.2 WPF app can reference it, and we don't want
// MSAL 4.85.2 reaching that app (it keeps its existing ADAL login and pinned
// MSAL 4.30.1). The MAUI app is always .NET, so the MSAL provider belongs here.
//
// It mirrors the shape of KeyVaultTokenCredential (the WPF ADAL one) so it can be
// dropped straight into KnoxVaultClient(Uri, TokenCredential) and the rest of the
// shared flow, but uses MSAL (Microsoft.Identity.Client) instead of ADAL.
using Azure.Core;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Knox
{
    /// <summary>
    /// An <see cref="Azure.Core.TokenCredential"/> backed by MSAL
    /// (Microsoft.Identity.Client) for use by the cross-platform Knox.App.
    ///
    /// Auth flow: try the token cache silently first; if interaction is required,
    /// fall back to an interactive prompt. The platform-specific UI handle
    /// (parent window on Windows, Android Activity on Android) is injected via
    /// the constructor's parentWindowProvider so this class stays platform-agnostic.
    /// </summary>
    public class MsalTokenCredential : TokenCredential
    {
        // Desktop "native client" redirect URI. Works for Windows interactive flows
        // out of the box. Android interactive sign-in additionally requires an
        // msauth://&lt;package&gt;/&lt;hash&gt; redirect registered on the Entra app
        // registration plus AndroidManifest config; pass a different redirectUri
        // and a parentWindowProvider returning the Activity when wiring that up.
        private const string DefaultRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";

        private readonly IPublicClientApplication _app;
        private readonly Func<object?>? _parentWindowProvider;

        /// <param name="clientId">Entra (Azure AD) application (client) id.</param>
        /// <param name="tenantId">Directory (tenant) id to authenticate against.</param>
        /// <param name="parentWindowProvider">
        /// Optional callback returning the platform UI parent for interactive auth
        /// (an <c>Activity</c> on Android, a window handle on Windows). May be null
        /// when only silent (cached) auth is expected.
        /// </param>
        /// <param name="redirectUri">
        /// Optional override for the redirect URI. Defaults to the desktop native
        /// client URI, which is appropriate for the Windows-first scenario.
        /// </param>
        public MsalTokenCredential(string clientId, string tenantId, Func<object?>? parentWindowProvider = null, string? redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentNullException(nameof(clientId));
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _parentWindowProvider = parentWindowProvider;
            _app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .WithRedirectUri(redirectUri ?? DefaultRedirectUri)
                .WithLogging(
                    (level, message, containsPii) => AuthLog.Write($"MSAL[{level}] {message}"),
                    Microsoft.Identity.Client.LogLevel.Verbose,
                    enablePiiLogging: false,
                    enableDefaultPlatformLogging: true)
                .Build();
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var scopes = requestContext.Scopes;
            AuthLog.Write($"GetToken: requesting scopes [{string.Join(", ", scopes ?? Array.Empty<string>())}]");

            AuthenticationResult result;
            try
            {
                // Silent-first: reuse a cached account/token if one is available.
                AuthLog.Write("Silent: getting cached accounts...");
                var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
                AuthLog.Write($"Silent: acquiring token silently (accounts={accounts.Count()})...");
                result = await _app
                    .AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
                AuthLog.Write("Silent: success.");
            }
            catch (MsalUiRequiredException)
            {
                // No usable cached token -> fall back to interactive sign-in.
                //
                // IMPORTANT: marshal the interactive call onto the UI/main thread.
                // Our silent path above uses ConfigureAwait(false), so by here we
                // are on a thread-pool thread. Android's embedded WebView must be
                // driven from the main thread, otherwise the AuthenticationAgent
                // activity opens, closes, and never delivers its result back to
                // MSAL -> the await hangs forever ("Signing in..." with no error).
                AuthLog.Write("Silent: UI required -> starting interactive sign-in.");
                result = await Microsoft.Maui.ApplicationModel.MainThread
                    .InvokeOnMainThreadAsync(() => AcquireInteractiveAsync(scopes ?? Array.Empty<string>(), cancellationToken))
                    .ConfigureAwait(false);
                AuthLog.Write("Interactive: success.");
            }

            AuthLog.Write($"GetToken: token acquired, expires {result.ExpiresOn:u}.");
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }

        // Builds and runs the interactive token request. Always invoke this on the
        // main thread (see GetTokenAsync) so the Android embedded WebView can hook
        // its result callback correctly.
        private Task<AuthenticationResult> AcquireInteractiveAsync(IEnumerable<string> scopes, CancellationToken cancellationToken)
        {
            var builder = _app.AcquireTokenInteractive(scopes);

            // No WithUseEmbeddedWebView(true): on Android we use the default
            // system browser (CustomTabs). The embedded WebView was unreliable --
            // it would complete auth but never hand the redirect/auth code back to
            // MSAL, hanging the await forever. The system browser returns via the
            // msauth://<package>/<cert-hash> redirect (see MsalAndroidRedirect +
            // the BrowserTabActivity intent-filter in AndroidManifest.xml).

            var parent = _parentWindowProvider?.Invoke();
            if (parent is IntPtr hwnd)
            {
                // Windows: MSAL expects the parent window handle (HWND).
                builder = builder.WithParentActivityOrWindow(hwnd);
            }
            else if (parent != null)
            {
                // Android: MSAL expects the current Activity (passed as object).
                builder = builder.WithParentActivityOrWindow(parent);
            }

            AuthLog.Write($"Interactive: launching prompt (parent={(parent == null ? "null" : parent.GetType().Name)})...");
            return builder.ExecuteAsync(cancellationToken);
        }
    }
}
