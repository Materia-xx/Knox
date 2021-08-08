namespace Knox
{
    public class TreeViewTagMetadataSecret : TreeViewTagMetadataBase
    {
        public string VaultName { get; set; }

        public string SecretName { get; set; }

        public TreeViewTagMetadataSecret(string vaultName, string secretName) : base(TreeViewTagMetadataTagType.Secret)
        {
            this.VaultName = vaultName;
            this.SecretName = secretName;
        }
    }
}
