using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;

namespace Knox
{
    public class KnoxVaultClient
    {
        // KeyVault nuget docs: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Security.KeyVault.Secrets_4.2.0/sdk/keyvault/Azure.Security.KeyVault.Secrets/README.md

        public Dictionary<string, SecretProperties> AllSecretProperties { get; set; } = new Dictionary<string, SecretProperties>(StringComparer.OrdinalIgnoreCase);

        private SecretClient client { get; set; }

        public KnoxVaultClient(Uri uri, TokenCredential credGetter)
        {
            client = new SecretClient(uri, credGetter);
            AllSecretProperties.Clear();
            var allProperties = client.GetPropertiesOfSecrets();
            foreach (var secretProperties in allProperties)
            {
                AllSecretProperties[secretProperties.Name] = secretProperties;
            }
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
