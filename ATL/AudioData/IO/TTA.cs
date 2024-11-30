using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using System.Collections.Generic;

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


        // Public declarations    
        public uint Samples => samplesSize;

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate => (int)sampleRate;
        public bool IsVBR => false;
        public AudioFormat AudioFormat { get; }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;
        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => (int)bitsPerSample;
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;

            bitsPerSample = 0;
            sampleRate = 0;
            samplesSize = 0;

            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public TTA(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, TTA_SIGNATURE);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            resetData();
            source.Seek(sizeNfo.ID3v2Size, SeekOrigin.Begin);

            bool result = false;

            byte[] buffer = new byte[4];
            if (source.Read(buffer, 0, buffer.Length) < buffer.Length) return false;
            if (IsValidHeader(buffer))
            {
                AudioDataOffset = source.Position - 4;
                AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

                source.Seek(2, SeekOrigin.Current); // audio format
                if (source.Read(buffer, 0, 2) < 2) return false;
                ChannelsArrangement = GuessFromChannelNumber(StreamUtils.DecodeUInt16(buffer));
                if (source.Read(buffer, 0, 2) < 2) return false;
                bitsPerSample = StreamUtils.DecodeUInt16(buffer);
                if (source.Read(buffer, 0, 4) < 4) return false;
                sampleRate = StreamUtils.DecodeUInt32(buffer);
                if (source.Read(buffer, 0, 4) < 4) return false;
                samplesSize = StreamUtils.DecodeUInt32(buffer);
                source.Seek(4, SeekOrigin.Current); // CRC

                BitRate = (sizeNfo.FileSize - sizeNfo.TotalTagSize) * 8.0 / (samplesSize * 1000.0 / sampleRate);
                Duration = samplesSize * 1000.0 / sampleRate;

                result = true;
            }

            return result;
        }


    }
}
