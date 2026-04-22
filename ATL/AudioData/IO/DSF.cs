using ATL.Logging;
using Commons;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Direct Stream Digital Stream files (aka "Sony DSD") manipulation (extension : .DSF, .DSD)
    /// </summary>
	class DSF : IAudioDataIO, IMetaDataEmbedder
    {
        // Header IDs
        private static readonly byte[] DSD_ID = Utils.Latin1Encoding.GetBytes("DSD ");
        private static readonly byte[] FMT_ID = Utils.Latin1Encoding.GetBytes("fmt ");
        private const string ID3_ID = "ID3 ";


        // Private declarations
        private ChannelsArrangement channelsArrangement;
        private uint bits;
        private uint sampleRate;

        private bool isValid;

        private SizeInfo sizeInfo;

        private long id3v2Offset;
        private readonly FileStructureHelper id3v2StructureHelper = new();


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
        public FileStructureHelper.Zone Id3v2Zone => id3v2StructureHelper.GetZone(ID3_ID);
        public FileStructureHelper.Zone Id3v2OldZone => id3v2StructureHelper.GetZone("id3_remove");
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

        public static bool IsValidHeader(ReadOnlySpan<byte> data)
        {
            return data.StartsWith(DSD_ID);
        }

        private bool readSonyDsd(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[8];

            source.Seek(16, SeekOrigin.Current); // Chunk size and file size
            if (source.Read(buffer, 0, 8) < 8) return false;
            id3v2Offset = BinaryPrimitives.ReadInt64LittleEndian(buffer);

            if (source.Read(buffer, 0, 4) < 4) return false;
            if (buffer.AsSpan().StartsWith(FMT_ID))
            {
                source.Seek(8, SeekOrigin.Current); // Chunk size

                if (source.Read(buffer, 0, 4) < 4) return false;
                int formatVersion = BinaryPrimitives.ReadInt32LittleEndian(buffer);

                if (formatVersion > 1)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSD format version " + formatVersion + " not supported");
                    return false;
                }

                isValid = true;

                source.Seek(8, SeekOrigin.Current); // Format ID (4), Channel type (4)

                if (source.Read(buffer, 0, 4) < 4) return false;
                uint channels = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
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
                sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
                if (source.Read(buffer, 0, 4) < 4) return false;
                bits = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

                if (source.Read(buffer, 0, 8) < 8) return false;
                ulong sampleCount = BinaryPrimitives.ReadUInt64LittleEndian(buffer);

                Duration = sampleCount * 1000.0 / sampleRate;
                BitRate = Math.Round((double)(sizeNfo.FileSize - source.Position) * 8 / Duration);

                AudioDataOffset = source.Position + 8;
                if (id3v2Offset > 0)
                    AudioDataSize = id3v2Offset - AudioDataOffset;
                else
                    AudioDataSize = sizeNfo.FileSize - AudioDataOffset;
            }

            // Load tag if exists
            if (id3v2Offset > 0)
            {
                if (readTagParams.PrepareForWriting)
                {
                    id3v2StructureHelper.AddZone(id3v2Offset, (int)(source.Length - id3v2Offset), ID3_ID);
                    id3v2StructureHelper.AddSize(12, source.Length, ID3_ID);
                    id3v2StructureHelper.AddIndex(20, id3v2Offset, false, ID3_ID);
                }
            }
            else
            {
                id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                if (readTagParams.PrepareForWriting)
                {
                    // Add EOF zone for future tag writing
                    id3v2StructureHelper.AddZone(source.Length, 0, ID3_ID);
                    id3v2StructureHelper.AddSize(12, source.Length, ID3_ID);
                    id3v2StructureHelper.AddIndex(20, source.Length, false, ID3_ID);
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;
            byte[] buffer = new byte[8];

            resetData();

            source.Seek(0, SeekOrigin.Begin);
            if (source.Read(buffer, 0, 4) < 4) return false;
            var span = buffer.AsSpan();

            return span.StartsWith(DSD_ID) && readSonyDsd(source, sizeNfo, readTagParams);
        }

        /// <inheritdoc/>
        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            // Nothing to do for DSF which defines no frame header for its embedded ID3v2 tag
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            // Nothing to do here
        }
    }
}
