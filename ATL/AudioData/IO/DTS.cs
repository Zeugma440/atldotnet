using System.Collections.Generic;
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
        private static readonly int[] BITRATES = { 32, 56, 64, 96, 112, 128, 192, 224, 256,
                                                 320, 384, 448, 512, 576, 640, 768, 960,
                                                 1024, 1152, 1280, 1344, 1408, 1411, 1472,
                                                 1536, 1920, 2048, 3072, 3840, 0, -1, 1 };

        // Private declarations
        private uint bits;
        private uint sampleRate;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public AudioFormat AudioFormat { get; }
        public bool IsVBR => false;
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;
        public int SampleRate => (int)sampleRate;
        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => (int)bits;
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType>(); // No supported metas
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bits = 0;
            sampleRate = 0;
            BitRate = 0;
            Duration = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public DTS(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private static ChannelsArrangement getChannelsArrangement(uint amode, bool isLfePresent)
        {
            return amode switch
            {
                0 => MONO,
                1 => DUAL_MONO,
                2 => STEREO,
                3 => STEREO_SUM_DIFFERENCE,
                4 => STEREO_LEFT_RIGHT_TOTAL,
                5 => isLfePresent ? LRCLFE : ISO_3_0_0,
                6 => isLfePresent ? DVD_5 : ISO_2_1_0,
                7 => isLfePresent ? DVD_11 : LRCS,
                8 => isLfePresent ? DVD_18 : QUAD,
                9 => isLfePresent ? ISO_3_2_1 : ISO_3_2_0,
                10 => isLfePresent ? CLCRLRSLSR_LFE : CLCRLRSLSR,
                11 => isLfePresent ? CLRLRRRO_LFE : CLRLRRRO,
                12 => isLfePresent ? CFCRLFRFLRRR_LFE : CFCRLFRFLRRR,
                13 => isLfePresent ? CLCCRLRSLSR_LFE : CLCCRLRSLSR,
                14 => isLfePresent ? CLCRLRSL1SL2SR1SR2_LFE : CLCRLRSL1SL2SR1SR2,
                15 => isLfePresent ? CLCCRLRSLSSR_LFE : CLCCRLRSLSSR,
                _ => UNKNOWN
            };
        }

        public static bool IsValidHeader(byte[] data)
        {
            return 0x7FFE8001 == StreamUtils.DecodeBEUInt32(data);
        }

        /// <inheritdoc/>
        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[4];

            resetData();

            if (source.Read(buffer, 0, 4) < 4) return false;
            if (!IsValidHeader(buffer)) return false;

            AudioDataOffset = source.Position - 4;
            AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;
            int coreFrameBitOffset = (int)(AudioDataOffset * 8);

            uint cpf = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 38, 1); // CPF

            uint amode = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 60, 6); // AMODE

            var value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 66, 4);
            sampleRate = value switch
            {
                1 => 8000,
                2 => 16000,
                3 => 32000,
                6 => 11025,
                7 => 22050,
                8 => 44100,
                11 => 12000,
                12 => 24000,
                13 => 48000,
                _ => 0
            };

            value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 70, 5); // RATE
            BitRate = (ushort)BITRATES[value];

            value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 80, 3); // EXT_AUDIO_ID
            uint extAudio = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 83, 1); // EXT_AUDIO
            if (1 == extAudio && 2 == value) sampleRate = 96000; // X96 frequency extension

            value = StreamUtils.ReadBEBits(source, coreFrameBitOffset + 85, 2); // LFF
            bool isLfePresent = (1 == value || 2 == value);
            ChannelsArrangement = getChannelsArrangement(amode, isLfePresent);

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

            Duration = sizeNfo.FileSize * 8.0 / BitRate;

            return true;
        }

    }
}