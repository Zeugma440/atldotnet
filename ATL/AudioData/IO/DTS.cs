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
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // Public declarations
        public double CompressionRatio
        {
            get { return getCompressionRatio(); }
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

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
        public int SampleRate
        {
            get { return (int)sampleRate; }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }
        public int BitDepth => (int)bits;
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
            return false;
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bits = 0;
            sampleRate = 0;
            bitrate = 0;
            duration = 0;
            isValid = false;
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

        private double getCompressionRatio()
        {
            // Get compression ratio
            if (isValid)
                return (double)sizeInfo.FileSize / ((duration / 1000.0 * sampleRate) * (channelsArrangement.NbChannels * bits / 8) + 44) * 100;
            else
                return 0;
        }

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

        /// <inheritdoc/>
        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            uint value;
            bool result = false;

            this.sizeInfo = sizeInfo;

            resetData();

            uint signatureChunk = StreamUtils.DecodeBEUInt32(source.ReadBytes(4)); // SYNC
            if (0x7FFE8001 == signatureChunk) // Core substream
            {
                isValid = true;
                AudioDataOffset = source.BaseStream.Position - 4;
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

                result = true;
            }

            return result;
        }

    }
}