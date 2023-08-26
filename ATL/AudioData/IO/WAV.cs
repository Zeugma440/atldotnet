using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Collections.Generic;
using System.Linq;
using static ATL.ChannelsArrangements;
using static ATL.TagData;
using System.Text;
using System.Buffers.Binary;

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
        private const string CHUNK_LIST = List.CHUNK_LIST;
        private const string CHUNK_DISP = DispTag.CHUNK_DISP;

        // Broadcast Wave metadata sub-chunk
        private const string CHUNK_BEXT = BextTag.CHUNK_BEXT;
        private const string CHUNK_IXML = IXmlTag.CHUNK_IXML;
        private const string CHUNK_ID3 = "id3 ";


        private ushort formatId;
        private ChannelsArrangement channelsArrangement;
        private uint sampleRate;
        private uint bytesPerSecond;
        private ushort bitsPerSample;
        private long sampleNumber;
        private long headerSize;

        private double bitrate;
        private double duration;

        private SizeInfo sizeInfo;
        private readonly string filePath;
        private readonly Format audioFormat;

        private bool _isLittleEndian;

        private long id3v2Offset;
        private FileStructureHelper id3v2StructureHelper = new FileStructureHelper(false);


        // Mapping between WAV frame codes and ATL frame codes
        private static IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
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
            { "info.ITRK", Field.TRACK_NUMBER }
        };


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public Format AudioFormat
        {
            get
            {
                Format f = new Format(audioFormat);
                f.Name = f.Name + " (" + getFormat() + ")";
                return f;
            }
        }
        public int SampleRate => (int)sampleRate;
        public bool IsVBR => false;
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;
        public string FileName => filePath;
        public double BitRate => bitrate;
        public int BitDepth => bitsPerSample;
        public double Duration => duration;
        public ChannelsArrangement ChannelsArrangement => channelsArrangement;
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return metaDataType == MetaDataIOFactory.TagType.ID3V1 || metaDataType == MetaDataIOFactory.TagType.ID3V2 || metaDataType == MetaDataIOFactory.TagType.NATIVE; // Native for bext, info and iXML chunks
        }
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
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }

        protected override bool isLittleEndian
        {
            get { return _isLittleEndian; }
        }

        public override string EncodeDate(DateTime date)
        {
            return TrackUtils.FormatISOTimestamp(date).Replace("T", " ");
        }


        // IMetaDataEmbedder
        public long HasEmbeddedID3v2
        {
            get { return id3v2Offset; }
        }
        public uint ID3v2EmbeddingHeaderSize
        {
            get { return 8; }
        }
        public FileStructureHelper.Zone Id3v2Zone
        {
            get { return id3v2StructureHelper.GetZone(CHUNK_ID3); }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            duration = 0;
            bitrate = 0;

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

        public WAV(string filePath, Format format)
        {
            this.filePath = filePath;
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
            bool result = true;

            bool isRf64 = false;
            long riffChunkSize;
            object formattedRiffChunkSize = 0;
            long riffChunkSizePos;
            byte[] data = new byte[4];
            byte[] data64 = new byte[8];

            source.Seek(0, SeekOrigin.Begin);

            // Read header
            source.Read(data, 0, 4);

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

            riffChunkSizePos = source.Position;
            source.Read(data, 0, 4);
            if (isLittleEndian) riffChunkSize = StreamUtils.DecodeUInt32(data); else riffChunkSize = StreamUtils.DecodeBEUInt32(data);
            if (riffChunkSize < uint.MaxValue) formattedRiffChunkSize = getFormattedRiffChunkSize(riffChunkSize, isRf64);

            // Format code
            source.Read(data, 0, 4);
            string str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(FORMAT_WAVE)) return false;


            string subChunkId = "";
            long chunkSize = 0;
            uint paddingSize = 0;
            long chunkDataPos;
            bool foundSample = false;
            bool foundCue = false;
            bool foundList = false;
            bool foundDisp = false;
            int dispIndex = 0;
            bool foundBext = false;
            bool foundIXml = false;

            // Sub-chunks loop
            // NB1 : we're testing source.Position + 8 because the chunk header (chunk ID and size) takes up 8 bytes
            // NB2 : uint.MaxValue is when the size declared in the traditional 32-bit header is discarded for the RF64 64-bit header
            long totalFileSize = riffChunkSize + 8;
            while (source.Position + 8 < totalFileSize || uint.MaxValue == riffChunkSize)
            {
                if (paddingSize > 0)
                {
                    source.Read(data, 0, 1);
                    // Padding has been forgotten !
                    if (data[0] > 31 && data[0] < 255)
                    {
                        // Align to the correct position
                        source.Seek(-1, SeekOrigin.Current);

                        // Update zone size (remove and replace zone with updated size), if it exists
                        FileStructureHelper sHelper = (subChunkId == CHUNK_ID3) ? id3v2StructureHelper : structureHelper;
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
                source.Read(data, 0, 4);
                subChunkId = Utils.Latin1Encoding.GetString(data);

                // Chunk size
                source.Read(data, 0, 4);
                chunkSize = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);
                // Word-align declared chunk size, as per specs
                paddingSize = (uint)(chunkSize % 2);

                chunkDataPos = source.Position;

                if (subChunkId.Equals(CHUNK_FORMAT64, StringComparison.OrdinalIgnoreCase)) // DS64 always appears before FMT
                {
                    source.Read(data64, 0, 8); // riffSize
                    if (uint.MaxValue == riffChunkSize)
                    {
                        riffChunkSize = StreamUtils.DecodeInt64(data64);
                        riffChunkSizePos = source.Position - 8;
                        formattedRiffChunkSize = getFormattedRiffChunkSize(riffChunkSize, isRf64);
                    }

                    source.Read(data64, 0, 8); // dataSize
                    AudioDataSize = StreamUtils.DecodeInt64(data64);

                    source.Read(data64, 0, 8); // sampleCount
                    sampleNumber = StreamUtils.DecodeInt64(data64);

                    source.Read(data, 0, 4); // wave table length
                    uint tableLength = StreamUtils.DecodeUInt32(data);
                    source.Seek(tableLength, SeekOrigin.Current); // wave table
                }
                else if (subChunkId.Equals(CHUNK_FORMAT, StringComparison.OrdinalIgnoreCase))
                {
                    source.Read(data, 0, 2);
                    if (isLittleEndian) formatId = StreamUtils.DecodeUInt16(data); else formatId = StreamUtils.DecodeBEUInt16(data);

                    source.Read(data, 0, 2);
                    if (isLittleEndian) channelsArrangement = GuessFromChannelNumber(StreamUtils.DecodeUInt16(data));
                    else channelsArrangement = GuessFromChannelNumber(StreamUtils.DecodeBEUInt16(data));

                    source.Read(data, 0, 4);
                    if (isLittleEndian) sampleRate = StreamUtils.DecodeUInt32(data); else sampleRate = StreamUtils.DecodeBEUInt32(data);

                    source.Read(data, 0, 4);
                    if (isLittleEndian) bytesPerSecond = StreamUtils.DecodeUInt32(data); else bytesPerSecond = StreamUtils.DecodeBEUInt32(data);

                    source.Seek(2, SeekOrigin.Current); // BlockAlign

                    source.Read(data, 0, 2);
                    if (isLittleEndian) bitsPerSample = StreamUtils.DecodeUInt16(data); else bitsPerSample = StreamUtils.DecodeBEUInt16(data);
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
                    source.Read(data, 0, 4);
                    uint inputSampleNumber;
                    if (isLittleEndian) inputSampleNumber = StreamUtils.DecodeUInt32(data); else inputSampleNumber = StreamUtils.DecodeBEUInt32(data);
                    if (inputSampleNumber < uint.MaxValue) sampleNumber = inputSampleNumber;
                }
                else if (subChunkId.Equals(CHUNK_SAMPLE, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);

                    foundSample = true;
                    tagExists = true;

                    SampleTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_CUE, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);

                    foundCue = true;
                    tagExists = true;

                    CueTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_LIST, StringComparison.OrdinalIgnoreCase))
                {
                    long initialPosition = source.Position - 8;

                    foundList = true;
                    tagExists = true;

                    string purpose = List.FromStream(source, this, readTagParams, chunkSize);

                    structureHelper.AddZone(initialPosition, (int)(chunkSize + 8), CHUNK_LIST + "." + purpose);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_LIST + "." + purpose);
                }
                else if (subChunkId.Equals(CHUNK_DISP, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId + "." + dispIndex);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId + "." + dispIndex);
                    dispIndex++;

                    foundDisp = true;
                    tagExists = true;

                    DispTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_BEXT, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);

                    foundBext = true;
                    tagExists = true;

                    BextTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_IXML, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, subChunkId);

                    foundIXml = true;
                    tagExists = true;

                    IXmlTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_ID3, StringComparison.OrdinalIgnoreCase))
                {
                    id3v2Offset = source.Position;

                    // Zone is already added by Id3v2.Read
                    id3v2StructureHelper.AddZone(id3v2Offset - 8, (int)(chunkSize + paddingSize + 8), CHUNK_ID3);
                    id3v2StructureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_ID3);
                }

                source.Seek(chunkDataPos + chunkSize, SeekOrigin.Begin);
            }

            // Add zone placeholders for future tag writing
            long eof = source.Length;
            if (readTagParams.PrepareForWriting)
            {
                if (!foundSample)
                {
                    structureHelper.AddZone(eof, 0, CHUNK_SAMPLE);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_SAMPLE);
                }
                if (!foundCue)
                {
                    structureHelper.AddZone(eof, 0, CHUNK_CUE);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_CUE);
                }
                if (!foundList)
                {
                    structureHelper.AddZone(eof, 0, CHUNK_LIST + "." + List.PURPOSE_INFO);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_LIST + "." + List.PURPOSE_INFO);

                    structureHelper.AddZone(eof, 0, CHUNK_LIST + "." + List.PURPOSE_ADTL);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_LIST + "." + List.PURPOSE_ADTL);
                }
                if (!foundDisp)
                {
                    structureHelper.AddZone(eof, 0, CHUNK_DISP + ".0");
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_DISP + ".0");
                }
                if (!foundBext)
                {
                    structureHelper.AddZone(eof, 0, CHUNK_BEXT);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_BEXT);
                }
                if (!foundIXml)
                {
                    structureHelper.AddZone(eof, 0, CHUNK_IXML);
                    structureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_IXML);
                }
            }

            // ID3 zone should be set as the very last one for Windows to be able to read the LIST INFO zone properly
            if (-1 == id3v2Offset)
            {
                id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                if (readTagParams.PrepareForWriting)
                {
                    id3v2StructureHelper.AddZone(eof, 0, CHUNK_ID3);
                    id3v2StructureHelper.AddSize(riffChunkSizePos, formattedRiffChunkSize, CHUNK_ID3);
                }
            }

            return result;
        }

        private object getFormattedRiffChunkSize(long input, bool isRf64)
        {
            if (isRf64) return input;
            else return (uint)input;
        }

        private string getFormat()
        {
            // Get format type name
            switch (formatId)
            {
                case 1: return WAV_FORMAT_PCM;
                case 2: return WAV_FORMAT_ADPCM;
                case 6: return WAV_FORMAT_ALAW;
                case 7: return WAV_FORMAT_MULAW;
                case 17: return WAV_FORMAT_DVI_IMA_ADPCM;
                case 85: return WAV_FORMAT_MP3;
                default: return "Unknown";
            }
        }

        private double getDuration()
        {
            // Get duration
            double result = 0;

            if ((sampleNumber == 0) && (bytesPerSecond > 0))
                result = (double)(sizeInfo.FileSize - headerSize - sizeInfo.ID3v1Size) / bytesPerSecond;
            if ((sampleNumber > 0) && (sampleRate > 0))
                result = sampleNumber * 1.0 / sampleRate;

            return result * 1000.0;
        }

        private double getBitrate()
        {
            return Math.Round(bitsPerSample / 1000.0 * sampleRate * channelsArrangement.NbChannels);
        }

        public bool Read(Stream source, SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            resetData();

            bool result = readWAV(source, readTagParams);

            // Process data if loaded and header valid
            if (result)
            {
                bitrate = getBitrate();
                duration = getDuration();
            }
            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true)) return write(w, zone);
        }

        private int write(BinaryWriter w, string zone)
        {
            int result = 0;

            if (zone.Equals(CHUNK_SAMPLE) && SampleTag.IsDataEligible(this)) result += SampleTag.ToStream(w, isLittleEndian, this);
            else if (zone.Equals(CHUNK_CUE) && CueTag.IsDataEligible(this)) result += CueTag.ToStream(w, isLittleEndian, this);
            else if (zone.StartsWith(CHUNK_LIST) && List.IsDataEligible(this))
            {
                string[] zoneParts = zone.Split('.');
                if (zoneParts.Length > 1) result += List.ToStream(w, isLittleEndian, zoneParts[1], this);
            }
            else if (zone.Equals(CHUNK_DISP + ".0") && DispTag.IsDataEligible(this)) result += DispTag.ToStream(w, isLittleEndian, this); // Process the 1st position as a whole
            else if (zone.Equals(CHUNK_BEXT) && BextTag.IsDataEligible(this)) result += BextTag.ToStream(w, isLittleEndian, this);
            else if (zone.Equals(CHUNK_IXML) && IXmlTag.IsDataEligible(this)) result += IXmlTag.ToStream(w, isLittleEndian, this);

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