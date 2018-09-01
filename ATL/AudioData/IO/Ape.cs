using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Monkey's Audio file manipulation (extension : .APE)
    /// </summary>
	class APE : IAudioDataIO
	{
		// Compression level codes
		public const int MONKEY_COMPRESSION_FAST       = 1000;  // Fast (poor)
		public const int MONKEY_COMPRESSION_NORMAL     = 2000;  // Normal (good)
		public const int MONKEY_COMPRESSION_HIGH       = 3000;  // High (very good)	
		public const int MONKEY_COMPRESSION_EXTRA_HIGH = 4000;  // Extra high (best)
		public const int MONKEY_COMPRESSION_INSANE     = 5000;  // Insane
		public const int MONKEY_COMPRESSION_BRAINDEAD  = 6000;  // BrainDead
	
		// Compression level names
		public static readonly string[] MONKEY_COMPRESSION = new string[7] { "Unknown", "Fast", "Normal", "High", "Extra High", "Insane", "BrainDead" };

		// Format flags, only for Monkey's Audio <= 3.97
		public const byte MONKEY_FLAG_8_BIT          = 1;  // Audio 8-bit
		public const byte MONKEY_FLAG_CRC            = 2;  // New CRC32 error detection
		public const byte MONKEY_FLAG_PEAK_LEVEL     = 4;  // Peak level stored
		public const byte MONKEY_FLAG_24_BIT         = 8;  // Audio 24-bit
		public const byte MONKEY_FLAG_SEEK_ELEMENTS  = 16; // Number of seek elements stored
		public const byte MONKEY_FLAG_WAV_NOT_STORED = 32; // WAV header not stored

		// Channel mode names
		public static readonly string[] MONKEY_MODE = new string[3]	{ "Unknown", "Mono", "Stereo" };
	

        ApeHeader header = new ApeHeader();				// common header

    	// Stuff loaded from the header:
		private int version;
		private string versionStr;
		private int	channels;
		private int	sampleRate;
		private int	bits;
		private uint peakLevel;
		private double peakLevelRatio;
		private long totalSamples;
		private int	compressionMode;
		private string compressionModeStr;
      
		// FormatFlags, only used with Monkey's <= 3.97
		private int formatFlags;
		private bool hasPeakLevel;
		private bool hasSeekElements;
		private bool wavNotStored;

        private double bitrate;
        private double duration;
        private bool isValid;

        private AudioDataManager.SizeInfo sizeInfo;
        private string filePath;



        // Real structure of Monkey's Audio header
        // common header for all versions
        private class ApeHeader
        {
            public char[] cID = new char[4]; // should equal 'MAC '
            public ushort nVersion;          // version number * 1000 (3.81 = 3810)
        }

        // old header for <= 3.97
        private struct ApeHeaderOld
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
        private struct ApeHeaderNew
        {
            public ushort nCompressionLevel;  // the compression level (see defines I.E. COMPRESSION_LEVEL_FAST)
            public ushort nFormatFlags;     // any format flags (for future use) Note: NOT the same flags as the old header!
            public uint nBlocksPerFrame;        // the number of audio blocks in one frame
            public uint nFinalFrameBlocks;  // the number of audio blocks in the final frame
            public uint nTotalFrames;           // the total number of frames
            public ushort nBitsPerSample;       // the bits per sample (typically 16)
            public ushort nChannels;            // the number of channels (1 or 2)
            public uint nSampleRate;            // the sample rate (typically 44100)
        }
        // data descriptor for >= 3.98
        private class ApeDescriptor
        {
            public ushort padded;                   // padding/reserved (always empty)
            public uint nDescriptorBytes;           // the number of descriptor bytes (allows later expansion of this header)
            public uint nHeaderBytes;               // the number of header APE_HEADER bytes
            public uint nSeekTableBytes;            // the number of bytes of the seek table
            public uint nHeaderDataBytes;           // the number of header data bytes (from original file)
            public uint nAPEFrameDataBytes;         // the number of bytes of APE frame data
            public uint nAPEFrameDataBytesHigh;     // the high order number of APE frame data bytes
            public uint nTerminatingDataBytes;      // the terminating data of the file (not including tag data)
            public byte[] cFileMD5 = new byte[16];  // the MD5 hash of the file (see notes for usage... it's a littly tricky)
        }


        public int Version
		{
			get { return this.version; }
		}
		public string VersionStr
		{
			get { return this.versionStr; }
		}	
		public int Channels
		{
			get { return this.channels; }
		}	
		public int Bits 
		{
			get { return this.bits; }
		}		
        public uint	PeakLevel
		{
			get { return this.peakLevel; }
		}
		public double PeakLevelRatio
		{
			get { return this.peakLevelRatio; }
		}
		public long	TotalSamples
		{
			get { return this.totalSamples; }
		}	
		public int CompressionMode
		{
			get { return this.compressionMode; }
		}	
		public string CompressionModeStr
		{
			get { return this.compressionModeStr; }
		}

		// FormatFlags, only used with Monkey's <= 3.97
		public int FormatFlags
		{
			get { return this.formatFlags; }
		}
		public bool	HasPeakLevel
		{
			get { return this.hasPeakLevel; }
		}
		public bool	HasSeekElements
		{
			get { return this.hasSeekElements; }
		}
		public bool	WavNotStored
		{
			get { return this.wavNotStored; }
		}

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES
        public int SampleRate
        {
            get { return this.sampleRate; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSLESS; }
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
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_APE) || (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
		{
			// Reset data
			isValid				= false;
			version            = 0;
			versionStr         = "";
			channels  		    = 0;
			sampleRate		    = 0;
			bits      		    = 0;
			peakLevel          = 0;
			peakLevelRatio     = 0.0;
			totalSamples       = 0;
			compressionMode    = 0;
			compressionModeStr = "";
			formatFlags        = 0;
			hasPeakLevel       = false;
			hasSeekElements    = false;
			wavNotStored       = false;
            bitrate = 0;
            duration = 0;
		}

		public APE(string filePath)
		{
            this.filePath = filePath;
			resetData();
		}

        
        // ---------- SUPPORT METHODS

        private void readCommonHeader(BinaryReader source)
        {
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

			header.cID = source.ReadChars(4);
			header.nVersion = source.ReadUInt16();
        }

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            ApeHeaderOld APE_OLD = new ApeHeaderOld();	// old header   <= 3.97
			ApeHeaderNew APE_NEW = new ApeHeaderNew();	// new header   >= 3.98
			ApeDescriptor APE_DESC = new ApeDescriptor(); // extra header >= 3.98

			int BlocksPerFrame;
			bool LoadSuccess;
			bool result = false;

            this.sizeInfo = sizeInfo;
            resetData();

            // reading data from file
            LoadSuccess = false;

            readCommonHeader(source);

			if ( StreamUtils.StringEqualsArr("MAC ",header.cID) )
			{
                isValid = true;
				version = header.nVersion;

				versionStr = ((double)version / 1000).ToString().Substring(0,4); //Str(FVersion / 1000 : 4 : 2, FVersionStr);
            
				// Load New Monkey's Audio Header for version >= 3.98
				if (header.nVersion >= 3980) 
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
					if (APE_DESC.nDescriptorBytes != 52) source.BaseStream.Seek(APE_DESC.nDescriptorBytes - 52, SeekOrigin.Current);
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
					sampleRate       = (int)APE_NEW.nSampleRate;
					channels         = APE_NEW.nChannels;
					formatFlags      = APE_NEW.nFormatFlags;
					bits             = APE_NEW.nBitsPerSample;
					compressionMode  = APE_NEW.nCompressionLevel;
					// calculate total uncompressed samples
					if (APE_NEW.nTotalFrames > 0)
					{
						totalSamples     = (long)(APE_NEW.nBlocksPerFrame) *
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

					compressionMode  = APE_OLD.nCompressionLevel;
					sampleRate       = (int)APE_OLD.nSampleRate;
					channels         = APE_OLD.nChannels;
					formatFlags      = APE_OLD.nFormatFlags;
					bits = 16;
					if ( (APE_OLD.nFormatFlags & MONKEY_FLAG_8_BIT ) != 0) bits =  8;
					if ( (APE_OLD.nFormatFlags & MONKEY_FLAG_24_BIT) != 0) bits = 24;

					hasSeekElements  = ( (APE_OLD.nFormatFlags & MONKEY_FLAG_PEAK_LEVEL   )  != 0);
					wavNotStored     = ( (APE_OLD.nFormatFlags & MONKEY_FLAG_SEEK_ELEMENTS) != 0);
					hasPeakLevel     = ( (APE_OLD.nFormatFlags & MONKEY_FLAG_WAV_NOT_STORED) != 0);
                  
					if (hasPeakLevel)
					{
						peakLevel        = (uint)APE_OLD.nInt;
						peakLevelRatio   = (peakLevel / (1 << bits) / 2.0) * 100.0;
					}

					// based on MAC_SDK_397 (APEinfo.cpp)
					if (version >= 3950) 
						BlocksPerFrame = 73728 * 4;
					else if ( (version >= 3900) || ((version >= 3800) && (MONKEY_COMPRESSION_EXTRA_HIGH == APE_OLD.nCompressionLevel)) )
						BlocksPerFrame = 73728;
					else
						BlocksPerFrame = 9216;

					// calculate total uncompressed samples
					if (APE_OLD.nTotalFrames>0)
					{
						totalSamples =  (long)(APE_OLD.nTotalFrames-1) *
							(long)(BlocksPerFrame) +
							(long)(APE_OLD.nFinalFrameBlocks);
					}
					LoadSuccess = true;
               
				}
				if (LoadSuccess) 
				{
					// compression profile name
					if ( (0 == (compressionMode % 1000)) && (compressionMode<=6000) )
					{
						compressionModeStr = MONKEY_COMPRESSION[compressionMode / 1000]; // int division
					}
					else 
					{
						compressionModeStr = compressionMode.ToString();
					}
					// length
					if (sampleRate > 0) duration = ((double)totalSamples * 1000.0 / sampleRate);
					// average bitrate
					if (duration > 0) bitrate = 8 * (sizeInfo.FileSize - sizeInfo.TotalTagSize) / (duration);
					// some extra sanity checks
					isValid   = ((bits>0) && (sampleRate>0) && (totalSamples>0) && (channels>0));
					result   = isValid;
				}
			}

			return result;
		}

	}
}