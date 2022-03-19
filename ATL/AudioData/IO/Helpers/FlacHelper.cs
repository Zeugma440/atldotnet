using ATL.AudioData.IO;
using Commons;
using System;
using System.IO;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate FLAC-like tags embedded in other formats (e.g. OGG)
    /// </summary>
    public static class FlacHelper
    {
        public sealed class FlacHeader
        {
            public string StreamMarker;
            public byte[] MetaDataBlockHeader = new byte[4];
            public byte[] Info = new byte[18];
            // 16-bytes MD5 Sum only applies to audio data

            public FlacHeader()
            {
                Reset();
            }

            public void Reset()
            {
                StreamMarker = "";
                Array.Clear(MetaDataBlockHeader, 0, 4);
                Array.Clear(Info, 0, 18);
            }

            public bool IsValid()
            {
                return StreamMarker.Equals(FLAC.FLAC_ID);
            }

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

            public int getSampleRate()
            {
                return Info[10] << 12 | Info[11] << 4 | Info[12] >> 4;
            }

            public byte getBitsPerSample()
            {
                return (byte)(((Info[12] & 1) << 4) | (Info[13] >> 4) + 1);
            }

            public long getSamples()
            {
                return Info[14] << 24 | Info[15] << 16 | Info[16] << 8 | Info[17];
            }
        }

        public static FlacHeader readHeader(Stream source)
        {
            // Read header data    
            FlacHeader flacHeader = new FlacHeader();

            byte[] data = new byte[4];
            source.Read(data, 0, 4);
            flacHeader.StreamMarker = Utils.Latin1Encoding.GetString(data, 0, 4);
            source.Read(flacHeader.MetaDataBlockHeader, 0, 4);
            source.Read(flacHeader.Info, 0, 18); // METADATA_BLOCK_STREAMINFO
            source.Seek(16, SeekOrigin.Current); // MD5 sum for audio data

            return flacHeader;
        }
    }
}
