using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Windows Media Audio 7,8 and 9 files manipulation (extension : .WMA)
    /// </summary>
	class TWMA : AudioDataReader, IMetaDataReader
	{
		// Channel modes
		public const byte WMA_CM_UNKNOWN = 0;                                               // Unknown
		public const byte WMA_CM_MONO = 1;                                                     // Mono
		public const byte WMA_CM_STEREO = 2;                                                 // Stereo

		// Channel mode names
		public static String[] WMA_MODE = new String[3] {"Unknown", "Mono", "Stereo"};

		private byte FChannelModeID;
		private int FSampleRate;
		private bool FIsVBR;
		private bool FIsLossless;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private int FTrack;
        private int FDisc;
        private ushort FRating;
		private String FYear;
		private String FGenre;
		private String FComment;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;
        private StreamUtils.StreamHandlerDelegate FPictureStreamHandler;
				
		public bool Exists // for compatibility with other tag readers
		{
			get { return true; }
		}
		public byte ChannelModeID // Channel mode code
		{
			get { return this.FChannelModeID; }
		}	
		public String ChannelMode // Channel mode name
		{
			get { return this.FGetChannelMode(); }
		}
		public int SampleRate // Sample rate (hz)
		{
			get { return this.FSampleRate; }
		}	

        public override bool IsVBR
		{
			get { return this.FIsVBR; }
		}
		public override int CodecFamily
		{
			get 
			{ 
				if (FIsLossless)
				{
					return AudioReaderFactory.CF_LOSSLESS;
				}
				else
				{
					return AudioReaderFactory.CF_LOSSY;
				}
			}
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }
		public bool IsStreamed
		{
			get { return true; }
		}
		public String Title // Song title
		{
			get { return this.FTitle; }
		}	
		public String Artist // Artist name
		{
			get { return this.FArtist; }
		}
        public String Composer // Composer name
        {
            get { return this.FComposer; }
        }
		public String Album // Album name
		{
			get { return this.FAlbum; }
		}	
		public ushort Track // Track number
		{
			get { return (ushort)this.FTrack; }
		}
        public ushort Disc // Disc number
        {
            get { return (ushort)this.FDisc; }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
        }
		public String Year // Year
		{
			get { return this.FYear; }
		}	
		public String Genre // Genre name
		{
			get { return this.FGenre; }
		}	
		public String Comment // Comment
		{
			get { return this.FComment; }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating the presence of embedded pictures
        {
            get { return this.FPictures; }
        }
			
		// Object IDs
        private static char[] WMA_HEADER_ID = new char[16] { (char)48, (char)38, (char)178, (char)117, (char)142, (char)102, (char)207, (char)17, (char)166, (char)217, (char)0, (char)170, (char)0, (char)98, (char)206, (char)108 };
        private static char[] WMA_FILE_PROPERTIES_ID = new char[16] { (char)161, (char)220, (char)171, (char)140, (char)71, (char)169, (char)207, (char)17, (char)142, (char)228, (char)0, (char)192, (char)12, (char)32, (char)83, (char)101 };
        private static char[] WMA_STREAM_PROPERTIES_ID = new char[16] { (char)145, (char)7, (char)220, (char)183, (char)183, (char)169, (char)207, (char)17, (char)142, (char)230, (char)0, (char)192, (char)12, (char)32, (char)83, (char)101 };
        private static char[] WMA_CONTENT_DESCRIPTION_ID = new char[16] { (char)51, (char)38, (char)178, (char)117, (char)142, (char)102, (char)207, (char)17, (char)166, (char)217, (char)0, (char)170, (char)0, (char)98, (char)206, (char)108 };
        private static char[] WMA_EXTENDED_CONTENT_DESCRIPTION_ID = new char[16] { (char)64, (char)164, (char)208, (char)210, (char)7, (char)227, (char)210, (char)17, (char)151, (char)240, (char)0, (char)160, (char)201, (char)94, (char)168, (char)80 };

		// Format IDs
		private const int WMA_ID				= 0x161;
		private const int WMA_PRO_ID			= 0x162;
		private const int WMA_LOSSLESS_ID		= 0x163;
		private const int WMA_GSM_CBR_ID		= 0x7A21;
		private const int WMA_GSM_VBR_ID		= 0x7A22;

		// Max. number of supported comment fields
		private const byte WMA_FIELD_COUNT = 11;

		// Names of supported comment fields (to be put in caps for reading purposes)
        // NB : WM/TITLE, WM/AUTHOR and WM/DESCRIPTION are not WMA extended fields; therefore
        // their ID will not appear as is in the WMA header. Their info is contained in the standard Content Description block
		private static String[] WMA_FIELD_NAME = new String[WMA_FIELD_COUNT] 
{
	"WM/TITLE", "WM/AUTHOR", "WM/ALBUMTITLE", "WM/TRACK", "WM/YEAR",
	"WM/GENRE", "WM/DESCRIPTION", "WM/PARTOFSET", "WM/COMPOSER", "WM/PICTURE","WM/SharedUserRating" };

		// Max. number of characters in tag field
		private const byte WMA_MAX_STRING_SIZE = 250;

		/*
		// Object ID
		ObjectID = array [1..16] of Char;

		  // Tag data
		TagData == array [1..WMA_FIELD_COUNT] of WideString;
		*/

		// File data - for internal use
		private class FileData
		{
            public long HeaderSize;
            public int FormatTag;										// Format ID tag
			public int MaxBitRate;                                // Max. bit rate (bps)
			public ushort Channels;                                // Number of channels
			public int SampleRate;                                   // Sample rate (hz)
			public int ByteRate;                                            // Byte rate
			public String[] Tag = new String[WMA_FIELD_COUNT];    // WMA tag information
			public void Reset()
			{
                HeaderSize = 0;
				FormatTag = 0;
				MaxBitRate = 0;
				Channels = 0;
				SampleRate = 0;
				ByteRate = 0;
				for (int i=0; i<Tag.Length; i++) Tag[i] = "";
			}
		}

		// ********************* Auxiliary functions & voids ********************

		private String ReadFieldString(BinaryReader Source, ushort DataSize)
		{
            String str = Encoding.Unicode.GetString(Source.ReadBytes(DataSize-2));
            Source.BaseStream.Seek(2, SeekOrigin.Current); // ignore the last null character
            return str;
		}

		// ---------------------------------------------------------------------------

		private void ReadTagStandard(BinaryReader Source, ref String[] Tag)
		{		
            ushort[] FieldSize = new ushort[5];
			String FieldValue;

            // Skip Content Description block size, unused
            Source.BaseStream.Seek(4, SeekOrigin.Current);

			// Read standard tag data
			for (int i=0;i<5;i++)
				FieldSize[i] = Source.ReadUInt16();
  
			for (int iterator=0; iterator<5; iterator++)
				if (FieldSize[iterator] > 0 )
				{
					// Read field value
					FieldValue = ReadFieldString(Source, FieldSize[iterator]);
					// Set corresponding tag field if supported
					switch(iterator)
					{
						case 0: Tag[0] = FieldValue; break; // Title
						case 1: Tag[1] = FieldValue; break; // Author
						case 3: Tag[6] = FieldValue; break; // Description
                        case 4: Tag[10] = FieldValue; break; // Rating
					}
				}
		}

		// ---------------------------------------------------------------------------

		private void ReadTagExtended(BinaryReader Source, ref String[] Tag)
		{
			ushort FieldCount;
			ushort DataSize;
			ushort DataType;
			String FieldName;
			String FieldValue = "";

			// Read extended tag data
			FieldCount = Source.ReadUInt16();
			for (int iterator1=0; iterator1 < FieldCount; iterator1++)
			{
				// Read field name
				DataSize = Source.ReadUInt16();
				FieldName = ReadFieldString(Source, DataSize);
				// Read value data type
				DataType = Source.ReadUInt16();
				DataSize = Source.ReadUInt16();
				
				// Read field value only if string (<-> DataType=0)
				// NB : DataType = 1
				if (0 == DataType) // Unicode string
				{
					FieldValue = ReadFieldString(Source, DataSize);
				}
				if (1 == DataType) // Byte array; not useful here
				{
                    if (FieldName.ToUpper().Equals("WM/PICTURE"))
                    {
                        FPictures.Add(MetaReaderFactory.PIC_CODE.Generic);
                        if (FPictureStreamHandler != null)
                        {
                            // Next 5 bytes usage is unknown
                            Source.BaseStream.Seek(5, SeekOrigin.Current);
                            String mimeType = StreamUtils.ReadNullTerminatedString(Source,2);
                            // Next 2 bytes usage is unknown
                            Source.BaseStream.Seek(2, SeekOrigin.Current);

                            MemoryStream mem = new MemoryStream(DataSize - 3 - (2 * mimeType.Length + 1) - 2);
                            StreamUtils.CopyStreamFrom(mem, Source, DataSize - 3 - (2 * mimeType.Length + 1) - 2);
                            FPictureStreamHandler(ref mem);
                            mem.Close();
                        }
                    }
                    else 
                    {
                        Source.BaseStream.Seek(DataSize, SeekOrigin.Current);
                    }
				}
				if (2 == DataType) // 32-bit Boolean; not useful here
				{
					Source.BaseStream.Seek(DataSize, SeekOrigin.Current);
				}
				if (3 == DataType) // 32-bit unsigned integer
				{
					FieldValue = (Source.ReadUInt32()+1).ToString();
				}
				if (4 == DataType) // 64-bit unsigned integer
				{
					FieldValue = Source.ReadUInt64().ToString();
				}
				if (4 == DataType) // 16-bit unsigned integer
				{
					FieldValue = Source.ReadUInt16().ToString();
				}					

				// Set corresponding tag field if supported
				for (int iterator2=0; iterator2<WMA_FIELD_COUNT; iterator2++)
					if ( WMA_FIELD_NAME[iterator2] == FieldName.Trim().ToUpper() )
					{
						Tag[iterator2] = FieldValue;
					}
			}
		}

		// ---------------------------------------------------------------------------

		private void ReadObject(char[] ID, BinaryReader Source, ref FileData Data)
		{
			// Read data from header object if supported
			if ( StreamUtils.ArrEqualsArr(WMA_FILE_PROPERTIES_ID,ID) )
			{
				// Read file properties
				Source.BaseStream.Seek(80, SeekOrigin.Current);
				Data.MaxBitRate = Source.ReadInt32();
			}
			if ( StreamUtils.ArrEqualsArr(WMA_STREAM_PROPERTIES_ID,ID) )
			{
				// Read stream properties
				Source.BaseStream.Seek(58, SeekOrigin.Current);
				Data.FormatTag = Source.ReadUInt16();
				Data.Channels = Source.ReadUInt16();
				Data.SampleRate = Source.ReadInt32();
				Data.ByteRate = Source.ReadInt32();    
			}
			if ( StreamUtils.ArrEqualsArr(WMA_CONTENT_DESCRIPTION_ID,ID) )
			{
				// Read standard tag data
				ReadTagStandard(Source, ref Data.Tag);
			}
			if ( StreamUtils.ArrEqualsArr(WMA_EXTENDED_CONTENT_DESCRIPTION_ID,ID) )
			{
				// Read extended tag data
				Source.BaseStream.Seek(4, SeekOrigin.Current);
				ReadTagExtended(Source, ref Data.Tag);
			}
		}

		// ---------------------------------------------------------------------------

		private bool ReadData(BinaryReader source, ref FileData Data)
		{
            Stream fs = source.BaseStream;
			char[] ID = new char[16];
			int ObjectCount;
			int ObjectSize;
			long Position;
            long initialPos = fs.Position;

            bool result = false;

			// Check for existing header
            ID = StreamUtils.ReadOneByteChars(source, 16);

			if ( StreamUtils.ArrEqualsArr(WMA_HEADER_ID,ID) )
			{
				fs.Seek(8, SeekOrigin.Current);
                ObjectCount = source.ReadInt32();		  
				fs.Seek(2, SeekOrigin.Current);
				// Read all objects in header and get needed data
				for (int iterator=0; iterator<ObjectCount; iterator++)
				{
					Position = fs.Position;
                    ID = StreamUtils.ReadOneByteChars(source, 16);
                    ObjectSize = source.ReadInt32();
                    ReadObject(ID, source, ref Data);
					fs.Seek(Position + ObjectSize, SeekOrigin.Begin);				
				}
                result = true;
			}

            Data.HeaderSize = fs.Position - initialPos;

            return result;
		}

		// ---------------------------------------------------------------------------

		private bool IsValid(FileData Data)
		{
			// Check for data validity
			return (
				(Data.MaxBitRate > 0) && (Data.MaxBitRate < 320000) &&
				((Data.Channels == WMA_CM_MONO) || (Data.Channels == WMA_CM_STEREO)) &&
				(Data.SampleRate >= 8000) && (Data.SampleRate <= 96000) &&
				(Data.ByteRate > 0) && (Data.ByteRate < 40000) );
		}
        
		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset variables
			FChannelModeID = WMA_CM_UNKNOWN;
			FSampleRate = 0;
			FIsVBR = false;
			FIsLossless = false;
			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FTrack = 0;
            FDisc = 0;
            FRating = 0;
			FYear = "";
			FGenre = "";
			FComment = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			// Get channel mode name
			return WMA_MODE[FChannelModeID];
		}

		// ********************** Public functions & voids **********************

		public TWMA()
		{
			// Create object  
			resetData();
		}

		// ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			FileData Data = new FileData();
            FPictureStreamHandler = pictureStreamHandler;

			// Reset variables and load file data
			Data.Reset();

            bool result = ReadData(source, ref Data);

			// Process data if loaded and valid
			if ( result && IsValid(Data) )
			{
				FValid = true;
				// Fill properties with loaded data
				FChannelModeID = (byte)Data.Channels;
				FSampleRate = Data.SampleRate;
				FDuration = (FFileSize-Data.HeaderSize) * 8 / Data.MaxBitRate;
				FBitrate = Data.ByteRate * 8;
				FIsVBR = (WMA_GSM_VBR_ID == Data.FormatTag);
				FIsLossless = (WMA_LOSSLESS_ID == Data.FormatTag);
				FTitle = Data.Tag[0].Trim();
				FArtist = Data.Tag[1].Trim();
				FAlbum = Data.Tag[2].Trim();
				FTrack = TrackUtils.ExtractTrackNumber(Data.Tag[3]);
				FYear = Data.Tag[4].Trim();
				FGenre = Data.Tag[5].Trim();
				FComment = Data.Tag[6].Trim();
                FDisc = TrackUtils.ExtractTrackNumber(Data.Tag[7]);
                FComposer = Data.Tag[8].Trim();
                FRating = TrackUtils.ExtractIntRating(Data.Tag[10]);
			}
	
			return result;
		}
	}
}