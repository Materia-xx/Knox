using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Knox
{
    public class KnoxVaultClient
    {
        // KeyVault nuget docs: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Security.KeyVault.Secrets_4.2.0/sdk/keyvault/Azure.Security.KeyVault.Secrets/README.md

        public Dictionary<string, SecretProperties> AllSecretProperties { get; set; } = new Dictionary<string, SecretProperties>(StringComparer.OrdinalIgnoreCase);

        private SecretClient client { get; set; }

        public KnoxVaultClient(Uri uri, TokenCredential credGetter)
        {
            // Key Vault throttles list operations (HTTP 429) and enumerating a vault
            // with many secrets makes several paged calls. The default SDK retry
            // policy (a few short retries) can be exhausted under sustained
            // throttling, which surfaced as "some secrets are missing" -- the
            // enumeration threw part-way and only partial data was kept. Give the
            // SDK a more patient, exponential retry budget for those transient 429s.
            var options = new SecretClientOptions
            {
                Retry =
                {
                    MaxRetries = 5,
                    Mode = RetryMode.Exponential,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(16),
                },
            };

            client = new SecretClient(uri, credGetter, options);
            LoadAllSecretProperties();
        }

        /// <summary>
        /// (Re)loads every secret's properties for the vault. Builds into a temporary
        /// map and only publishes it after the FULL enumeration succeeds, so a failure
        /// part-way through can never leave a partially-populated cache (the bug where
        /// secrets past a certain point were silently missing). If the enumeration
        /// throws (e.g. throttling that outlasts the per-request retries), the whole
        /// enumeration is retried from scratch a few times before giving up.
        /// </summary>
        public void LoadAllSecretProperties()
        {
            const int maxAttempts = 3;
            Exception lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var loaded = new Dictionary<string, SecretProperties>(StringComparer.OrdinalIgnoreCase);
                    foreach (var secretProperties in client.GetPropertiesOfSecrets())
                    {
                        loaded[secretProperties.Name] = secretProperties;
                    }

                    // Full enumeration succeeded -- publish atomically.
                    AllSecretProperties = loaded;
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    // Transient (throttling / network). Back off, then re-enumerate
                    // from the start; partial results are discarded, not exposed.
                    lastError = ex;
                    Thread.Sleep(TimeSpan.FromSeconds(attempt * 2));
                }
            }

            // Ran out of attempts; propagate so the caller can report/skip this vault
            // rather than silently browsing an incomplete secret list.
            throw new KnoxVaultLoadException(
                $"Failed to list all secrets for '{client.VaultUri}' after {maxAttempts} attempts.", lastError);
        }


        public KeyVaultSecret GetSecret(string secretName)
        {
            return client.GetSecret(secretName).Value;
        }

        private SecretProperties GetSecretPropertiesWithVersion(string secretName)
        {
            // If the cached version of the properties doesn't contain a version, then update it so it does
            // This will happen when the form first loads and it gets all properties, but that call will not include the version for some odd reason.
            if (AllSecretProperties[secretName].Version == null)
            {
                var currentSecret = GetSecret(secretName);
                AllSecretProperties[secretName] = currentSecret.Properties;
            }
            return AllSecretProperties[secretName];
        }

        public KeyVaultSecret CreateSecret(string secretName, string password)
        {
            var secret = new KeyVaultSecret(secretName, password);
            var addedSecret = client.SetSecret(secret).Value;
            AllSecretProperties[secretName] = addedSecret.Properties;
            return addedSecret;
        }

        public KeyVaultSecret CloneTo(KeyVaultSecret fromSecret, string secretName)
        {
            var toSecret = CreateSecret(secretName, fromSecret.Value);
            CopySecretProperties(fromSecret, toSecret);
            AllSecretProperties[secretName] = toSecret.Properties;
            return toSecret;
        }

        public void DeleteSecret(string secretName)
        {
            client.StartDeleteSecret(secretName);
            AllSecretProperties.Remove(secretName);

            // This program doesn't try to do purging. Head on over to the Azure portal to do that.
        }

        private void CopySecretProperties(KeyVaultSecret fromSecret, KeyVaultSecret toSecret)
        {
            // Copy old version tags to the new version
            foreach (var tagKvP in fromSecret.Properties.Tags)
            {
                toSecret.Properties.Tags[tagKvP.Key] = tagKvP.Value;
            }
            toSecret.Properties.ContentType = fromSecret.Properties.ContentType;
            toSecret.Properties.Enabled = fromSecret.Properties.Enabled;

            // Update the new version with the cloned tags
            client.UpdateSecretProperties(toSecret.Properties);

            // There is more to copy, but this program doesn't make use of it (activation/expire dates, etc)
        }

        public void UpdateSecret(string secretName, bool updatePassword, string newPassword, List<SecretTag> tags)
        {
            if (updatePassword)
            {
                // A password update is really a new version with everything cloned
                var oldVersion = GetSecret(secretName);
                var newVersion = client.SetSecret(secretName, newPassword).Value;
                CopySecretProperties(oldVersion, newVersion);
            }

            // Now do any additional tag updates that were passed in
            var secret = GetSecret(secretName);
            secret.Properties.Tags.Clear();
            foreach (var tag in tags)
            {
                secret.Properties.Tags.Add(new KeyValuePair<string, string>(tag.Name, tag.Value));
            }

            // There seems to be a bug in the KeyVault lib where if there are 0 tags it doesn't update the secret to also have 0 tags
            // but if there is just 1 tag, it will update the secret to just have that 1 tag.
            // for this reason, when deleting tags, make sure to still have at-least 1 tag in the collection
            // In this program I'm using "Folder" as the 1 tag that is always there.
            var newVersionSecretProperties = client.UpdateSecretProperties(secret.Properties).Value;

            // Update the local properties cache
            AllSecretProperties[secretName] = newVersionSecretProperties;
        }
    }
}
