using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Dummy audio data provider
    /// </summary>
    public class DummyReader : IAudioDataIO
    {
        /// <summary>
        /// Instanciate a dummy reader
        /// </summary>
        /// <param name="filePath"></param>
        public DummyReader(string filePath)
        {
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Instancing a Dummy Audio Data Reader for " + filePath);
            FileName = filePath;
        }
        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public double BitRate => 0;

        /// <inheritdoc/>
        public double Duration => 0;

        /// <inheritdoc/>
        public int SampleRate => 0;

        /// <inheritdoc/>
        public int BitDepth => -1;

        /// <inheritdoc/>
        public bool IsVBR => false;

        /// <inheritdoc/>
        public AudioFormat AudioFormat => new AudioFormat(Format.UNKNOWN_FORMAT);

        /// <inheritdoc/>
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;

        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }
        /// <inheritdoc/>
        public long AudioDataSize { get; set; }

        /// <inheritdoc/>
        public ChannelsArrangements.ChannelsArrangement ChannelsArrangement => ChannelsArrangements.UNKNOWN;

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;

        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return true;
        }
    }
}
