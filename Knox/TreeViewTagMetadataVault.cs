namespace Knox
{
    public class TreeViewTagMetadataVault : TreeViewTagMetadataBase
    {
        public string VaultName { get; set; }

        public TreeViewTagMetadataVault(string vaultName) : base(TreeViewTagMetadataTagType.Vault)
        {
            this.VaultName = vaultName;
        }
    }
}
