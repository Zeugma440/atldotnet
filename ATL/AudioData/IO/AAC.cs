using System;
using System.IO;
using Commons;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Advanced Audio Coding files manipulation (extensions : .AAC)
    /// 
    /// Implementation notes
    /// 
    ///     1. LATM and LOAS/LATM support is missing
    /// 
    /// </summary>
	class AAC : IAudioDataIO
    {

        // Header type codes
        public const byte AAC_HEADER_TYPE_UNKNOWN = 0;                       // Unknown
        public const byte AAC_HEADER_TYPE_ADIF = 1;                          // ADIF
        public const byte AAC_HEADER_TYPE_ADTS = 2;                          // ADTS

        // Header type names
        public static readonly string[] AAC_HEADER_TYPE = { "Unknown", "ADIF", "ADTS" };

        // MPEG version codes
        public const byte AAC_MPEG_VERSION_UNKNOWN = 0;                      // Unknown
        public const byte AAC_MPEG_VERSION_2 = 1;                            // MPEG-2
        public const byte AAC_MPEG_VERSION_4 = 2;                            // MPEG-4

        // MPEG version names
        public static readonly string[] AAC_MPEG_VERSION = { "Unknown", "MPEG-2", "MPEG-4" };

        // Profile codes
        public const byte AAC_PROFILE_UNKNOWN = 0;                           // Unknown
        public const byte AAC_PROFILE_MAIN = 1;                              // Main
        public const byte AAC_PROFILE_LC = 2;                                // LC
        public const byte AAC_PROFILE_SSR = 3;                               // SSR
        public const byte AAC_PROFILE_LTP = 4;                               // LTP

        // Profile names
        public static readonly string[] AAC_PROFILE = { "Unknown", "AAC Main", "AAC LC", "AAC SSR", "AAC LTP" };

        // Bit rate type codes
        public const byte AAC_BITRATE_TYPE_UNKNOWN = 0;                      // Unknown
        public const byte AAC_BITRATE_TYPE_CBR = 1;                          // CBR
        public const byte AAC_BITRATE_TYPE_VBR = 2;                          // VBR

        // Bit rate type names
        public static readonly string[] AAC_BITRATE_TYPE = { "Unknown", "CBR", "VBR" };

        // Sample rate values
        private static readonly int[] SAMPLE_RATE = {   96000, 88200, 64000, 48000, 44100, 32000,
                                                        24000, 22050, 16000, 12000, 11025, 8000,
                                                        0, 0, 0, 0 };

        private byte headerTypeID;
        private byte bitrateTypeID;
        private double bitrate;
        private int sampleRate;
        private ChannelsArrangement channelsArrangement;

        private AudioDataManager.SizeInfo sizeInfo;
        private readonly string fileName;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR
        {
            get { return (AAC_BITRATE_TYPE_VBR == bitrateTypeID); }
        }
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        public double BitRate
        {
            get { return bitrate / 1000.0; }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public int SampleRate
        {
            get { return sampleRate; }
        }

        public int BitDepth => -1; // Irrelevant for lossy formats

        public string FileName
        {
            get { return fileName; }
        }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TagType.ID3V1)
                || (metaDataType == MetaDataIOFactory.TagType.ID3V2)
                || (metaDataType == MetaDataIOFactory.TagType.APE)
                || (metaDataType == MetaDataIOFactory.TagType.NATIVE);
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            headerTypeID = AAC_HEADER_TYPE_UNKNOWN;
            bitrateTypeID = AAC_BITRATE_TYPE_UNKNOWN;

            bitrate = 0;
            sampleRate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public AAC(string fileName, Format format)
        {
            this.fileName = fileName;
            AudioFormat = format;
            resetData();
        }

        // ********************** Private functions & procedures *********************

        // Calculate duration time
        private double getDuration()
        {
            if (0 == bitrate)
                return 0;
            else
                return 8.0 * (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 1000 / bitrate;
        }

        public static bool IsValidHeader(byte[] data)
        {
            return recognizeHeaderType(data) != AAC_BITRATE_TYPE_UNKNOWN;
        }

        private static byte recognizeHeaderType(byte[] data)
        {
            if (data.Length < 4) return AAC_BITRATE_TYPE_UNKNOWN;

            if ("ADIF".Equals(Utils.Latin1Encoding.GetString(data, 0, 4)))
            {
                return AAC_HEADER_TYPE_ADIF;
            }
            else if ((0xFF == data[0]) && (0xF0 == ((data[1]) & 0xF0)))
            {
                return AAC_HEADER_TYPE_ADTS;
            }
            else return AAC_BITRATE_TYPE_UNKNOWN;
        }

        // Get header type of the file
        private byte recognizeHeaderType(Stream source)
        {
            byte[] header = new byte[4];

            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            source.Read(header, 0, header.Length);

            byte result = recognizeHeaderType(header);
            if (result != AAC_BITRATE_TYPE_UNKNOWN)
            {
                AudioDataOffset = source.Position - 4;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;
            }
            return result;
        }

        // Read ADIF header data
        private void readADIF(Stream Source)
        {
            int Position;

            Position = (int)(sizeInfo.ID3v2Size * 8 + 32);
            if (0 == StreamUtils.ReadBEBits(Source, Position, 1)) Position += 3;
            else Position += 75;
            if (0 == StreamUtils.ReadBEBits(Source, Position, 1)) bitrateTypeID = AAC_BITRATE_TYPE_CBR;
            else bitrateTypeID = AAC_BITRATE_TYPE_VBR;

            Position++;

            bitrate = (int)StreamUtils.ReadBEBits(Source, Position, 23);

            if (AAC_BITRATE_TYPE_CBR == bitrateTypeID) Position += 51;
            else Position += 31;

            Position += 2;

            uint channels = 1;
            sampleRate = SAMPLE_RATE[StreamUtils.ReadBEBits(Source, Position, 4)];
            Position += 4;
            channels += StreamUtils.ReadBEBits(Source, Position, 4);
            Position += 4;
            channels += StreamUtils.ReadBEBits(Source, Position, 4);
            Position += 4;
            channels += StreamUtils.ReadBEBits(Source, Position, 4);
            Position += 4;
            channels += StreamUtils.ReadBEBits(Source, Position, 2);
            channelsArrangement = ChannelsArrangements.GuessFromChannelNumber((int)channels);
        }

        // Read ADTS header data
        private void readADTS(Stream source)
        {
            int frames = 0;
            int totalSize = 0;
            int position;

            do
            {
                frames++;
                position = (int)(sizeInfo.ID3v2Size + totalSize) * 8;

                if (StreamUtils.ReadBEBits(source, position, 12) != 0xFFF) break;

                position += 18;

                sampleRate = SAMPLE_RATE[StreamUtils.ReadBEBits(source, position, 4)];
                position += 5;

                uint channels = StreamUtils.ReadBEBits(source, position, 3);
                channelsArrangement = ChannelsArrangements.GuessFromChannelNumber((int)channels);

                position += 7;

                totalSize += (int)StreamUtils.ReadBEBits(source, position, 13);
                position += 13;

                if (0x7FF == StreamUtils.ReadBEBits(source, position, 11))
                    bitrateTypeID = AAC_BITRATE_TYPE_VBR;
                else
                    bitrateTypeID = AAC_BITRATE_TYPE_CBR;

                if (AAC_BITRATE_TYPE_CBR == bitrateTypeID) break;
            }
            while (source.Length > sizeInfo.ID3v2Size + totalSize);
            bitrate = (int)Math.Round(8 * totalSize / 1024.0 / frames * sampleRate);
        }

        // Read data from file
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected bool read(Stream source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = true;

            resetData();

            headerTypeID = recognizeHeaderType(source);
            // Read header data
            if (AAC_HEADER_TYPE_ADIF == headerTypeID) readADIF(source);
            else if (AAC_HEADER_TYPE_ADTS == headerTypeID) readADTS(source);
            else result = false;

            return result;
        }
    }
}