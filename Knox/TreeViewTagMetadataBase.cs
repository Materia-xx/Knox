namespace Knox
{
    public class TreeViewTagMetadataBase
    {
        public TreeViewTagMetadataTagType TagType { get; set; }

        public TreeViewTagMetadataBase(TreeViewTagMetadataTagType tagType)
        {
            this.TagType = tagType;
        }
    }
}
