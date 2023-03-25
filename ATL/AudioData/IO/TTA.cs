using ATL.Logging;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for True Audio files manipulation (extensions : .TTA)
    /// 
    /// NB : Only supports TTA1
    /// </summary>
	class TTA : IAudioDataIO
    {
        private static readonly byte[] TTA_SIGNATURE = Utils.Latin1Encoding.GetBytes("TTA1");

        // Private declarations
        private uint bitsPerSample;
        private uint sampleRate;
        private uint samplesSize;

        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // Public declarations    
        public double CompressionRatio => getCompressionRatio();
        public uint Samples => samplesSize;

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate => (int)sampleRate;
        public bool IsVBR => false;
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;
        public string FileName => filePath;
        public double BitRate => bitrate;
        public int BitDepth => (int)bitsPerSample;
        public double Duration => duration;
        public ChannelsArrangement ChannelsArrangement => channelsArrangement;
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TagType.APE) || (metaDataType == MetaDataIOFactory.TagType.ID3V1) || (metaDataType == MetaDataIOFactory.TagType.ID3V2);
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            duration = 0;
            bitrate = 0;
            isValid = false;

            bitsPerSample = 0;
            sampleRate = 0;
            samplesSize = 0;

            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public TTA(string filePath, Format format)
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
                return (double)sizeInfo.FileSize / (samplesSize * (channelsArrangement.NbChannels * bitsPerSample / 8) + 44) * 100;
            else
                return 0;
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, TTA_SIGNATURE);
        }

        public bool Read(Stream source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;
            resetData();
            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            bool result = false;

            byte[] buffer = new byte[4];
            source.Read(buffer, 0, buffer.Length);
            if (IsValidHeader(buffer))
            {
                isValid = true;

                AudioDataOffset = source.Position - 4;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;

                source.Seek(2, SeekOrigin.Current); // audio format
                source.Read(buffer, 0, 2);
                channelsArrangement = GuessFromChannelNumber(StreamUtils.DecodeUInt16(buffer));
                source.Read(buffer, 0, 2);
                bitsPerSample = StreamUtils.DecodeUInt16(buffer);
                source.Read(buffer, 0, 4);
                sampleRate = StreamUtils.DecodeUInt32(buffer);
                source.Read(buffer, 0, 4);
                samplesSize = StreamUtils.DecodeUInt32(buffer);
                source.Seek(4, SeekOrigin.Current); // CRC

                bitrate = (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 8.0 / (samplesSize * 1000.0 / sampleRate);
                duration = samplesSize * 1000.0 / sampleRate;

                result = true;
            }

            return result;
        }


    }
}
