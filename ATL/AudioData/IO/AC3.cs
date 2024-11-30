using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Dolby Digital files manipulation (extension : .AC3)
    /// </summary>
	class AC3 : IAudioDataIO
    {
        // Standard bitrates (KBit/s)
        private static readonly int[] BITRATES = new int[] { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160,
                                                        192, 224, 256, 320, 384, 448, 512, 576, 640 };

        // Private declarations 
        private uint sampleRate;

        private ChannelsArrangement channelsArrangement;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        /// <inheritdoc/>
        public bool IsVBR => false;
        /// <inheritdoc/>
        public AudioFormat AudioFormat { get; }
        /// <inheritdoc/>
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;
        /// <inheritdoc/>
        public string FileName { get; }
        /// <inheritdoc/>
        public double BitRate { get; private set; }
        /// <inheritdoc/>
        public double Duration { get; private set; }
        /// <inheritdoc/>
        public int SampleRate => (int)this.sampleRate;
        /// <inheritdoc/>
        public int BitDepth => -1; // Irrelevant for lossy formats
        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement => channelsArrangement;
        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.APE };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }
        /// <inheritdoc/>
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            sampleRate = 0;
            Duration = 0;
            BitRate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public AC3(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        public static bool IsValidHeader(byte[] data)
        {
            return 30475 == StreamUtils.DecodeUInt16(data);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            resetData();

            byte[] buffer = new byte[2];
            source.Seek(0, SeekOrigin.Begin);
            if (source.Read(buffer, 0, 2) < 2) return false;

            if (!IsValidHeader(buffer)) return false;

            AudioDataOffset = source.Position - 2;
            AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

            source.Seek(2, SeekOrigin.Current);
            if (source.Read(buffer, 0, 1) < 1) return false;

            sampleRate = (buffer[0] & 0xC0) switch
            {
                0 => 48000,
                0x40 => 44100,
                0x80 => 32000,
                _ => 0
            };

            BitRate = BITRATES[(buffer[0] & 0x3F) >> 1];

            source.Seek(1, SeekOrigin.Current);
            if (source.Read(buffer, 0, 1) < 1) return false;

            channelsArrangement = (buffer[0] & 0xE0) switch
            {
                0 => DUAL_MONO,
                0x20 => MONO,
                0x40 => STEREO,
                0x60 => ISO_3_0_0,
                0x80 => ISO_2_1_0,
                0xA0 => ISO_3_1_0,
                0xC0 => ISO_2_2_0,
                0xE0 => ISO_3_2_0,
                _ => UNKNOWN
            };

            Duration = sizeNfo.FileSize * 8.0 / BitRate;

            return true;
        }
    }
}