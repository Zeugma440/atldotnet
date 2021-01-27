using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Apple Core Audio files manipulation (extension : .CAF)
    /// 
    /// Implementation notes
    /// 
    ///     1. "Functional" metadata reading and writing
    /// 
    ///     Due to the rarity of CAF files with actual metadata (i.e. strg or info chunks) :
    ///       - the implementation of metadata reading is experimental, as in "theroretically working, but untested"
    ///       - there is no implementation for metadata writing
    ///       
    ///     Anyone who wants these features and has "rich" CAF files is welcome to open a new github issue about it.
    /// </summary>
    class CAF : MetaDataIO, IAudioDataIO
    {
        private const uint CAF_MAGIC_NUMBER = 1667327590; // 'caff'

        public const string CHUNK_AUDIO_DESC = "desc";
        public const string CHUNK_PACKET_TABLE = "pakt";
        public const string CHUNK_CHANNEL_LAYOUT = "chan";
        public const string CHUNK_COOKIE = "kuki";
        public const string CHUNK_STRINGS = "strg";
        public const string CHUNK_INFO = "info";
        public const string CHUNK_UMID = "umid";
        public const string CHUNK_AUDIO = "data";
        public const string CHUNK_PADDING = "free";

        // Mapping between CAF channels layout codes and ATL ChannelsArrangement
        private static Dictionary<uint, ChannelsArrangement> channelsMapping = new Dictionary<uint, ChannelsArrangement>() {
            { (100 << 16) | 1, MONO },
            { (101 << 16) | 2, STEREO },
            { (102 << 16) | 2, STEREO }, // Stereo / headphone playback
            { (103 << 16) | 2, STEREO_LEFT_RIGHT_TOTAL },
            { (104 << 16) | 2, JOINT_STEREO_MID_SIDE },
            { (105 << 16) | 2, STEREO_XY },
            { (106 << 16) | 2, STEREO_BINAURAL },
            { (107 << 16) | 4, AMBISONIC_B },
            { (108 << 16) | 4, QUAD },
            { (109 << 16) | 5, PENTAGONAL },
            { (110 << 16) | 6, HEXAGONAL },
            { (111 << 16) | 8, OCTAGONAL },
            { (112 << 16) | 8, CUBE },
            { (113 << 16) | 3, ISO_3_0_0 },
            { (114 << 16) | 3, ISO_3_0_0 }, // ATL doesn't differentiate multiple channel combinations
            { (115 << 16) | 4, LRCS },
            { (116 << 16) | 4, LRCS }, // ATL doesn't differentiate multiple channel combinations
            { (117 << 16) | 5, ISO_3_2_0 },
            { (118 << 16) | 5, ISO_3_2_0 }, // ATL doesn't differentiate multiple channel combinations
            { (119 << 16) | 5, ISO_3_2_0 }, // ATL doesn't differentiate multiple channel combinations
            { (120 << 16) | 5, ISO_3_2_0 }, // ATL doesn't differentiate multiple channel combinations
            { (121 << 16) | 6, ISO_3_2_1 },
            { (122 << 16) | 6, ISO_3_2_1 }, // ATL doesn't differentiate multiple channel combinations
            { (123 << 16) | 6, ISO_3_2_1 }, // ATL doesn't differentiate multiple channel combinations
            { (124 << 16) | 6, ISO_3_2_1 }, // ATL doesn't differentiate multiple channel combinations
            { (125 << 16) | 7, MPEG_6_1 },
            { (126 << 16) | 8, MPEG_7_1 },
            { (127 << 16) | 8, MPEG_7_1 }, // ATL doesn't differentiate multiple channel combinations
            { (128 << 16) | 8, MPEG_7_1 }, // ATL doesn't differentiate multiple channel combinations
            { (129 << 16) | 8, MPEG_7_1 }, // ATL doesn't differentiate multiple channel combinations
            { (130 << 16) | 8, SMPTE_DTV },
            { (131 << 16) | 3, ITU_2_1 },
            { (132 << 16) | 4, ITU_2_2 },
            { (133 << 16) | 3, DVD_4 },
            { (134 << 16) | 4, DVD_5 },
            { (135 << 16) | 5, DVD_6 },
            { (136 << 16) | 4, DVD_10 },
            { (137 << 16) | 5, DVD_11 },
            { (138 << 16) | 5, DVD_18 },
            { (139 << 16) | 6, AUDIOUNIT_6_0 },
            { (140 << 16) | 7, AUDIOUNIT_7_0 },
            { (141 << 16) | 6, AAC_6_0 },
            { (142 << 16) | 7, AAC_6_1 },
            { (143 << 16) | 7, AAC_7_0 },
            { (144 << 16) | 8, AAC_OCTAGONAL },
            { (145 << 16) | 16, TMH_10_2_STD },
            { (146 << 16) | 21, TMH_10_2_FULL }
        };

        // Mapping between CAF format ID and format names
        private static Dictionary<string, KeyValuePair<int, string>> formatsMapping = new Dictionary<string, KeyValuePair<int, string>>()
        {
            { "none", new KeyValuePair<int, string>(0,"") },
            { "lpcm", new KeyValuePair<int, string>(1,"Linear PCM") },
            { "ima4", new KeyValuePair<int, string>(2,"Apple IMA 4:1 ADPCM") },
            { "aac ", new KeyValuePair<int, string>(3,"MPEG-4 AAC") },
            { "MAC3", new KeyValuePair<int, string>(4,"MACE 3:1") },
            { "MAC6", new KeyValuePair<int, string>(5,"MACE 6:1") },
            { "ulaw", new KeyValuePair<int, string>(6,"μLaw 2:1") },
            { "alaw", new KeyValuePair<int, string>(7,"aLaw 2:1") },
            { ".mp1", new KeyValuePair<int, string>(8,"MPEG-1 or 2, Layer 1") },
            { ".mp2", new KeyValuePair<int, string>(9,"MPEG-1 or 2, Layer 2") },
            { ".mp3", new KeyValuePair<int, string>(10,"MPEG-1 or 2, Layer 3") },
            { "alac", new KeyValuePair<int, string>(11,"Apple Lossless") }
        };

        // Mapping between CAF information keys and ATL frame codes
        private static Dictionary<string, byte> frameMapping = new Dictionary<string, byte>() {
            { "artist", TagData.TAG_FIELD_ARTIST },
            { "album", TagData.TAG_FIELD_ALBUM },
            { "track number", TagData.TAG_FIELD_TRACK_NUMBER },
            { "year", TagData.TAG_FIELD_PUBLISHING_DATE },
            { "composer", TagData.TAG_FIELD_COMPOSER },
            { "genre", TagData.TAG_FIELD_GENRE },
            { "title", TagData.TAG_FIELD_TITLE },
            { "recorded date", TagData.TAG_FIELD_RECORDING_DATE },
            { "comments", TagData.TAG_FIELD_COMMENT },
            { "copyright", TagData.TAG_FIELD_COPYRIGHT }
        };


        // Private declarations 
        private Format containerAudioFormat;
        private KeyValuePair<int, string> containeeAudioFormat;

        private uint sampleRate;
        private bool isVbr;
        private int codecFamily;

        private double bitrate;
        private double duration;
        private uint channelsPerFrame;
        double secondsPerByte;
        private ChannelsArrangement channelsArrangement;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public bool IsVBR
        {
            get { return isVbr; }
        }
        public Format AudioFormat
        {
            get
            {
                containerAudioFormat = new Format(containerAudioFormat);
                containerAudioFormat.Name += " / " + containeeAudioFormat.Value;
                containerAudioFormat.ID += containeeAudioFormat.Key;
                return containerAudioFormat;
            }
        }
        public int CodecFamily
        {
            get { return codecFamily; }
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
        public int SampleRate
        {
            get { return (int)sampleRate; }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE);
        }
        public long HasEmbeddedID3v2
        {
            get { return -2; }
        }


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

            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            sampleRate = 0;
            duration = 0;
            bitrate = 0;
            isVbr = false;
            codecFamily = 0;
            channelsPerFrame = 0;
            channelsArrangement = null;
            secondsPerByte = 0;
        }

        public CAF(string filePath, Format format)
        {
            this.filePath = filePath;
            containerAudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private bool readFileHeader(BinaryReader source)
        {
            uint fileType = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            if (fileType != CAF_MAGIC_NUMBER) return false;

            source.BaseStream.Seek(4, SeekOrigin.Current); // Useless here

            return true;
        }

        private void readAudioDescriptionChunk(BinaryReader source)
        {
            double sampleRate = StreamUtils.DecodeBEDouble(source.ReadBytes(8)); // aka frames per second
            string formatId = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
            uint formatFlags = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            uint bytesPerPacket = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            uint framesPerPacket = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            channelsPerFrame = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            uint bitsPerChannel = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            this.sampleRate = (uint)Math.Round(sampleRate);

            // Compute audio duration
            if (bytesPerPacket > 0)
            {
                double secondsPerPacket = framesPerPacket / sampleRate;
                secondsPerByte = secondsPerPacket / bytesPerPacket;
                // Duration will be determined using the size of 'data' chunk
            }
            else
            {
                secondsPerByte = 0;
                isVbr = true;
                // Duration will be dertermiend using data from the 'pakt' chunk
            }

            // Determine audio properties according to the format ID
            codecFamily = AudioDataIOFactory.CF_LOSSY;
            switch (formatId)
            {
                case ("lpcm"):
                case ("alac"):
                    codecFamily = AudioDataIOFactory.CF_LOSSLESS;
                    break;
            }

            // Determine format
            if (formatsMapping.ContainsKey(formatId))
                containeeAudioFormat = formatsMapping[formatId];
            else
                containeeAudioFormat = formatsMapping["none"];
        }

        private void readChannelLayoutChunk(BinaryReader source)
        {
            uint channelLayout = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            // we don't need anything else

            if (channelsMapping.ContainsKey(channelLayout)) channelsArrangement = channelsMapping[channelLayout];
        }

        // WARNING : EXPERIMENTAL / UNTESTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        private void readStringChunk(BinaryReader source, string id, long chunkSize)
        {
            string cookieStr = Utils.Latin1Encoding.GetString(source.ReadBytes((int)chunkSize));
            SetMetaField(id, cookieStr, true);
        }

        // WARNING : EXPERIMENTAL / UNTESTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        private void readStringsChunk(BinaryReader source)
        {
            uint nbEntries = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            Dictionary<uint, long> stringIds = new Dictionary<uint, long>();
            for (int i = 0; i < nbEntries; i++)
            {
                uint stringId = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                long stringOffset = StreamUtils.DecodeBEInt64(source.ReadBytes(8));
                stringIds.Add(stringId, stringOffset);
            }
            long initialPos = source.BaseStream.Position;

            string stringValue;
            foreach (uint id in stringIds.Keys)
            {
                source.BaseStream.Seek(initialPos + stringIds[id], SeekOrigin.Begin);
                stringValue = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                SetMetaField("str-" + id, stringValue, true);
            }
        }

        // WARNING : EXPERIMENTAL / UNTESTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        private void readInfoChunk(BinaryReader source, bool readAllMetaFrames)
        {
            uint nbEntries = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            for (int i = 0; i < nbEntries; i++)
            {
                string infoKey = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                string infoVal = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                SetMetaField(infoKey, infoVal, readAllMetaFrames);
            }
        }

        private void readPaktChunk(BinaryReader source)
        {
            long nbPackets = StreamUtils.DecodeBEInt64(source.ReadBytes(8));
            long nbFrames = StreamUtils.DecodeBEInt64(source.ReadBytes(8));

            //duration = nbFrames * channelsPerFrame * bitsPerChannel / 8;
            duration = nbFrames * 1000d / sampleRate;
        }

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;
            ResetData();

            bool result = false;

            source.BaseStream.Seek(0, SeekOrigin.Begin);

            result = readFileHeader(source);

            long cursorPos = source.BaseStream.Position;
            long audioChunkSize = 0;

            // Iterate through chunks
            while (cursorPos < source.BaseStream.Length)
            {
                string chunkType = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                long chunkSize = StreamUtils.DecodeBEInt64(source.ReadBytes(8));

                if (readTagParams.PrepareForWriting) structureHelper.AddZone(cursorPos, chunkSize + 12, chunkType);

                switch (chunkType)
                {
                    case CHUNK_AUDIO_DESC:
                        readAudioDescriptionChunk(source);
                        break;
                    case CHUNK_CHANNEL_LAYOUT:
                        readChannelLayoutChunk(source);
                        break;
                    case CHUNK_COOKIE:
                    case CHUNK_UMID:
                        if (readTagParams.PrepareForWriting || readTagParams.ReadAllMetaFrames) readStringChunk(source, chunkType, chunkSize);
                        break;
                    case CHUNK_STRINGS:
                        if (readTagParams.PrepareForWriting || readTagParams.ReadAllMetaFrames) readStringsChunk(source);
                        break;
                    case CHUNK_INFO:
                        readInfoChunk(source, readTagParams.PrepareForWriting || readTagParams.ReadAllMetaFrames);
                        break;
                    case CHUNK_AUDIO:
                        audioChunkSize = chunkSize;
                        if (secondsPerByte > 0) duration = chunkSize * secondsPerByte * 1000;
                        break;
                    case CHUNK_PACKET_TABLE:
                        if (0 == secondsPerByte) readPaktChunk(source);
                        break;
                }
                source.BaseStream.Seek(cursorPos + chunkSize + 12, SeekOrigin.Begin);
                cursorPos = source.BaseStream.Position;
            }
            bitrate = audioChunkSize * 8d / duration;
            if (null == channelsArrangement)
            {
                if (1 == channelsPerFrame) channelsArrangement = MONO;
                else if (2 == channelsPerFrame) channelsArrangement = STEREO;
                else channelsArrangement = new ChannelsArrangement((int)channelsPerFrame, "Custom layout (" + channelsPerFrame + " channels)");
            }

            return result;
        }

        // WARNING : NOT IMPLEMENTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            throw new System.NotImplementedException();
        }
    }
}