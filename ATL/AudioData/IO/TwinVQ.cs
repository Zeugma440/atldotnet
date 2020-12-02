using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Collections.Generic;
using System.Text;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for TwinVQ files manipulation (extension : .VQF)
    /// </summary>
	class TwinVQ : MetaDataIO, IAudioDataIO
    {
        // Twin VQ header ID
        private const string TWIN_ID = "TWIN";

        // Mapping between TwinVQ frame codes and ATL frame codes
        private static IDictionary<string, byte> frameMapping = new Dictionary<string, byte>
        {
            { "NAME", TagData.TAG_FIELD_TITLE },
            { "ALBM", TagData.TAG_FIELD_ALBUM },
            { "AUTH", TagData.TAG_FIELD_ARTIST },
            { "(c) ", TagData.TAG_FIELD_COPYRIGHT },
            { "MUSC", TagData.TAG_FIELD_COMPOSER },
            { "CDCT", TagData.TAG_FIELD_CONDUCTOR },
            { "TRCK", TagData.TAG_FIELD_TRACK_NUMBER }, // Unofficial; found in sample files
            { "DATE", TagData.TAG_FIELD_RECORDING_DATE }, // Unofficial; found in sample files
            { "GENR", TagData.TAG_FIELD_GENRE }, // Unofficial; found in sample files
            { "COMT", TagData.TAG_FIELD_COMMENT }
            // TODO - handle integer extension sub-chunks : YEAR, TRAC
        };


        // Private declarations
        private int sampleRate;
        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        public bool Corrupted // True if file corrupted
        {
            get { return this.isCorrupted(); }
        }
        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }


        // TwinVQ chunk header
        private class ChunkHeader
        {
            public string ID;
            public uint Size;                                            // Chunk size
            public void Reset()
            {
                Size = 0;
            }
        }

        // File header data - for internal use
        private class HeaderInfo
        {
            // Real structure of TwinVQ file header
            public char[] ID = new char[4];                           // Always "TWIN"
            public char[] Version = new char[8];                         // Version ID
            public uint Size;                                           // Header size
            public ChunkHeader Common = new ChunkHeader();      // Common chunk header
            public uint ChannelMode;             // Channel mode: 0 - mono, 1 - stereo
            public uint BitRate;                                     // Total bit rate
            public uint SampleRate;                               // Sample rate (khz)
            public uint SecurityLevel;                                     // Always 0
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate // Sample rate (hz)
        {
            get { return this.sampleRate; }
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
            get { return AudioDataIOFactory.CF_LOSSY; }
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
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE) || (metaDataType == MetaDataIOFactory.TAG_ID3V1);
        }

        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }
        public override byte FieldCodeFixedLength
        {
            get { return 4; }
        }
        protected override bool isLittleEndian
        {
            get { return false; }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            duration = 0;
            bitrate = 0;
            isValid = false;
            sampleRate = 0;

            ResetData();
        }

        public TwinVQ(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private static bool readHeader(BinaryReader source, ref HeaderInfo Header)
        {
            bool result = true;

            // Read header and get file size
            Header.ID = source.ReadChars(4);
            Header.Version = source.ReadChars(8);
            Header.Size = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.Common.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
            Header.Common.Size = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.ChannelMode = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.BitRate = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.SampleRate = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.SecurityLevel = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            return result;
        }

        private static ChannelsArrangement getChannelArrangement(HeaderInfo Header)
        {
            switch (Header.ChannelMode)
            {
                case 0: return MONO;
                case 1: return STEREO;
                default: return new ChannelsArrangement((int)Header.ChannelMode);
            }
        }

        private static uint getBitRate(HeaderInfo Header)
        {
            return Header.BitRate;
        }

        private int GetSampleRate(HeaderInfo Header)
        {
            int result = (int)Header.SampleRate;
            switch (result)
            {
                case 11: result = 11025; break;
                case 22: result = 22050; break;
                case 44: result = 44100; break;
                default: result = (ushort)(result * 1000); break;
            }
            return result;
        }

        // Get duration from header
        private double getDuration(HeaderInfo Header)
        {
            return Math.Abs(sizeInfo.FileSize - Header.Size - 20) * 1000.0 / 125.0 / (double)Header.BitRate;
        }

        private static bool headerEndReached(ChunkHeader Chunk)
        {
            // Check for header end
            return (((byte)(Chunk.ID[0]) < 32) ||
                ((byte)(Chunk.ID[1]) < 32) ||
                ((byte)(Chunk.ID[2]) < 32) ||
                ((byte)(Chunk.ID[3]) < 32) ||
                "DSIZ".Equals(Chunk.ID));
        }

        private void readTag(BinaryReader source, HeaderInfo Header, ReadTagParams readTagParams)
        {
            ChunkHeader chunk = new ChunkHeader();
            string data;
            bool first = true;
            long tagStart = -1;

            source.BaseStream.Seek(40, SeekOrigin.Begin);
            do
            {
                // Read chunk header (length : 8 bytes)
                chunk.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                chunk.Size = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

                // Read chunk data and set tag item if chunk header valid
                if (headerEndReached(chunk)) break;

                if (first)
                {
                    tagStart = source.BaseStream.Position - 8;
                    first = false;
                }
                tagExists = true; // If something else than mandatory info is stored, we can consider metadata is present
                data = Encoding.UTF8.GetString(source.ReadBytes((int)chunk.Size)).Trim();

                SetMetaField(chunk.ID, data, readTagParams.ReadAllMetaFrames);
            }
            while (source.BaseStream.Position < source.BaseStream.Length);

            if (readTagParams.PrepareForWriting)
            {
                // Zone goes from the first field after COMM to the last field before DSIZ
                if (-1 == tagStart) structureHelper.AddZone(source.BaseStream.Position - 8, 0);
                else structureHelper.AddZone(tagStart, (int)(source.BaseStream.Position - tagStart - 8));
                structureHelper.AddSize(12, Header.Size);
            }
        }

        private bool isCorrupted()
        {
            // Check for file corruption
            return ((isValid) &&
                ((0 == channelsArrangement.NbChannels) ||
                (bitrate < 8000) || (bitrate > 192000) ||
                (sampleRate < 8000) || (sampleRate > 44100) ||
                (duration < 0.1) || (duration > 10000)));
        }

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            HeaderInfo Header = new HeaderInfo();

            resetData();
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            bool result = readHeader(source, ref Header);
            // Process data if loaded and header valid
            if (result && StreamUtils.StringEqualsArr(TWIN_ID, Header.ID))
            {
                isValid = true;
                // Fill properties with header data
                channelsArrangement = getChannelArrangement(Header);
                bitrate = getBitRate(Header);
                sampleRate = GetSampleRate(Header);
                duration = getDuration(Header);
                // Get tag information and fill properties
                readTag(source, Header, readTagParams);
            }
            return result;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;
            string recordingYear = "";

            IDictionary<byte, string> map = tag.ToMap();

            // 1st pass to gather date information
            foreach (byte frameType in map.Keys)
            {
                if (map[frameType].Length > 0 && TagData.TAG_FIELD_RECORDING_YEAR == frameType) // No frame with empty value
                {
                    recordingYear = map[frameType];
                }
            }
            if (recordingYear.Length > 0)
            {
                string recordingDate = Utils.ProtectValue(tag.RecordingDate);
                if (0 == recordingDate.Length || !recordingDate.StartsWith(recordingYear)) map[TagData.TAG_FIELD_RECORDING_DATE] = recordingYear;
            }

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    if (frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, tag, map);
                            writeTextFrame(w, s, value);
                            result++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion && fieldInfo.NativeFieldCode.Length > 0)
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                    result++;
                }
            }

            return result;
        }

        private void writeTextFrame(BinaryWriter writer, string frameCode, string text)
        {
            writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            writer.Write(StreamUtils.EncodeBEUInt32((uint)textBytes.Length));
            writer.Write(textBytes);
        }

        // Specific implementation for conservation of fields that are required for playback
        public override bool Remove(BinaryWriter w)
        {
            TagData tag = new TagData();

            foreach (byte b in frameMapping.Values)
            {
                tag.IntegrateValue(b, "");
            }

            string fieldCode;
            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                fieldCode = fieldInfo.NativeFieldCode.ToLower();
                if (!fieldCode.StartsWith("_") && !fieldCode.Equals("DSIZ") && !fieldCode.Equals("COMM"))
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    tag.AdditionalFields.Add(emptyFieldInfo);
                }
            }

            BinaryReader r = new BinaryReader(w.BaseStream);
            return Write(r, w, tag);
        }

    }
}