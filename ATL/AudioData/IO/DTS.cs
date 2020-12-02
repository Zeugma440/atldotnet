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
        public uint Bits
        {
            get { return bits; }
        }
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
            return false;
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bits = 0;
            sampleRate = 0;
            bitrate = 0;
            duration = 0;
            isValid = false;
        }

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

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            uint signatureChunk;
            ushort aWord;
            byte[] specDTS;
            bool result = false;

            this.sizeInfo = sizeInfo;

            resetData();

            signatureChunk = source.ReadUInt32();
            if ( /*0x7FFE8001*/ 25230975 == signatureChunk)
            {
                source.BaseStream.Seek(3, SeekOrigin.Current);
                specDTS = source.ReadBytes(8);

                isValid = true;

                aWord = (ushort)(specDTS[1] | (specDTS[0] << 8));

                switch ((aWord & 0x0FC0) >> 6)
                {
                    case 0: channelsArrangement = MONO; break;
                    case 1: channelsArrangement = DUAL_MONO; break;
                    case 2: channelsArrangement = STEREO; break;
                    case 3: channelsArrangement = STEREO_SUM_DIFFERENCE; break;
                    case 4: channelsArrangement = STEREO_LEFT_RIGHT_TOTAL; break;
                    case 5: channelsArrangement = ISO_3_0_0; break;
                    case 6: channelsArrangement = ISO_2_1_0; break;
                    case 7: channelsArrangement = LRCS; break;
                    case 8: channelsArrangement = QUAD; break;
                    case 9: channelsArrangement = ISO_3_2_0; break;
                    case 10: channelsArrangement = CLCRLRSLSR; break;
                    case 11: channelsArrangement = CLRLRRRO; break;
                    case 12: channelsArrangement = CFCRLFRFLRRR; break;
                    case 13: channelsArrangement = CLCCRLRSLSR; break;
                    case 14: channelsArrangement = CLCRLRSL1SL2SR1SR2; break;
                    case 15: channelsArrangement = CLCCRLRSLSSR; break;
                    default: channelsArrangement = UNKNOWN; break;
                }

                switch ((aWord & 0x3C) >> 2)
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

                aWord = (ushort)(specDTS[2] | (specDTS[1] << 8));
                bitrate = (ushort)BITRATES[(aWord & 0x03E0) >> 5];

                aWord = (ushort)(specDTS[7] | (specDTS[6] << 8));
                switch ((aWord & 0x01C0) >> 6)
                {
                    case 0:
                    case 1: bits = 16; break;
                    case 2:
                    case 3: bits = 20; break;
                    case 4:
                    case 5: bits = 24; break;
                    default: bits = 16; break;
                }

                duration = sizeInfo.FileSize * 8.0 / bitrate;

                result = true;
            }

            return result;
        }

    }
}