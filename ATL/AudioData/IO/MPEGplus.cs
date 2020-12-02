using ATL.Logging;
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
        // Channel mode names
        private static readonly string[] MPP_MODE = new String[3] { "Unknown", "Stereo", "Joint Stereo" };
        // Sample frequencies
        private static readonly int[] MPP_SAMPLERATES = new int[4] { 44100, 48000, 37800, 32000 };

        // Used with ProfileID property
        private const byte MPP_PROFILE_QUALITY0 = 9;        // '--quality 0' profile
        private const byte MPP_PROFILE_QUALITY1 = 10;       // '--quality 1' profile
        private const byte MPP_PROFILE_TELEPHONE = 11;        // 'Telephone' profile
        private const byte MPP_PROFILE_THUMB = 1;          // 'Thumb' (poor) quality
        private const byte MPP_PROFILE_RADIO = 2;        // 'Radio' (normal) quality
        private const byte MPP_PROFILE_STANDARD = 3;    // 'Standard' (good) quality
        private const byte MPP_PROFILE_XTREME = 4;   // 'Xtreme' (very good) quality
        private const byte MPP_PROFILE_INSANE = 5;   // 'Insane' (excellent) quality
        private const byte MPP_PROFILE_BRAINDEAD = 6; // 'BrainDead' (excellent) quality
        private const byte MPP_PROFILE_QUALITY9 = 7; // '--quality 9' (excellent) quality
        private const byte MPP_PROFILE_QUALITY10 = 8;  // '--quality 10' (excellent) quality
        private const byte MPP_PROFILE_UNKNOWN = 0;               // Unknown profile
        private const byte MPP_PROFILE_EXPERIMENTAL = 12;

        // Profile names
        private static readonly string[] MPP_PROFILE = new String[13]
        {
            "Unknown", "Thumb", "Radio", "Standard", "Xtreme", "Insane", "BrainDead",
            "--quality 9", "--quality 10", "--quality 0", "--quality 1", "Telephone", "Experimental"
        };

        // ID code for stream version > 6
        private const long STREAM_VERSION_7_ID = 120279117;  // 120279117 = 'MP+' + #7
        private const long STREAM_VERSION_71_ID = 388714573; // 388714573 = 'MP+' + #23
        private const long STREAM_VERSION_8_ID = 0x4D50434B; // 'MPCK'


        private int frameCount;
        private long FSampleCount;
        private int sampleRate;
        private byte FStreamVersion;
        private byte profileID;
        private string encoder;

        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;

        private SizeInfo sizeInfo;
        private readonly String filePath;


        // File header data - for internal use
        private class HeaderRecord
        {
            public byte[] ByteArray = new byte[32];               // Data as byte array
            public int[] IntegerArray = new int[8];            // Data as integer array
        }


        /* Unused for now
                public int FrameCount // Number of frames
                {
                    get { return this.frameCount; }
                }	
                public byte StreamVersion // Stream version
                {
                    get { return this.FStreamVersion; }
                }	
                public byte ProfileID // Profile code
                {
                    get { return this.profileID; }
                }	
                public String Profile // Profile name
                {
                    get { return this.getProfile(); }
                }	
                public bool Corrupted // True if file corrupted
                {
                    get { return this.isCorrupted(); }
                }		       
                public String Encoder // Encoder used
                {
                    get { return this.encoder; }
                }
        */

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
            get { return this.sampleRate; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2) || (metaDataType == MetaDataIOFactory.TAG_APE);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            frameCount = 0;
            FStreamVersion = 0;
            sampleRate = 0;
            FSampleCount = 0;
            encoder = "";
            profileID = MPP_PROFILE_UNKNOWN;
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
                temp = Header.ByteArray[(i * 4)] * 0x00000001 +
                        Header.ByteArray[(i * 4) + 1] * 0x00000100 +
                        Header.ByteArray[(i * 4) + 2] * 0x00010000 +
                        Header.ByteArray[(i * 4) + 3] * 0x01000000;
                Header.IntegerArray[i] = temp;
            }

            // If VS8 file, looks for the (mandatory) stream header packet
            if (80 == getStreamVersion(Header))
            {
                String packetKey = "";
                long packetSize = 0; // Packet size (int only since we are dealing with the header packet)
                long initialPos;
                bool headerFound = false;

                // Let's go back right after the 32-bit version marker
                source.BaseStream.Seek(sizeInfo.ID3v2Size + 4, SeekOrigin.Begin);

                while (!headerFound)
                {
                    initialPos = source.BaseStream.Position;
                    packetKey = Utils.Latin1Encoding.GetString(source.ReadBytes(2));

                    packetSize = readVariableSizeInteger(source);

                    // SV8 stream header packet
                    if (packetKey.Equals("SH"))
                    {
                        // Skip CRC-32 and stream version
                        source.BaseStream.Seek(5, SeekOrigin.Current);
                        FSampleCount = readVariableSizeInteger(source);
                        readVariableSizeInteger(source); // Skip beginning silence

                        byte b = source.ReadByte(); // Sample frequency (3) + Max used bands (5)
                        sampleRate = MPP_SAMPLERATES[(b & 0b11100000) >> 5]; // First 3 bits

                        b = source.ReadByte(); // Channel count (4) + Mid/Side Stereo used (1) + Audio block frames (3)
                        int channelCount = (b & 0b11110000) >> 4; // First 4 bits
                        bool isMidSideStereo = (b & 0b00001000) > 0; // First 4 bits
                        if (isMidSideStereo) channelsArrangement = JOINT_STEREO_MID_SIDE;
                        else channelsArrangement = ChannelsArrangements.GuessFromChannelNumber(channelCount);
                        long framesPerPacket = (long)Math.Pow(4, (b & 0b00000111)); // Last 3 bits

                        profileID = MPP_PROFILE_UNKNOWN;   // Profile info is SV7-only
                        encoder = "";                      // Encoder info is SV7-only

                        // MPC has variable bitrate; only MPC versions < 7 display fixed bitrate
                        duration = (double)FSampleCount * 1000.0 / sampleRate;
                        bitrate = calculateAverageBitrate(duration);

                        headerFound = true;
                    }
                    // Continue searching for header
                    source.BaseStream.Seek(initialPos + 2, SeekOrigin.Begin);
                }
            }

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

        private static String getSV7Encoder(HeaderRecord Header)
        {
            int EncoderID;
            String result = "";

            EncoderID = Header.ByteArray[10 + 2 + 15];
            if (0 == EncoderID)
            {
                //FEncoder := 'Buschmann 1.7.0...9, Klemm 0.90...1.05';
            }
            else
            {
                switch (EncoderID % 10)
                {
                    case 0: result = "Release " + (EncoderID / 100) + "." + ((EncoderID / 10) % 10); break;
                    case 2: result = "Beta " + (EncoderID / 100) + "." + (EncoderID % 100); break; // Not exactly...
                    case 4: goto case 2;
                    case 6: goto case 2;
                    case 8: goto case 2;
                    default: result = "--Alpha-- " + (EncoderID / 100) + "." + (EncoderID % 100); break;
                }
            }

            return result;
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
            else
                if ((50 <= getStreamVersion(Header)) && (getStreamVersion(Header) <= 71))
                result = Header.IntegerArray[1];
            else result = 0;

            return result;
        }

        private double getSV7BitRate(HeaderRecord Header)
        {
            double result = 0;

            // Try to get bit rate from header
            if ((60 >= getStreamVersion(Header)) /*|| (5 == GetStreamVersion(Header))*/ )
            {
                result = (ushort)((Header.IntegerArray[0] >> 23) & 0x01FF);
            }

            // Calculate bit rate if not given
            result = calculateAverageBitrate(getSV7Duration());

            return result;
        }

        private double calculateAverageBitrate(double duration)
        {
            double result = 0;
            long CompressedSize;

            CompressedSize = sizeInfo.FileSize - sizeInfo.TotalTagSize;

            if (duration > 0) result = Math.Round(CompressedSize * 8.0 / duration);

            return result;
        }

        private static byte getSV7ProfileID(HeaderRecord Header)
        {
            byte result = MPP_PROFILE_UNKNOWN;
            // Get MPEGplus profile (exists for stream version 7 only)
            if ((70 == getStreamVersion(Header)) || (71 == getStreamVersion(Header)))
                // ((and $F0) shr 4) is needed because samplerate is stored in the same byte!
                switch (((Header.ByteArray[10] & 0xF0) >> 4))
                {
                    case 1: result = MPP_PROFILE_EXPERIMENTAL; break;
                    case 5: result = MPP_PROFILE_QUALITY0; break;
                    case 6: result = MPP_PROFILE_QUALITY1; break;
                    case 7: result = MPP_PROFILE_TELEPHONE; break;
                    case 8: result = MPP_PROFILE_THUMB; break;
                    case 9: result = MPP_PROFILE_RADIO; break;
                    case 10: result = MPP_PROFILE_STANDARD; break;
                    case 11: result = MPP_PROFILE_XTREME; break;
                    case 12: result = MPP_PROFILE_INSANE; break;
                    case 13: result = MPP_PROFILE_BRAINDEAD; break;
                    case 14: result = MPP_PROFILE_QUALITY9; break;
                    case 15: result = MPP_PROFILE_QUALITY10; break;
                }

            return result;
        }

        /* Unused for now
                private String getChannelMode()
                {
                    return MPP_MODE[channelModeID];
                }

                private String getProfile()
                {
                    return MPP_PROFILE[profileID];
                }

                private bool isCorrupted()
                {
                    // Check for file corruption
                    return ( (bitrate < 3) || (bitrate > 480) );
                }
        */

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
                FStreamVersion = version;
                if (version < 80)
                {
                    // Fill properties with SV7 header data
                    sampleRate = getSV7SampleRate(Header);
                    channelsArrangement = getSV7ChannelsArrangement(Header);
                    frameCount = getSV7FrameCount(Header);
                    bitrate = getSV7BitRate(Header);
                    profileID = getSV7ProfileID(Header);
                    encoder = getSV7Encoder(Header);
                    duration = getSV7Duration();
                }
                else
                {
                    // VS8 data already read
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