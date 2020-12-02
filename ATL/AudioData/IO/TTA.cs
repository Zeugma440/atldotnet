using ATL.Logging;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for True Audio files manipulation (extensions : .TTA)
    /// </summary>
	class TTA : IAudioDataIO
    {
        private const string TTA_SIGNATURE = "TTA1";

        // Private declarations
        private uint audioFormat;
        private uint bitsPerSample;
        private uint sampleRate;
        private uint samplesSize;
        private uint cRC32;

        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // Public declarations    
        public uint Bits
        {
            get { return bitsPerSample; }
        }
        public double CompressionRatio
        {
            get { return getCompressionRatio(); }
        }
        public uint Samples // Number of samples
        {
            get { return samplesSize; }
        }
        /*
        public uint CRC32
        {
            get { return cRC32; }
        }
        public uint AudioFormat
        {
            get { return audioFormat; }
        }
        */


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate
        {
            get { return (int)sampleRate; }
        }
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
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_APE) || (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            duration = 0;
            bitrate = 0;
            isValid = false;

            audioFormat = 0;
            bitsPerSample = 0;
            sampleRate = 0;
            samplesSize = 0;
            cRC32 = 0;
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

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;
            resetData();
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            bool result = false;

            if (TTA_SIGNATURE.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(4))))
            {
                isValid = true;

                audioFormat = source.ReadUInt16();
                channelsArrangement = ChannelsArrangements.GuessFromChannelNumber(source.ReadUInt16());
                bitsPerSample = source.ReadUInt16();
                sampleRate = source.ReadUInt32();
                samplesSize = source.ReadUInt32();
                cRC32 = source.ReadUInt32();

                bitrate = (double)(sizeInfo.FileSize - sizeInfo.TotalTagSize) * 8.0 / ((double)samplesSize * 1000.0 / sampleRate);
                duration = (double)samplesSize * 1000.0 / sampleRate;

                result = true;
            }

            return result;
        }


    }
}
