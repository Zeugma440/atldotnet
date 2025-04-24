using System;
using System.Collections.Generic;
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

        private AudioDataManager.SizeInfo sizeInfo;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        /// <inheritdoc/>
        public bool IsVBR => AAC_BITRATE_TYPE_VBR == bitrateTypeID;

        /// <inheritdoc/>
        public AudioFormat AudioFormat
        {
            get;
        }
        /// <inheritdoc/>
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;
        /// <inheritdoc/>
        public double BitRate => bitrate / 1000.0;
        /// <inheritdoc/>
        public double Duration => getDuration();
        /// <inheritdoc/>
        public int SampleRate { get; private set; }

        /// <inheritdoc/>
        public int BitDepth => -1; // Irrelevant for lossy formats
        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }
        /// <inheritdoc/>
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            headerTypeID = AAC_HEADER_TYPE_UNKNOWN;
            bitrateTypeID = AAC_BITRATE_TYPE_UNKNOWN;

            bitrate = 0;
            SampleRate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public AAC(string fileName, AudioFormat format)
        {
            this.FileName = fileName;
            AudioFormat = format;
            resetData();
        }

        // ********************** Private functions & procedures *********************

        // Calculate duration time
        private double getDuration()
        {
            if (Utils.ApproxEquals(bitrate, 0)) return 0;
            return 8.0 * (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 1000 / bitrate;
        }

        public static bool IsValidHeader(byte[] data)
        {
            var headerTypeID = recognizeHeaderType(data);
            // Read header data
            if (AAC_HEADER_TYPE_ADIF == headerTypeID) return true;
            else if (AAC_HEADER_TYPE_ADTS == headerTypeID)
            {
                if (StreamUtils.ReadBits(data, 0, 8) != 0xFF) return false;
                if (StreamUtils.ReadBits(data, 12, 4) != 0xF) return false;
                if (StreamUtils.ReadBits(data, 9, 2) != 0) return false;  // Make sure Layer is 0
                return true;
            }
            else return false;
        }

        private static byte recognizeHeaderType(byte[] data)
        {
            if (data.Length < 4) return AAC_BITRATE_TYPE_UNKNOWN;

            if ("ADIF".Equals(Utils.Latin1Encoding.GetString(data, 0, 4)))
            {
                return AAC_HEADER_TYPE_ADIF;
            }

            if (0xFF == data[0] && 0xF0 == (data[1] & 0xF0)) return AAC_HEADER_TYPE_ADTS;

            return AAC_BITRATE_TYPE_UNKNOWN;
        }

        // Get header type of the file
        private byte recognizeHeaderType(Stream source)
        {
            byte[] header = new byte[4];

            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            if (source.Read(header, 0, header.Length) < header.Length) return AAC_HEADER_TYPE_UNKNOWN;

            byte result = recognizeHeaderType(header);
            if (result != AAC_BITRATE_TYPE_UNKNOWN)
            {
                AudioDataOffset = source.Position - 4;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;
            }
            return result;
        }

        // Read ADIF header data
        private bool readADIF(Stream source)
        {
            var Position = (int)(sizeInfo.ID3v2Size * 8 + 32);
            if (0 == StreamUtils.ReadBEBits(source, Position, 1)) Position += 3;
            else Position += 75;
            if (0 == StreamUtils.ReadBEBits(source, Position, 1)) bitrateTypeID = AAC_BITRATE_TYPE_CBR;
            else bitrateTypeID = AAC_BITRATE_TYPE_VBR;

            Position++;

            bitrate = (int)StreamUtils.ReadBEBits(source, Position, 23);

            if (AAC_BITRATE_TYPE_CBR == bitrateTypeID) Position += 51;
            else Position += 31;

            Position += 2;

            uint channels = 1;
            SampleRate = SAMPLE_RATE[StreamUtils.ReadBEBits(source, Position, 4)];
            Position += 4;
            channels += StreamUtils.ReadBEBits(source, Position, 4);
            Position += 4;
            channels += StreamUtils.ReadBEBits(source, Position, 4);
            Position += 4;
            channels += StreamUtils.ReadBEBits(source, Position, 4);
            Position += 4;
            channels += StreamUtils.ReadBEBits(source, Position, 2);
            ChannelsArrangement = GuessFromChannelNumber((int)channels);

            return ChannelsArrangement != UNKNOWN;
        }

        // Read ADTS header data
        private bool readADTS(Stream source)
        {
            int frames = 0;
            int totalSize = 0;
            bool result = false;

            do
            {
                frames++;
                var position = (int)(sizeInfo.ID3v2Size + totalSize) * 8;

                if (StreamUtils.ReadBEBits(source, position, 12) != 0xFFF) break;
                position += 12;
                position += 1;
                if (StreamUtils.ReadBEBits(source, position, 2) != 0) break; // Make sure Layer is 0
                position += 5;

                SampleRate = SAMPLE_RATE[StreamUtils.ReadBEBits(source, position, 4)];
                position += 5;

                uint channels = StreamUtils.ReadBEBits(source, position, 3);
                ChannelsArrangement = GuessFromChannelNumber((int)channels);

                position += 7;

                totalSize += (int)StreamUtils.ReadBEBits(source, position, 13);
                position += 13;

                if (0x7FF == StreamUtils.ReadBEBits(source, position, 11))
                    bitrateTypeID = AAC_BITRATE_TYPE_VBR;
                else
                    bitrateTypeID = AAC_BITRATE_TYPE_CBR;

                if (1 ==  frames) result = true;

                if (AAC_BITRATE_TYPE_CBR == bitrateTypeID) break;
            }
            while (source.Length > sizeInfo.ID3v2Size + totalSize);
            bitrate = (int)Math.Round(8 * totalSize / 1024.0 / frames * SampleRate);

            return result;
        }

        // Read data from file

        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected bool read(Stream source, MetaDataIO.ReadTagParams readTagParams)
        {
            resetData();

            headerTypeID = recognizeHeaderType(source);
            // Read header data
            if (AAC_HEADER_TYPE_ADIF == headerTypeID) return readADIF(source);
            else if (AAC_HEADER_TYPE_ADTS == headerTypeID) return readADTS(source);
            else return false;
        }
    }
}