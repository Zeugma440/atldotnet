using Commons;
using System.Collections.Generic;
using System.IO;
using System;

namespace ATL.AudioData.IO
{
    internal abstract class VorbisTagHolder : MetaDataHolder, IMetaData
    {
        protected VorbisTag vorbisTag;


        protected void createVorbisTag(bool writePicturesWithMetadata, bool writeMetadataFramingBit, bool hasCoreSignature, bool managePadding)
        {
            vorbisTag = new VorbisTag(writePicturesWithMetadata, writeMetadataFramingBit, hasCoreSignature, managePadding);
            tagData = vorbisTag.tagData;
        }

        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }

        protected virtual byte ratingConvention
        {
            get { return MetaDataIO.RC_APE; }
        }

        /// <inheritdoc/>
        public bool Exists
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Exists;
            }
        }
        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0]);
                nativeFormat.Name = "Native / Vorbis";
                return new List<Format>(new Format[1] { nativeFormat });
            }
        }
        /// <inheritdoc/>
        public long PaddingSize
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PaddingSize;
            }
        }
        /// <inheritdoc/>
        public long Size
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Size;
            }
        }
    }
}
