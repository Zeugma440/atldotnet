using Commons;
using System.Collections.Generic;
using System.IO;
using System;

namespace ATL.AudioData.IO
{
    public class TagHolder : MetadataHolder
    {
        private MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY;
        private byte m_ratingConvention = MetaDataIO.RC_ID3v2;

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

        /*protected override byte ratingConvention
        {
            get { return m_ratingConvention; }
        }

        public void setRatingConvention(byte value)
        {
            m_ratingConvention = value;
        }
        */
    }
}
