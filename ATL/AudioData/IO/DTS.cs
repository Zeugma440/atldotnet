using ATL.Logging;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Digital Theatre System files manipulation (extension : .DTS)
    /// </summary>
	class DTS : IAudioDataIO
    {
        // Standard bitrates (KBit/s)
        private static readonly int[] BITRATES = new int[32] { 32, 56, 64, 96, 112, 128, 192, 224, 256,
                                                        320, 384, 448, 512, 576, 640, 768, 960,
                                                        1024, 1152, 1280, 1344, 1408, 1411, 1472,
                                                        1536, 1920, 2048, 3072, 3840, 0, -1, 1 };

        // Private declarations
        private ChannelsArrangement channelsArrangement;
        private uint bits;
        private uint sampleRate;

        private double bitrate;
        private double duration;

        private readonly string filePath;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public Format AudioFormat
        {
            get;
        }
        public bool IsVBR => false;
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;
        public int SampleRate => (int)sampleRate;
        public string FileName => filePath;
        public double BitRate => bitrate;
        public int BitDepth => (int)bits;
        public double Duration => duration;
        public ChannelsArrangement ChannelsArrangement => channelsArrangement;
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType) => false;
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bits = 0;
            sampleRate = 0;
            bitrate = 0;
            duration = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public DTS(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private ChannelsArrangement getChannelsArrangement(uint amode, bool isLfePresent)
        {
            switch (amode)
            {
                case 0: return MONO;
                case 1: return DUAL_MONO;
                case 2: return STEREO;
                case 3: return STEREO_SUM_DIFFERENCE;
                case 4: return STEREO_LEFT_RIGHT_TOTAL;
                case 5: return isLfePresent ? LRCLFE : ISO_3_0_0;
                case 6: return isLfePresent ? DVD_5 : ISO_2_1_0;
                case 7: return isLfePresent ? DVD_11 : LRCS;
                case 8: return isLfePresent ? DVD_18 : QUAD;
                case 9: return isLfePresent ? ISO_3_2_1 : ISO_3_2_0;
                case 10: return isLfePresent ? CLCRLRSLSR_LFE : CLCRLRSLSR;
                case 11: return isLfePresent ? CLRLRRRO_LFE : CLRLRRRO;
                case 12: return isLfePresent ? CFCRLFRFLRRR_LFE : CFCRLFRFLRRR;
                case 13: return isLfePresent ? CLCCRLRSLSR_LFE : CLCCRLRSLSR;
                case 14: return isLfePresent ? CLCRLRSL1SL2SR1SR2_LFE : CLCRLRSL1SL2SR1SR2;
                case 15: return isLfePresent ? CLCCRLRSLSSR_LFE : CLCCRLRSLSSR;
                default: return UNKNOWN;
            }
        }

        public static bool IsValidHeader(byte[] data)
        {
            return 0x7FFE8001 == StreamUtils.DecodeBEUInt32(data);
        }

        /// <inheritdoc/>
        public bool Read(Stream source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            uint value;
            bool result = false;
            byte[] buffer = new byte[4];

            resetData();

            source.Read(buffer, 0, 4);
            if (IsValidHeader(buffer))
            {
                result = true;
                AudioDataOffset = source.Position - 4;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;
                int coreFrameBitOffset = (int)(AudioDataOffset * 8);

                uint cpf = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 38, 1); // CPF

                uint amode = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 60, 6); // AMODE

                value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 66, 4); // SFREQ
                switch (value)
                {
                    case 1: sampleRate = 8000; break;
                    case 2: sampleRate = 16000; break;
                    case 3: sampleRate = 32000; break;
                    case 6: sampleRate = 11025; break;
                    case 7: sampleRate = 22050; break;
                    case 8: sampleRate = 44100; break;
                    case 11: sampleRate = 12000; break;
                    case 12: sampleRate = 24000; break;
                    case 13: sampleRate = 48000; break;
                    default: sampleRate = 0; break;
                }

                value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 70, 5); // RATE
                bitrate = (ushort)BITRATES[value];

                value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 80, 3); // EXT_AUDIO_ID
                uint extAudio = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 83, 1); // EXT_AUDIO
                if (1 == extAudio && 2 == value) sampleRate = 96000; // X96 frequency extension

                value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 85, 2); // LFF
                bool isLfePresent = (1 == value || 2 == value);
                channelsArrangement = getChannelsArrangement(amode, isLfePresent);

                int filtsOffset = coreFrameBitOffset + 88 + ((1 == cpf) ? 16 : 0);
                value = StreamUtils.ReadBEBits(source, filtsOffset + 7, 3); // PCMR
                switch (value)
                {
                    case 0:
                    case 1: bits = 16; break;
                    case 2:
                    case 3: bits = 20; break;
                    case 6:
                    case 5: bits = 24; break; // This is not a typo; the table actually skips 4
                    default: bits = 16; break;
                }

                duration = sizeInfo.FileSize * 8.0 / bitrate;
            }

            return result;
        }

    }
}