using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Dummy metadata provider
    /// </summary>
    public class DummyTag : MetaDataHolder, IMetaDataIO
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public DummyTag()
        {
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Instancing a Dummy Meta Data Reader");
            tagData = new TagData();
        }

        /// <inheritdoc/>
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.ID3V2;
        }

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get { return new List<Format>(new Format[1] { Factory.UNKNOWN_FORMAT }); }
        }

        /// <inheritdoc/>
        public bool Exists
        {
            get { return true; }
        }
        /// <inheritdoc/>
        public long Size
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public long PaddingSize
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public bool Write(Stream s, TagData tag, Action<float> writeProgress = null)
        {
            return true;
        }
        /// <inheritdoc/>
        public Task<bool> WriteAsync(Stream s, TagData tag, IProgress<float> writeProgress = null)
        {
            return Task.FromResult(true);
        }
        /// <inheritdoc/>
        public bool Read(Stream source, MetaDataIO.ReadTagParams readTagParams)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool Remove(Stream s)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<bool> RemoveAsync(Stream s)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
