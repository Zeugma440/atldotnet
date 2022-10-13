using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Collections.Generic;
using static ATL.ChannelsArrangements;
using static ATL.TagData;
using System.Text;

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
    /// 
    ///     2. Multi-purpose LIST chunks
    ///     
    ///     ATL does not support LIST chunks with multiple purposes (e.g. adtl _and_ info)
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

        private const string HEADER_RIFF = "RIFF";
        private const string HEADER_RIFX = "RIFX";

        private const string FORMAT_WAVE = "WAVE";

        // Standard sub-chunks
        private const string CHUNK_FORMAT = "fmt ";
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
        private int sampleNumber;
        private uint headerSize;

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
            { "info.ICMT", Field.COMMENT },
            { "info.ICOP", Field.COPYRIGHT },
            { "info.ICRD", Field.RECORDING_DATE },
            { "info.IGNR", Field.GENRE },
            { "info.IRTD", Field.RATING },
            { "info.TRCK", Field.TRACK_NUMBER }
        };


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate
        {
            get { return (int)this.sampleRate; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public Format AudioFormat
        {
            get
            {
                Format f = new Format(audioFormat);
                f.Name = f.Name + " (" + getFormat() + ")";
                return f;
            }
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

            ResetData();
        }

        public WAV(string filePath, Format format)
        {
            this.filePath = filePath;
            this.audioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private bool readWAV(Stream source, ReadTagParams readTagParams)
        {
            bool result = true;
            uint riffChunkSize;
            long riffChunkSizePos;
            byte[] data = new byte[4];

            source.Seek(0, SeekOrigin.Begin);

            // Read header
            source.Read(data, 0, 4);
            string str = Utils.Latin1Encoding.GetString(data);
            if (str.Equals(HEADER_RIFF))
            {
                _isLittleEndian = true;
            }
            else if (str.Equals(HEADER_RIFX))
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

            // Format code
            source.Read(data, 0, 4);
            str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(FORMAT_WAVE)) return false;


            string subChunkId = "";
            uint chunkSize = 0;
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
            while (source.Position < riffChunkSize + 8)
            {
                // Chunk ID
                if (paddingSize > 0)
                {
                    source.Read(data, 0, 1);
                    // Padding has been forgotten !
                    if (data[0] != 0)
                    {
                        // Align to the correct position
                        source.Seek(-1, SeekOrigin.Current);
                        // Update zone size (remove and replace zone with updated size)
                        FileStructureHelper.Zone previousZone = structureHelper.GetZone(subChunkId);
                        previousZone.Size--;
                        structureHelper.RemoveZone(subChunkId);
                        structureHelper.AddZone(previousZone);
                    }
                }
                source.Read(data, 0, 4);
                subChunkId = Utils.Latin1Encoding.GetString(data);

                // Chunk size
                source.Read(data, 0, 4);
                chunkSize = isLittleEndian ? StreamUtils.DecodeUInt32(data) : StreamUtils.DecodeBEUInt32(data);
                // Word-align declared chunk size, as per specs
                paddingSize = chunkSize % 2;

                chunkDataPos = source.Position;

                if (subChunkId.Equals(CHUNK_FORMAT, StringComparison.OrdinalIgnoreCase))
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
                    AudioDataSize = chunkSize;
                    headerSize = riffChunkSize - chunkSize;
                }
                else if (subChunkId.Equals(CHUNK_FACT, StringComparison.OrdinalIgnoreCase))
                {
                    source.Read(data, 0, 4);
                    if (isLittleEndian) sampleNumber = StreamUtils.DecodeInt32(data); else sampleNumber = StreamUtils.DecodeBEInt32(data);
                }
                else if (subChunkId.Equals(CHUNK_SAMPLE, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId);

                    foundSample = true;
                    tagExists = true;

                    SampleTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_CUE, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId);

                    foundCue = true;
                    tagExists = true;

                    CueTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_LIST, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId);

                    foundList = true;
                    tagExists = true;

                    List.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_DISP, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId + "." + dispIndex);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId + "." + dispIndex);
                    dispIndex++;

                    foundDisp = true;
                    tagExists = true;

                    DispTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_BEXT, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId);

                    foundBext = true;
                    tagExists = true;

                    BextTag.FromStream(source, this, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_IXML, StringComparison.OrdinalIgnoreCase))
                {
                    structureHelper.AddZone(source.Position - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId);

                    foundIXml = true;
                    tagExists = true;

                    IXmlTag.FromStream(source, this, readTagParams, chunkSize);
                }
                else if (subChunkId.Equals(CHUNK_ID3, StringComparison.OrdinalIgnoreCase))
                {
                    id3v2Offset = source.Position;

                    // Zone is already added by Id3v2.Read
                    id3v2StructureHelper.AddZone(id3v2Offset - 8, (int)(chunkSize + paddingSize + 8), subChunkId);
                    id3v2StructureHelper.AddSize(riffChunkSizePos, riffChunkSize, subChunkId);
                }

                source.Seek(chunkDataPos + chunkSize, SeekOrigin.Begin);
            }

            // Add zone placeholders for future tag writing
            if (readTagParams.PrepareForWriting)
            {
                if (!foundSample)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_SAMPLE);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_SAMPLE);
                }
                if (!foundCue)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_CUE);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_CUE);
                }
                if (!foundList)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_LIST);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_LIST);
                }
                if (!foundDisp)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_DISP + ".0");
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_DISP + ".0");
                }
                if (!foundBext)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_BEXT);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_BEXT);
                }
                if (!foundIXml)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_IXML);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_IXML);
                }
            }

            // ID3 zone should be set as the very last one for Windows to be able to read the LIST INFO zone properly
            if (-1 == id3v2Offset)
            {
                id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                if (readTagParams.PrepareForWriting)
                {
                    id3v2StructureHelper.AddZone(source.Position, 0, CHUNK_ID3);
                    id3v2StructureHelper.AddSize(riffChunkSizePos, riffChunkSize, CHUNK_ID3);
                }
            }

            return result;
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
                result = (double)(sampleNumber / sampleRate);

            return result * 1000.0;
        }

        private double getBitrate()
        {
            return Math.Round((double)bitsPerSample / 1000.0 * sampleRate * channelsArrangement.NbChannels);
        }

        public bool Read(BinaryReader source, SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, ReadTagParams readTagParams)
        {
            resetData();

            bool result = readWAV(source.BaseStream, readTagParams);

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
            else if (zone.Equals(CHUNK_LIST) && List.IsDataEligible(this)) result += List.ToStream(w, isLittleEndian, this);
            else if (zone.Equals(CHUNK_DISP + ".0") && DispTag.IsDataEligible(this)) result += DispTag.ToStream(w, isLittleEndian, this); // Process the 1st position as a whole
            else if (zone.Equals(CHUNK_BEXT) && BextTag.IsDataEligible(this)) result += BextTag.ToStream(w, isLittleEndian, this);
            else if (zone.Equals(CHUNK_IXML) && IXmlTag.IsDataEligible(this)) result += IXmlTag.ToStream(w, isLittleEndian, this);

            return result;
        }

        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(CHUNK_ID3));
            if (isLittleEndian)
            {
                StreamUtils.WriteInt32(s, (int)tagSize);
            }
            else
            {
                StreamUtils.WriteBytes(s, StreamUtils.EncodeBEInt32((int)tagSize));
            }
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            if (tagSize % 2 > 0) s.WriteByte(0);
        }
    }
}