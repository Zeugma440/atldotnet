using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Collections.Generic;

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
	class WAV : MetaDataIO, IAudioDataIO
    {
        // Format type names
        public const String WAV_FORMAT_UNKNOWN = "Unknown";
        public const String WAV_FORMAT_PCM = "Windows PCM";
        public const String WAV_FORMAT_ADPCM = "Microsoft ADPCM";
        public const String WAV_FORMAT_ALAW = "A-LAW";
        public const String WAV_FORMAT_MULAW = "MU-LAW";
        public const String WAV_FORMAT_DVI_IMA_ADPCM = "DVI/IMA ADPCM";
        public const String WAV_FORMAT_MP3 = "MPEG Layer III";

        private const string HEADER_RIFF = "RIFF";
        private const string HEADER_RIFX = "RIFX";

        private const string FORMAT_WAVE = "WAVE";

        // Standard sub-chunks
        private const String CHUNK_FORMAT = "fmt ";
        private const String CHUNK_FACT = "fact";
        private const String CHUNK_DATA = "data";

        // Broadcast Wave metadata sub-chunk
        private const String CHUNK_BEXT = "bext";
        private const String CHUNK_INFO = "LIST";
        private const String CHUNK_IXML = "iXML";


        // Used with ChannelModeID property
        public const byte WAV_CM_MONO = 1;                     // Index for mono mode
        public const byte WAV_CM_STEREO = 2;                 // Index for stereo mode

        // Channel mode names
        public String[] WAV_MODE = new String[3] { "Unknown", "Mono", "Stereo" };

        //		private ushort formatID;
        private ushort channelNumber;
        private uint sampleRate;
        private uint bytesPerSecond;
        //		private ushort blockAlign;
        private ushort bitsPerSample;
        private int sampleNumber;
        private uint headerSize;

        private double bitrate;
        private double duration;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private bool _isLittleEndian;


        private static IDictionary<string, byte> frameMapping; // Mapping between WAV frame codes and ATL frame codes


        /* Unused for now
        public ushort FormatID // Format type code
		{
			get { return this.formatID; }
		}	
		public String Format // Format type name
		{
			get { return this.getFormat(); }
		}	
		public byte ChannelNumber // Number of channels
		{
			get { return this.channelNumber; }
		}		 
		public String ChannelMode // Channel mode name
		{
			get { return this.getChannelMode(); }
		}	
		public byte BitsPerSample // Bits/sample
        {
            get { return this.bitsPerSample; }
        }
		public uint BytesPerSecond // Bytes/second
		{
			get { return this.bytesPerSecond; }
		}
		public ushort BlockAlign // Block alignment
		{
			get { return this.blockAlign; }
		}			
		public ushort HeaderSize // Header size (bytes)
		{
			get { return this.headerSize; }
		}	
        */


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
            get { return bitrate / 1000.0; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_ID3V1 || metaDataType == MetaDataIOFactory.TAG_ID3V2 || metaDataType == MetaDataIOFactory.TAG_NATIVE); // Native for bext, info and iXML chunks
        }


        // MetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }

        protected override bool isLittleEndian
        {
            get { return _isLittleEndian; }
        }

        // ---------- CONSTRUCTORS & INITIALIZERS

        static WAV()
        {
            frameMapping = new Dictionary<string, byte>
            {
                { "bext.description", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
            };
        }

        protected void resetData()
        {
            duration = 0;
            bitrate = 0;

            //            formatID = 0;
            channelNumber = 0;
            sampleRate = 0;
            bytesPerSecond = 0;
            //            blockAlign = 0;
            bitsPerSample = 0;
            sampleNumber = 0;
            headerSize = 0;

            ResetData();
        }

        public WAV(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private void parseBext(Stream source, ReadTagParams readTagParams)
        {
            string str;
            byte[] data = new byte[256];

            // Description
            source.Read(data, 0, 256);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) setMetaField("bext.description", str, readTagParams.ReadAllMetaFrames);

            // Originator
            source.Read(data, 0, 32);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 32).Trim());
            if (str.Length > 0) setMetaField("bext.originator", str, readTagParams.ReadAllMetaFrames);

            // OriginatorReference
            source.Read(data, 0, 32);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 32).Trim());
            if (str.Length > 0) setMetaField("bext.originatorReference", str, readTagParams.ReadAllMetaFrames);

            // OriginationDate
            source.Read(data, 0, 10);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 10).Trim());
            if (str.Length > 0) setMetaField("bext.originationDate", str, readTagParams.ReadAllMetaFrames);

            // OriginationTime
            source.Read(data, 0, 8);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 8).Trim());
            if (str.Length > 0) setMetaField("bext.originationTime", str, readTagParams.ReadAllMetaFrames);

            // TimeReference
            source.Read(data, 0, 8);
            long timeReference = StreamUtils.DecodeUInt64(data);
            setMetaField("bext.timeReference", timeReference.ToString(), readTagParams.ReadAllMetaFrames);

            // BEXT version
            source.Read(data, 0, 2);
            int intData = StreamUtils.DecodeUInt16(data);
            setMetaField("bext.version", intData.ToString(), readTagParams.ReadAllMetaFrames);

            // UMID
            source.Read(data, 0, 64);
            str = "";

            int usefulLength = 32; // "basic" UMID
            if (data[12] > 19) usefulLength = 64; // data[12] gives the size of remaining UMID
            for (int i = 0; i < usefulLength; i++) str = str + data[i].ToString("X2");

            setMetaField("bext.UMID", str, readTagParams.ReadAllMetaFrames);

            // LoudnessValue
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            setMetaField("bext.loudnessValue", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // LoudnessRange
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            setMetaField("bext.loudnessRange", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // MaxTruePeakLevel
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            setMetaField("bext.maxTruePeakLevel", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // MaxMomentaryLoudness
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            setMetaField("bext.maxMomentaryLoudness", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // MaxShortTermLoudness
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            setMetaField("bext.maxShortTermLoudness", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // Reserved
            source.Seek(180, SeekOrigin.Current);

            // CodingHistory
            long initialPos = source.Position;
            if (StreamUtils.FindSequence(source, new byte[2] { 13, 10 } /* CR LF */ ))
            {
                long endPos = source.Position - 2;
                source.Seek(initialPos, SeekOrigin.Begin);

                if (data.Length < (int)(endPos - initialPos)) data = new byte[(int)(endPos - initialPos)];
                source.Read(data, 0, (int)(endPos - initialPos));

                str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, (int)(endPos - initialPos)).Trim());
                if (str.Length > 0) setMetaField("bext.codingHistory", str, readTagParams.ReadAllMetaFrames);
            }
        }

        private void parseInfo(Stream source, ReadTagParams readTagParams)
        {
            // TODO
        }

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

            riffChunkSizePos = source.Position;
            source.Read(data, 0, 4);
            if (isLittleEndian) riffChunkSize = StreamUtils.DecodeUInt32(data); else riffChunkSize = StreamUtils.DecodeBEUInt32(data);

            // Format code
            source.Read(data, 0, 4);
            str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(FORMAT_WAVE)) return false;


            string subChunkId;
            uint chunkSize;
            long chunkDataPos;
            bool foundBext = false;
            bool foundInfo = false;

            // Sub-chunks loop
            while (source.Position < riffChunkSize + 8)
            {
                // Chunk ID
                source.Read(data, 0, 4);
                if (0 == data[0]) // Sometimes data segment ends with a parasite null byte
                {
                    source.Seek(-3, SeekOrigin.Current);
                    source.Read(data, 0, 4);
                }

                subChunkId = Utils.Latin1Encoding.GetString(data);

                // Chunk size
                source.Read(data, 0, 4);
                if (isLittleEndian) chunkSize = StreamUtils.DecodeUInt32(data); else chunkSize = StreamUtils.DecodeBEUInt32(data);

                chunkDataPos = source.Position;

                if (subChunkId.Equals(CHUNK_FORMAT))
                {
                    source.Seek(2, SeekOrigin.Current); // FormatId

                    source.Read(data, 0, 2);
                    if (isLittleEndian) channelNumber = StreamUtils.DecodeUInt16(data); else channelNumber = StreamUtils.DecodeBEUInt16(data);
                    if (channelNumber != WAV_CM_MONO && channelNumber != WAV_CM_STEREO) return false;

                    source.Read(data, 0, 4);
                    if (isLittleEndian) sampleRate = StreamUtils.DecodeUInt32(data); else sampleRate = StreamUtils.DecodeBEUInt32(data);

                    source.Read(data, 0, 4);
                    if (isLittleEndian) bytesPerSecond = StreamUtils.DecodeUInt32(data); else bytesPerSecond = StreamUtils.DecodeBEUInt32(data);

                    source.Seek(2, SeekOrigin.Current); // BlockAlign

                    source.Read(data, 0, 2);
                    if (isLittleEndian) bitsPerSample = StreamUtils.DecodeUInt16(data); else bitsPerSample = StreamUtils.DecodeBEUInt16(data);
                }
                else if (subChunkId.Equals(CHUNK_DATA))
                {
                    headerSize = riffChunkSize - chunkSize;
                }
                else if (subChunkId.Equals(CHUNK_FACT))
                {
                    source.Read(data, 0, 4);
                    if (isLittleEndian) sampleNumber = StreamUtils.DecodeInt32(data); else sampleNumber = StreamUtils.DecodeBEInt32(data);
                }
                else if (subChunkId.Equals(CHUNK_BEXT))
                {
                    structureHelper.AddZone(source.Position - 8, (int)chunkSize, subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSizePos, subChunkId);

                    foundBext = true;
                    tagExists = true;

                    parseBext(source, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_INFO))
                {
                    structureHelper.AddZone(source.Position - 8, (int)chunkSize, subChunkId);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSizePos, subChunkId);

                    foundInfo = true;
                    tagExists = true;

                    parseInfo(source, readTagParams);
                }
                else if (subChunkId.Equals(CHUNK_IXML))
                {
                }

                source.Seek(chunkDataPos + chunkSize, SeekOrigin.Begin);
            }

            // Add zone placeholders for future tag writing
            if (readTagParams.PrepareForWriting)
            {
                if (!foundBext)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_BEXT);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSizePos, CHUNK_BEXT);
                }
                if (!foundInfo)
                {
                    structureHelper.AddZone(source.Position, 0, CHUNK_BEXT);
                    structureHelper.AddSize(riffChunkSizePos, riffChunkSizePos, CHUNK_BEXT);
                }
            }

            return result;
        }

        /* Unused for now
		private String getFormat()
		{
			// Get format type name
			switch (formatID)
			{
				case 1: return WAV_FORMAT_PCM;
				case 2: return WAV_FORMAT_ADPCM;
				case 6: return WAV_FORMAT_ALAW;
				case 7: return WAV_FORMAT_MULAW;
				case 17: return WAV_FORMAT_DVI_IMA_ADPCM;
				case 85: return WAV_FORMAT_MP3;
				default : return "";  
			}
		}

		private String getChannelMode()
		{
			// Get channel mode name
			return WAV_MODE[channelNumber];
		}
        */

        private double getDuration()
        {
            // Get duration
            double result = 0;

            if ((sampleNumber == 0) && (bytesPerSecond > 0))
                result = (double)(sizeInfo.FileSize - headerSize - sizeInfo.ID3v1Size) / bytesPerSecond;
            if ((sampleNumber > 0) && (sampleRate > 0))
                result = (double)(sampleNumber / sampleRate);

            return result;
        }

        private double getBitrate()
        {
            return Math.Round((double)this.bitsPerSample * this.sampleRate * this.channelNumber);
        }

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
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

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;

            if (zone.Equals(CHUNK_BEXT)) result += writeBextChunk(tag, w);

            return result;
        }

        private int writeBextChunk(TagData tag, BinaryWriter w)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_BEXT));

            long sizePos = w.BaseStream.Position;
            w.Write((int)0); // Placeholder for chunk size that will be rewritten at the end of the method

            IDictionary<string, string> additionalFields = AdditionalFields;


            // Text fields
            writeFixedFieldStrValue("bext.description", 256, additionalFields, w);
            writeFixedFieldStrValue("bext.originator", 32, additionalFields, w);
            writeFixedFieldStrValue("bext.originatorReference", 32, additionalFields, w);
            writeFixedFieldStrValue("bext.originationDate", 10, additionalFields, w);
            writeFixedFieldStrValue("bext.originationTime", 8, additionalFields, w);


            // Numeric fields
            ulong ulongVal = 0;
            if (additionalFields.Keys.Contains("bext.timeReference"))
            {
                if (Utils.IsNumeric(additionalFields["bext.timeReference"], true))
                {
                    ulongVal = ulong.Parse(additionalFields["bext.timeReference"]);
                }
            }
            w.Write(ulongVal);

            ushort ushortVal = 0;
            if (additionalFields.Keys.Contains("bext.version"))
            {
                if (Utils.IsNumeric(additionalFields["bext.version"], true))
                {
                    ushortVal = ushort.Parse(additionalFields["bext.version"]);
                }
            }
            w.Write(ushortVal);

            byte[] data = new byte[64];
            if (additionalFields.Keys.Contains("bext.UMID"))
            {
                string fieldValue = additionalFields["bext.UMID"];
                string hexValue = "";

                for (int i = 0; i < fieldValue.Length / 2; i++)
                {
                    hexValue += fieldValue[(2*i)] + fieldValue[(2*i) + 1];
                }
            }
            w.Write(data);



            // Rewrite chunk size header
            long finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            if (isLittleEndian)
            {
                w.Write((int)(finalPos - sizePos - 4));
            } else
            {
                w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));
            }

            return 14;
        }

        private void writeFixedFieldStrValue(string field, int length, IDictionary<string, string> additionalFields, BinaryWriter w)
        {
            if (additionalFields.Keys.Contains(field))
            {
                w.Write(Utils.BuildStrictLengthStringBytes(additionalFields[field], length, 0, Utils.Latin1Encoding));
            }
            else
            {
                w.Write(Utils.BuildStrictLengthString("", length, '\0'));
            }
        }

    }
}