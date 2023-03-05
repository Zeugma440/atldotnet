using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for DSD Stream File files manipulation (extension : .DSF)
    /// </summary>
	class DSF : IAudioDataIO, IMetaDataEmbedder
    {
        // Headers ID
        public const string DSD_ID = "DSD ";
        public const string FMT_ID = "fmt ";
        public const string DATA_ID = "data";


        // Private declarations 
        private ChannelsArrangement channelsArrangement;
        private uint bits;
        private uint sampleRate;

        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private long id3v2Offset;
        private readonly FileStructureHelper id3v2StructureHelper = new FileStructureHelper();


        // Public declarations 
        public double CompressionRatio
        {
            get { return getCompressionRatio(); }
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate
        {
            get { return (int)sampleRate; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSLESS; }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }

        public int BitDepth => (int)bits;

        public double Duration
        {
            get { return duration; }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return metaDataType == MetaDataIOFactory.TagType.ID3V2;
        }

        // IMetaDataEmbedder
        public long HasEmbeddedID3v2
        {
            get { return id3v2Offset; }
        }
        public uint ID3v2EmbeddingHeaderSize
        {
            get { return 0; }
        }
        public FileStructureHelper.Zone Id3v2Zone
        {
            get { return id3v2StructureHelper.GetZone(FileStructureHelper.DEFAULT_ZONE_NAME); }
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bits = 0;
            sampleRate = 0;
            duration = 0;
            bitrate = 0;
            isValid = false;
            id3v2Offset = -1;
            id3v2StructureHelper.Clear();
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public DSF(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        // Get compression ratio 
        private double getCompressionRatio()
        {
            if (isValid)
                return (double)sizeInfo.FileSize / ((duration / 1000.0 * sampleRate) * (channelsArrangement.NbChannels * bits / 8) + 44) * 100;
            else
                return 0;
        }

        /// <inheritdoc/>
        public bool Read(Stream source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;
            bool result = false;
            byte[] buffer = new byte[8];

            resetData();

            source.Seek(0, SeekOrigin.Begin);
            source.Read(buffer, 0, 4);
            if (DSD_ID.Equals(Utils.Latin1Encoding.GetString(buffer, 0, 4)))
            {
                source.Seek(16, SeekOrigin.Current); // Chunk size and file size
                source.Read(buffer, 0, 8);
                id3v2Offset = StreamUtils.DecodeInt64(buffer);

                source.Read(buffer, 0, 4);
                if (FMT_ID.Equals(Utils.Latin1Encoding.GetString(buffer, 0, 4)))
                {
                    source.Seek(8, SeekOrigin.Current); // Chunk size

                    source.Read(buffer, 0, 4);
                    int formatVersion = StreamUtils.DecodeInt32(buffer);

                    if (formatVersion > 1)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported");
                        return result;
                    }

                    isValid = true;

                    source.Seek(8, SeekOrigin.Current); // Format ID (4), Channel type (4)

                    source.Read(buffer, 0, 4);
                    uint channels = StreamUtils.DecodeUInt32(buffer);
                    switch (channels)
                    {
                        case 1: channelsArrangement = MONO; break;
                        case 2: channelsArrangement = STEREO; break;
                        case 3: channelsArrangement = ISO_3_0_0; break;
                        case 4: channelsArrangement = QUAD; break;
                        case 5: channelsArrangement = LRCLFE; break;
                        case 6: channelsArrangement = ISO_3_2_0; break;
                        case 7: channelsArrangement = ISO_3_2_1; break;
                        default: channelsArrangement = UNKNOWN; break;
                    }

                    source.Read(buffer, 0, 4);
                    sampleRate = StreamUtils.DecodeUInt32(buffer);
                    source.Read(buffer, 0, 4);
                    bits = StreamUtils.DecodeUInt32(buffer);

                    source.Read(buffer, 0, 8);
                    ulong sampleCount = StreamUtils.DecodeUInt64(buffer);

                    duration = sampleCount * 1000.0 / sampleRate;
                    bitrate = Math.Round(((double)(sizeInfo.FileSize - source.Position)) * 8 / duration); //time to calculate average bitrate

                    AudioDataOffset = source.Position + 8;
                    if (id3v2Offset > 0)
                        AudioDataSize = id3v2Offset - AudioDataOffset;
                    else
                        AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

                    result = true;
                }

                // Load tag if exists
                if (id3v2Offset > 0)
                {
                    if (readTagParams.PrepareForWriting)
                    {
                        id3v2StructureHelper.AddZone(id3v2Offset, (int)(source.Length - id3v2Offset));
                        id3v2StructureHelper.AddSize(12, source.Length);
                        id3v2StructureHelper.AddIndex(20, id3v2Offset);
                    }
                }
                else
                {
                    id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                    if (readTagParams.PrepareForWriting)
                    {
                        // Add EOF zone for future tag writing
                        id3v2StructureHelper.AddZone(source.Length, 0);
                        id3v2StructureHelper.AddSize(12, source.Length);
                        id3v2StructureHelper.AddIndex(20, source.Length);
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            // Nothing to do here; DSF format defines no frame header for its embedded ID3v2 tag
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            // Nothing to do here
        }
    }
}