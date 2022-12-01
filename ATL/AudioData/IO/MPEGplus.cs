using Commons;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for MusePack / MPEGplus files manipulation (extensions : .MPC, .MP+)
    /// </summary>
	class MPEGplus : IAudioDataIO
    {
        // Sample frequencies
        private static readonly int[] MPP_SAMPLERATES = new int[4] { 44100, 48000, 37800, 32000 };

        // ID code for stream version > 6
        private const long STREAM_VERSION_7_ID = 120279117;  // 120279117 = 'MP+' + #7
        private const long STREAM_VERSION_71_ID = 388714573; // 388714573 = 'MP+' + #23
        private const long STREAM_VERSION_8_ID = 0x4D50434B; // 'MPCK'


        private int frameCount;
        private int sampleRate;

        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // File header data - for internal use
        private sealed class HeaderRecord
        {
            public byte[] ByteArray = new byte[32];               // Data as byte array
            public int[] IntegerArray = new int[8];            // Data as integer array
        }

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public bool IsVBR
        {
            get { return true; }
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
        public int BitDepth => -1; // Irrelevant for lossy formats
        public double Duration
        {
            get { return duration; }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public int SampleRate
        {
            get { return sampleRate; }
        }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TagType.ID3V1) || (metaDataType == MetaDataIOFactory.TagType.ID3V2) || (metaDataType == MetaDataIOFactory.TagType.APE);
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }



        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            frameCount = 0;
            sampleRate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public MPEGplus(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private bool readHeader(BinaryReader source, ref HeaderRecord Header)
        {
            bool result = true;
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // Read header and get file size
            Header.ByteArray = source.ReadBytes(32);

            // if transfer is not complete
            int temp;
            for (int i = 0; i < Header.IntegerArray.Length; i++)
            {
                temp = Header.ByteArray[i * 4] * 0x00000001 +
                        Header.ByteArray[(i * 4) + 1] * 0x00000100 +
                        Header.ByteArray[(i * 4) + 2] * 0x00010000 +
                        Header.ByteArray[(i * 4) + 3] * 0x01000000;
                Header.IntegerArray[i] = temp;
            }

            // If VS8 file, looks for the (mandatory) stream header packet
            if (80 == getStreamVersion(Header))
            {
                string packetKey;
                bool headerFound = false;

                // Let's go back right after the 32-bit version marker
                source.BaseStream.Seek(sizeInfo.ID3v2Size + 4, SeekOrigin.Begin);

                while (!headerFound)
                {
                    long initialPos = source.BaseStream.Position;
                    packetKey = Utils.Latin1Encoding.GetString(source.ReadBytes(2));

                    readVariableSizeInteger(source); // Packet size (unused)

                    // SV8 stream header packet
                    if (packetKey.Equals("SH"))
                    {
                        AudioDataOffset = initialPos;
                        // Skip CRC-32 and stream version
                        source.BaseStream.Seek(5, SeekOrigin.Current);
                        long sampleCount = readVariableSizeInteger(source);
                        readVariableSizeInteger(source); // Skip beginning silence

                        byte b = source.ReadByte(); // Sample frequency (3) + Max used bands (5)
                        sampleRate = MPP_SAMPLERATES[(b & 0b11100000) >> 5]; // First 3 bits

                        b = source.ReadByte(); // Channel count (4) + Mid/Side Stereo used (1) + Audio block frames (3)
                        int channelCount = (b & 0b11110000) >> 4; // First 4 bits
                        bool isMidSideStereo = (b & 0b00001000) > 0; // First 4 bits
                        if (isMidSideStereo) channelsArrangement = JOINT_STEREO_MID_SIDE;
                        else channelsArrangement = ChannelsArrangements.GuessFromChannelNumber(channelCount);

                        // MPC has variable bitrate; only MPC versions < 7 display fixed bitrate
                        duration = sampleCount * 1000.0 / sampleRate;
                        bitrate = calculateAverageBitrate(duration);

                        headerFound = true;
                    }
                    // Continue searching for header
                    source.BaseStream.Seek(initialPos + 2, SeekOrigin.Begin);
                }
            }
            else
            {
                AudioDataOffset = sizeInfo.ID3v2Size;
            }
            AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;

            return result;
        }

        private static byte getStreamVersion(HeaderRecord Header)
        {
            byte result;

            // Get MPEGplus stream version
            if (STREAM_VERSION_7_ID == Header.IntegerArray[0]) result = 70;
            else if (STREAM_VERSION_71_ID == Header.IntegerArray[0]) result = 71;
            else if (STREAM_VERSION_8_ID == StreamUtils.ReverseInt32(Header.IntegerArray[0])) result = 80;
            else
            {
                switch ((Header.ByteArray[1] % 32) / 2) //Int division
                {
                    case 3: result = 40; break;
                    case 7: result = 50; break;
                    case 11: result = 60; break;
                    default: result = 0; break;
                }
            }

            return result;
        }

        /* Get samplerate from header
            Note: this is the same byte where profile is stored
        */
        private static int getSV7SampleRate(HeaderRecord header)
        {
            if (getStreamVersion(header) > 50)
            {
                return MPP_SAMPLERATES[header.ByteArray[10] & 3];
            }
            else
            {
                return 44100; // Fixed to 44.1 Khz before SV5
            }
        }

        private static ChannelsArrangement getSV7ChannelsArrangement(HeaderRecord Header)
        {
            ChannelsArrangement result;

            if ((70 == getStreamVersion(Header)) || (71 == getStreamVersion(Header)))
                // Get channel mode for stream version 7
                if ((Header.ByteArray[11] % 128) < 64) result = STEREO;
                else result = JOINT_STEREO; // TODO - could actually be either intensity stereo or mid/side stereo; however that code is obscure....
            else
                // Get channel mode for stream version 4-6
                if (0 == (Header.ByteArray[2] % 128)) result = STEREO;
            else result = JOINT_STEREO;

            return result;
        }

        private static int getSV7FrameCount(HeaderRecord Header)
        {
            int result;

            // Get frame count
            if (40 == getStreamVersion(Header)) result = Header.IntegerArray[1] >> 16;
            else if ((50 <= getStreamVersion(Header)) && (getStreamVersion(Header) <= 71))
            {
                result = Header.IntegerArray[1];
            }
            else result = 0;

            return result;
        }

        private double getSV7BitRate()
        {
            // Calculate bit rate
            return calculateAverageBitrate(getSV7Duration());
        }

        private double calculateAverageBitrate(double duration)
        {
            double result = 0;
            long CompressedSize;

            CompressedSize = sizeInfo.FileSize - sizeInfo.TotalTagSize;

            if (duration > 0) result = Math.Round(CompressedSize * 8.0 / duration);

            return result;
        }

        private double getSV7Duration()
        {
            // Calculate duration time
            if (sampleRate > 0) return (frameCount * 1152.0 * 1000.0 / sampleRate);
            else return 0;
        }


        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            HeaderRecord Header = new HeaderRecord();
            bool result;
            byte version;
            this.sizeInfo = sizeInfo;

            resetData();

            // Load header from file to variable
            result = readHeader(source, ref Header);
            version = getStreamVersion(Header);
            // Process data if loaded and file valid
            if (result && (sizeInfo.FileSize > 0) && (version > 0))
            {
                if (version < 80)
                {
                    // Fill properties with SV7 header data
                    sampleRate = getSV7SampleRate(Header);
                    channelsArrangement = getSV7ChannelsArrangement(Header);
                    frameCount = getSV7FrameCount(Header);
                    bitrate = getSV7BitRate();
                    duration = getSV7Duration();
                }
                else
                {
                    // SV8 data already read
                }
            }

            return result;
        }

        // Specific to MPC SV8
        // See specifications
        private static long readVariableSizeInteger(BinaryReader source)
        {
            long result = 0;
            byte b = 128;

            // Data is coded with a Big-endian, 7-byte variable-length record
            while ((b & 128) > 0)
            {
                b = source.ReadByte();
                result = (result << 7) + (b & 127); // Big-endian
            }

            return result;
        }
    }
}