using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using System.Collections.Generic;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for DSD Stream File files manipulation (extension : .DSF)
    /// </summary>
	class DSF : IAudioDataIO, IMetaDataEmbedder
    {
        // Headers ID
        private static readonly byte[] DSD_ID = Utils.Latin1Encoding.GetBytes("DSD ");
        private static readonly byte[] FMT_ID = Utils.Latin1Encoding.GetBytes("fmt ");


        // Private declarations 
        private ChannelsArrangement channelsArrangement;
        private uint bits;
        private uint sampleRate;

        private bool isValid;

        private SizeInfo sizeInfo;

        private long id3v2Offset;
        private readonly FileStructureHelper id3v2StructureHelper = new FileStructureHelper();


        // Public declarations 
        public double CompressionRatio => getCompressionRatio();


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate => (int)sampleRate;
        public bool IsVBR => false;
        public AudioFormat AudioFormat { get; }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;
        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => (int)bits;
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement => channelsArrangement;
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.ID3V2 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        // IMetaDataEmbedder
        public long HasEmbeddedID3v2 => id3v2Offset;
        public uint ID3v2EmbeddingHeaderSize => 0;
        public FileStructureHelper.Zone Id3v2Zone => id3v2StructureHelper.GetZone(FileStructureHelper.DEFAULT_ZONE_NAME);
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bits = 0;
            sampleRate = 0;
            Duration = 0;
            BitRate = 0;
            isValid = false;
            id3v2Offset = -1;
            id3v2StructureHelper.Clear();
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public DSF(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        // Get compression ratio 
        private double getCompressionRatio()
        {
            if (isValid) return sizeInfo.FileSize / (Duration / 1000.0 * sampleRate * (channelsArrangement.NbChannels * bits / 8.0) + 44) * 100;
            return 0;
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, DSD_ID);
        }

        /// <inheritdoc/>
        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;
            bool result = false;
            byte[] buffer = new byte[8];

            resetData();

            source.Seek(0, SeekOrigin.Begin);
            if (source.Read(buffer, 0, 4) < 4) return false;
            if (StreamUtils.ArrBeginsWith(buffer, DSD_ID))
            {
                source.Seek(16, SeekOrigin.Current); // Chunk size and file size
                if (source.Read(buffer, 0, 8) < 8) return false;
                id3v2Offset = StreamUtils.DecodeInt64(buffer);

                if (source.Read(buffer, 0, 4) < 4) return false;
                if (StreamUtils.ArrBeginsWith(buffer, FMT_ID))
                {
                    source.Seek(8, SeekOrigin.Current); // Chunk size

                    if (source.Read(buffer, 0, 4) < 4) return false;
                    int formatVersion = StreamUtils.DecodeInt32(buffer);

                    if (formatVersion > 1)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported");
                        return result;
                    }

                    isValid = true;

                    source.Seek(8, SeekOrigin.Current); // Format ID (4), Channel type (4)

                    if (source.Read(buffer, 0, 4) < 4) return false;
                    uint channels = StreamUtils.DecodeUInt32(buffer);
                    channelsArrangement = channels switch
                    {
                        1 => MONO,
                        2 => STEREO,
                        3 => ISO_3_0_0,
                        4 => QUAD,
                        5 => LRCLFE,
                        6 => ISO_3_2_0,
                        7 => ISO_3_2_1,
                        _ => UNKNOWN
                    };

                    if (source.Read(buffer, 0, 4) < 4) return false;
                    sampleRate = StreamUtils.DecodeUInt32(buffer);
                    if (source.Read(buffer, 0, 4) < 4) return false;
                    bits = StreamUtils.DecodeUInt32(buffer);

                    if (source.Read(buffer, 0, 8) < 8) return false;
                    ulong sampleCount = StreamUtils.DecodeUInt64(buffer);

                    Duration = sampleCount * 1000.0 / sampleRate;
                    BitRate = Math.Round(((double)(sizeNfo.FileSize - source.Position)) * 8 / Duration); //time to calculate average bitrate

                    AudioDataOffset = source.Position + 8;
                    if (id3v2Offset > 0)
                        AudioDataSize = id3v2Offset - AudioDataOffset;
                    else
                        AudioDataSize = sizeNfo.FileSize - AudioDataOffset;

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