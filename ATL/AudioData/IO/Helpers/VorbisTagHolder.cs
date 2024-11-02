using System.Collections.Generic;

namespace ATL.AudioData.IO
{
    internal abstract class VorbisTagHolder : MetaDataHolder
    {
        protected readonly VorbisTag vorbisTag;

        protected VorbisTagHolder(bool writePicturesWithMetadata, bool writeMetadataFramingBit, bool hasCoreSignature,
            bool managePadding)
        {
            vorbisTag = new VorbisTag(writePicturesWithMetadata, writeMetadataFramingBit, hasCoreSignature,
                managePadding, tagData);
        }


        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }

        protected virtual byte ratingConvention => MetaDataIO.RC_APE;

        public bool Exists => ((IMetaDataIO)vorbisTag).Exists;

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0])
                {
                    Name = "Native tagging / Vorbis"
                };
                return new List<Format>(new[] { nativeFormat });
            }
        }

        public long PaddingSize => ((IMetaDataIO)vorbisTag).PaddingSize;
        public long Size => ((IMetaDataIO)vorbisTag).Size;
    }
}
