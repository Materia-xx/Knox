using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Knox
{
    public static class KeyVaultInteraction
    {
        public static Dictionary<string, KnoxVaultClient> VaultClients { get; set; } = new Dictionary<string, KnoxVaultClient>();
        private static Dictionary<string, TokenCredential> CachedCredGetters { get; set; } = new Dictionary<string, TokenCredential>();

        public static void InitClients()
        {
            // TODO: Make it so the program looks for this json in the standard user data folder for the app
            var configFilePath = @"D:\a\Data\Knox\Starspark.json";
            var fileContents = File.ReadAllText(configFilePath);
            var vaultRegistrations = JsonConvert.DeserializeObject<List<VaultRegistration>>(fileContents);

            foreach (var registration in vaultRegistrations)
            {
                string credGetterKey = $"{registration.ClientId}.{registration.TenantId}";
                if (!CachedCredGetters.ContainsKey(credGetterKey))
                {
                    var browserCredentialOptions = new InteractiveBrowserCredentialOptions()
                    {
                        ClientId = registration.ClientId,
                        TenantId = registration.TenantId,
                        RedirectUri = new Uri(registration.RedirectUri) // Todo: can this just be hardcoded?
                    };
                    CachedCredGetters[credGetterKey] = new InteractiveBrowserCredential(browserCredentialOptions);
                }

                foreach (var vaultName in registration.VaultNames)
                {
                    var vaultUri = new Uri($"https://{vaultName}.vault.azure.net/");

                    if (!VaultClients.ContainsKey(vaultName))
                    {
                        VaultClients.Add(vaultName, new KnoxVaultClient(vaultUri, CachedCredGetters[credGetterKey]));
                    }
                }
            }
        }
    }
}
