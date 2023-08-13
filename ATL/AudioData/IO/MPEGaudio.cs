using ATL.Logging;
using Commons;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for MPEG Audio Layer files manipulation (extensions : .MP1, .MP2, .MP3)
    /// </summary>
	class MPEGaudio : IAudioDataIO
    {
        // Limitation constants
        public const int MAX_MPEG_FRAME_LENGTH = 8068;          // Max. MPEG frame length according to all extreme values
        public const int MIN_MPEG_BIT_RATE = 8;                 // Min. bit rate value (KBit/s)
        public const int MAX_MPEG_BIT_RATE = 448;               // Max. bit rate value (KBit/s)
        public const double MIN_ALLOWED_DURATION = 0.1;         // Min. song duration value


        // VBR Vendor ID strings
        public const string VENDOR_ID_LAME = "LAME";                      // For LAME
        public const string VENDOR_ID_GOGO_NEW = "GOGO";            // For GoGo (New)
        public const string VENDOR_ID_GOGO_OLD = "MPGE";            // For GoGo (Old)

        // Table for bit rates (KBit/s)
        public static readonly ushort[,,] MPEG_BIT_RATE = new ushort[4, 4, 16]
        {
	       // For MPEG 2.5
		    {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0},
                {0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0},
                {0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0}
            },
	       // Reserved
		    {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
            },
	       // For MPEG 2
		    {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0},
                {0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0},
                {0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0}
            },
	       // For MPEG 1
		    {
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0},
                {0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0},
                {0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0}
            }
        };

        // Sample rate codes
        public const byte MPEG_SAMPLE_RATE_LEVEL_3 = 0;                    // Level 3
        public const byte MPEG_SAMPLE_RATE_LEVEL_2 = 1;                    // Level 2
        public const byte MPEG_SAMPLE_RATE_LEVEL_1 = 2;                    // Level 1
        public const byte MPEG_SAMPLE_RATE_UNKNOWN = 3;              // Unknown value

        // Table for sample rates
        public static readonly ushort[,] MPEG_SAMPLE_RATE = new ushort[4, 4]
        {
            {11025, 12000, 8000, 0},                                   // For MPEG 2.5
		    {0, 0, 0, 0},                                                  // Reserved
		    {22050, 24000, 16000, 0},                                    // For MPEG 2
		    {44100, 48000, 32000, 0}                                     // For MPEG 1
    	};

        // VBR header ID for Xing/FhG
        public const String VBR_ID_XING = "Xing";                       // Xing VBR ID
        public const String VBR_ID_FHG = "VBRI";                         // FhG VBR ID

        // MPEG version codes
        public const byte MPEG_VERSION_2_5 = 0;                            // MPEG 2.5
        public const byte MPEG_VERSION_UNKNOWN = 1;                 // Unknown version
        public const byte MPEG_VERSION_2 = 2;                                // MPEG 2
        public const byte MPEG_VERSION_1 = 3;                                // MPEG 1

        // MPEG version names
        public static readonly String[] MPEG_VERSION = new String[4] { "MPEG 2.5", "MPEG ?", "MPEG 2", "MPEG 1" };

        // MPEG layer codes
        public const byte MPEG_LAYER_UNKNOWN = 0;                     // Unknown layer
        public const byte MPEG_LAYER_III = 1;                             // Layer III
        public const byte MPEG_LAYER_II = 2;                               // Layer II
        public const byte MPEG_LAYER_I = 3;                                 // Layer I

        // MPEG layer names
        public static readonly String[] MPEG_LAYER = new String[4] { "Layer ?", "Layer III", "Layer II", "Layer I" };

        // Channel mode codes
        public const byte MPEG_CM_STEREO = 0;                                // Stereo
        public const byte MPEG_CM_JOINT_STEREO = 1;                    // Joint Stereo
        public const byte MPEG_CM_DUAL_CHANNEL = 2;                    // Dual Channel
        public const byte MPEG_CM_MONO = 3;                                    // Mono
        public const byte MPEG_CM_UNKNOWN = 4;                         // Unknown mode

        // Channel mode names
        public static readonly String[] MPEG_CM_MODE = new String[5] { "Stereo", "Joint Stereo", "Dual Channel", "Mono", "Unknown" };

        // Extension mode codes (for Joint Stereo)
        public const byte MPEG_CM_EXTENSION_OFF = 0;        // IS and MS modes set off
        public const byte MPEG_CM_EXTENSION_IS = 1;             // Only IS mode set on
        public const byte MPEG_CM_EXTENSION_MS = 2;             // Only MS mode set on
        public const byte MPEG_CM_EXTENSION_ON = 3;          // IS and MS modes set on
        public const byte MPEG_CM_EXTENSION_UNKNOWN = 4;     // Unknown extension mode

        // Emphasis mode codes
        public const byte MPEG_EMPHASIS_NONE = 0;                              // None
        public const byte MPEG_EMPHASIS_5015 = 1;                          // 50/15 ms
        public const byte MPEG_EMPHASIS_UNKNOWN = 2;               // Unknown emphasis
        public const byte MPEG_EMPHASIS_CCIT = 3;                         // CCIT J.17

        // Emphasis names
        public static readonly String[] MPEG_EMPHASIS = new String[4] { "None", "50/15 ms", "Unknown", "CCIT J.17" };

        // Encoder codes
        public const byte MPEG_ENCODER_UNKNOWN = 0;                // Unknown encoder
        public const byte MPEG_ENCODER_XING = 1;                              // Xing
        public const byte MPEG_ENCODER_FHG = 2;                                // FhG
        public const byte MPEG_ENCODER_LAME = 3;                              // LAME
        public const byte MPEG_ENCODER_BLADE = 4;                            // Blade
        public const byte MPEG_ENCODER_GOGO = 5;                              // GoGo
        public const byte MPEG_ENCODER_SHINE = 6;                            // Shine
        public const byte MPEG_ENCODER_QDESIGN = 7;                        // QDesign

        // Encoder names
        public static readonly String[] MPEG_ENCODER = new String[8] { "Unknown", "Xing", "FhG", "LAME", "Blade", "GoGo", "Shine", "QDesign" };

        // Xing/FhG VBR header data
        public sealed class VBRData
        {
            public bool Found;                            // True if VBR header found
            public char[] ID = new char[4];            // Header ID: "Xing" or "VBRI"
            public int Frames;                              // Total number of frames
            public int Bytes;                                // Total number of bytes
            public byte Scale;                                  // VBR scale (1..100)
            public string VendorID;                         // Vendor ID (if present)

            public void Reset()
            {
                Found = false;
                Array.Clear(ID, 0, ID.Length);
                Frames = 0;
                Bytes = 0;
                Scale = 0;
                VendorID = "";
            }
        }

        // MPEG frame header data
        public sealed class FrameHeader
        {
            public bool Found;                                 // True if frame found
            public long Position;                        // Frame position in the file
            public bool Xing;                                 // True if Xing encoder
            public byte VersionID;                                 // MPEG version ID
            public byte LayerID;                                     // MPEG layer ID
            public bool ProtectionBit;                    // True if protected by CRC
            public ushort BitRateID;                                   // Bit rate ID
            public ushort SampleRateID;                             // Sample rate ID
            public bool PaddingBit;                           // True if frame padded
            public bool PrivateBit;                              // Extra information
            public byte ModeID;                                    // Channel mode ID
            public byte ModeExtensionID;      // Mode extension ID (for Joint Stereo)
            public bool CopyrightBit;                    // True if audio copyrighted
            public bool OriginalBit;                        // True if original media
            public byte EmphasisID;                                    // Emphasis ID

            private int m_size;                                 // Frame size (bytes)

            public void Reset()
            {
                Found = false;
                Position = 0;
                m_size = -1;
                Xing = false;

                VersionID = MPEG_VERSION_UNKNOWN;
                LayerID = 0;
                ProtectionBit = false;
                BitRateID = 0;
                SampleRateID = MPEG_SAMPLE_RATE_UNKNOWN;
                PaddingBit = false;
                PrivateBit = false;
                ModeID = MPEG_CM_UNKNOWN;
                ModeExtensionID = MPEG_CM_EXTENSION_UNKNOWN;
                CopyrightBit = false;
                OriginalBit = false;
                EmphasisID = MPEG_EMPHASIS_UNKNOWN;
            }

            public void LoadFromByteArray(byte[] data)
            {
                VersionID = (byte)((data[1] >> 3) & 3);
                LayerID = (byte)((data[1] >> 1) & 3);
                ProtectionBit = (data[1] & 1) != 1;
                BitRateID = (ushort)(data[2] >> 4);
                SampleRateID = (ushort)((data[2] >> 2) & 3);
                PaddingBit = (((data[2] >> 1) & 1) == 1);
                PrivateBit = ((data[2] & 1) == 1);
                ModeID = (byte)((data[3] >> 6) & 3);
                ModeExtensionID = (byte)((data[3] >> 4) & 3);
                CopyrightBit = (((data[3] >> 3) & 1) == 1);
                OriginalBit = (((data[3] >> 2) & 1) == 1);
                EmphasisID = (byte)(data[3] & 3);
                m_size = -1;
            }

            // This formula only works for Layers II and III
            public int Size
            {
                get
                {
                    if (m_size < 1) m_size = (int)Math.Floor(getCoefficient(this) * getBitRate(this) * 1000.0 / getSampleRate(this)) + getPadding(this);
                    return m_size;
                }
            }
        }

        private VBRData vbrData = new VBRData();
        private FrameHeader HeaderFrame = new FrameHeader();
        private SizeInfo sizeInfo;
        private readonly string filePath;
        private readonly Format audioFormat;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        /// <summary>
        /// VBR header data
        /// </summary>
        public VBRData VBR { get => vbrData; }
        public bool IsVBR { get => vbrData.Found; }
        public double BitRate { get => getBitRate(); }
        public int BitDepth => -1; // Irrelevant for lossy formats
        public double Duration { get => getDuration(); }
        public ChannelsArrangement ChannelsArrangement { get => getChannelsArrangement(HeaderFrame); }
        public int SampleRate { get => getSampleRate(); }
        public string FileName { get => filePath; }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        public Format AudioFormat
        {
            get
            {
                Format f = new Format(audioFormat);
                f.Name = f.Name + " (" + getLayer() + ")";
                return f;
            }
        }
        public int CodecFamily
        {
            get => AudioDataIOFactory.CF_LOSSY;
        }

        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TagType.ID3V1) || (metaDataType == MetaDataIOFactory.TagType.ID3V2) || (metaDataType == MetaDataIOFactory.TagType.APE);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            vbrData.Reset();
            HeaderFrame.Reset();
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public MPEGaudio(string filePath, Format format)
        {
            this.filePath = filePath;
            audioFormat = format;
            resetData();
        }

        // ********************* Auxiliary functions & voids ********************

        public static bool IsValidFrameHeader(byte[] data)
        {
            if (data.Length < 4) return false;

            // Check for valid frame header
            return !(
                (data[0] != 0xFF) ||                                  // First 11 bits are set
                ((data[1] & 0xE0) != 0xE0) ||                         // First 11 bits are set
                (((data[1] >> 3) & 3) == MPEG_VERSION_UNKNOWN) ||     // MPEG version > 1
                (((data[1] >> 1) & 3) == MPEG_LAYER_UNKNOWN) ||       // Layer I, II or III
                ((data[2] & 0xF0) == 0xF0) ||                         // Bitrate index is not 'bad'
                ((data[2] & 0xF0) == 0) ||                            // Bitrate index is not 'free'
                (((data[2] >> 2) & 3) == MPEG_SAMPLE_RATE_UNKNOWN) || // Sampling rate is not 'reserved'
                ((data[3] & 3) == MPEG_EMPHASIS_UNKNOWN)              // Emphasis is not 'reserved'
                );
        }

        /// <summary>
        /// Get frame size coefficient
        /// </summary>
        private static byte getCoefficient(FrameHeader Frame)
        {
            if (MPEG_VERSION_1 == Frame.VersionID)
                if (MPEG_LAYER_I == Frame.LayerID) return 48;
                else return 144;
            else
                if (MPEG_LAYER_I == Frame.LayerID) return 24;
            else if (MPEG_LAYER_II == Frame.LayerID) return 144;
            else return 72;
        }

        private static ushort getBitRate(FrameHeader Frame)
        {
            return MPEG_BIT_RATE[Frame.VersionID, Frame.LayerID, Frame.BitRateID];
        }

        private static ushort getSampleRate(FrameHeader Frame)
        {
            return MPEG_SAMPLE_RATE[Frame.VersionID, Frame.SampleRateID];
        }

        private static byte getPadding(FrameHeader Frame)
        {
            if (Frame.PaddingBit)
                if (MPEG_LAYER_I == Frame.LayerID) return 4;
                else return 1;
            else return 0;
        }

        /// <summary>
        /// Get bit rate, calculate average bit rate if VBR header found
        /// </summary>
        private double getBitRate()
        {
            if (vbrData.Found && (vbrData.Frames > 0))
                return Math.Round(
                        (
                            ((double)vbrData.Bytes / vbrData.Frames - getPadding(HeaderFrame)) *
                            (double)getSampleRate(HeaderFrame) / getCoefficient(HeaderFrame)
                            ) / 1000.0
                    );
            else
                return getBitRate(HeaderFrame);
        }

        private ushort getSampleRate()
        {
            return getSampleRate(HeaderFrame);
        }

        private static VBRData getXingInfo(Stream source)
        {
            VBRData result = new VBRData();
            byte[] data = new byte[8];

            result.Found = true;
            result.ID = VBR_ID_XING.ToCharArray();
            source.Seek(4, SeekOrigin.Current);
            source.Read(data, 0, 8);

            result.Frames =
                data[0] * 0x1000000 +
                data[1] * 0x10000 +
                data[2] * 0x100 +
                data[3];
            result.Bytes =
                data[4] * 0x1000000 +
                data[5] * 0x10000 +
                data[6] * 0x100 +
                data[7];

            source.Seek(103, SeekOrigin.Current);

            source.Read(data, 0, 1);
            result.Scale = data[0];
            source.Read(data, 0, 8);
            result.VendorID = Utils.Latin1Encoding.GetString(data, 0, 8);

            return result;
        }

        private static VBRData getFhGInfo(Stream source)
        {
            VBRData result = new VBRData();
            byte[] data = new byte[9];

            // Extract FhG VBR info at given position
            result.Found = true;
            result.ID = VBR_ID_FHG.ToCharArray();
            source.Seek(5, SeekOrigin.Current);
            source.Read(data, 0, 9);

            result.Scale = data[0];
            result.Bytes =
                data[1] * 0x1000000 +
                data[2] * 0x10000 +
                data[3] * 0x100 +
                data[4];
            result.Frames =
                data[5] * 0x1000000 +
                data[6] * 0x10000 +
                data[7] * 0x100 +
                data[8];

            result.VendorID = "";

            return result;
        }

        private static VBRData findVBR(Stream source, long position)
        {
            VBRData result;
            byte[] data = new byte[4];

            // Check for VBR header at given position
            source.Seek(position, SeekOrigin.Begin);

            source.Read(data, 0, 4);
            string vbrId = Utils.Latin1Encoding.GetString(data);

            if (VBR_ID_XING.Equals(vbrId)) result = getXingInfo(source);
            else if (VBR_ID_FHG.Equals(vbrId)) result = getFhGInfo(source);
            else
            {
                result = new VBRData();
                result.Reset();
            }

            return result;
        }

        /// <summary>
        /// Get MPEG layer name
        /// </summary>
        private string getLayer()
        {
            return MPEG_LAYER[HeaderFrame.LayerID];
        }

        private static byte getVBRDeviation(FrameHeader Frame)
        {
            if (MPEG_VERSION_1 == Frame.VersionID)
                if (Frame.ModeID != MPEG_CM_MONO) return 36;
                else return 21;
            else
                if (Frame.ModeID != MPEG_CM_MONO) return 21;
            else return 13;
        }

        private double getDuration()
        {
            if (HeaderFrame.Found)
                if (vbrData.Found && (vbrData.Frames > 0))
                    return vbrData.Frames * getCoefficient(HeaderFrame) * 8.0 * 1000.0 / getSampleRate(HeaderFrame);
                else
                {
                    long MPEGSize = sizeInfo.FileSize - sizeInfo.TotalTagSize;
                    return (MPEGSize/* - HeaderFrame.Position*/) / getBitRate(HeaderFrame) * 8;
                }
            else
                return 0;
        }

        private ChannelsArrangement getChannelsArrangement(FrameHeader frame)
        {
            switch (frame.ModeID)
            {
                case MPEG_CM_STEREO: return STEREO;
                case MPEG_CM_JOINT_STEREO: return JOINT_STEREO;
                case MPEG_CM_DUAL_CHANNEL: return DUAL_MONO;
                case MPEG_CM_MONO: return MONO;
                default: return UNKNOWN;
            }
        }

        public static bool HasValidFrame(Stream source)
        {
            VBRData dummyVbr = null;
            return findFrame(source, ref dummyVbr, null).Found;
        }

        private static FrameHeader findNextFrame(Stream source, FrameHeader frame)
        {
            FrameHeader nextHeader = new FrameHeader();
            nextHeader.Reset();

            byte[] nextHeaderData = new byte[4];
            source.Seek(frame.Position + frame.Size, SeekOrigin.Begin);
            source.Read(nextHeaderData, 0, 4);

            if (IsValidFrameHeader(nextHeaderData))
            {
                nextHeader.LoadFromByteArray(nextHeaderData);
                nextHeader.Position = source.Position - 4;
                nextHeader.Found = true;
            }
            return nextHeader;
        }

        private static FrameHeader findFrame(Stream source, ref VBRData oVBR, SizeInfo sizeInfo)
        {
            byte[] headerData = new byte[4];
            FrameHeader result = new FrameHeader();

            source.Read(headerData, 0, 4);
            result.Found = IsValidFrameHeader(headerData);

            /*
             * Many things can actually be found before a proper MP3 header :
             *    - Padding with 0x55, 0xAA and even 0xFF bytes
             *    - RIFF header declaring either MP3 or WAVE data
             *    - Xing encoder-specific frame
             *    - One of the above with a few "parasite" bytes before their own header
             * 
             * The most solid way to deal with all of them is to "scan" the file until proper MP3 header is found.
             * This method may not the be fastest, but ensures audio data is actually detected, whatever garbage lies before
             */

            if (!result.Found)
            {
                // "Quick win" for files starting with padding bytes
                // 4 identical bytes => MP3 starts with padding bytes => Skip padding
                if ((headerData[0] == headerData[1]) && (headerData[1] == headerData[2]) && (headerData[2] == headerData[3]))
                {
                    long paddingEndOffset = StreamUtils.TraversePadding(source);
                    source.Seek(paddingEndOffset, SeekOrigin.Begin);

                    // If padding uses 0xFF bytes, take one step back in case MP3 header lies there
                    if (0xFF == headerData[0]) source.Seek(-1, SeekOrigin.Current);

                    source.Read(headerData, 0, 4);
                    result.Found = IsValidFrameHeader(headerData);
                }

                // Blindly look for the MP3 header
                if (!result.Found)
                {
                    source.Seek(-4, SeekOrigin.Current);
                    long id3v2Size = (null == sizeInfo) ? 0 : sizeInfo.ID3v2Size;
                    long limit = id3v2Size + (long)Math.Round((source.Length - id3v2Size) * 0.3);

                    // Look for the beginning of the MP3 header (2nd byte is variable, so it cannot be searched using a static value)
                    while (!result.Found && source.Position < limit)
                    {
                        while (0xFF != source.ReadByte() && source.Position < limit) { /* just advance the stream */ }

                        source.Seek(-1, SeekOrigin.Current);
                        source.Read(headerData, 0, 4);
                        result.Found = IsValidFrameHeader(headerData);

                        // Valid header candidate found
                        // => let's see if it is a legit MP3 header by using its Size descriptor to find the next header
                        // which in turn should at least share the same version ID and layer
                        if (result.Found)
                        {
                            result.LoadFromByteArray(headerData);
                            result.Position = source.Position - 4;

                            FrameHeader nextFrame = findNextFrame(source, result);
                            result.Found = nextFrame.Found;

                            if (result.Found && result.LayerID == nextFrame.LayerID && result.VersionID == nextFrame.VersionID)
                            {
                                source.Seek(result.Position + 4, SeekOrigin.Begin); // Success ! Go back to header candidate position
                                break;
                            }

                            // Restart looking for a candidate
                            source.Seek(result.Position + 1, SeekOrigin.Begin);
                            result.Found = false;
                        }
                        else
                        {
                            if (source.Position < limit) source.Seek(-3, SeekOrigin.Current);
                            result.Found = false;
                        }
                    }
                }
            }

            if (result.Found && oVBR != null)
            {
                result.LoadFromByteArray(headerData);
                result.Position = source.Position - 4;

                // Look for VBR signature
                oVBR = findVBR(source, result.Position + getVBRDeviation(result));

                // If no VBR found, examine the next header to make sure mode and bitrate are consistent
                if (!oVBR.Found)
                {
                    FrameHeader nextFrame = findNextFrame(source, result);
                    if (nextFrame.Found)
                    {
                        bool sameBase = result.LayerID == nextFrame.LayerID && result.VersionID == nextFrame.VersionID;
                        bool sameProperties = result.ModeID == nextFrame.ModeID && result.BitRateID == nextFrame.BitRateID;

                        if (!sameBase) result.Found = false;
                        else if (!sameProperties)
                        {
                            // First and second headers contradict each other => sample the first 10 headers
                            // and get the most frequent mode and bitrate
                            List<FrameHeader> headers = new List<FrameHeader>
                            {
                                result,
                                nextFrame
                            };
                            for (int i = 0; i < 8; i++)
                            {
                                FrameHeader aFrame = findNextFrame(source, result);
                                if (aFrame.Found) headers.Add(aFrame);
                            }
                            byte finalModeId = headers.GroupBy(x => x.ModeID)
                                .Select(x => new { Mode = x.Key, Count = x.Count() })
                                .OrderByDescending(x => x.Count)
                                .First().Mode;
                            ushort finalBitrateId = headers.GroupBy(x => x.BitRateID)
                                .Select(x => new { BitRateID = x.Key, Count = x.Count() })
                                .OrderByDescending(x => x.Count)
                                .First().BitRateID;
                            result.ModeID = finalModeId;
                            result.BitRateID = finalBitrateId;
                        }
                    }
                }
            }

            return result;
        }

        public bool Read(Stream source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;
            resetData();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            reader.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            HeaderFrame = findFrame(reader, ref vbrData, sizeInfo);

            bool result = HeaderFrame.Found;
            AudioDataOffset = HeaderFrame.Position;
            AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;

            if (!result)
            {
                resetData();
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Could not detect MPEG Audio header starting @ " + sizeInfo.ID3v2Size);
            }

            return result;
        }

    }
}