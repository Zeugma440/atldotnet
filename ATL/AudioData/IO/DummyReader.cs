using System.IO;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Dummy audio data provider
    /// </summary>
    public class DummyReader : IAudioDataIO
    {
        private readonly string filePath;

        /// <summary>
        /// Instanciate a dummy reader
        /// </summary>
        /// <param name="filePath"></param>
        public DummyReader(string filePath)
        {
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Instancing a Dummy Audio Data Reader for " + filePath);
            this.filePath = filePath;
        }
        /// <inheritdoc/>
        public string FileName
        {
            get { return filePath; }
        }
        /// <inheritdoc/>
        public double BitRate
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public double Duration
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public int SampleRate
        {
            get { return 0; }
        }

        /// <inheritdoc/>
        public int BitDepth => -1;

        /// <inheritdoc/>
        public bool IsVBR
        {
            get { return false; }
        }
        /// <inheritdoc/>
        public Format AudioFormat
        {
            get { return Factory.UNKNOWN_FORMAT; }
        }
        /// <inheritdoc/>
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }
        /// <inheritdoc/>
        public long AudioDataSize { get; set; }
        /// <inheritdoc/>
        public ID3v1 ID3v1
        {
            get { return new ID3v1(); }
        }
        /// <inheritdoc/>
        public ID3v2 ID3v2
        {
            get { return new ID3v2(); }
        }
        /// <inheritdoc/>
        public APEtag APEtag
        {
            get { return new APEtag(); }
        }
        /// <inheritdoc/>
        public IMetaDataIO NativeTag
        {
            get { return new DummyTag(); }
        }
        /// <inheritdoc/>
        public ChannelsArrangements.ChannelsArrangement ChannelsArrangement
        {
            get { return ChannelsArrangements.UNKNOWN; }
        }
        /// <inheritdoc/>
        public bool RemoveTagFromFile(MetaDataIOFactory.TagType type)
        {
            return true;
        }
        /// <inheritdoc/>
        public bool AddTagToFile(MetaDataIOFactory.TagType type)
        {
            return true;
        }
        /// <inheritdoc/>
        public bool UpdateTagInFile(TagData theTag, MetaDataIOFactory.TagType type)
        {
            return true;
        }
        /// <inheritdoc/>
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return true;
        }
        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return true;
        }
    }
}
