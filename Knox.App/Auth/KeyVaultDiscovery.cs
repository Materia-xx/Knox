// Key Vault discovery for the cross-platform (MAUI) app.
//
// Lives in Knox.App (not Knox.Core) because it depends on MsalTokenCredential,
// which is MSAL-based and therefore kept out of the netstandard2.0 Knox.Core
// (consumed by the .NET Framework WPF app). The WPF app is unaffected and keeps
// using KeyVaultInteraction.InitClients (ADAL).
//
// This mirrors the enumeration logic in Knox/KeyVaultInteraction.cs: acquire an
// Azure Resource Manager token, list the caller's subscriptions, then list the
// Key Vaults in each subscription. Models are reused from Knox.Core (Knox.Models).
using Azure.Core;
using Knox.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Knox
{
    public static class KeyVaultDiscovery
    {
        // Azure Resource Manager scope used to enumerate subscriptions and resources.
        private const string ArmScope = "https://management.azure.com/.default";

        /// <summary>
        /// Signs the user in (interactively if needed) via MSAL and returns the
        /// names of every Key Vault the signed-in user can see across all of their
        /// subscriptions.
        /// </summary>
        /// <param name="clientId">Entra application (client) id.</param>
        /// <param name="tenantId">Directory (tenant) id.</param>
        /// <param name="parentWindowProvider">
        /// Platform UI parent for interactive auth (Android Activity, or a Windows
        /// HWND as an IntPtr). May be null when a cached token is expected.
        /// </param>
        /// <param name="redirectUri">
        /// Optional redirect URI override. On Windows pass "http://localhost" to use
        /// the system-browser loopback flow; on Android leave null so MSAL uses its
        /// default msal{ClientId}://auth scheme.
        /// </param>
        public static async Task<List<string>> ListAccessibleVaultsAsync(
            string clientId,
            string tenantId,
            Func<object?>? parentWindowProvider = null,
            string? redirectUri = null,
            CancellationToken cancellationToken = default)
        {
            var credential = new MsalTokenCredential(clientId, tenantId, parentWindowProvider, redirectUri);
            AuthLog.Write("Discovery: acquiring ARM token...");
            var token = await credential
                .GetTokenAsync(new TokenRequestContext(new[] { ArmScope }), cancellationToken)
                .ConfigureAwait(false);
            AuthLog.Write("Discovery: ARM token acquired; listing subscriptions...");

            var vaultNames = new List<string>();

            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

                var subsJson = await http
                    .GetStringAsync("https://management.azure.com/subscriptions?api-version=2020-01-01")
                    .ConfigureAwait(false);
                var subs = JsonConvert.DeserializeObject<SubscriptionsListModel>(subsJson);
                if (subs?.value == null)
                {
                    AuthLog.Write("Discovery: subscriptions response had no 'value'.");
                    return vaultNames;
                }
                AuthLog.Write($"Discovery: {subs.value.Count} subscription(s) returned.");

                foreach (var sub in subs.value)
                {
                    var vaultsUrl =
                        $"https://management.azure.com/subscriptions/{sub.subscriptionId}/resources" +
                        "?api-version=2021-04-01&$filter=resourceType eq 'Microsoft.KeyVault/vaults'";
                    AuthLog.Write($"Discovery: listing vaults in subscription {sub.subscriptionId}...");
                    var vaultsJson = await http.GetStringAsync(vaultsUrl).ConfigureAwait(false);
                    var vaults = JsonConvert.DeserializeObject<KeyVaultsListModel>(vaultsJson);
                    if (vaults?.value == null)
                    {
                        continue;
                    }

                    foreach (var vault in vaults.value)
                    {
                        if (!string.IsNullOrEmpty(vault.name) && !vaultNames.Contains(vault.name))
                        {
                            vaultNames.Add(vault.name);
                        }
                    }
                }
            }

            vaultNames.Sort(StringComparer.OrdinalIgnoreCase);
            AuthLog.Write($"Discovery: done. {vaultNames.Count} vault(s) total.");
            return vaultNames;
        }
    }
}
