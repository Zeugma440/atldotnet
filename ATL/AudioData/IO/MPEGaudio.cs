using ATL.Logging;
using Commons;
using System;
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
        public const string VBR_ID_XING = "Xing";                       // Xing VBR ID
        public const string VBR_ID_FHG = "VBRI";                         // FhG VBR ID

        // MPEG version codes
        public const byte MPEG_VERSION_2_5 = 0;                            // MPEG 2.5
        public const byte MPEG_VERSION_UNKNOWN = 1;                 // Unknown version
        public const byte MPEG_VERSION_2 = 2;                                // MPEG 2
        public const byte MPEG_VERSION_1 = 3;                                // MPEG 1

        // MPEG version names
        public static readonly string[] MPEG_VERSION = new string[] { "MPEG 2.5", "MPEG ?", "MPEG 2", "MPEG 1" };

        // MPEG layer codes
        public const byte MPEG_LAYER_UNKNOWN = 0;                     // Unknown layer
        public const byte MPEG_LAYER_III = 1;                             // Layer III
        public const byte MPEG_LAYER_II = 2;                               // Layer II
        public const byte MPEG_LAYER_I = 3;                                 // Layer I

        // MPEG layer names
        public static readonly string[] MPEG_LAYER = new string[] { "Layer ?", "Layer III", "Layer II", "Layer I" };

        // Channel mode codes
        public const byte MPEG_CM_STEREO = 0;                                // Stereo
        public const byte MPEG_CM_JOINT_STEREO = 1;                    // Joint Stereo
        public const byte MPEG_CM_DUAL_CHANNEL = 2;                    // Dual Channel
        public const byte MPEG_CM_MONO = 3;                                    // Mono
        public const byte MPEG_CM_UNKNOWN = 4;                         // Unknown mode

        // Channel mode names
        public static readonly string[] MPEG_CM_MODE = new string[] { "Stereo", "Joint Stereo", "Dual Channel", "Mono", "Unknown" };

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
        public static readonly string[] MPEG_EMPHASIS = new string[] { "None", "50/15 ms", "Unknown", "CCIT J.17" };

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
        public static readonly string[] MPEG_ENCODER = new string[] { "Unknown", "Xing", "FhG", "LAME", "Blade", "GoGo", "Shine", "QDesign" };

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
            public long Offset;                         // Frame position in the file
            private bool Xing;                                 // True if Xing encoder
            public byte VersionID;                                 // MPEG version ID
            public byte LayerID;                                     // MPEG layer ID
            private bool ProtectionBit;                    // True if protected by CRC
            public ushort BitRateID;                                   // Bit rate ID
            private ushort SampleRateID;                             // Sample rate ID
            private bool PaddingBit;                           // True if frame padded
            private bool PrivateBit;                              // Extra information
            public byte ModeID;                                    // Channel mode ID
            private byte ModeExtensionID;      // Mode extension ID (for Joint Stereo)
            private bool CopyrightBit;                    // True if audio copyrighted
            private bool OriginalBit;                        // True if original media
            private byte EmphasisID;                                    // Emphasis ID

            private int m_size;                                  // Frame size (bytes)

            public void Reset()
            {
                Found = false;
                Offset = 0;
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
                PaddingBit = ((data[2] >> 1) & 1) == 1;
                PrivateBit = (data[2] & 1) == 1;
                ModeID = (byte)((data[3] >> 6) & 3);
                ModeExtensionID = (byte)((data[3] >> 4) & 3);
                CopyrightBit = ((data[3] >> 3) & 1) == 1;
                OriginalBit = ((data[3] >> 2) & 1) == 1;
                EmphasisID = (byte)(data[3] & 3);
                m_size = -1;
            }

            /// <summary>
            /// Get MPEG layer name
            /// </summary>
            public string Layer => MPEG_LAYER[LayerID];
            public ushort BitRate => MPEG_BIT_RATE[VersionID, LayerID, BitRateID];
            public ushort SampleRate => MPEG_SAMPLE_RATE[VersionID, SampleRateID];
            public int Padding => PaddingBit ? 1 : 0;


            public int Size
            {
                get
                {
                    if (m_size < 1)
                    {
                        if (MPEG_LAYER_I == LayerID)
                        {
                            m_size = (int)((Math.Floor(Coefficient * BitRate * 1000.0 / SampleRate) + Padding) * 4);
                        }
                        else // Layers II and III
                        {
                            m_size = (int)Math.Floor(Coefficient * BitRate * 1000.0 / SampleRate) + Padding;
                        }
                    }

                    return m_size;
                }
            }

            /// <summary>
            /// Get frame size coefficient
            /// https://stackoverflow.com/a/62539671
            /// </summary>
            public byte Coefficient
            {
                get
                {
                    if (MPEG_VERSION_1 == VersionID)
                        if (MPEG_LAYER_I == LayerID) return 12;
                        else return 144;
                    else
                        if (MPEG_LAYER_I == LayerID) return 12;
                    else if (MPEG_LAYER_II == LayerID) return 144;
                    else return 72;
                }
            }

            public byte VBRDeviation
            {
                get
                {
                    if (MPEG_VERSION_1 == VersionID)
                        if (ModeID != MPEG_CM_MONO) return 36;
                        else return 21;
                    else
                        if (ModeID != MPEG_CM_MONO) return 21;
                    else return 13;
                }
            }

            public ChannelsArrangement ChannelsArrangement
            {
                get
                {
                    switch (ModeID)
                    {
                        case MPEG_CM_STEREO: return STEREO;
                        case MPEG_CM_JOINT_STEREO: return JOINT_STEREO;
                        case MPEG_CM_DUAL_CHANNEL: return DUAL_MONO;
                        case MPEG_CM_MONO: return MONO;
                        default: return UNKNOWN;
                    }
                }
            }

            public double Duration => Size * 1.0 / BitRate * 8.0;
        }

        private VBRData vbrData = new VBRData();
        private FrameHeader FirstFrame = new FrameHeader();
        private readonly AudioFormat audioFormat;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        /// <summary>
        /// VBR header data
        /// </summary>
        public VBRData VBR => vbrData;

        public bool IsVBR => vbrData.Found;
        public double BitRate => getBitRate();
        public int BitDepth => -1; // Irrelevant for lossy formats
        public double Duration => getDuration();
        public ChannelsArrangement ChannelsArrangement => FirstFrame.ChannelsArrangement;
        public int SampleRate => FirstFrame.SampleRate;
        public string FileName { get; }

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        public AudioFormat AudioFormat
        {
            get
            {
                AudioFormat f = new AudioFormat(audioFormat);
                f.Name = f.Name + " (" + FirstFrame.Layer + ")";
                return f;
            }
        }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            vbrData.Reset();
            FirstFrame.Reset();
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public MPEGaudio(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
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
        /// Get bit rate, calculate average bit rate if VBR header found
        /// </summary>
        private double getBitRate()
        {
            if (vbrData.Found && (vbrData.Frames > 0))
                return Math.Round(
                            ((double)vbrData.Bytes / vbrData.Frames - FirstFrame.Padding) *
                            FirstFrame.SampleRate / FirstFrame.Coefficient
                            / 1000.0
                    );
            else
                return FirstFrame.BitRate;
        }

        private static VBRData getXingInfo(Stream source)
        {
            VBRData result = new VBRData();
            byte[] data = new byte[8];

            result.Found = true;
            result.ID = VBR_ID_XING.ToCharArray();
            source.Seek(4, SeekOrigin.Current);
            if (source.Read(data, 0, 8) < 8) return result;

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

            if (source.Read(data, 0, 1) < 1) return result;
            result.Scale = data[0];
            if (source.Read(data, 0, 8) < 8) return result;
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
            if (source.Read(data, 0, 9) < 9) return result;

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

            if (4 == source.Read(data, 0, 4))
            {
                string vbrId = Utils.Latin1Encoding.GetString(data);
                if (VBR_ID_XING.Equals(vbrId)) return getXingInfo(source);
                if (VBR_ID_FHG.Equals(vbrId)) return getFhGInfo(source);
            }

            result = new VBRData();
            result.Reset();
            return result;
        }

        private double getDuration()
        {
            if (FirstFrame.Found)
                if (vbrData.Found && (vbrData.Frames > 0))
                    return vbrData.Frames * FirstFrame.Coefficient * 8.0 * 1000.0 / FirstFrame.SampleRate;
                else return AudioDataSize * 1.0 / FirstFrame.BitRate * 8.0;
            else
                return 0;
        }

        public static bool HasValidFrame(Stream source)
        {
            VBRData dummyVbr = null; // Skips bitrate check
            return findFirstFrame(source, ref dummyVbr, null).Found;
        }

        /// <summary>
        /// Find next MPEG frame
        /// NB : The source need to be positioned at the end of an MPEG frame header
        /// </summary>
        private static FrameHeader findNextFrame(Stream source, FrameHeader startingFrame, byte[] buffer)
        {
            FrameHeader nextHeader = new FrameHeader();
            nextHeader.Reset();

            source.Seek(startingFrame.Offset + startingFrame.Size, SeekOrigin.Begin);
            if (source.Read(buffer, 0, 4) < 4) return nextHeader;

            if (IsValidFrameHeader(buffer))
            {
                nextHeader.LoadFromByteArray(buffer);
                nextHeader.Offset = source.Position - 4;
                nextHeader.Found = true;
            }
            return nextHeader;
        }

        private static FrameHeader findFirstFrame(Stream source, ref VBRData oVBR, SizeInfo sizeInfo)
        {
            FrameHeader result = new FrameHeader();
            byte[] buffer = new byte[4];

            if (source.Read(buffer, 0, 4) < 4) return result;
            result.Found = IsValidFrameHeader(buffer);

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
            if (!result.Found && (buffer[0] == buffer[1]) && (buffer[1] == buffer[2]) && (buffer[2] == buffer[3]))
            {
                // "Quick win" for files starting with padding bytes
                // 4 identical bytes => file starts with padding bytes => Skip padding
                long paddingEndOffset = StreamUtils.TraversePadding(source);
                source.Seek(paddingEndOffset, SeekOrigin.Begin);

                // If padding uses 0xFF bytes, take one step back in case header lies there
                if (0xFF == buffer[0]) source.Seek(-1, SeekOrigin.Current);

                if (source.Read(buffer, 0, 4) < 4) return result;
                result.Found = IsValidFrameHeader(buffer);
            }

            long id3v2Size = (null == sizeInfo) ? 0 : sizeInfo.ID3v2Size;
            long limit = id3v2Size + (long)Math.Round((source.Length - id3v2Size) * 0.3);
            int iterations = 0;
            while (source.Position < limit)
            {
                // If above method fails, blindly look for the first frame header
                // Look for the beginning of the header (2nd byte is variable, so it cannot be searched using a static value)
                while (!result.Found && source.Position < limit)
                {
                    while (0xFF != source.ReadByte() && source.Position < limit) { /* just advance the stream */ }

                    source.Seek(-1, SeekOrigin.Current);
                    if (source.Read(buffer, 0, 4) < 4) break;
                    result.Found = IsValidFrameHeader(buffer);
                }

                // Valid header candidate found
                // => let's see if it is a legit MP3 header by using its Size descriptor to find the next header
                // which in turn should at least share the same version ID and layer
                if (result.Found)
                {
                    result.LoadFromByteArray(buffer);
                    result.Offset = source.Position - 4;

                    // One single frame to analyze
                    if (0 == iterations && source.Length <= result.Offset + result.Size) break;

                    FrameHeader nextFrame = findNextFrame(source, result, buffer);
                    result.Found = nextFrame.Found;

                    if (result.Found && result.LayerID == nextFrame.LayerID && result.VersionID == nextFrame.VersionID)
                    {
                        source.Seek(result.Offset + 4, SeekOrigin.Begin); // Success ! Go back to header candidate position
                        break;
                    }

                    // Restart looking for a candidate
                    source.Seek(result.Offset + 1, SeekOrigin.Begin);
                    result.Found = false;
                }
                else
                {
                    if (source.Position < limit) source.Seek(-3, SeekOrigin.Current);
                    result.Found = false;
                }
                iterations++;
            }

            if (result.Found && oVBR != null)
            {
                // Look for VBR signature
                oVBR = findVBR(source, result.Offset + result.VBRDeviation);

                // If no VBR found, examine the next header to make sure mode and bitrate are consistent
                if (!oVBR.Found)
                {
                    FrameHeader nextFrame = findNextFrame(source, result, buffer);
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
                                FrameHeader aFrame = findNextFrame(source, result, buffer);
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

        private long parseExactAudioDataSize(BufferedBinaryReader reader)
        {
            byte[] buffer = new byte[4];
            reader.Seek(FirstFrame.Offset, SeekOrigin.Begin);
            FrameHeader nextFrame = findNextFrame(reader, FirstFrame, buffer);
            while (nextFrame.Found)
            {
                nextFrame = findNextFrame(reader, nextFrame, buffer);
            }
            return reader.Position - FirstFrame.Offset;
        }

        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            resetData();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            reader.Seek(sizeNfo.ID3v2Size, SeekOrigin.Begin);
            FirstFrame = findFirstFrame(reader, ref vbrData, sizeNfo);

            if (!FirstFrame.Found)
            {
                resetData();
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Could not detect MPEG Audio header starting @ " + sizeNfo.ID3v2Size);
                return false;
            }

            AudioDataOffset = FirstFrame.Offset;
            if (Settings.MP3_parseExactDuration) AudioDataSize = parseExactAudioDataSize(reader);
            else AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

            return true;
        }

    }
}