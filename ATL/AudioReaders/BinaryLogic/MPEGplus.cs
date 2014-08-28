using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for MusePack / MPEGplus files manipulation (extensions : .MPC, .MP+)
    /// </summary>
	class TMPEGplus : AudioDataReader
	{	
		// Used with ChannelModeID property
		private const byte MPP_CM_STEREO = 1;               // Index for stereo mode
		private const byte MPP_CM_JOINT_STEREO = 2;   // Index for joint-stereo mode

		// Channel mode names
		private String[] MPP_MODE = new String[3] {"Unknown", "Stereo", "Joint Stereo"};
        // Sample frequencies
        private int[] MPP_SAMPLERATES = new int[4] { 44100, 48000, 37800, 32000 };

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
		private String[] MPP_PROFILE = new String[13]
	{
		"Unknown", "Thumb", "Radio", "Standard", "Xtreme", "Insane", "BrainDead",
		"--quality 9", "--quality 10", "--quality 0", "--quality 1", "Telephone", "Experimental"};
    
		private byte FChannelModeID;
		private int FFrameCount;
        private long FSampleCount;
		private int FSampleRate;
		private byte FStreamVersion;
		private byte FProfileID;
		private String FEncoder;
	
		public bool Valid // True if header valid
		{
			get { return this.FValid; }
		}
		public byte ChannelModeID // Channel mode code
		{
			get { return this.FChannelModeID; }
		}	
		public String ChannelMode // Channel mode name
		{
			get { return this.FGetChannelMode(); }
		}		  
		public int FrameCount // Number of frames
		{
			get { return this.FFrameCount; }
		}	

        public override bool IsVBR
		{
			get { return true; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSY; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }
		
        public byte StreamVersion // Stream version
		{
			get { return this.FStreamVersion; }
		}	
		public int SampleRate
		{
			get { return this.FSampleRate; }
		}
		public byte ProfileID // Profile code
		{
			get { return this.FProfileID; }
		}	
		public String Profile // Profile name
		{
			get { return this.FGetProfile(); }
		}	

        public bool Corrupted // True if file corrupted
		{
			get { return this.FIsCorrupted(); }
		}		       
		public String Encoder // Encoder used
		{
			get { return this.FEncoder; }
		}

		// ID code for stream version > 6
		private const long STREAM_VERSION_7_ID = 120279117;  // 120279117 = 'MP+' + #7
		private const long STREAM_VERSION_71_ID = 388714573; // 388714573 = 'MP+' + #23
        private const long STREAM_VERSION_8_ID = 0x4D50434B; // 'MPCK'

		// File header data - for internal use
		private class HeaderRecord
		{
			public byte[] ByteArray = new byte[32];               // Data as byte array
			public int[] IntegerArray = new int[8];            // Data as integer array
		}

		// ********************* Auxiliary functions & voids ********************

		private bool ReadHeader(BinaryReader source, ref HeaderRecord Header)
		{
			bool result = true;
            Stream fs = source.BaseStream;
		
			fs.Seek(FID3v2.Size, SeekOrigin.Begin);
		
			// Read header and get file size
			Header.ByteArray = source.ReadBytes(32);
		
			// if transfer is not complete
			int temp;
			for (int i=0; i<Header.IntegerArray.Length; i++)
			{
				temp =	Header.ByteArray[(i*4)]	*	0x00000001 + 
						Header.ByteArray[(i*4)+1] * 0x00000100 +
						Header.ByteArray[(i*4)+2] * 0x00010000 +
						Header.ByteArray[(i*4)+3] * 0x01000000;
				Header.IntegerArray[i] = temp;
			}

			// If VS8 file, looks for the (mandatory) stream header packet
            if (80 == GetStreamVersion(Header))
            {
                String packetKey = "";
                long packetSize = 0; // Packet size (int only since we are dealing with the header packet)
                long initialPos;
                bool headerFound = false;

                // Let's go back right after the 32-bit version marker
                fs.Seek(ID3v2.Size+4, SeekOrigin.Begin);

                while (!headerFound)
                {
                    initialPos = fs.Position;
                    packetKey = new String( StreamUtils.ReadOneByteChars(source,2) );

                    packetSize = readVariableSizeInteger(source);

                    // SV8 stream header packet
                    if (packetKey.Equals("SH"))
                    {
                        // Skip CRC-32 and stream version
                        source.BaseStream.Seek(5, SeekOrigin.Current);
                        FSampleCount = readVariableSizeInteger(source);
                        readVariableSizeInteger(source); // Skip beginning silence

                        byte b = source.ReadByte();
                        FSampleRate = MPP_SAMPLERATES[ (b & 224) >> 5]; // First 3 bits
                        b = source.ReadByte();
                        long framesPerPacket = (long)Math.Pow(4, (b & 7) ); // Last 3 bits

                        FProfileID = MPP_PROFILE_UNKNOWN;   // Profile info is SV7-only
                        FEncoder = "";                      // Encoder info is SV7-only
                        
                        // MPC has variable bitrate; only MPC versions < 7 display fixed bitrate
                        FDuration = (double)FSampleCount / FSampleRate;
                        FBitrate = calculateAverageBitrate(FDuration);
 
                        headerFound = true;
                    }
                    // Continue searching for header
                    fs.Seek(initialPos+2, SeekOrigin.Begin);
                }
            }

			return result;
		}

		// ---------------------------------------------------------------------------

		private byte GetStreamVersion(HeaderRecord Header)
		{
			byte result;

			// Get MPEGplus stream version
			if (STREAM_VERSION_7_ID == Header.IntegerArray[0])
				result = 70;
			else if (STREAM_VERSION_71_ID == Header.IntegerArray[0])
				result = 71;
            else if (STREAM_VERSION_8_ID == StreamUtils.ReverseInt32(Header.IntegerArray[0]))
                result = 80;
			else
				switch( (Header.ByteArray[1] % 32) / 2 ) //Int division
				{
					case 3: result = 40; break;
					case 7: result = 50; break;
					case 11: result = 60; break;
					default: result = 0; break;
				}

			return result;
		}

		// ---------------------------------------------------------------------------

		private int GetSampleRate(HeaderRecord Header)
		{
			/* get samplerate from header
			   note: this is the same byte where profile is stored
			*/
			return MPP_SAMPLERATES[Header.ByteArray[10] & 3];
		}

		// ---------------------------------------------------------------------------

		private String GetEncoder(HeaderRecord Header)
		{
			int EncoderID;
			String result = "";

			EncoderID = Header.ByteArray[10+2+15];   
			if (0 == EncoderID)
			{
				//FEncoder := 'Buschmann 1.7.0...9, Klemm 0.90...1.05';
			} 
			else 
			{
				switch ( EncoderID % 10 ) 
				{
					case 0:  result = "Release "+(EncoderID / 100)+"."+ ( (EncoderID / 10) % 10 ); break;
					case 2 : result = "Beta "+(EncoderID / 100)+"."+ (EncoderID % 100); break; // Not exactly...
					case 4: goto case 2;
					case 6: goto case 2;
					case 8: goto case 2;
					default: result = "--Alpha-- "+(EncoderID / 100)+"."+(EncoderID % 100); break;
				}
			}
		
			return result;
		}
		// ---------------------------------------------------------------------------

		private byte GetChannelModeID(HeaderRecord Header)
		{
			byte result;

			if ((70 == GetStreamVersion(Header)) || (71 == GetStreamVersion(Header)))
				// Get channel mode for stream version 7
				if ((Header.ByteArray[11] % 128) < 64) result = MPP_CM_STEREO;
				else result = MPP_CM_JOINT_STEREO;
			else
				// Get channel mode for stream version 4-6
				if (0 == (Header.ByteArray[2] % 128)) result = MPP_CM_STEREO;
			else result = MPP_CM_JOINT_STEREO;
	
			return result;
		}

		// ---------------------------------------------------------------------------

		private int GetFrameCount(HeaderRecord Header)
		{
			int result;

			// Get frame count
			if (40 == GetStreamVersion(Header) ) result = Header.IntegerArray[1] >> 16;
			else
				if ((50 <= GetStreamVersion(Header)) && (GetStreamVersion(Header) <= 71) )
				result = Header.IntegerArray[1];
			else result = 0; 
 
			return result;
		}

		// ---------------------------------------------------------------------------

		private double getBitRate(HeaderRecord Header)
		{
            double result = 0;

			// Try to get bit rate from header
			if ( (60 >= GetStreamVersion(Header)) /*|| (5 == GetStreamVersion(Header))*/ )
			{
				result = (ushort)((Header.IntegerArray[0] >> 23)& 0x01FF);
			}

            // Calculate bit rate if not given
            result = calculateAverageBitrate(FGetDuration());

            return result;
		}

        // ---------------------------------------------------------------------------

        private double calculateAverageBitrate(double duration)
        {
            double result = 0;
            long CompressedSize;

            CompressedSize = FFileSize - FID3v1.Size - FID3v2.Size - FAPEtag.Size;

            if (duration > 0) result = Math.Round(CompressedSize * 8 / duration);

            return result;
        }

		// ---------------------------------------------------------------------------

		private byte GetProfileID(HeaderRecord Header)
		{
			byte result = MPP_PROFILE_UNKNOWN;
			// Get MPEGplus profile (exists for stream version 7 only)
			if ( (70 == GetStreamVersion(Header)) || (71 == GetStreamVersion(Header)) )
				// ((and $F0) shr 4) is needed because samplerate is stored in the same byte!
				switch( ((Header.ByteArray[10] & 0xF0) >> 4) )
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

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			FValid = false;
			FChannelModeID = 0;
			FFrameCount = 0;
			FStreamVersion = 0;
			FSampleRate = 0;
            FSampleCount = 0;
			FEncoder = "";
			FProfileID = MPP_PROFILE_UNKNOWN;
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			return MPP_MODE[FChannelModeID];
		}

		// ---------------------------------------------------------------------------

		private String FGetProfile()
		{
			return MPP_PROFILE[FProfileID];
		}

		// ---------------------------------------------------------------------------

		private double FGetDuration()
		{
			// Calculate duration time
			if (FSampleRate > 0)
				return ((double)FFrameCount * 1152 / FSampleRate);
			else return 0;
		}

		// ---------------------------------------------------------------------------

		private bool FIsCorrupted()
		{
			// Check for file corruption
			return ( (FValid) && (FBitrate < 3) || (FBitrate > 480) );
		}

		// ********************** Public functions & voids **********************

		public TMPEGplus()
		{  
			resetData();
		}

		// ---------------------------------------------------------------------------

		// No explicit destructors with C#

		// ---------------------------------------------------------------------------

		public override bool ReadFromFile(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			HeaderRecord Header = new HeaderRecord();
			bool result;
            byte version;
            Stream fs = source.BaseStream;

			// Load header from file to variable
			Array.Clear(Header.ByteArray,0,Header.ByteArray.Length);
			Array.Clear(Header.IntegerArray,0,Header.IntegerArray.Length);

            // At first try to load ID3v2 tag data, then header
            FID3v1.ReadFromFile(source);
            FID3v2.ReadFromFile(source, pictureStreamHandler);
            FAPEtag.ReadFromFile(source, pictureStreamHandler);

            result = ReadHeader(source, ref Header);
            version = GetStreamVersion(Header);
            // Process data if loaded and file valid
            if ((result) && (FFileSize > 0) && (version > 0))
            {
                FValid = true;
                FStreamVersion = version;
                if (version < 80)
                {
                    // Fill properties with SV7 header data
                    FSampleRate = GetSampleRate(Header);
                    FChannelModeID = GetChannelModeID(Header);
                    FFrameCount = GetFrameCount(Header);
                    FBitrate = getBitRate(Header);
                    FProfileID = GetProfileID(Header);
                    FEncoder = GetEncoder(Header);
                    FDuration = FGetDuration();
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