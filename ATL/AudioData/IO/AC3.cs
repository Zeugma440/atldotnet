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
        private static readonly int[] BITRATES = new int[19] { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160,
                                                        192, 224, 256, 320, 384, 448, 512, 576, 640 };

        // Private declarations 
        private uint sampleRate;

        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;

        private readonly string filePath;


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
            get { return (int)this.sampleRate; }
        }

        public int BitDepth => -1; // Irrelevant for lossy formats

        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return metaDataType == MetaDataIOFactory.TagType.APE;
        }
        public long HasEmbeddedID3v2
        {
            get { return -2; }
        }

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            sampleRate = 0;
            duration = 0;
            bitrate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public AC3(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            ushort signatureChunk;
            byte aByte;
            resetData();

            bool result = false;

            source.BaseStream.Seek(0, SeekOrigin.Begin);
            signatureChunk = source.ReadUInt16();

            if (30475 == signatureChunk)
            {
                AudioDataOffset = source.BaseStream.Position - 2;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;

                source.BaseStream.Seek(2, SeekOrigin.Current);
                aByte = source.ReadByte();

                switch (aByte & 0xC0)
                {
                    case 0: sampleRate = 48000; break;
                    case 0x40: sampleRate = 44100; break;
                    case 0x80: sampleRate = 32000; break;
                    default: sampleRate = 0; break;
                }

                bitrate = BITRATES[(aByte & 0x3F) >> 1];

                source.BaseStream.Seek(1, SeekOrigin.Current);
                aByte = source.ReadByte();

                switch (aByte & 0xE0)
                {
                    case 0: channelsArrangement = DUAL_MONO; break;
                    case 0x20: channelsArrangement = MONO; break;
                    case 0x40: channelsArrangement = STEREO; break;
                    case 0x60: channelsArrangement = ISO_3_0_0; break;
                    case 0x80: channelsArrangement = ISO_2_1_0; break;
                    case 0xA0: channelsArrangement = ISO_3_1_0; break;
                    case 0xC0: channelsArrangement = ISO_2_2_0; break;
                    case 0xE0: channelsArrangement = ISO_3_2_0; break;
                    default: channelsArrangement = UNKNOWN; break;
                }

                duration = sizeInfo.FileSize * 8.0 / bitrate;

                result = true;
            }

            return result;
        }
    }
}