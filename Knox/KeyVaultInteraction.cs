using Azure.Core;
using Azure.Identity;
using Knox.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Knox
{
    // Note: the registered app should have "Api permissions"
    //    Azure Key Vault - user_impersonation
    //    Azure Service Management - user_impersonation
    //    Microsoft Graph - User.Read
    // Also the App should be set up with a client redirect URI of https://login.microsoftonline.com/common/oauth2/nativeclient

    public class KeyVaultCredential : TokenCredential // I didn't see any TokenCredential in Azure.Identity that got tokens silently/auto like this, so here is a custom one that does it.
    {
        private string ClientId { get; set; }
        private string TenantId { get; set; }

        private static Uri OobUri = new Uri("urn:ietf:wg:oauth:2.0:oob");

        public KeyVaultCredential(string clientId, string tenantId)
        {
            this.ClientId = clientId;
            this.TenantId = tenantId;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{this.TenantId}");
            var authResult = authContext.AcquireTokenAsync("https://vault.azure.net", KnoxSettings.Current.ClientId, OobUri, new PlatformParameters(PromptBehavior.Auto)).Result;
            return new AccessToken(authResult.AccessToken, authResult.ExpiresOn);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var accessToken = GetToken(requestContext, cancellationToken);
            return new ValueTask<AccessToken>(accessToken);
        }
    }

    public static class KeyVaultInteraction
    {
        public static Dictionary<string, KnoxVaultClient> VaultClients { get; set; } = new Dictionary<string, KnoxVaultClient>();
        private static Dictionary<string, TokenCredential> CachedCredGetters { get; set; } = new Dictionary<string, TokenCredential>();
        private static Uri AppRedirectUri = new Uri("https://login.microsoftonline.com/common/oauth2/nativeclient");

        public static void InitClients()
        {
            // If the clientid/tenant id is not set then we can't create any clients yet
            if (string.IsNullOrWhiteSpace(KnoxSettings.Current.ClientId) || string.IsNullOrWhiteSpace(KnoxSettings.Current.TenantId))
            {
                return;
            }

            try
            {
                AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{KnoxSettings.Current.TenantId}");
                var access_token = authContext.AcquireTokenAsync("https://management.azure.com/", KnoxSettings.Current.ClientId, AppRedirectUri, new PlatformParameters(PromptBehavior.Auto)).Result.AccessToken;

                var credGetters = new List<TokenCredential>();
                credGetters.Add(new KeyVaultCredential(KnoxSettings.Current.ClientId, KnoxSettings.Current.TenantId));
                var browserCredentialOptions = new InteractiveBrowserCredentialOptions()
                {
                    ClientId = KnoxSettings.Current.ClientId,
                    TenantId = KnoxSettings.Current.TenantId,
                    RedirectUri = AppRedirectUri
                };
                // I think for the most part this one won't be used, adding it as a fallback just in case
                credGetters.Add(new InteractiveBrowserCredential(browserCredentialOptions));
                var credGetter = new ChainedTokenCredential(credGetters.ToArray());

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"bearer {access_token}");
                var subsJson = client.GetStringAsync("https://management.azure.com/subscriptions?api-version=2014-04-01-preview").Result;
                var subs = JsonConvert.DeserializeObject<SubscriptionsListModel>(subsJson);

                foreach (var sub in subs.value)
                {
                    var keyVaultsJson = client.GetStringAsync($"https://management.azure.com/subscriptions/{sub.subscriptionId}/resources?api-version=2021-04-01&$filter=resourceType eq 'Microsoft.KeyVault/vaults'").Result;
                    var keyVaults = JsonConvert.DeserializeObject<KeyVaultsListModel>(keyVaultsJson);

                    foreach (var keyVault in keyVaults.value)
                    {
                        var vaultName = keyVault.name;
                        var vaultUri = new Uri($"https://{vaultName}.vault.azure.net/");
                        if (!VaultClients.ContainsKey(vaultName))
                        {
                            VaultClients.Add(vaultName, new KnoxVaultClient(vaultUri, credGetter));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
