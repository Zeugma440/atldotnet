using System;
using System.Buffers.Binary;
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

        public static bool IsValidHeader(ReadOnlySpan<byte> data)
        {
            return 0x7FFE8001 == BinaryPrimitives.ReadUInt32BigEndian(data);
        }

        /// <inheritdoc/>
        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            Span<byte> buffer = stackalloc byte[4];

            resetData();

            if (source.Read(buffer) < 4) return false;
            if (!IsValidHeader(buffer)) return false;

            AudioDataOffset = source.Position - 4;
            AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

            source.Seek(AudioDataOffset + 4, SeekOrigin.Begin);

            Span<byte> span = stackalloc byte[7];
            if (source.Read(span) != 7) return false;

            uint cpf = StreamUtils.ReadBitsBE(span, 6, 1);

            uint amode = StreamUtils.ReadBitsBE(span, 28, 6);

            uint sfreq = StreamUtils.ReadBitsBE(span, 34, 4);
            sampleRate = sfreq switch
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

            uint value = StreamUtils.ReadBitsBE(span, 38, 5); // RATE
            BitRate = (ushort)BITRATES[value];

            value = StreamUtils.ReadBitsBE(span, 48, 3); // EXT_AUDIO_ID
            uint extAudio = StreamUtils.ReadBitsBE(span, 51, 1); // EXT_AUDIO
            if (1 == extAudio && 2 == value) sampleRate = 96000; // X96 frequency extension

            value = StreamUtils.ReadBitsBE(span, 53, 2); // LFF
            bool isLfePresent = (1 == value || 2 == value);
            ChannelsArrangement = getChannelsArrangement(amode, isLfePresent);

            long filtsOffset = AudioDataOffset + 11 + ((1 == cpf) ? 2 : 0);
            source.Seek(filtsOffset, SeekOrigin.Begin);
            if (source.Read(span) != 7) return false;
            value = StreamUtils.ReadBitsBE(span, 7, 3); // PCMR
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
