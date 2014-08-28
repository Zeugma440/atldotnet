using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for OGG Vorbis files manipulation (extensions : .OGG)
    /// </summary>
	class TOggVorbis : AudioDataReader, IMetaDataReader
	{
		// Used with ChannelModeID property
		private const byte VORBIS_CM_MONO = 1;				// Code for mono mode		
		private const byte VORBIS_CM_STEREO = 2;			// Code for stereo mode
		private const byte VORBIS_CM_MULTICHANNEL = 6;		// Code for Multichannel Mode

        private const String PICTURE_METADATA_ID_NEW = "METADATA_BLOCK_PICTURE";
        private const String PICTURE_METADATA_ID_OLD = "COVERART";

		// Channel mode names
		private static String[] VORBIS_MODE = new String[4]
		{"Unknown", "Mono", "Stereo", "Multichannel"};

        private FileInfo Info = new FileInfo();
        
        private byte FChannelModeID;
		private int FSampleRate;
		private ushort FBitRateNominal;
		private int FSamples;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private ushort FTrack;
        private ushort FDisc;
        private ushort FRating;
		private String FDate;
		private String FGenre;
		private String FComment;
		private String FVendor;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;
        private StreamUtils.StreamHandlerDelegate FPictureStreamHandler;
      
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
		public ushort BitRateNominal // Nominal bit rate
		{
			get { return this.FBitRateNominal; }
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

		public String Title // Song title
		{
			get { return this.FTitle; }
			set { this.FTitle = value; }
		}
        public String Artist // Artist name
        {
            get { return this.FArtist; }
            set { this.FArtist = value; }
        }
        public String Composer // Composer name
        {
            get { return this.FComposer; }
            set { this.FComposer = value; }
        }
		public String Album // Album name
		{
			get { return this.FAlbum; }
			set { this.FAlbum = value; }
		}			  
		public ushort Track // Track number
		{
			get { return this.FTrack; }
			set { this.FTrack = value; }
		}
        public ushort Disc // Disc number
        {
            get { return this.FDisc; }
            set { this.FDisc = value; }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
            set { this.FRating = value; }
        }	
		public String Year // Year
		{
			get { return this.FDate; }
			set { this.FDate = value; }
		}			  
		public String Genre // Genre name
		{
			get { return this.FGenre; }
			set { this.FGenre = value; }
		}			  
		public String Comment // Comment
		{
			get { return this.FComment; }
			set { this.FComment = value; }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating the presence of embedded pictures
        {
            get { return this.FPictures; }
        }


		public String Vendor // Vendor string
		{
			get { return this.FVendor; }
		}	
		public bool Exists // True if ID3v2 tag exists
		{
            get { return FID3v2.Exists; }
		}	
		public bool Valid // True if file valid
		{
			get { return this.FIsValid(); }
		}	
  
		// Ogg page header ID
		private const String OGG_PAGE_ID = "OggS";

		// Vorbis parameter frame ID
		private String VORBIS_PARAMETERS_ID = (char)1 + "vorbis";

		// Vorbis tag frame ID
		private String VORBIS_TAG_ID = (char)3 + "vorbis";

		// Max. number of supported comment fields
		private const sbyte VORBIS_FIELD_COUNT = 12;

		// Names of supported comment fields
		private String[] VORBIS_FIELD = new String[VORBIS_FIELD_COUNT] 
			{
				"TITLE", "ARTIST", "ALBUM", "TRACKNUMBER", "DATE", "GENRE", "COMMENT",
				"PERFORMER", "DESCRIPTION", "DISCNUMBER", "COMPOSER", "RATING" };

		// CRC table for checksum calculating
		private uint[] CRC_TABLE = new uint[(0xFF)+1] {
														  0x00000000, 0x04C11DB7, 0x09823B6E, 0x0D4326D9, 0x130476DC, 0x17C56B6B,
														  0x1A864DB2, 0x1E475005, 0x2608EDB8, 0x22C9F00F, 0x2F8AD6D6, 0x2B4BCB61,
														  0x350C9B64, 0x31CD86D3, 0x3C8EA00A, 0x384FBDBD, 0x4C11DB70, 0x48D0C6C7,
														  0x4593E01E, 0x4152FDA9, 0x5F15ADAC, 0x5BD4B01B, 0x569796C2, 0x52568B75,
														  0x6A1936C8, 0x6ED82B7F, 0x639B0DA6, 0x675A1011, 0x791D4014, 0x7DDC5DA3,
														  0x709F7B7A, 0x745E66CD, 0x9823B6E0, 0x9CE2AB57, 0x91A18D8E, 0x95609039,
														  0x8B27C03C, 0x8FE6DD8B, 0x82A5FB52, 0x8664E6E5, 0xBE2B5B58, 0xBAEA46EF,
														  0xB7A96036, 0xB3687D81, 0xAD2F2D84, 0xA9EE3033, 0xA4AD16EA, 0xA06C0B5D,
														  0xD4326D90, 0xD0F37027, 0xDDB056FE, 0xD9714B49, 0xC7361B4C, 0xC3F706FB,
														  0xCEB42022, 0xCA753D95, 0xF23A8028, 0xF6FB9D9F, 0xFBB8BB46, 0xFF79A6F1,
														  0xE13EF6F4, 0xE5FFEB43, 0xE8BCCD9A, 0xEC7DD02D, 0x34867077, 0x30476DC0,
														  0x3D044B19, 0x39C556AE, 0x278206AB, 0x23431B1C, 0x2E003DC5, 0x2AC12072,
														  0x128E9DCF, 0x164F8078, 0x1B0CA6A1, 0x1FCDBB16, 0x018AEB13, 0x054BF6A4,
														  0x0808D07D, 0x0CC9CDCA, 0x7897AB07, 0x7C56B6B0, 0x71159069, 0x75D48DDE,
														  0x6B93DDDB, 0x6F52C06C, 0x6211E6B5, 0x66D0FB02, 0x5E9F46BF, 0x5A5E5B08,
														  0x571D7DD1, 0x53DC6066, 0x4D9B3063, 0x495A2DD4, 0x44190B0D, 0x40D816BA,
														  0xACA5C697, 0xA864DB20, 0xA527FDF9, 0xA1E6E04E, 0xBFA1B04B, 0xBB60ADFC,
														  0xB6238B25, 0xB2E29692, 0x8AAD2B2F, 0x8E6C3698, 0x832F1041, 0x87EE0DF6,
														  0x99A95DF3, 0x9D684044, 0x902B669D, 0x94EA7B2A, 0xE0B41DE7, 0xE4750050,
														  0xE9362689, 0xEDF73B3E, 0xF3B06B3B, 0xF771768C, 0xFA325055, 0xFEF34DE2,
														  0xC6BCF05F, 0xC27DEDE8, 0xCF3ECB31, 0xCBFFD686, 0xD5B88683, 0xD1799B34,
														  0xDC3ABDED, 0xD8FBA05A, 0x690CE0EE, 0x6DCDFD59, 0x608EDB80, 0x644FC637,
														  0x7A089632, 0x7EC98B85, 0x738AAD5C, 0x774BB0EB, 0x4F040D56, 0x4BC510E1,
														  0x46863638, 0x42472B8F, 0x5C007B8A, 0x58C1663D, 0x558240E4, 0x51435D53,
														  0x251D3B9E, 0x21DC2629, 0x2C9F00F0, 0x285E1D47, 0x36194D42, 0x32D850F5,
														  0x3F9B762C, 0x3B5A6B9B, 0x0315D626, 0x07D4CB91, 0x0A97ED48, 0x0E56F0FF,
														  0x1011A0FA, 0x14D0BD4D, 0x19939B94, 0x1D528623, 0xF12F560E, 0xF5EE4BB9,
														  0xF8AD6D60, 0xFC6C70D7, 0xE22B20D2, 0xE6EA3D65, 0xEBA91BBC, 0xEF68060B,
														  0xD727BBB6, 0xD3E6A601, 0xDEA580D8, 0xDA649D6F, 0xC423CD6A, 0xC0E2D0DD,
														  0xCDA1F604, 0xC960EBB3, 0xBD3E8D7E, 0xB9FF90C9, 0xB4BCB610, 0xB07DABA7,
														  0xAE3AFBA2, 0xAAFBE615, 0xA7B8C0CC, 0xA379DD7B, 0x9B3660C6, 0x9FF77D71,
														  0x92B45BA8, 0x9675461F, 0x8832161A, 0x8CF30BAD, 0x81B02D74, 0x857130C3,
														  0x5D8A9099, 0x594B8D2E, 0x5408ABF7, 0x50C9B640, 0x4E8EE645, 0x4A4FFBF2,
														  0x470CDD2B, 0x43CDC09C, 0x7B827D21, 0x7F436096, 0x7200464F, 0x76C15BF8,
														  0x68860BFD, 0x6C47164A, 0x61043093, 0x65C52D24, 0x119B4BE9, 0x155A565E,
														  0x18197087, 0x1CD86D30, 0x029F3D35, 0x065E2082, 0x0B1D065B, 0x0FDC1BEC,
														  0x3793A651, 0x3352BBE6, 0x3E119D3F, 0x3AD08088, 0x2497D08D, 0x2056CD3A,
														  0x2D15EBE3, 0x29D4F654, 0xC5A92679, 0xC1683BCE, 0xCC2B1D17, 0xC8EA00A0,
														  0xD6AD50A5, 0xD26C4D12, 0xDF2F6BCB, 0xDBEE767C, 0xE3A1CBC1, 0xE760D676,
														  0xEA23F0AF, 0xEEE2ED18, 0xF0A5BD1D, 0xF464A0AA, 0xF9278673, 0xFDE69BC4,
														  0x89B8FD09, 0x8D79E0BE, 0x803AC667, 0x84FBDBD0, 0x9ABC8BD5, 0x9E7D9662,
														  0x933EB0BB, 0x97FFAD0C, 0xAFB010B1, 0xAB710D06, 0xA6322BDF, 0xA2F33668,
														  0xBCB4666D, 0xB8757BDA, 0xB5365D03, 0xB1F740B4};

		// Ogg page header
		private class OggHeader 
		{
			public char[] ID = new char[4];                           // Always "OggS"
			public byte StreamVersion;                     // Stream structure version
			public byte TypeFlag;                                  // Header type flag
			public long AbsolutePosition;                 // Absolute granule position
			public int Serial;                                 // Stream serial number
			public int PageNumber;                             // Page sequence number
			public int Checksum;                                      // Page checksum
			public byte Segments;                           // Number of page segments
			public byte[] LacingValues = new byte[0xFF]; // Lacing values - segment sizes

			public void Reset()
			{
				Array.Clear(ID,0,ID.Length);
				StreamVersion = 0;
				TypeFlag = 0;
				AbsolutePosition = 0;
				Serial = 0;
				PageNumber = 0;
				Checksum = 0;
				Segments = 0;
				Array.Clear(LacingValues,0,LacingValues.Length);
			}

            public int GetPageLength()
            {
                int length = 0;
                for (int i = 0; i < Segments; i++)
                {
                    length += LacingValues[i];
                }
                return length;
            }
		}

		// Vorbis parameter header
		private class VorbisHeader
		{
			public char[] ID = new char[7];                    // Always #1 + "vorbis"
			public byte[] BitstreamVersion = new byte[4];  // Bitstream version number
			public byte ChannelMode;                             // Number of channels
			public int SampleRate;                                 // Sample rate (hz)
			public int BitRateMaximal;                         // Bit rate upper limit
			public int BitRateNominal;                             // Nominal bit rate
			public int BitRateMinimal;                         // Bit rate lower limit
			public byte BlockSize;             // Coded size for small and long blocks
			public byte StopFlag;                                          // Always 1

			public void Reset()
			{
				Array.Clear(ID,0,ID.Length);
				Array.Clear(BitstreamVersion,0,BitstreamVersion.Length);
				ChannelMode = 0;
				SampleRate = 0;
				BitRateMaximal = 0;
				BitRateNominal = 0;
				BitRateMinimal = 0;
				BlockSize = 0;
				StopFlag = 0;
			}
		}

		// Vorbis tag data
		private class VorbisTag
		{
			public char[] ID = new char[7];                    // Always #3 + "vorbis"
			public int Fields;                                 // Number of tag fields
            public long size;                                  // Size of Vorbis Tag
			public String[] FieldData = new String [VORBIS_FIELD_COUNT+1]; // Tag field data

			public void Reset()
			{
				Array.Clear(ID,0,ID.Length);
				Fields = 0;
				// Don't fill this with null, rather with "" :)
				for (int i=0; i<VORBIS_FIELD_COUNT+1; i++)
				{
					FieldData[i] = "";
				}
			}
		}

		// File data
		private class FileInfo
		{
			public OggHeader FPage = new OggHeader();
			public OggHeader SPage = new OggHeader();
			public OggHeader LPage = new OggHeader();   // First, second and last page
			public VorbisHeader Parameters = new VorbisHeader(); // Vorbis parameter header
			public VorbisTag Tag = new VorbisTag();                 // Vorbis tag data
			public int Samples;                             // Total number of samples
			public int SPagePos;                        // Position of second Ogg page
			public int TagEndPos;                                  // Tag end position

			public void Reset()
			{
				FPage.Reset();
				SPage.Reset();
				LPage.Reset();
				Parameters.Reset();
				Tag.Reset();
				Samples = 0;
				SPagePos = 0;
				TagEndPos = 0;
			}
		}

		// ---------------------------------------------------------------------------

        // Reads large data chunks by streaming
        private void SetExtendedTagItem(Stream Source, int size)
        {
            String tagId = "";
            int IdSize = 1;
            char c = StreamUtils.ReadOneByteChar(Source);
            
            while (c != '=')
            {
                tagId += c;
                IdSize++;
                c = StreamUtils.ReadOneByteChar(Source);
            }

            if (tagId.Equals(PICTURE_METADATA_ID_NEW))
            {
                if (FPictureStreamHandler != null)
                {
                    size = size - 1 - PICTURE_METADATA_ID_NEW.Length;
                    // Read the whole base64-encoded picture header _and_ binary data
                    MemoryStream mem = new MemoryStream(size);
                        StreamUtils.CopyMemoryStreamFrom(mem, Source, size);
                        byte[] encodedData = mem.GetBuffer();
                    mem.Close();

                    mem = new MemoryStream();
                        StreamUtils.DecodeFrom64(encodedData, mem);
                        mem.Seek(0, SeekOrigin.Begin);
                        TFLACFile.FlacMetaDataBlockPicture block = TFLACFile.ReadMetadataBlockPicture(mem);
                        FPictures.Add(block.picCode);

                        MemoryStream picMem = new MemoryStream(block.picDataLength);
                            StreamUtils.CopyMemoryStreamFrom(picMem, mem, block.picDataLength);
                            //for (int i = 0; i < block.picDataLength; i++) picMem.WriteByte((byte)mem.ReadByte());
                            FPictureStreamHandler(ref picMem);
                        picMem.Close();
                    mem.Close();
                }
                else
                {
                    Pictures.Add(MetaReaderFactory.PIC_CODE.Generic);
                }
            }
            else if (tagId.Equals(PICTURE_METADATA_ID_OLD)) // Deprecated picture info
            {
                Pictures.Add(MetaReaderFactory.PIC_CODE.Generic);
                if (FPictureStreamHandler != null)
                {
                    size = size - 1 - PICTURE_METADATA_ID_OLD.Length;
                    // Read the whole base64-encoded picture binary data
                    MemoryStream mem = new MemoryStream(size);
                        StreamUtils.CopyMemoryStreamFrom(mem, Source, size);
                        byte[] encodedData = mem.GetBuffer();
                    mem.Close();

                    mem = new MemoryStream();
                        StreamUtils.DecodeFrom64(encodedData, mem);
                        FPictureStreamHandler(ref mem);
                    mem.Close();
                }
            }
        }

		private void SetTagItem(String Data, ref FileInfo Info)
		{
			int Separator;	
			String FieldID;
			String FieldData;

			// Set Vorbis tag item if supported comment field found
			Separator = Data.IndexOf("=");
			if (Separator > 0)
			{
				FieldID = Data.Substring(0,Separator).ToUpper();		
				FieldData = Data.Substring(Separator + 1, Data.Length - FieldID.Length - 1);
				for (int index=0; index < VORBIS_FIELD_COUNT; index++)
					if (VORBIS_FIELD[index] == FieldID)
						Info.Tag.FieldData[index+1] = FieldData.Trim();
			}
			else
				if ("" == Info.Tag.FieldData[0]) Info.Tag.FieldData[0] = Data;		
		}

		// ---------------------------------------------------------------------------

		private void ReadTag(BinaryReader Source, ref FileInfo Info)
		{
			int Index;
			int Size;
            long initialPos;
			long Position;
            String strData;

			// Read Vorbis tag
			Index = 0;
            initialPos = Source.BaseStream.Position;
            Info.Tag.ID = Source.ReadChars(7);
            if (StreamUtils.StringEqualsArr(VORBIS_TAG_ID, Info.Tag.ID))
            {
			    do
			    {
                    Size = Source.ReadInt32();
                    Info.Tag.size += Size;

                    Position = Source.BaseStream.Position;
                    if (Size < 250)
                    {
                        strData = Encoding.UTF8.GetString(Source.ReadBytes(Size));
                        // Set Vorbis tag item
                        SetTagItem(strData.Trim(), ref Info);
                    }
                    else
                    {
                        SetExtendedTagItem(Source.BaseStream, Size);
                    }
                    Source.BaseStream.Seek(Position + Size, SeekOrigin.Begin);
                    if (0 == Index) Info.Tag.Fields = Source.ReadInt32();
                    
                    Index++;
                } while (Index <= Info.Tag.Fields);
			}
            Info.TagEndPos = (int)Source.BaseStream.Position;
            Info.Tag.size = Info.TagEndPos - initialPos;
		}

		// ---------------------------------------------------------------------------

		private long GetSamples(BinaryReader Source)
		{  
			int DataIndex;	
			// Using byte instead of char here to avoid mistaking range of bytes for unicode chars
			byte[] Data = new byte[251];
			OggHeader Header = new OggHeader();

			// Get total number of samples
			int result = 0;

			for (int index=1; index<=50; index++)
			{
				DataIndex = (int)(Source.BaseStream.Length - (/*Data.Length*/251 - 10) * index - 10);
				Source.BaseStream.Seek(DataIndex, SeekOrigin.Begin);
				Data = Source.ReadBytes(251);

				// Get number of PCM samples from last Ogg packet header
				for (int iterator=251 - 10; iterator>=0; iterator--)
				{
					char[] tempArray = new char[4] { (char)Data[iterator],
													   (char)Data[iterator + 1],
													   (char)Data[iterator + 2],
													   (char)Data[iterator + 3] };
					if ( StreamUtils.StringEqualsArr(OGG_PAGE_ID,tempArray) ) 
					{
						Source.BaseStream.Seek(DataIndex + iterator, SeekOrigin.Begin);
        
						Header.ID = Source.ReadChars(4);
						Header.StreamVersion = Source.ReadByte();
						Header.TypeFlag = Source.ReadByte();
						Header.AbsolutePosition = Source.ReadInt64();
						Header.Serial = Source.ReadInt32();
						Header.PageNumber = Source.ReadInt32();
						Header.Checksum = Source.ReadInt32();
						Header.Segments = Source.ReadByte();
						Header.LacingValues = Source.ReadBytes(0xFF);
						return Header.AbsolutePosition;
					}
				}
			}
			return result;
		}

		// ---------------------------------------------------------------------------

		private bool GetInfo(BinaryReader source, ref FileInfo Info)
		{
            Stream fs = source.BaseStream;

			// Get info from file
			bool result = false;
            FID3v2.ReadFromFile(source, FPictureStreamHandler);

            // Check for ID3v2
			source.BaseStream.Seek(FID3v2.Size, SeekOrigin.Begin);    

            // Read global file header
			Info.FPage.ID = source.ReadChars(4);
			Info.FPage.StreamVersion = source.ReadByte();
			Info.FPage.TypeFlag = source.ReadByte();
			Info.FPage.AbsolutePosition = source.ReadInt64();
			Info.FPage.Serial = source.ReadInt32();
			Info.FPage.PageNumber = source.ReadInt32();
			Info.FPage.Checksum = source.ReadInt32();
			Info.FPage.Segments = source.ReadByte();
			Info.FPage.LacingValues = source.ReadBytes(0xFF);

			if ( StreamUtils.StringEqualsArr(OGG_PAGE_ID,Info.FPage.ID) )
			{
				source.BaseStream.Seek(FID3v2.Size + Info.FPage.Segments + 27, SeekOrigin.Begin);

				// Read Vorbis stream info
				//Source.Read(Info.Parameters, 30);					
				Info.Parameters.ID = source.ReadChars(7);
				Info.Parameters.BitstreamVersion = source.ReadBytes(4);
				Info.Parameters.ChannelMode = source.ReadByte();
				Info.Parameters.SampleRate = source.ReadInt32();
				Info.Parameters.BitRateMaximal = source.ReadInt32();
				Info.Parameters.BitRateNominal = source.ReadInt32();
				Info.Parameters.BitRateMinimal = source.ReadInt32();
				Info.Parameters.BlockSize = source.ReadByte();
				Info.Parameters.StopFlag = source.ReadByte();

				if ( StreamUtils.StringEqualsArr(VORBIS_PARAMETERS_ID, Info.Parameters.ID) ) 
				{
                    // Reads all related Vorbis pages that describe file header
                    bool loop = true;
                    bool first = true;
                    MemoryStream s = new MemoryStream();
                    BinaryReader memSource = new BinaryReader(s);
                        while(loop) {
						    Info.SPagePos = (int)fs.Position;

						    Info.SPage.ID = source.ReadChars(4);
						    Info.SPage.StreamVersion = source.ReadByte();
						    Info.SPage.TypeFlag = source.ReadByte();
                            // 0 marks a new page
                            if (0 == Info.SPage.TypeFlag)
                            {
                                loop = first;
                            }
                            if (loop)
                            {
                                Info.SPage.AbsolutePosition = source.ReadInt64();
                                Info.SPage.Serial = source.ReadInt32();
                                Info.SPage.PageNumber = source.ReadInt32();
                                Info.SPage.Checksum = source.ReadInt32();
                                Info.SPage.Segments = source.ReadByte();
                                Info.SPage.LacingValues = source.ReadBytes(Info.SPage.Segments);
                                s.Write(source.ReadBytes(Info.SPage.GetPageLength()), 0, Info.SPage.GetPageLength());
                            }
                            first = false;
                        }
                        s.Seek(0, SeekOrigin.Begin);

						// Read Vorbis tag
                        ReadTag(memSource, ref Info);
                    memSource.Close();
		    
					// Get total number of samples
					Info.Samples = (int)GetSamples(source);

					result = true;
				}
			}

			return result;
		}

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset variables
			FChannelModeID = 0;
			FSampleRate = 0;
			FBitRateNominal = 0;
			FSamples = 0;
			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FTrack = 0;
            FDisc = 0;
            FRating = 0;
			FDate = "";
			FGenre = "";
			FComment = "";
			FVendor = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			String result;
			// Get channel mode name
			if (FChannelModeID > 2) result = VORBIS_MODE[3]; 
			else
				result = VORBIS_MODE[FChannelModeID];

			return VORBIS_MODE[FChannelModeID];
		}

		// ---------------------------------------------------------------------------

		private double FGetDuration()
		{
			double result;
			// Calculate duration time
			if (FSamples > 0)
				if (FSampleRate > 0)
					result = ((double)FSamples / FSampleRate);
				else
					result = 0;
			else
				if ((FBitRateNominal > 0) && (FChannelModeID > 0))
				result = ((double)FFileSize - FID3v2.Size) /
					(double)FBitRateNominal / FChannelModeID / 125 * 2;
			else
				result = 0;
		
			return result;
		}

		// ---------------------------------------------------------------------------

		private double FGetBitRate()
		{
			// Calculate average bit rate
			double result = 0;

			if (FGetDuration() > 0)
                result = (FFileSize - FID3v2.Size - Info.Tag.size )*8 / FGetDuration();
	
			return result;
		}

		// ---------------------------------------------------------------------------

		private bool FIsValid()
		{
			// Check for file correctness
			return ( ( ((VORBIS_CM_MONO <= FChannelModeID) && (FChannelModeID <= VORBIS_CM_STEREO)) || (VORBIS_CM_MULTICHANNEL == FChannelModeID) ) &&
				(FSampleRate > 0) && (FGetDuration() > 0.1) && (FGetBitRate() > 0) );
		}

		// ********************** Public functions & voids **********************

		public TOggVorbis()
		{
			// Object constructor
			resetData();  
		}

		// ---------------------------------------------------------------------------

		// No explicit destructors with C#

		// ---------------------------------------------------------------------------

        public override bool ReadFromFile(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
            FPictureStreamHandler = pictureStreamHandler;
			bool result = false;

			Info.Reset();

			if ( GetInfo(source, ref Info) )
			{
                FValid = true;
				// Fill variables
				FChannelModeID = Info.Parameters.ChannelMode;
				FSampleRate = Info.Parameters.SampleRate;
				FBitRateNominal = (ushort)(Info.Parameters.BitRateNominal / 1000); // Integer division
				FSamples = Info.Samples;
                FDuration = FGetDuration();
                FBitrate = FGetBitRate();

				FTitle = Info.Tag.FieldData[1];
				if (Info.Tag.FieldData[2] != "") FArtist = Info.Tag.FieldData[2];
				else FArtist = Info.Tag.FieldData[8];
				FAlbum = Info.Tag.FieldData[3];
				FTrack = TrackUtils.ExtractTrackNumber(Info.Tag.FieldData[4]);
				FDate = Info.Tag.FieldData[5];
				FGenre = Info.Tag.FieldData[6];
				if (Info.Tag.FieldData[7] != "") FComment = Info.Tag.FieldData[7];
				else FComment = Info.Tag.FieldData[9];
                FDisc = TrackUtils.ExtractTrackNumber(Info.Tag.FieldData[10]);
                FComposer = Info.Tag.FieldData[11];
                FRating = TrackUtils.ExtractIntRating( Info.Tag.FieldData[12] );
				FVendor = Info.Tag.FieldData[0];
				result = true;
			}
			return result;
		}

	}
}