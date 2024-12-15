using ATL.AudioData.IO;
using System;
using System.IO;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate FLAC-like tags embedded in other formats (e.g. OGG)
    /// </summary>
    internal static class FlacHelper
    {
        /// <summary>
        /// Represents general information extracted from a FLAC file
        /// </summary>
        public sealed class FlacHeader
        {
            private const byte FLAG_LAST_METADATA_BLOCK = 0x80;

            private byte[] StreamMarker = new byte[4];
            private readonly byte[] MetaDataBlockHeader = new byte[4];
            private readonly byte[] Info = new byte[18];
            // 16-bytes MD5 Sum only applies to audio data

            /// <summary>
            /// Contruct a new FlacHeader object
            /// </summary>
            public FlacHeader()
            {
                Reset();
            }

            /// <summary>
            /// Reset all data
            /// </summary>
            public void Reset()
            {
                Offset = -1;
                StreamMarker = new byte[4];
                Array.Clear(MetaDataBlockHeader, 0, 4);
                Array.Clear(Info, 0, 18);
            }

            /// <summary>
            /// Read data from the given stream
            /// </summary>
            /// <param name="source">Stream to read data from</param>
            public void FromStream(Stream source)
            {
                Offset = source.Position;
                if (source.Read(StreamMarker, 0, 4) < 4) return;
                if (source.Read(MetaDataBlockHeader, 0, 4) < 4) return;
                // METADATA_BLOCK_STREAMINFO
                if (source.Read(Info, 0, 18) < 18) return;
                // MD5 sum for audio data
                source.Seek(16, SeekOrigin.Current);
            }

            /// <summary>
            /// Offset of the header
            /// </summary>
            public long Offset { get; private set; }

            /// <summary>
            /// True if the header has valid data; false if it doesn't
            /// </summary>
            /// <returns></returns>
            public bool IsValid()
            {
                return IsValidHeader(StreamMarker);
            }

            /// <summary>
            /// Get the channels arrangement
            /// </summary>
            /// <returns>Channels arrangement</returns>
            public ChannelsArrangement getChannelsArrangement()
            {
                int channels = (Info[12] >> 1) & 0x7;
                switch (channels)
                {
                    case 0b0000: return MONO;
                    case 0b0001: return STEREO;
                    case 0b0010: return ISO_3_0_0;
                    case 0b0011: return QUAD;
                    case 0b0100: return ISO_3_2_0;
                    case 0b0101: return ISO_3_2_1;
                    case 0b0110: return LRCLFECrLssRss;
                    case 0b0111: return LRCLFELrRrLssRss;
                    case 0b1000: return JOINT_STEREO_LEFT_SIDE;
                    case 0b1001: return JOINT_STEREO_RIGHT_SIDE;
                    case 0b1010: return JOINT_STEREO_MID_SIDE;
                    default: return UNKNOWN;
                }
            }

            /// <summary>
            /// Returns true if the metadata block exists; false if it doesn't
            /// NB : We're testing if the very first metadata block we're into (STREAMINFO) is the last one
            /// If it isn't, it means there are other metadata blocks.
            /// </summary>
            public bool MetadataExists => 0 == (MetaDataBlockHeader[1] & FLAG_LAST_METADATA_BLOCK);

            /// <summary>
            /// Sample rate
            /// </summary>
            public int SampleRate => Info[10] << 12 | Info[11] << 4 | Info[12] >> 4;

            /// <summary>
            /// Bits per sample
            /// </summary>
            public byte BitsPerSample => (byte)(((Info[12] & 1) << 4) | (Info[13] >> 4) + 1);

            /// <summary>
            /// Number of samples
            /// </summary>
            public long NbSamples => Info[14] << 24 | Info[15] << 16 | Info[16] << 8 | Info[17];
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, FLAC.FLAC_ID);
        }

        /// <summary>
        /// Read FLAC headers from the given source
        /// </summary>
        /// <param name="source">Source to read data from</param>
        /// <returns>FLAC headers</returns>
        public static FlacHeader ReadHeader(Stream source)
        {
            // Read header data    
            FlacHeader flacHeader = new FlacHeader();
            flacHeader.FromStream(source);
            return flacHeader;
        }
    }
}
