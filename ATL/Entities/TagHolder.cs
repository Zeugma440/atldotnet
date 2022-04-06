
namespace ATL.AudioData.IO
{
    public class TagHolder : MetadataHolder
    {
        private MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY;

        public TagHolder()
        {
            tagData = new TagData();
        }

        public TagHolder(TagData tagData)
        {
            this.tagData = new TagData(tagData);
        }

        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return tagType;
        }
    }
}
