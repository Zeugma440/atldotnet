using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Monkey's Audio file manipulation (extension : .APE)
    /// </summary>
	class TMonkey : AudioDataReader
	{
		// Compression level codes
		public const int MONKEY_COMPRESSION_FAST       = 1000;  // Fast (poor)
		public const int MONKEY_COMPRESSION_NORMAL     = 2000;  // Normal (good)
		public const int MONKEY_COMPRESSION_HIGH       = 3000;  // High (very good)	
		public const int MONKEY_COMPRESSION_EXTRA_HIGH = 4000;  // Extra high (best)
		public const int MONKEY_COMPRESSION_INSANE     = 5000;  // Insane
		public const int MONKEY_COMPRESSION_BRAINDEAD  = 6000;  // BrainDead
	
		// Compression level names
		public String[] MONKEY_COMPRESSION = new String[7]
	{ "Unknown", "Fast", "Normal", "High", "Extra High", "Insane", "BrainDead" };

		// Format flags, only for Monkey's Audio <= 3.97
		public const byte MONKEY_FLAG_8_BIT          = 1;  // Audio 8-bit
		public const byte MONKEY_FLAG_CRC            = 2;  // New CRC32 error detection
		public const byte MONKEY_FLAG_PEAK_LEVEL     = 4;  // Peak level stored
		public const byte MONKEY_FLAG_24_BIT         = 8;  // Audio 24-bit
		public const byte MONKEY_FLAG_SEEK_ELEMENTS  = 16; // Number of seek elements stored
		public const byte MONKEY_FLAG_WAV_NOT_STORED = 32; // WAV header not stored

		// Channel mode names
		public String[] MONKEY_MODE = new String[3]
	{ "Unknown", "Mono", "Stereo" };
	

        APE_HEADER APE = new APE_HEADER();				// common header

    	// Stuff loaded from the header:
		private int FVersion;
		private String FVersionStr;
		private int	FChannels;
		private int	FSampleRate;
		private int	FBits;
		private uint FPeakLevel;
		private double FPeakLevelRatio;
		private long FTotalSamples;
		private int	FCompressionMode;
		private String FCompressionModeStr;
      
		// FormatFlags, only used with Monkey's <= 3.97
		private int FFormatFlags;
		private bool FHasPeakLevel;
		private bool FHasSeekElements;
		private bool FWavNotStored;


        public int Version
		{
			get { return this.FVersion; }
		}
		public String VersionStr
		{
			get { return this.FVersionStr; }
		}	
		public int Channels
		{
			get { return this.FChannels; }
		}	
		public int SampleRate
		{
			get { return this.FSampleRate; }
		}	
		public int Bits 
		{
			get { return this.FBits; }
		}		
		
        public override bool IsVBR
		{
			get { return false; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSLESS; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

        public uint	PeakLevel
		{
			get { return this.FPeakLevel; }
		}
		public double PeakLevelRatio
		{
			get { return this.FPeakLevelRatio; }
		}
		public long	TotalSamples
		{
			get { return this.FTotalSamples; }
		}	
		public int CompressionMode
		{
			get { return this.FCompressionMode; }
		}	
		public String CompressionModeStr
		{
			get { return this.FCompressionModeStr; }
		}

		// FormatFlags, only used with Monkey's <= 3.97
		public int FormatFlags
		{
			get { return this.FFormatFlags; }
		}
		public bool	HasPeakLevel
		{
			get { return this.FHasPeakLevel; }
		}
		public bool	HasSeekElements
		{
			get { return this.FHasSeekElements; }
		}
		public bool	WavNotStored
		{
			get { return this.FWavNotStored; }
		}
    

		// Real structure of Monkey's Audio header
		// common header for all versions
		private class APE_HEADER
		{
			public char[] cID = new char[4]; // should equal 'MAC '
			public ushort nVersion;          // version number * 1000 (3.81 = 3810)
		}

		// old header for <= 3.97
		private struct APE_HEADER_OLD
		{
			public ushort nCompressionLevel; // the compression level
			public ushort nFormatFlags;      // any format flags (for future use)
			public ushort nChannels;         // the number of channels (1 or 2)
			public uint nSampleRate;         // the sample rate (typically 44100)
			public uint nHeaderBytes;        // the bytes after the MAC header that compose the WAV header
			public uint nTerminatingBytes;   // the bytes after that raw data (for extended info)
			public uint nTotalFrames;        // the number of frames in the file
			public uint nFinalFrameBlocks;   // the number of samples in the final frame
			public int nInt;
		}
		// new header for >= 3.98
		private struct APE_HEADER_NEW
		{
			public ushort nCompressionLevel;  // the compression level (see defines I.E. COMPRESSION_LEVEL_FAST)
			public ushort nFormatFlags;		// any format flags (for future use) Note: NOT the same flags as the old header!
			public uint nBlocksPerFrame;		// the number of audio blocks in one frame
			public uint nFinalFrameBlocks;	// the number of audio blocks in the final frame
			public uint nTotalFrames;			// the total number of frames
			public ushort nBitsPerSample;		// the bits per sample (typically 16)
			public ushort nChannels;			// the number of channels (1 or 2)
			public uint nSampleRate;			// the sample rate (typically 44100)
		}
		// data descriptor for >= 3.98
		private class APE_DESCRIPTOR
		{
			public ushort padded;					// padding/reserved (always empty)
			public uint nDescriptorBytes;			// the number of descriptor bytes (allows later expansion of this header)
			public uint nHeaderBytes;			    // the number of header APE_HEADER bytes
			public uint nSeekTableBytes;	        // the number of bytes of the seek table
			public uint nHeaderDataBytes;		    // the number of header data bytes (from original file)
			public uint nAPEFrameDataBytes;		    // the number of bytes of APE frame data
			public uint nAPEFrameDataBytesHigh;	    // the high order number of APE frame data bytes
			public uint nTerminatingDataBytes;		// the terminating data of the file (not including tag data)
			public byte[] cFileMD5 = new byte[16];	// the MD5 hash of the file (see notes for usage... it's a littly tricky)
		}

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset data
			FValid				= false;
			FVersion            = 0;
			FVersionStr         = "";
			FChannels  		    = 0;
			FSampleRate		    = 0;
			FBits      		    = 0;
			FPeakLevel          = 0;
			FPeakLevelRatio     = 0.0;
			FTotalSamples       = 0;
			FCompressionMode    = 0;
			FCompressionModeStr = "";
			FFormatFlags        = 0;
			FHasPeakLevel       = false;
			FHasSeekElements    = false;
			FWavNotStored       = false;
		}

		// ********************** Public functions & voids **********************

		public TMonkey()
		{
			// Create object  
			resetData();
		}

		// ---------------------------------------------------------------------------

		// No explicit destructors in C#

		// ---------------------------------------------------------------------------

        private void readCommonHeader(BinaryReader source)
        {
            source.BaseStream.Seek(FID3v2.Size, SeekOrigin.Begin);

			APE.cID = source.ReadChars(4);
			APE.nVersion = source.ReadUInt16();
        }

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{   
			APE_HEADER_OLD APE_OLD = new APE_HEADER_OLD();	// old header   <= 3.97
			APE_HEADER_NEW APE_NEW = new APE_HEADER_NEW();	// new header   >= 3.98
			APE_DESCRIPTOR APE_DESC = new APE_DESCRIPTOR(); // extra header >= 3.98

            Stream fs = source.BaseStream;

			int BlocksPerFrame;
			bool LoadSuccess;
			long TagSize;
			bool result = false;

			// reading data from file
			LoadSuccess = false;


            // load tags first
            FID3v2.Read(source, pictureStreamHandler);
            FID3v1.Read(source);
            FAPEtag.Read(source, pictureStreamHandler);

            // calculate total tag size
            TagSize = 0;
            if (FID3v1.Exists) TagSize += FID3v1.Size;
            if (FID3v2.Exists) TagSize += FID3v2.Size;
            if (FAPEtag.Exists) TagSize += FAPEtag.Size;

            readCommonHeader(source);

			if ( StreamUtils.StringEqualsArr("MAC ",APE.cID) )
			{
                FValid = true;
				FVersion = APE.nVersion;

				FVersionStr = ((double)FVersion / 1000).ToString().Substring(0,4); //Str(FVersion / 1000 : 4 : 2, FVersionStr);
            
				// Load New Monkey's Audio Header for version >= 3.98
				if (APE.nVersion >= 3980) 
				{
					APE_DESC.padded = 0;
					APE_DESC.nDescriptorBytes = 0;
					APE_DESC.nHeaderBytes = 0;
					APE_DESC.nSeekTableBytes = 0;
					APE_DESC.nHeaderDataBytes = 0;
					APE_DESC.nAPEFrameDataBytes = 0;
					APE_DESC.nAPEFrameDataBytesHigh = 0;
					APE_DESC.nTerminatingDataBytes = 0;
					Array.Clear(APE_DESC.cFileMD5,0,APE_DESC.cFileMD5.Length);

					APE_DESC.padded = source.ReadUInt16();
					APE_DESC.nDescriptorBytes = source.ReadUInt32();
					APE_DESC.nHeaderBytes = source.ReadUInt32();
					APE_DESC.nSeekTableBytes = source.ReadUInt32();
					APE_DESC.nHeaderDataBytes = source.ReadUInt32();
					APE_DESC.nAPEFrameDataBytes = source.ReadUInt32();
					APE_DESC.nAPEFrameDataBytesHigh = source.ReadUInt32();
					APE_DESC.nTerminatingDataBytes = source.ReadUInt32();
					APE_DESC.cFileMD5 = source.ReadBytes(16);

					// seek past description header
					if (APE_DESC.nDescriptorBytes != 52) fs.Seek(APE_DESC.nDescriptorBytes - 52, SeekOrigin.Current);
					// load new ape_header
					if (APE_DESC.nHeaderBytes > 24/*sizeof(APE_NEW)*/) APE_DESC.nHeaderBytes = 24/*sizeof(APE_NEW)*/;
                  				
					APE_NEW.nCompressionLevel = 0;
					APE_NEW.nFormatFlags = 0;
					APE_NEW.nBlocksPerFrame = 0;
					APE_NEW.nFinalFrameBlocks = 0;
					APE_NEW.nTotalFrames = 0;
					APE_NEW.nBitsPerSample = 0;
					APE_NEW.nChannels = 0;
					APE_NEW.nSampleRate = 0;

					APE_NEW.nCompressionLevel = source.ReadUInt16();
					APE_NEW.nFormatFlags = source.ReadUInt16();
					APE_NEW.nBlocksPerFrame = source.ReadUInt32();
					APE_NEW.nFinalFrameBlocks = source.ReadUInt32();
					APE_NEW.nTotalFrames = source.ReadUInt32();
					APE_NEW.nBitsPerSample = source.ReadUInt16();
					APE_NEW.nChannels = source.ReadUInt16();
					APE_NEW.nSampleRate = source.ReadUInt32();
				
					// based on MAC SDK 3.98a1 (APEinfo.h)
					FSampleRate       = (int)APE_NEW.nSampleRate;
					FChannels         = APE_NEW.nChannels;
					FFormatFlags      = APE_NEW.nFormatFlags;
					FBits             = APE_NEW.nBitsPerSample;
					FCompressionMode  = APE_NEW.nCompressionLevel;
					// calculate total uncompressed samples
					if (APE_NEW.nTotalFrames > 0)
					{
						FTotalSamples     = (long)(APE_NEW.nBlocksPerFrame) *
							(long)(APE_NEW.nTotalFrames-1) +
							(long)(APE_NEW.nFinalFrameBlocks);
					}
					LoadSuccess = true;
				}
				else 
				{
					// Old Monkey <= 3.97               

					APE_OLD.nCompressionLevel = 0;
					APE_OLD.nFormatFlags = 0;
					APE_OLD.nChannels = 0;
					APE_OLD.nSampleRate = 0;
					APE_OLD.nHeaderBytes = 0;
					APE_OLD.nTerminatingBytes = 0;
					APE_OLD.nTotalFrames = 0;
					APE_OLD.nFinalFrameBlocks = 0;
					APE_OLD.nInt = 0;

					APE_OLD.nCompressionLevel = source.ReadUInt16();
					APE_OLD.nFormatFlags = source.ReadUInt16();
					APE_OLD.nChannels = source.ReadUInt16();
					APE_OLD.nSampleRate = source.ReadUInt32();
					APE_OLD.nHeaderBytes = source.ReadUInt32();
					APE_OLD.nTerminatingBytes = source.ReadUInt32();
					APE_OLD.nTotalFrames = source.ReadUInt32();
					APE_OLD.nFinalFrameBlocks = source.ReadUInt32();
					APE_OLD.nInt = source.ReadInt32();				

					FCompressionMode  = APE_OLD.nCompressionLevel;
					FSampleRate       = (int)APE_OLD.nSampleRate;
					FChannels         = APE_OLD.nChannels;
					FFormatFlags      = APE_OLD.nFormatFlags;
					FBits = 16;
					if ( (APE_OLD.nFormatFlags & MONKEY_FLAG_8_BIT ) != 0) FBits =  8;
					if ( (APE_OLD.nFormatFlags & MONKEY_FLAG_24_BIT) != 0) FBits = 24;

					FHasSeekElements  = ( (APE_OLD.nFormatFlags & MONKEY_FLAG_PEAK_LEVEL   )  != 0);
					FWavNotStored     = ( (APE_OLD.nFormatFlags & MONKEY_FLAG_SEEK_ELEMENTS) != 0);
					FHasPeakLevel     = ( (APE_OLD.nFormatFlags & MONKEY_FLAG_WAV_NOT_STORED) != 0);
                  
					if (FHasPeakLevel)
					{
						FPeakLevel        = (uint)APE_OLD.nInt;
						FPeakLevelRatio   = (FPeakLevel / (1 << FBits) / 2.0) * 100.0;
					}

					// based on MAC_SDK_397 (APEinfo.cpp)
					if (FVersion >= 3950) 
						BlocksPerFrame = 73728 * 4;
					else if ( (FVersion >= 3900) || ((FVersion >= 3800) && (MONKEY_COMPRESSION_EXTRA_HIGH == APE_OLD.nCompressionLevel)) )
						BlocksPerFrame = 73728;
					else
						BlocksPerFrame = 9216;

					// calculate total uncompressed samples
					if (APE_OLD.nTotalFrames>0)
					{
						FTotalSamples =  (long)(APE_OLD.nTotalFrames-1) *
							(long)(BlocksPerFrame) +
							(long)(APE_OLD.nFinalFrameBlocks);
					}
					LoadSuccess = true;
               
				}
				if (LoadSuccess) 
				{
					// compression profile name
					if ( (0 == (FCompressionMode % 1000)) && (FCompressionMode<=6000) )
					{
						FCompressionModeStr = MONKEY_COMPRESSION[FCompressionMode / 1000]; // int division
					}
					else 
					{
						FCompressionModeStr = FCompressionMode.ToString();
					}
					// length
					if (FSampleRate>0) FDuration = ((double)FTotalSamples / FSampleRate);
					// average bitrate
					if (FDuration>0) FBitrate = 8*(FFileSize - (long)(TagSize)) / (FDuration);
					// some extra sanity checks
					FValid   = ((FBits>0) && (FSampleRate>0) && (FTotalSamples>0) && (FChannels>0));
					result   = FValid;
				}
			}

			return result;
		}

	}
}