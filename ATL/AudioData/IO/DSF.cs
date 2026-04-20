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
    /// Class for Direct Stream Digital Interchange files manipulation
    ///  - DSD Stream File (extension : .DSF)
    ///  - Direct Stream Digital Interchange File (extension : .DFF, .DSD)
    /// </summary>
	class DSF : IAudioDataIO, IMetaDataEmbedder
    {
        // Headers ID
        private static readonly byte[] DFF_ID = Utils.Latin1Encoding.GetBytes("FRM8");
        private static readonly byte[] DSD_ID = Utils.Latin1Encoding.GetBytes("DSD ");
        private static readonly byte[] FMT_ID = Utils.Latin1Encoding.GetBytes("fmt ");
        private static readonly byte[] PROP_ID = Utils.Latin1Encoding.GetBytes("PROP");
        private static readonly byte[] ID3_ID = Utils.Latin1Encoding.GetBytes("ID3 ");


        // Private declarations
        private int formatType = -1; // 0 = DSF; 1 = DFF
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
        public FileStructureHelper.Zone Id3v2OldZone => id3v2StructureHelper.GetZone("id3_remove");
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            formatType = -1;
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
            return data.StartsWith(DSD_ID) || data.StartsWith(DFF_ID);
        }

        private bool readDsd(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[8];
            formatType = 0;

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
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported");
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
                BitRate = Math.Round(((double)(sizeNfo.FileSize - source.Position)) * 8 / Duration); //time to calculate average bitrate

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

            return true;
        }

        private bool readDsf(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[8];
            long id3v2Size = -1;
            formatType = 1;

            source.Seek(12, SeekOrigin.Begin); // Chunk size and file size
            if (source.Read(buffer, 0, 4) < 4) return false;
            if (!buffer.AsSpan().StartsWith(DSD_ID)) return false;

            // Read chunks
            do
            {
                long chunkOffset = source.Position;
                if (source.Read(buffer, 0, 4) < 4) return false;
                var chunkId = buffer[..4].AsSpan();

                if (source.Read(buffer, 0, 8) < 8) return false;
                long chunkSize = BinaryPrimitives.ReadInt64BigEndian(buffer); // Should be unsigned

                if (chunkId.StartsWith(PROP_ID))
                {
                    source.Seek(4, SeekOrigin.Current); // 'SND ' propType
                    do
                    {
                        long subChunkOffset = source.Position;
                        if (source.Read(buffer, 0, 4) < 4) return false;
                        var subChunkId = buffer[..4].AsSpan();

                        if (source.Read(buffer, 0, 8) < 8) return false;
                        long subChunkSize = BinaryPrimitives.ReadInt64BigEndian(buffer); // Should be unsigned

                        // ID3 can be located as a subchunk inside the PROP chunk
                        // TODO create size counter on the PROPS chunk header
                        if (subChunkId.StartsWith(ID3_ID))
                        {
                            id3v2Offset = source.Position;
                            id3v2Size = chunkSize;
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

                // TODO AudioDataOffset, AudioDataSize, Props

                source.Seek(chunkOffset + 12 + chunkSize, SeekOrigin.Begin);
            } while (source.Position < source.Length);

            // Load tag if exists
            if (id3v2Offset > 0)
            {
                if (readTagParams.PrepareForWriting)
                {
                    id3v2StructureHelper.AddZone(id3v2Offset - 12, id3v2Size + 12);
                    id3v2StructureHelper.AddSize(4, source.Length);
                }
            }
            else
            {
                id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                if (readTagParams.PrepareForWriting)
                {
                    // Add EOF zone for future tag writing
                    id3v2StructureHelper.AddZone(source.Length, 0);
                    id3v2StructureHelper.AddSize(4, source.Length);
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

            if (span.StartsWith(DSD_ID)) return readDsd(source, sizeNfo, readTagParams);
            return span.StartsWith(DFF_ID) && readDsf(source, sizeNfo, readTagParams);
        }

        /// <inheritdoc/>
        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            // Nothing to do for DSF (format 0) which defines no frame header for its embedded ID3v2 tag
            if (1 != formatType) return;

            StreamUtils.WriteBytes(s, ID3_ID);
            s.Write(StreamUtils.EncodeBEUInt64((ulong)tagSize));
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            // Nothing to do here
        }
    }
}
