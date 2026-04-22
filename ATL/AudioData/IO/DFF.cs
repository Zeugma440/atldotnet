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
    /// Class for Direct Stream Digital Direct Stream Digital Interchange (aka "Philips DSD") files manipulation (extension : .DFF)
    /// </summary>
	class DFF : IAudioDataIO, IMetaDataEmbedder
    {
        // Header IDs
        private static readonly byte[] DFF_ID = Utils.Latin1Encoding.GetBytes("FRM8");
        private static readonly byte[] DSD_ID = Utils.Latin1Encoding.GetBytes("DSD ");
        private static readonly byte[] PROP_ID = Utils.Latin1Encoding.GetBytes("PROP");
        private const string ID3__ID = "ID3 ";
        private static readonly byte[] ID3_ID = Utils.Latin1Encoding.GetBytes(ID3__ID);
        private static readonly byte[] FS_ID = Utils.Latin1Encoding.GetBytes("FS  ");
        private static readonly byte[] CHNL_ID = Utils.Latin1Encoding.GetBytes("CHNL");

        // Multichannel channels IDs
        private const string LEFT_SURROUND_ID = "LS  ";
        private const string RIGHT_SURROUND_ID = "RS  ";
        private const string CENTER_ID = "C   ";
        private const string LFE_ID = "LFE ";


        // Private declarations
        private ChannelsArrangement channelsArrangement;
        private uint bits;
        private uint sampleRate;

        private bool isValid;

        private SizeInfo sizeInfo;

        private long id3v2Offset;
        private readonly FileStructureHelper id3v2StructureHelper = new(false);


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
        public uint ID3v2EmbeddingHeaderSize => 12;
        public FileStructureHelper.Zone Id3v2Zone => id3v2StructureHelper.GetZone(ID3__ID);
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
        public DFF(string filePath, AudioFormat format)
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
            return data.StartsWith(DFF_ID);
        }

        private ChannelsArrangement computeChannelsArr(IList<string> channels)
        {
            return channels.Count switch
            {
                4 when channels.Contains(LFE_ID) && channels.Contains(CENTER_ID) => LRCLFE,
                4 when channels.Contains(LEFT_SURROUND_ID) && channels.Contains(RIGHT_SURROUND_ID) => ITU_2_2,
                5 when channels.Contains(LFE_ID) => DVD_18,
                5 when channels.Contains(CENTER_ID) => ISO_3_2_0,
                _ => GuessFromChannelNumber(channels.Count)
            };
        }

        private bool readPhilipsDsd(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[8];
            long id3v2Size = -1;
            bool id3v2AsSubChunk = false;
            long propChunkOffset = -1;
            long propChunkSize = -1;

            source.Seek(12, SeekOrigin.Begin); // Chunk size and file size
            if (source.Read(buffer, 0, 4) < 4) return false;
            if (!buffer.AsSpan().StartsWith(DSD_ID)) return false;

            // Read chunks
            do
            {
                long chunkOffset = source.Position;
                if (source.Read(buffer, 0, 4) < 4) break;
                var chunkId = buffer[..4].AsSpan();

                if (source.Read(buffer, 0, 8) < 8) break;
                long chunkSize = BinaryPrimitives.ReadInt64BigEndian(buffer); // Should be unsigned

                if (chunkId.StartsWith(PROP_ID))
                {
                    propChunkOffset = chunkOffset;
                    propChunkSize = chunkSize;
                    source.Seek(4, SeekOrigin.Current); // 'SND ' propType
                    do
                    {
                        long subChunkOffset = source.Position;
                        if (source.Read(buffer, 0, 4) < 4) break;
                        var subChunkId = buffer[..4].AsSpan();

                        if (source.Read(buffer, 0, 8) < 8) break;
                        long subChunkSize = BinaryPrimitives.ReadInt64BigEndian(buffer); // Should be unsigned

                        if (subChunkId.StartsWith(ID3_ID))
                        {
                            // ID3 can be located as a subchunk inside the PROP chunk
                            id3v2AsSubChunk = true;
                            id3v2Offset = source.Position;
                            id3v2Size = chunkSize;
                        }
                        else if (subChunkId.StartsWith(FS_ID)) // Sample rate
                        {
                            // Total number of bits in one second per channel
                            if (source.Read(buffer, 0, 4) < 4) break;
                            sampleRate = BinaryPrimitives.ReadUInt32BigEndian(buffer);
                        }
                        else if (subChunkId.StartsWith(CHNL_ID)) // Channels
                        {
                            if (source.Read(buffer, 0, 2) < 2) break;
                            var nbChannels = BinaryPrimitives.ReadUInt16BigEndian(buffer);
                            var channels = new List<string>(nbChannels);
                            for (int i = 0; i < nbChannels; i++)
                            {
                                if (source.Read(buffer, 0, 4) < 4) break;
                                channels.Add(Utils.Latin1Encoding.GetString(buffer[..4]));
                            }

                            channelsArrangement = computeChannelsArr(channels);
                        }

                        source.Seek(subChunkOffset + 12 + subChunkSize, SeekOrigin.Begin);
                    } while (source.Position < chunkOffset + chunkSize);
                }

                // ID3 can also be located as an independent chunk
                if (chunkId.StartsWith(ID3_ID))
                {
                    id3v2Offset = source.Position;
                    id3v2Size = chunkSize;
                }

                if (chunkId.StartsWith(DSD_ID))
                {
                    AudioDataOffset = source.Position;
                    AudioDataSize = chunkSize;
                }

                source.Seek( Math.Min(chunkOffset + 12 + chunkSize, source.Length), SeekOrigin.Begin);
            } while (source.Position < source.Length);

            Duration = AudioDataSize * 8000.0 / channelsArrangement.NbChannels / sampleRate;
            var bitRateFloat = AudioDataSize * 8.0 / Duration;
            BitRate = Math.Round(bitRateFloat);
            bits = 1; // Per specs

            // Load tag if exists
            if (id3v2Offset > 0)
            {
                if (readTagParams.PrepareForWriting)
                {
                    id3v2StructureHelper.AddZone(id3v2Offset - 12, id3v2Size + 12, ID3__ID);
                    id3v2StructureHelper.AddSize(4, source.Length, ID3__ID);
                    if (id3v2AsSubChunk) id3v2StructureHelper.AddSize(propChunkOffset + 4, propChunkSize, ID3__ID);
                }
            }
            else
            {
                id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                if (readTagParams.PrepareForWriting)
                {
                    // Add EOF zone for future tag writing
                    id3v2StructureHelper.AddZone(source.Length, 0, ID3__ID);
                    id3v2StructureHelper.AddSize(4, source.Length, ID3__ID);
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

            return span.StartsWith(DFF_ID) && readPhilipsDsd(source, sizeNfo, readTagParams);
        }

        /// <inheritdoc/>
        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            StreamUtils.WriteBytes(s, ID3_ID);
            s.Write(StreamUtils.EncodeBEUInt64((ulong)tagSize));
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            // Nothing to do here
        }
    }
}
