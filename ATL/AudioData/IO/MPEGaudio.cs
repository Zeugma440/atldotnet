using ATL.Logging;
using Commons;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for MPEG Audio Layer files manipulation (extensions : .MP1, .MP2, .MP3)
    /// </summary>
	class MPEGaudio : IAudioDataIO
	{

		// Table for bit rates (KBit/s)
		public static readonly ushort[,,] MPEG_BIT_RATE = new ushort[4,4,16]
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
		public static readonly ushort[,] MPEG_SAMPLE_RATE = new ushort[4,4]
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
		public static readonly String[] MPEG_VERSION = new String[4] {"MPEG 2.5", "MPEG ?", "MPEG 2", "MPEG 1"};

		// MPEG layer codes
		public const byte MPEG_LAYER_UNKNOWN = 0;                     // Unknown layer
		public const byte MPEG_LAYER_III = 1;                             // Layer III
		public const byte MPEG_LAYER_II = 2;                               // Layer II
		public const byte MPEG_LAYER_I = 3;                                 // Layer I

		// MPEG layer names
		public static readonly String[] MPEG_LAYER = new String[4]	{"Layer ?", "Layer III", "Layer II", "Layer I"};

		// Channel mode codes
		public const byte MPEG_CM_STEREO = 0;                                // Stereo
		public const byte MPEG_CM_JOINT_STEREO = 1;                    // Joint Stereo
		public const byte MPEG_CM_DUAL_CHANNEL = 2;                    // Dual Channel
		public const byte MPEG_CM_MONO = 3;                                    // Mono
		public const byte MPEG_CM_UNKNOWN = 4;                         // Unknown mode

		// Channel mode names
		public static readonly String[] MPEG_CM_MODE = new String[5] {"Stereo", "Joint Stereo", "Dual Channel", "Mono", "Unknown"};

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
		public static readonly String[] MPEG_EMPHASIS = new String[4] {"None", "50/15 ms", "Unknown", "CCIT J.17"};

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
		public static readonly String[] MPEG_ENCODER = new String[8] {"Unknown", "Xing", "FhG", "LAME", "Blade", "GoGo", "Shine", "QDesign"};

		// Xing/FhG VBR header data
		public class VBRData
		{
			public bool Found;                            // True if VBR header found
			public char[] ID = new char[4];            // Header ID: "Xing" or "VBRI"
			public int Frames;                              // Total number of frames
			public int Bytes;                                // Total number of bytes
			public byte Scale;                                  // VBR scale (1..100)
			public String VendorID;                         // Vendor ID (if present)

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
		public class FrameHeader
		{
			public bool Found;                                 // True if frame found
			public long Position;                        // Frame position in the file
			public int Size;                                 // Frame size (bytes)
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

            public void Reset()
            {
                Found = false;
                Position = 0;
                Size = 0;
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
            }
        }  
      
		private String vendorID;
		private VBRData vbrData = new VBRData();
		private FrameHeader HeaderFrame = new FrameHeader();
        private SizeInfo sizeInfo;
        private readonly String filePath;
    
		public VBRData VBR // VBR header data
		{
			get { return this.vbrData; }
		}	
		public FrameHeader Frame // Frame header data
		{
			get { return this.HeaderFrame; }
		}
        public String Version // MPEG version name
        {
            get { return this.getVersion(); }
        }
        public String Layer // MPEG layer name
        {
            get { return this.getLayer(); }
        }
        public String ChannelMode // Channel mode name
        {
            get { return this.getChannelMode(); }
        }
        public String Emphasis // Emphasis name
        {
            get { return this.getEmphasis(); }
        }
        public long Frames // Total number of frames
        {
            get { return this.getFrames(); }
        }
        public byte EncoderID // Guessed encoder ID
        {
            get { return this.getEncoderID(); }
        }
        public String Encoder // Guessed encoder name
        {
            get { return this.getEncoder(); }
        }
        public bool Valid // True if MPEG file valid
        {
            get { return this.getValid(); }
        }
        public bool IsVBR
		{
			get { return this.vbrData.Found; }
		}
        public double BitRate
        {
            get { return getBitRate() / 1000.0; }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public int SampleRate
        {
            get { return getSampleRate(); }
        }
        public string FileName
        {
            get { return filePath; }
        }

        // Limitation constants
        public const int MAX_MPEG_FRAME_LENGTH = 8068;          // Max. MPEG frame length according to all extreme values
        public const int MIN_MPEG_BIT_RATE = 8;                 // Min. bit rate value (KBit/s)
        public const int MAX_MPEG_BIT_RATE = 448;               // Max. bit rate value (KBit/s)
		public const double MIN_ALLOWED_DURATION = 0.1;         // Min. song duration value


        // VBR Vendor ID strings
        public const String VENDOR_ID_LAME = "LAME";                      // For LAME
		public const String VENDOR_ID_GOGO_NEW = "GOGO";            // For GoGo (New)
		public const String VENDOR_ID_GOGO_OLD = "MPGE";            // For GoGo (Old)


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            // Reset all variables
            vendorID = "";

            vbrData.Reset();
            HeaderFrame.Reset();
        }

        public MPEGaudio(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        public bool AllowsParsableMetadata
        {
            get { return true; }
        }
        public bool HasNativeMeta()
        {
            return false;
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2) || (metaDataType == MetaDataIOFactory.TAG_APE);
        }

        // ********************* Auxiliary functions & voids ********************

        private static bool isValidFrameHeader(byte[] HeaderData)
		{
            if (HeaderData.Length != 4) return false;

			// Check for valid frame header
            return !(
                ((HeaderData[0] & 0xFF) != 0xFF) ||                         // First 11 bits are set
                ((HeaderData[1] & 0xE0) != 0xE0) ||                         // First 11 bits are set
                (((HeaderData[1] >> 3) & 3) == MPEG_VERSION_UNKNOWN) ||     // MPEG version > 1
                (((HeaderData[1] >> 1) & 3) == MPEG_LAYER_UNKNOWN) ||       // Layer I, II or III
                ((HeaderData[2] & 0xF0) == 0xF0) ||                         // Bitrate index is not 'bad'
                ((HeaderData[2] & 0xF0) == 0) ||                            // Bitrate index is not 'free'
                (((HeaderData[2] >> 2) & 3) == MPEG_SAMPLE_RATE_UNKNOWN) || // Sampling rate is not 'reserved'
                ((HeaderData[3] & 3) == MPEG_EMPHASIS_UNKNOWN)              // Emphasis is not 'reserved'
                );
		}

		private static byte getCoefficient(FrameHeader Frame)
		{
			// Get frame size coefficient
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
			// Get bit rate
			return MPEG_BIT_RATE[Frame.VersionID, Frame.LayerID, Frame.BitRateID];
		}

		private static ushort getSampleRate(FrameHeader Frame)
		{
			// Get sample rate
			return MPEG_SAMPLE_RATE[Frame.VersionID, Frame.SampleRateID];
		}

		private static byte getPadding(FrameHeader Frame)
		{
			// Get frame padding
			if (Frame.PaddingBit)
				if (MPEG_LAYER_I == Frame.LayerID) return 4;
				else return 1;
			else return 0;
		}

		private static int getFrameSize(FrameHeader Frame)
		{
			ushort Coefficient;
			ushort BitRate;
			ushort SampleRate;
			ushort Padding;

			// Calculate MPEG frame length
			Coefficient = getCoefficient(Frame);
			BitRate = getBitRate(Frame);
			SampleRate = getSampleRate(Frame);
			Padding = getPadding(Frame);
		
            // This formula only works for Layers II and III
			return (int)Math.Floor(Coefficient * BitRate * 1000.0 / SampleRate) + Padding; 
		}

		private static bool isXing(int Index, byte[] Data)
		{
			// Get true if Xing encoder
			return ( (Data[Index] == 0) &&
				(Data[Index + 1] == 0) &&
				(Data[Index + 2] == 0) &&
				(Data[Index + 3] == 0) &&
				(Data[Index + 4] == 0) &&
				(Data[Index + 5] == 0) );
		}

		private static VBRData getXingInfo(Stream source, long position)
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

            result.Scale = (byte)source.ReadByte();
            source.Read(data, 0, 8);
            result.VendorID = Utils.Latin1Encoding.GetString(data, 0, 8);
            /*
                        result.Frames =
                            Data[position + 8] * 0x1000000 +
                            Data[position + 9] * 0x10000 +
                            Data[position + 10] * 0x100 +
                            Data[position + 11];
                        result.Bytes =
                            Data[position + 12] * 0x1000000 +
                            Data[position + 13] * 0x10000 +
                            Data[position + 14] * 0x100 +
                            Data[position + 15];
                        result.Scale = Data[position + 119];

                        // Vendor ID may not be present
                        result.VendorID = Utils.Latin1Encoding.GetString(Data, position+120, 8);
            */

            return result;
		}

		private static VBRData getFhGInfo(Stream source, long position)
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

            /*
			result.Scale = Data[position + 9];
			result.Bytes =
				Data[position + 10] * 0x1000000 +
				Data[position + 11] * 0x10000 +
				Data[position + 12] * 0x100 +
				Data[position + 13];
			result.Frames =
				Data[position + 14] * 0x1000000 +
				Data[position + 15] * 0x10000 +
				Data[position + 16] * 0x100 +
				Data[position + 17];
            */
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

			if ( VBR_ID_XING.Equals(vbrId) ) result = getXingInfo(source, position);
            else if (VBR_ID_FHG.Equals(vbrId)) result = getFhGInfo(source, position);
            else
            {
                result = new VBRData();
                result.Reset();
            }

			return result;
		}

		private static byte getVBRDeviation(FrameHeader Frame)
		{
			// Calculate VBR deviation
			if (MPEG_VERSION_1 == Frame.VersionID)
				if (Frame.ModeID != MPEG_CM_MONO) return 36;
				else return 21;
			else
				if (Frame.ModeID != MPEG_CM_MONO) return 21;
			else return 13;
		}

		private static FrameHeader findFrame(Stream source, ref VBRData oVBR)
		{
			byte[] headerData = new byte[4];  
			FrameHeader result = new FrameHeader();

            result.Found = false;

            source.Read(headerData, 0, 4);

            if (isValidFrameHeader(headerData))
            {
                result.LoadFromByteArray(headerData);

                result.Found = true;
                result.Position = source.Position;
                result.Size = getFrameSize(result);

                // result.Xing = isXing(i + 4, Data); // Will look into it when encoder ID is needed by upper interfaces

                // Look for VBR signature
                oVBR = findVBR(source, result.Position - 4 + getVBRDeviation(result));
            }
            else
            {
                result.Found = false;
            }
/*
			for (int i=0; i <= Data.Length - MAX_MPEG_FRAME_LENGTH; i++)
			{
				// Decode data if frame header found
				if ( isValidFrameHeader(HeaderData) )
				{
                    result.Reset();
					decodeHeader(HeaderData, ref result);
                    frameLength = getFrameLength(result);
					// Check for next frame and try to find VBR header
                    if (validFrameAt(i + frameLength, Data))
					{
                        result.Found = true;
						result.Position = i;
                        result.Size = frameLength;
						result.Xing = isXing(i + 4, Data);
						oVBR = findVBR(i + getVBRDeviation(result), Data);
						break;
					}
				}
				// Prepare next data block
                Array.ConstrainedCopy(Data, i + 1, HeaderData, 0, 4);
			}
*/

			return result;
		}

        // Nightmarish implementation to be redone when Vendor ID is really useful
		private static String findVendorID(byte[] Data, int Size)
		{
			String VendorID;
			String result = "";

			// Search for vendor ID
			if ( (Data.Length - Size - 8) < 0 ) Size = Data.Length - 8;
			for (int i=0; i <= Size; i++)
			{
                VendorID = Utils.Latin1Encoding.GetString(Data, Data.Length - i - 8, 4);
				if (VENDOR_ID_LAME == VendorID)
				{
                    result = VendorID + Utils.Latin1Encoding.GetString(Data, Data.Length - i - 4, 4);
					break;
				}
				else if (VENDOR_ID_GOGO_NEW == VendorID)
				{
					result = VendorID;
					break;
				}
			}
			return result;
		}

		private String getVersion()
		{
			// Get MPEG version name
			return MPEG_VERSION[HeaderFrame.VersionID];
		}

		private String getLayer()
		{
			// Get MPEG layer name
			return MPEG_LAYER[HeaderFrame.LayerID];
		}

		private double getBitRate()
		{
			// Get bit rate, calculate average bit rate if VBR header found
			if ((vbrData.Found) && (vbrData.Frames > 0))
				return Math.Round(((double)vbrData.Bytes / vbrData.Frames - getPadding(HeaderFrame)) *
					(double)getSampleRate(HeaderFrame) / getCoefficient(HeaderFrame));
			else
				return getBitRate(HeaderFrame) * 1000;
		}

		private ushort getSampleRate()
		{
			// Get sample rate
			return getSampleRate(HeaderFrame);
		}

		private String getChannelMode()
		{
			// Get channel mode name
			return MPEG_CM_MODE[HeaderFrame.ModeID];
		}

		private String getEmphasis()
		{
			// Get emphasis name
			return MPEG_EMPHASIS[HeaderFrame.EmphasisID];
		}

		private long getFrames()
		{
 			// Get total number of frames, calculate if VBR header not found
			if (vbrData.Found) return vbrData.Frames;
			else
			{
                long MPEGSize = sizeInfo.FileSize - sizeInfo.ID3v2Size - sizeInfo.ID3v1Size - sizeInfo.APESize;
    
				return (long)Math.Floor(1.0*(MPEGSize - HeaderFrame.Position) / getFrameSize(HeaderFrame));
			}
		}

		private double getDuration()
		{
			// Calculate song duration
			if (HeaderFrame.Found)
				if ((vbrData.Found) && (vbrData.Frames > 0))
					return vbrData.Frames * getCoefficient(HeaderFrame) * 8.0 / getSampleRate(HeaderFrame);
				else
				{
                    long MPEGSize = sizeInfo.FileSize - sizeInfo.ID3v2Size - sizeInfo.ID3v1Size - sizeInfo.APESize;
                    return (MPEGSize - HeaderFrame.Position) / getBitRate(HeaderFrame) / 1000.0 * 8;
				}
			else
				return 0;
		}

		private byte getVBREncoderID()
		{
			// Guess VBR encoder and get ID
			byte result = 0;

			if (VENDOR_ID_LAME == vbrData.VendorID.Substring(0, 4))
				result = MPEG_ENCODER_LAME;
			if (VENDOR_ID_GOGO_NEW == vbrData.VendorID.Substring(0, 4))
				result = MPEG_ENCODER_GOGO;
			if (VENDOR_ID_GOGO_OLD == vbrData.VendorID.Substring(0, 4))
				result = MPEG_ENCODER_GOGO;
			if ( StreamUtils.StringEqualsArr(VBR_ID_XING,vbrData.ID) &&
				(vbrData.VendorID.Substring(0, 4) != VENDOR_ID_LAME) &&
				(vbrData.VendorID.Substring(0, 4) != VENDOR_ID_GOGO_NEW) &&
				(vbrData.VendorID.Substring(0, 4) != VENDOR_ID_GOGO_OLD) )
				result = MPEG_ENCODER_XING;
			if ( StreamUtils.StringEqualsArr(VBR_ID_FHG,vbrData.ID))
				result = MPEG_ENCODER_FHG;

			return result;
		}

		private byte getCBREncoderID()
		{
			// Guess CBR encoder and get ID
			byte result = MPEG_ENCODER_FHG;

			if ( (HeaderFrame.OriginalBit) &&
				(HeaderFrame.ProtectionBit) )
				result = MPEG_ENCODER_LAME;
			if ( (getBitRate(HeaderFrame) <= 160) &&
				(MPEG_CM_STEREO == HeaderFrame.ModeID)) 
				result = MPEG_ENCODER_BLADE;
			if ((HeaderFrame.CopyrightBit) &&
				(HeaderFrame.OriginalBit) &&
				(! HeaderFrame.ProtectionBit) )
				result = MPEG_ENCODER_XING;
			if ((HeaderFrame.Xing) &&
				(HeaderFrame.OriginalBit) )
				result = MPEG_ENCODER_XING;
			if (MPEG_LAYER_II == HeaderFrame.LayerID)
				result = MPEG_ENCODER_QDESIGN;
			if ((MPEG_CM_DUAL_CHANNEL == HeaderFrame.ModeID) &&
				(HeaderFrame.ProtectionBit) )
				result = MPEG_ENCODER_SHINE;
			if (VENDOR_ID_LAME == vendorID.Substring(0, 4))
				result = MPEG_ENCODER_LAME;
			if (VENDOR_ID_GOGO_NEW == vendorID.Substring(0, 4))
				result = MPEG_ENCODER_GOGO;
		
			return result;
		}

		private byte getEncoderID()
		{
			// Get guessed encoder ID
			if (HeaderFrame.Found)
				if (vbrData.Found) return getVBREncoderID();
				else return getCBREncoderID();
			else
				return 0;
		}

		private String getEncoder()
		{
			String VendorID = "";
			String result;

			// Get guessed encoder name and encoder version for LAME
			result = MPEG_ENCODER[getEncoderID()];
			if (vbrData.VendorID != "") VendorID = vbrData.VendorID;
			if (vendorID != "") VendorID = vendorID;
			if ( (MPEG_ENCODER_LAME == getEncoderID()) &&
				(VendorID.Length >= 8) &&
				Char.IsDigit(VendorID[4]) &&
				(VendorID[5] == '.') &&
				(Char.IsDigit(VendorID[6]) &&
				Char.IsDigit(VendorID[7]) ))
				result =
					result + (char)32 +
					VendorID[4] +
					VendorID[5] +
					VendorID[6] +
					VendorID[7];
			return result;
		}

		private bool getValid()
		{
			// Check for right MPEG file data
			return
				((HeaderFrame.Found) &&
				(getBitRate() >= MIN_MPEG_BIT_RATE) &&
				(getBitRate() <= MAX_MPEG_BIT_RATE) &&
				(getDuration() >= MIN_ALLOWED_DURATION));
		}

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
//			byte[] Data = new byte[MAX_MPEG_FRAME_LENGTH * 2];
            this.sizeInfo = sizeInfo;
            resetData();

			bool result = false;

            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
			HeaderFrame = findFrame(source.BaseStream, ref vbrData);

            // Try to search in the middle if no frame found at the beginning
            /* 
             * TODO - this is a shabby implementation -> wrap with a unit test and optimize, or delete
             * 
             * /
            if ( ! HeaderFrame.Found ) 
			{
                source.BaseStream.Seek((long)Math.Floor((sizeInfo.FileSize- sizeInfo.ID3v2Size) / 2.0),SeekOrigin.Begin);
                HeaderFrame = findFrame(Data, ref FVBR);
			}
			
            
            // Search for vendor ID at the end if CBR encoded
/*
 *  This is a nightmarish implementation; to be redone when vendor ID is required by upper interfaces
 *  
			if ( (HeaderFrame.Found) && (! FVBR.Found) )
			{
                fs.Seek(sizeInfo.FileSize - Data.Length - sizeInfo.ID3v1Size - sizeInfo.APESize, SeekOrigin.Begin);
                fs.Read(Data, 0, Data.Length);
                vendorID = findVendorID(Data, HeaderFrame.Size * 5);
			}
*/
			result = HeaderFrame.Found;

            if (!result) resetData();
	
			return result;
		}

    }
}