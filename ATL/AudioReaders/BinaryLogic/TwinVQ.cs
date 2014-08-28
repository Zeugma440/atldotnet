using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for TwinVQ files manipulation (extension : .VQF)
    /// </summary>
	class TTwinVQ : AudioDataReader
	{
	 
		// Used with ChannelModeID property
		public const byte TWIN_CM_MONO = 1;               // Index for mono mode
		public const byte TWIN_CM_STEREO = 2;           // Index for stereo mode

		// Channel mode names
		public String[] TWIN_MODE = new String[3] {"Unknown", "Mono", "Stereo"};

		// Private declarations
		private byte FChannelModeID;
		private ushort FSampleRate;
		private String FTitle;
		private String FComment;
		private String FAuthor;
		private String FCopyright;
		private String FOriginalFile;
		private String FAlbum;
      
		public byte ChannelModeID // Channel mode code
		{
			get { return this.FChannelModeID; }
		}
		public String ChannelMode // Channel mode name
		{
			get { return this.FGetChannelMode(); }
		}	
		public ushort SampleRate // Sample rate (hz)
		{
			get { return this.FSampleRate; }
		}

        public override bool IsVBR
		{
			get { return false; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSY; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		public String Title // Title name
		{
			get { return this.FTitle; }
		}		  
		public String Comment // Comment
		{
			get { return this.FComment; }
		}	
		public String Author // Author name
		{
			get { return this.FAuthor; }
		}	
		public String Copyright // Copyright
		{
			get { return this.FCopyright; }
		}	
		public String OriginalFile // Original file name
		{
			get { return this.FOriginalFile; }
		}	
		public String Album // Album title
		{
			get { return this.FAlbum; }
		}
		public bool Corrupted // True if file corrupted
		{
			get { return this.FIsCorrupted(); }
		}	

		// Twin VQ header ID
		private const String TWIN_ID = "TWIN";
  
		// Max. number of supported tag-chunks
		private const byte TWIN_CHUNK_COUNT = 6;

		// Names of supported tag-chunks
		private String[] TWIN_CHUNK = new String[TWIN_CHUNK_COUNT]
	{ "NAME", "COMT", "AUTH", "(c) ", "FILE", "ALBM"};


		// TwinVQ chunk header
		private class ChunkHeader
		{
			public char[] ID = new char[4];                                // Chunk ID
			public uint Size;                                            // Chunk size
			public void Reset()
			{
				Array.Clear(ID,0,ID.Length);
				Size = 0;
			}
		}

		// File header data - for internal use
		private class HeaderInfo
		{
			// Real structure of TwinVQ file header
			public char[] ID = new char[4];                           // Always "TWIN"
			public char[] Version = new char[8];                         // Version ID
			public uint Size;                                           // Header size
			public ChunkHeader Common = new ChunkHeader();      // Common chunk header
			public uint ChannelMode;             // Channel mode: 0 - mono, 1 - stereo
			public uint BitRate;                                     // Total bit rate
			public uint SampleRate;                               // Sample rate (khz)
			public uint SecurityLevel;                                     // Always 0
			// Extended data
			public String[] Tag = new String[TWIN_CHUNK_COUNT];     // Tag information
		}

		// ********************* Auxiliary functions & voids ********************

		private bool ReadHeader(BinaryReader source, ref HeaderInfo Header)
		{
			bool result = true;
            Stream fs = source.BaseStream;

			// Read header and get file size
			Header.ID = source.ReadChars(4);
			Header.Version = source.ReadChars(8);
			Header.Size = source.ReadUInt32();
			Header.Common.ID = source.ReadChars(4);
			Header.Common.Size = source.ReadUInt32();
			Header.ChannelMode = source.ReadUInt32();
			Header.BitRate = source.ReadUInt32();
			Header.SampleRate = source.ReadUInt32();
			Header.SecurityLevel = source.ReadUInt32();

			return result;
		}

		// ---------------------------------------------------------------------------

		private byte GetChannelModeID(HeaderInfo Header)
		{
			// Get channel mode from header
			switch ( StreamUtils.ReverseInt32((int)(Header.ChannelMode >> 16)) )
			{
				case 0: return TWIN_CM_MONO;
				case 1: return TWIN_CM_STEREO;
				default: return 0;
			}
		}

		// ---------------------------------------------------------------------------

		private byte GetBitRate(HeaderInfo Header)
		{
			// Get bit rate from header
            return (byte)StreamUtils.ReverseInt32((int)(Header.BitRate >> 16));
		}

		// ---------------------------------------------------------------------------

		private ushort GetSampleRate(HeaderInfo Header)
		{
            ushort result = (ushort)StreamUtils.ReverseInt32((int)(Header.SampleRate >> 16));
			// Get real sample rate from header  
			switch(result)
			{
				case 11: result = 11025; break;
				case 22: result = 22050; break;
				case 44: result = 44100; break;
				default: result = (ushort)(result * 1000); break;
			}
			return result;
		}

		// ---------------------------------------------------------------------------

		private double GetDuration(HeaderInfo Header)
		{
			// Get duration from header
            return Math.Abs((FFileSize - StreamUtils.ReverseInt32((int)(Header.Size >> 16)) - 20)) / 125 /
                StreamUtils.ReverseInt32((int)(Header.BitRate >> 16));
		}

		// ---------------------------------------------------------------------------

		private bool HeaderEndReached(ChunkHeader Chunk)
		{
			// Check for header end
			return ( ((byte)(Chunk.ID[1]) < 32) ||
				((byte)(Chunk.ID[2]) < 32) ||
				((byte)(Chunk.ID[3]) < 32) ||
				((byte)(Chunk.ID[4]) < 32) ||
				StreamUtils.StringEqualsArr("DATA",Chunk.ID) );
		}

		// ---------------------------------------------------------------------------

		private void SetTagItem(String ID, String Data, ref HeaderInfo Header)
		{
			// Set tag item if supported tag-chunk found
			for (byte iterator=0; iterator<TWIN_CHUNK_COUNT;iterator++)
				if (ID == TWIN_CHUNK[iterator]) Header.Tag[iterator] = Data;
		}

		// ---------------------------------------------------------------------------

		private bool ReadTag(BinaryReader source, ref HeaderInfo Header)
		{ 
			ChunkHeader Chunk = new ChunkHeader();
			char[] Data = new char[250];
            Stream fs = source.BaseStream;

            bool result = false;

			fs.Seek(16, SeekOrigin.Begin);
			do
			{
				Array.Clear(Data,0,Data.Length);
				
                // Read chunk header (length : 8 bytes)
                Chunk.ID = source.ReadChars(4);
                Chunk.Size = source.ReadUInt32();		

				// Read chunk data and set tag item if chunk header valid
				if ( HeaderEndReached(Chunk) ) break;

                Data = source.ReadChars(StreamUtils.ReverseInt32((int)(Chunk.Size >> 16)) % 250);

				SetTagItem(new String(Chunk.ID), new String(Data), ref Header);

                result = true;
			}
			while (fs.Position < fs.Length);

            return result;
		}

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			FChannelModeID = 0;
			FSampleRate = 0;
			FTitle = "";
			FComment = "";
			FAuthor = "";
			FCopyright = "";
			FOriginalFile = "";
			FAlbum = "";
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			return TWIN_MODE[FChannelModeID];
		}

		// ---------------------------------------------------------------------------

		private bool FIsCorrupted()
		{
			// Check for file corruption
			return ( (FValid) &&
				((0 == FChannelModeID) ||
                (FBitrate < 8000) || (FBitrate > 192000) ||
				(FSampleRate < 8000) || (FSampleRate > 44100) ||
				(FDuration < 0.1) || (FDuration > 10000)) );
		}

		// ********************** Public functions & voids **********************

		public TTwinVQ()
		{
			resetData();
		}

		// ---------------------------------------------------------------------------

        public override bool ReadFromFile(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			HeaderInfo Header = new HeaderInfo();

			bool result = ReadHeader(source, ref Header);
			// Process data if loaded and header valid
			if ( (result) && StreamUtils.StringEqualsArr(TWIN_ID,Header.ID) )
			{
				FValid = true;
				// Fill properties with header data
				FChannelModeID = GetChannelModeID(Header);
				FBitrate = GetBitRate(Header)*1000;
				FSampleRate = GetSampleRate(Header);
				FDuration = GetDuration(Header);
				// Get tag information and fill properties
				ReadTag(source, ref Header);
				FTitle = Header.Tag[0].Trim();
				FComment = Header.Tag[1].Trim();
				FAuthor = Header.Tag[2].Trim();
				FCopyright = Header.Tag[3].Trim();
				FOriginalFile = Header.Tag[4].Trim();
				FAlbum = Header.Tag[5].Trim();
			}
			return result;
		}

	}
}