using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.ChannelsArrangements;
using static ATL.TagData;

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
        private static readonly Dictionary<uint, ChannelsArrangement> channelsMapping = new Dictionary<uint, ChannelsArrangement>() {
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

        // Mapping between CAF format ID, ATL format ID and custom format names
        private static readonly Dictionary<string, KeyValuePair<int, string>> formatsMapping = new Dictionary<string, KeyValuePair<int, string>>()
        {
            { "none", new KeyValuePair<int, string>(Format.UNKNOWN_FORMAT.ID,"") },
            { "lpcm", new KeyValuePair<int, string>(AudioDataIOFactory.CID_WAV,"Linear PCM") },
            { "ima4", new KeyValuePair<int, string>(AudioDataIOFactory.CID_WAV,"Apple IMA 4:1 ADPCM") },
            { "aac ", new KeyValuePair<int, string>(AudioDataIOFactory.CID_AAC, "MPEG-4 AAC") },
            { "MAC3", new KeyValuePair<int, string>(Format.UNKNOWN_FORMAT.ID,"MACE 3:1") }, // Macintosh Audio Compression
            { "MAC6", new KeyValuePair<int, string>(Format.UNKNOWN_FORMAT.ID, "MACE 6:1") },
            { "ulaw", new KeyValuePair<int, string>(AudioDataIOFactory.CID_WAV,"μLaw 2:1") },
            { "alaw", new KeyValuePair<int, string>(AudioDataIOFactory.CID_WAV,"aLaw 2:1") },
            { ".mp1", new KeyValuePair<int, string>(AudioDataIOFactory.CID_MPEG,"MPEG-1 or 2, Layer 1") },
            { ".mp2", new KeyValuePair<int, string>(AudioDataIOFactory.CID_MPEG,"MPEG-1 or 2, Layer 2") },
            { ".mp3", new KeyValuePair<int, string>(AudioDataIOFactory.CID_MPEG,"MPEG-1 or 2, Layer 3") },
            { "alac", new KeyValuePair<int, string>(AudioDataIOFactory.CID_MP4,"Apple Lossless") }
        };

        // Mapping between CAF information keys and ATL frame codes
        private static readonly Dictionary<string, Field> frameMapping = new Dictionary<string, Field>() {
            { "artist", Field.ARTIST },
            { "album", Field.ALBUM },
            { "track number", Field.TRACK_NUMBER },
            { "year", Field.PUBLISHING_DATE },
            { "composer", Field.COMPOSER },
            { "genre", Field.GENRE },
            { "title", Field.TITLE },
            { "recorded date", Field.RECORDING_DATE },
            { "comments", Field.COMMENT },
            { "copyright", Field.COPYRIGHT }
        };


        // Private declarations 
        private AudioFormat audioFormat;

        private uint sampleRate;
        private bool isVbr;

        private uint channelsPerFrame;
        private uint bitsPerChannel;
        double secondsPerByte;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public bool IsVBR => isVbr;

        public AudioFormat AudioFormat => audioFormat;

        public int CodecFamily { get; private set; }

        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => (int)(bitsPerChannel * channelsPerFrame);

        public double Duration { get; private set; }

        public int SampleRate => (int)sampleRate;

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE };
        }

        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;

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

            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            sampleRate = 0;
            Duration = 0;
            BitRate = 0;
            isVbr = false;
            CodecFamily = 0;
            bitsPerChannel = 0;
            channelsPerFrame = 0;
            ChannelsArrangement = null;
            secondsPerByte = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public CAF(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            audioFormat = format;
            resetData();
        }

        public static bool IsValidHeader(byte[] data)
        {
            return CAF_MAGIC_NUMBER == StreamUtils.DecodeBEUInt32(data);
        }


        // ---------- SUPPORT METHODS

        private bool readFileHeader(BufferedBinaryReader source)
        {
            if (!IsValidHeader(source.ReadBytes(4))) return false;

            AudioDataOffset = source.Position - 4;
            source.Seek(4, SeekOrigin.Current); // Useless here

            return true;
        }

        private void readAudioDescriptionChunk(BufferedBinaryReader source)
        {
            double m_sampleRate = StreamUtils.DecodeBEDouble(source.ReadBytes(8)); // aka frames per second
            string formatId = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
            source.Seek(4, SeekOrigin.Current); // format flags
            uint bytesPerPacket = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            uint framesPerPacket = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            channelsPerFrame = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            bitsPerChannel = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            sampleRate = (uint)Math.Round(m_sampleRate);

            // Compute audio duration
            if (bytesPerPacket > 0)
            {
                double secondsPerPacket = framesPerPacket / m_sampleRate;
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
            CodecFamily = AudioDataIOFactory.CF_LOSSY;
            switch (formatId)
            {
                case ("lpcm"):
                case ("alac"):
                    CodecFamily = AudioDataIOFactory.CF_LOSSLESS;
                    break;
            }

            // Set audio data format
            var format = formatsMapping.TryGetValue(formatId, out var value) ? value : formatsMapping["none"];
            var audioDataFormat = new Format(format.Key, format.Value, format.Value, false);
            audioFormat = new AudioFormat(audioFormat);
            audioFormat.DataFormat = audioDataFormat;

            var containerFormat = AudioDataIOFactory.GetInstance().getFormat(audioFormat.ContainerId);
            if (null == containerFormat) audioFormat.Name = audioFormat.DataFormat.ShortName;
            else audioFormat.Name = containerFormat.Name + " / " + audioFormat.DataFormat.ShortName;

            audioFormat.ComputeId();
        }

        private void readChannelLayoutChunk(BufferedBinaryReader source)
        {
            uint channelLayout = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            // we don't need anything else

            if (channelsMapping.TryGetValue(channelLayout, out var value)) ChannelsArrangement = value;
        }

        // WARNING : EXPERIMENTAL / UNTESTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        private void readStringChunk(BufferedBinaryReader source, string id, long chunkSize)
        {
            string cookieStr = Utils.Latin1Encoding.GetString(source.ReadBytes((int)chunkSize));
            SetMetaField(id, cookieStr, true);
        }

        // WARNING : EXPERIMENTAL / UNTESTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        private void readStringsChunk(BufferedBinaryReader source)
        {
            uint nbEntries = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            Dictionary<uint, long> stringIds = new Dictionary<uint, long>();
            for (int i = 0; i < nbEntries; i++)
            {
                uint stringId = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                long stringOffset = StreamUtils.DecodeBEInt64(source.ReadBytes(8));
                stringIds.Add(stringId, stringOffset);
            }
            long initialPos = source.Position;

            string stringValue;
            foreach (uint id in stringIds.Keys)
            {
                source.Seek(initialPos + stringIds[id], SeekOrigin.Begin);
                stringValue = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                SetMetaField("str-" + id, stringValue, true);
            }
        }

        // WARNING : EXPERIMENTAL / UNTESTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        private void readInfoChunk(BufferedBinaryReader source, bool readAllMetaFrames)
        {
            uint nbEntries = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            for (int i = 0; i < nbEntries; i++)
            {
                string infoKey = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                string infoVal = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                SetMetaField(infoKey, infoVal, readAllMetaFrames);
            }
        }

        private void readPaktChunk(BufferedBinaryReader source)
        {
            source.Seek(8, SeekOrigin.Current); // nbPackets
            long nbFrames = StreamUtils.DecodeBEInt64(source.ReadBytes(8));

            Duration = nbFrames * 1000d / sampleRate;
        }

        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            ResetData();
            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            reader.Seek(0, SeekOrigin.Begin);

            bool result = readFileHeader(reader);
            long cursorPos = reader.Position;
            long audioChunkSize = 0;

            // Iterate through chunks
            while (cursorPos < reader.Length)
            {
                string chunkType = Utils.Latin1Encoding.GetString(reader.ReadBytes(4));
                long chunkSize = StreamUtils.DecodeBEInt64(reader.ReadBytes(8));

                if (readTagParams.PrepareForWriting) structureHelper.AddZone(cursorPos, chunkSize + 12, chunkType);

                switch (chunkType)
                {
                    case CHUNK_AUDIO_DESC:
                        readAudioDescriptionChunk(reader);
                        break;
                    case CHUNK_CHANNEL_LAYOUT:
                        readChannelLayoutChunk(reader);
                        break;
                    case CHUNK_COOKIE:
                    case CHUNK_UMID:
                        if (readTagParams.PrepareForWriting || readTagParams.ReadAllMetaFrames) readStringChunk(reader, chunkType, chunkSize);
                        break;
                    case CHUNK_STRINGS:
                        if (readTagParams.PrepareForWriting || readTagParams.ReadAllMetaFrames) readStringsChunk(reader);
                        break;
                    case CHUNK_INFO:
                        readInfoChunk(reader, readTagParams.PrepareForWriting || readTagParams.ReadAllMetaFrames);
                        break;
                    case CHUNK_AUDIO:
                        AudioDataOffset = cursorPos;
                        audioChunkSize = chunkSize;
                        AudioDataSize = chunkSize + 12;
                        if (secondsPerByte > 0) Duration = chunkSize * secondsPerByte * 1000;
                        break;
                    case CHUNK_PACKET_TABLE:
                        if (Utils.ApproxEquals(secondsPerByte, 0)) readPaktChunk(reader);
                        break;
                }
                reader.Seek(cursorPos + chunkSize + 12, SeekOrigin.Begin);
                cursorPos = reader.Position;
            }
            BitRate = audioChunkSize * 8d / Duration;
            if (null == ChannelsArrangement)
            {
                if (1 == channelsPerFrame) ChannelsArrangement = MONO;
                else if (2 == channelsPerFrame) ChannelsArrangement = STEREO;
                else ChannelsArrangement = new ChannelsArrangement((int)channelsPerFrame, "Custom layout (" + channelsPerFrame + " channels)");
            }

            return result;
        }

        // WARNING : NOT IMPLEMENTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        /// <inheritdoc/>
        protected override int write(TagData tag, Stream s, string zone)
        {
            throw new NotImplementedException();
        }
    }
}