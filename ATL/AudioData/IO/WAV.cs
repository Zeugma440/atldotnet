using ATL.Logging;
using Commons;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for PCM (uncompressed audio) files manipulation (extension : .WAV)
    /// 
    /// Implementation notes
    /// 
    ///     1. BEXT metadata - UMID field
    ///     
    ///     UMID field is decoded "as is" using the hex notation. No additional interpretation has been done so far.
    ///     
    /// </summary>
	class WAV : MetaDataIO, IAudioDataIO, IMetaDataEmbedder
    {
        // Format type names
        public const string WAV_FORMAT_UNKNOWN = "Unknown";
        public const string WAV_FORMAT_PCM = "Windows PCM";
        public const string WAV_FORMAT_ADPCM = "Microsoft ADPCM";
        public const string WAV_FORMAT_ALAW = "A-LAW";
        public const string WAV_FORMAT_MULAW = "MU-LAW";
        public const string WAV_FORMAT_DVI_IMA_ADPCM = "DVI/IMA ADPCM";
        public const string WAV_FORMAT_MP3 = "MPEG Layer III";

        private static readonly byte[] HEADER_RIFF = Utils.Latin1Encoding.GetBytes("RIFF");
        private static readonly byte[] HEADER_RIFX = Utils.Latin1Encoding.GetBytes("RIFX");
        private static readonly byte[] HEADER_RF64 = Utils.Latin1Encoding.GetBytes("RF64");

        private const string FORMAT_WAVE = "WAVE";

        // Standard sub-chunks
        private const string CHUNK_FORMAT = "fmt ";
        private const string CHUNK_FORMAT64 = "ds64";
        private const string CHUNK_FACT = "fact";
        private const string CHUNK_DATA = "data";
        private const string CHUNK_SAMPLE = SampleTag.CHUNK_SAMPLE;
        private const string CHUNK_CUE = CueTag.CHUNK_CUE;
        private const string CHUNK_LIST = List.CHUNK_LIST; // That's the one Windows Explorer looks into
        private const string CHUNK_DISP = DispTag.CHUNK_DISP;

        // Broadcast Wave metadata sub-chunk
        private const string CHUNK_BEXT = BextTag.CHUNK_BEXT;
        private const string CHUNK_IXML = IXmlTag.CHUNK_IXML;
        private const string CHUNK_XMP = XmpTag.CHUNK_XMP;
        private const string CHUNK_CART = CartTag.CHUNK_CART;
        private const string CHUNK_ID3 = "id3 ";
        private const string CHUNK_ILLEGAL_ID3 = "id3_remove";

        // List of supported metadata chunks
        public static readonly ISet<string> metadataChunks = new HashSet<string>
        {
            CHUNK_SAMPLE, CHUNK_CUE, CHUNK_LIST, CHUNK_DISP, CHUNK_BEXT, CHUNK_IXML, CHUNK_XMP, CHUNK_CART, CHUNK_ID3
        };


        private ushort formatId;
        private uint sampleRate;
        private uint bytesPerSecond;
        private ushort bitsPerSample;
        private long sampleNumber;
        private long headerSize;

        private SizeInfo sizeInfo;
        private readonly AudioFormat audioFormat;

        private bool _isLittleEndian;

        private long id3v2Offset;
        private FileStructureHelper id3v2StructureHelper = new(false);


        // Mapping between WAV frame codes and ATL frame codes
        private static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { "bext.description", Field.GENERAL_DESCRIPTION },
            { "info.INAM", Field.TITLE },
            { "info.TITL", Field.TITLE },
            { "info.IART", Field.ARTIST },
            { "info.IPRD", Field.ALBUM },
            { "info.ICMT", Field.COMMENT },
            { "info.ICOP", Field.COPYRIGHT },
            { "info.ICRD", Field.RECORDING_DATE },
            { "info.IGNR", Field.GENRE },
            { "info.IRTD", Field.RATING },
            { "info.TRCK", Field.TRACK_NUMBER },
            { "info.IPRT", Field.TRACK_NUMBER },
            { "info.ITRK", Field.TRACK_NUMBER },
            { "info.ITCH", Field.ENCODED_BY },
            { "info.ISFT", Field.ENCODER },
            { "info.ILNG", Field.LANGUAGE}
        };


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public AudioFormat AudioFormat
        {
            get
            {
                AudioFormat f = new AudioFormat(audioFormat);
                f.Name = f.Name + " (" + getFormat() + ")";
                return f;
            }
        }
        public int SampleRate => (int)sampleRate;
        public bool IsVBR => false;
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;
        public string FileName { get; }

        public double BitRate => getBitrate();

        public int BitDepth => bitsPerSample;
        public double Duration => getDuration();

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            // Native for bext, info and iXML chunks
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        /// <inheritdoc/>
        protected override bool supportsPictures => true;

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // MetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier
            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        protected override bool isLittleEndian => _isLittleEndian;

        public override string EncodeDate(DateTime date)
        {
            return TrackUtils.FormatISOTimestamp(date).Replace("T", " ");
        }


        // IMetaDataEmbedder
        public long HasEmbeddedID3v2 => id3v2Offset;

        public uint ID3v2EmbeddingHeaderSize => 8;

        public FileStructureHelper.Zone Id3v2Zone => id3v2StructureHelper.GetZone(CHUNK_ID3);

        public FileStructureHelper.Zone Id3v2OldZone => id3v2StructureHelper.GetZone(CHUNK_ILLEGAL_ID3);


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            formatId = 0;
            sampleRate = 0;
            bytesPerSecond = 0;
            bitsPerSample = 0;
            sampleNumber = 0;
            headerSize = 0;

            id3v2Offset = -1;
            id3v2StructureHelper.Clear();

            AudioDataOffset = -1;
            AudioDataSize = 0;

            _isLittleEndian = false;

            ResetData();
        }

        public WAV(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            audioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, HEADER_RIFF) || StreamUtils.ArrBeginsWith(data, HEADER_RIFX) || StreamUtils.ArrBeginsWith(data, HEADER_RF64);
        }

        private bool readWAV(Stream source, ReadTagParams readTagParams)
        {
            bool isRf64 = false;
            object formattedRiffChunkSize = 0;
            byte[] data = new byte[4];
            byte[] data64 = new byte[8];

            long offset = sizeInfo.IsID3v2Embedded ? 0 : sizeInfo.ID3v2Size;
            source.Seek(offset, SeekOrigin.Begin);

            // Read header
            if (source.Read(data, 0, 4) < 4) return false;

            if (data.SequenceEqual(HEADER_RF64)) isRf64 = true;

            if (data.SequenceEqual(HEADER_RIFF) || isRf64)
            {
                _isLittleEndian = true;
            }
            else if (data.SequenceEqual(HEADER_RIFX))
            {
                _isLittleEndian = false;
            }
            else
            {
                return false;
            }

            // Force creation of FileStructureHelper with detected endianness
            structureHelper = new FileStructureHelper(isLittleEndian);
            id3v2StructureHelper = new FileStructureHelper(isLittleEndian);

            var riffChunkSizePos = source.Position;
            if (source.Read(data, 0, 4) < 4) return false;
            long riffChunkSize = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);
            if (riffChunkSize < uint.MaxValue) formattedRiffChunkSize = getFormattedRiffChunkSize(riffChunkSize, isRf64);

            // Format code
            if (source.Read(data, 0, 4) < 4) return false;
            string str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(FORMAT_WAVE)) return false;


            string subChunkId = "";
            uint paddingSize = 0;
            ISet<string> foundSubChunks = new HashSet<string>();

            int dispIndex = 0;

            // Sub-chunks loop
            // NB1 : we're testing source.Position + 8 because the chunk header (chunk ID and size) takes up 8 bytes
            // NB2 : uint.MaxValue is when the size declared in the traditional 32-bit header is discarded for the RF64 64-bit header
            long totalFileSize = riffChunkSize + 8;
            while (source.Position + 8 < totalFileSize || uint.MaxValue == riffChunkSize)
            {
                if (paddingSize > 0)
                {
                    if (source.Read(data, 0, 1) < 1) return false;
                    // Padding has been forgotten !
                    if (data[0] > 31 && data[0] < 255)
                    {
                        // Align to the correct position
                        source.Seek(-1, SeekOrigin.Current);

                        // Update zone size (remove and replace zone with updated size), if it exists
                        FileStructureHelper sHelper = subChunkId == CHUNK_ID3 ? id3v2StructureHelper : structureHelper;
                        FileStructureHelper.Zone previousZone = sHelper.GetZone(subChunkId);
                        if (previousZone != null)
                        {
                            previousZone.Size--;
                            sHelper.RemoveZone(subChunkId);
                            sHelper.AddZone(previousZone);
                        }
                    }
                }
                // Chunk ID
                if (source.Read(data, 0, 4) < 4) return false;
                subChunkId = Utils.Latin1Encoding.GetString(data);

                // Chunk size
                if (source.Read(data, 0, 4) < 4) return false;
                long chunkSize = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);
                // Word-align declared chunk size, as per specs
                paddingSize = (uint)(chunkSize % 2);

                var chunkDataPos = source.Position;

                if (subChunkId.Equals(CHUNK_FORMAT64, StringComparison.OrdinalIgnoreCase)) // DS64 always appears before FMT
                {
                    if (source.Read(data64, 0, 8) < 8) return false; // riffSize
                    if (uint.MaxValue == riffChunkSize)
                    {
                        riffChunkSize = StreamUtils.DecodeInt64(data64);
                        riffChunkSizePos = source.Position - 8;
                        formattedRiffChunkSize = getFormattedRiffChunkSize(riffChunkSize, isRf64);
                    }

                    if (source.Read(data64, 0, 8) < 8) return false; // dataSize
                    AudioDataSize = StreamUtils.DecodeInt64(data64);

                    if (source.Read(data64, 0, 8) < 8) return false; // sampleCount
                    sampleNumber = StreamUtils.DecodeInt64(data64);

                    if (source.Read(data, 0, 4) < 4) return false; // wave table length
                    uint tableLength = StreamUtils.DecodeUInt32(data);
                    source.Seek(tableLength, SeekOrigin.Current); // wave table
                }
                else if (subChunkId.Equals(CHUNK_FORMAT, StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Read(data, 0, 2) < 2) return false;
                    formatId = isLittleEndian ? StreamUtils.DecodeUInt16(data) : StreamUtils.DecodeBEUInt16(data);

                    if (source.Read(data, 0, 2) < 2) return false;
                    ChannelsArrangement = GuessFromChannelNumber(isLittleEndian ? StreamUtils.DecodeUInt16(data) : StreamUtils.DecodeBEUInt16(data));

                    if (source.Read(data, 0, 4) < 4) return false;
                    sampleRate = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);

                    if (source.Read(data, 0, 4) < 4) return false;
                    bytesPerSecond = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);

                    source.Seek(2, SeekOrigin.Current); // BlockAlign

                    if (source.Read(data, 0, 2) < 2) return false;
                    bitsPerSample = isLittleEndian ? StreamUtils.DecodeUInt16(data) : StreamUtils.DecodeBEUInt16(data);
                }
                else if (subChunkId.Equals(CHUNK_DATA, StringComparison.OrdinalIgnoreCase))
                {
                    AudioDataOffset = chunkDataPos;
                    // Handle RF64 where size has already been set by DS64
                    // NB : Some files in the wild can have an erroneous size of 0x00FFFFFF
                    if (AudioDataSize > 0 && (uint.MaxValue == chunkSize || 0x00FFFFFF == chunkSize)) chunkSize = AudioDataSize;
                    else AudioDataSize = chunkSize;
                    headerSize = riffChunkSize - AudioDataSize;
                }
                else if (subChunkId.Equals(CHUNK_FACT, StringComparison.OrdinalIgnoreCase))
                {
                    if (source.Read(data, 0, 4) < 4) return false;
                    var inputSampleNumber = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);
                    if (inputSampleNumber < uint.MaxValue) sampleNumber = inputSampleNumber;
                }
                else if (subChunkId.Equals(CHUNK_SAMPLE, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);
                    foundSubChunks.Add(CHUNK_SAMPLE);

                    SampleTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_CUE, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);
                    foundSubChunks.Add(CHUNK_CUE);
                    
                    CueTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_LIST, StringComparison.OrdinalIgnoreCase))
                {
                    long initialPosition = source.Position - 8;
                    foundSubChunks.Add(CHUNK_LIST);
                    
                    string purpose = List.FromStream(source, this, readTagParams, chunkSize);

                    structureHelper.AddZone(initialPosition, (int)(chunkSize + 8), CHUNK_LIST + "." + purpose);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_LIST + "." + purpose);
                }
                else if (subChunkId.Equals(CHUNK_DISP, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId + "." + dispIndex);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId + "." + dispIndex);
                    dispIndex++;
                    foundSubChunks.Add(CHUNK_DISP);

                    DispTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_BEXT, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);
                    foundSubChunks.Add(CHUNK_BEXT);

                    BextTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_IXML, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);
                    foundSubChunks.Add(CHUNK_IXML);
                    
                    IXmlTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_XMP, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);
                    foundSubChunks.Add(CHUNK_XMP);
                    
                    XmpTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_CART, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);
                    foundSubChunks.Add(CHUNK_CART);
                    
                    CartTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_ID3, StringComparison.OrdinalIgnoreCase))
                {
                    id3v2Offset = source.Position;

                    // Zone is already added by Id3v2.Read
                    id3v2StructureHelper.AddZone(id3v2Offset - 8, (int)(chunkSize + paddingSize + 8), CHUNK_ID3);
                    id3v2StructureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_ID3);
                }

                var nextPos = chunkDataPos + chunkSize;
                if (nextPos > source.Length) break;
                source.Seek(nextPos, SeekOrigin.Begin);
            }

            // Specific processing to remove metadata located after the "data" subchunk on RF64 files
            //   => Look for subchunk headers starting from EOF, as we can't know the size of the data subchunk
            // (implemented that way on ATL < 7.06 but not compliant to ITU-R BS.2088-1 recommendations)
            if (readTagParams.PrepareForRemoving && isRf64)
            {
                long searchOffset = Math.Min(5L * 1024 * 1024, (long)Math.Round(source.Length * 0.2));
                
                // Make sure to detect all variants
                var metadataChunksAll = new HashSet<string>();
                foreach (string id in metadataChunks)
                {
                    metadataChunksAll.Add(id);
                    metadataChunksAll.Add(id.ToLower());
                    metadataChunksAll.Add(id.ToUpper());
                }

                foreach (string metaChunkId in metadataChunksAll)
                {
                    source.Seek(-searchOffset, SeekOrigin.End);
                    if (!StreamUtils.FindSequence(source, Utils.Latin1Encoding.GetBytes(metaChunkId))) continue;

                    // Read subchunk size
                    if (source.Read(data, 0, 4) < 4) return false;
                    long chunkSize = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);
                    // Word-align declared chunk size, as per specs
                    paddingSize = (uint)(chunkSize % 2);

                    // Sanity check for unwanted matches (e.g. "IXML" as XML tag name)
                    if (chunkSize > searchOffset) continue;

                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Found chunk after data field : " + metaChunkId + " @ " + (source.Position - 4)+" ["+chunkSize+"]");

                    // Add it as a zone for further removal
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), metaChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, metaChunkId);
                    foundSubChunks.Add(metaChunkId);

                    // Set bogus field to signal metadata existence
                    tagData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.ANY, metaChunkId, "yes"));
                }
            }

            // Add zone placeholders for future tag writing
            // NB : As per ITU-R BS.2088-1 recommendations, any metadata subchunk should be located _before_ the data subchunk
            // to be compatible with very large files (>4GB; RF64)
            var newChunkOffset = AudioDataOffset - 8;
            if (readTagParams.PrepareForWriting && !readTagParams.PrepareForRemoving)
            {
                if (!foundSubChunks.Contains(CHUNK_SAMPLE))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_SAMPLE);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_SAMPLE);
                }
                if (!foundSubChunks.Contains(CHUNK_CUE))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_CUE);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_CUE);
                }
                if (!foundSubChunks.Contains(CHUNK_LIST))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_LIST + "." + List.PURPOSE_INFO);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_LIST + "." + List.PURPOSE_INFO);

                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_LIST + "." + List.PURPOSE_ADTL);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_LIST + "." + List.PURPOSE_ADTL);
                }
                if (!foundSubChunks.Contains(CHUNK_DISP))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_DISP + ".0");
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_DISP + ".0");
                }
                if (!foundSubChunks.Contains(CHUNK_BEXT))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_BEXT);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_BEXT);
                }
                if (!foundSubChunks.Contains(CHUNK_IXML))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_IXML);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_IXML);
                }
                if (!foundSubChunks.Contains(CHUNK_XMP))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_XMP);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_XMP);
                }
                if (!foundSubChunks.Contains(CHUNK_CART))
                {
                    structureHelper.AddZone(newChunkOffset, 0, CHUNK_CART);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_CART);
                }
            }

            // ID3 zone should be set as the very last one for Windows to be able to read the LIST INFO zone properly
            if (-1 == id3v2Offset)
            {
                // Switch status to "tried to read, but nothing found"
                id3v2Offset = 0;
                if (readTagParams.PrepareForWriting)
                {
                    // Add zone placeholder
                    id3v2StructureHelper.AddZone(newChunkOffset, 0, CHUNK_ID3);
                    id3v2StructureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_ID3);

                    // Remove illegal tagging, if it exists
                    if (!sizeInfo.IsID3v2Embedded && sizeInfo.ID3v2Size > 0)
                    {
                        id3v2StructureHelper.AddZone(0, sizeInfo.ID3v2Size, CHUNK_ILLEGAL_ID3);
                    }
                }
            }

            return true;
        }

        private static object getFormattedRiffChunkSize(long input, bool isRf64)
        {
            if (isRf64) return input;
            return (uint)input;
        }

        private string getFormat()
        {
            // Get format type name
            return formatId switch
            {
                1 => WAV_FORMAT_PCM,
                2 => WAV_FORMAT_ADPCM,
                6 => WAV_FORMAT_ALAW,
                7 => WAV_FORMAT_MULAW,
                17 => WAV_FORMAT_DVI_IMA_ADPCM,
                85 => WAV_FORMAT_MP3,
                _ => "Unknown"
            };
        }

        private double getDuration()
        {
            double result = 0;

            if (sampleNumber == 0 && bytesPerSecond > 0)
                result = (double)(sizeInfo.FileSize - headerSize - sizeInfo.ID3v1Size) / bytesPerSecond;
            if (sampleNumber > 0 && sampleRate > 0)
                result = sampleNumber * 1.0 / sampleRate;

            return result * 1000.0;
        }

        private double getBitrate()
        {
            return Math.Round(bitsPerSample / 1000.0 * sampleRate * ChannelsArrangement.NbChannels);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            resetData();
            return readWAV(source, readTagParams);
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true);
            return write(w, new TagHolder(tag), zone);
        }

        private int write(BinaryWriter w, MetaDataHolder tag, string zone)
        {
            int result = 0;

            switch (zone)
            {
                case CHUNK_SAMPLE when SampleTag.IsDataEligible(tag):
                    result += SampleTag.ToStream(w, isLittleEndian, tag);
                    break;
                case CHUNK_CUE when CueTag.IsDataEligible(tag):
                    result += CueTag.ToStream(w, isLittleEndian, tag);
                    break;
                default:
                    {
                        if (zone.StartsWith(CHUNK_LIST) && List.IsDataEligible(tag))
                        {
                            string[] zoneParts = zone.Split('.');
                            if (zoneParts.Length > 1) result += List.ToStream(w, isLittleEndian, zoneParts[1], tag, this);
                        }
                        else if (zone.Equals(CHUNK_DISP + ".0") && DispTag.IsDataEligible(tag)) result += DispTag.ToStream(w, isLittleEndian, tag); // Process the 1st position as a whole
                        else if (zone.Equals(CHUNK_BEXT) && BextTag.IsDataEligible(tag)) result += BextTag.ToStream(w, isLittleEndian, tag);
                        else if (zone.Equals(CHUNK_IXML) && IXmlTag.IsDataEligible(tag)) result += IXmlTag.ToStream(w.BaseStream, isLittleEndian, tag);
                        else if (zone.Equals(CHUNK_XMP) && XmpTag.IsDataEligible(tag)) result += XmpTag.ToStream(w.BaseStream, tag, isLittleEndian, true);
                        else if (zone.Equals(CHUNK_CART) && CartTag.IsDataEligible(tag)) result += CartTag.ToStream(w, isLittleEndian, tag);

                        break;
                    }
            }

            return result;
        }

        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            var span = new Span<byte>(new byte[4]);

            Utils.Latin1Encoding.GetBytes(CHUNK_ID3.AsSpan(), span);
            s.Write(span);
            if (isLittleEndian) BinaryPrimitives.WriteInt32LittleEndian(span, (int)tagSize);
            else BinaryPrimitives.WriteInt32BigEndian(span, (int)tagSize);
            s.Write(span);
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            if (tagSize % 2 > 0) s.WriteByte(0);
        }
    }
}