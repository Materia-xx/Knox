namespace Knox
{
    public class TreeViewTagMetadataVirtualFolder : TreeViewTagMetadataBase
    {
        public string VaultName { get; set; }

        public string FolderName { get; set; }

        public TreeViewTagMetadataVirtualFolder(string vaultName, string folderName) : base(TreeViewTagMetadataTagType.VirtualFolder)
        {
            this.VaultName = vaultName;
            this.FolderName = folderName;
        }
    }
}
