using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for APEtag 1.0 and 2.0 tags manipulation
    /// </summary>
	public class TAPEtag : IMetaDataReader
	{
		private bool FExists;
		private int FVersion;
		private long FSize;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private ushort FTrack;
        private byte FDisc;
        private ushort FRating;
        private String FRatingStr;
		private String FYear;
		private String FGenre;
		private String FComment;
		private String FCopyright;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;
        private StreamUtils.StreamHandlerDelegate FPictureStreamHandler;
      
		public bool Exists // True if tag found
		{
			get { return this.FExists; }
		}
		public int Version // Tag version
		{
			get { return this.FVersion; }
		}
		public long Size // Total tag size
		{
			get { return this.FSize; }
		}
		public String Title // Song title
		{
			get { return this.FTitle ; } 
			set { FSetTitle(value); }
		}
		public String Artist // Artist name
		{
			get { return this.FArtist; }
			set { FSetArtist(value); }
		}
        public String Composer // Composer name
        {
            get { return this.FComposer; }
            set { FSetComposer(value); }
        }
		public String Album // Album title
		{
			get { return this.FAlbum; }
			set { FSetAlbum(value); }
		}
		public ushort Track // Track number
		{
			get { return this.FTrack; }
			set { FSetTrack(value); }
		}
        public ushort Disc // Disc number
        {
            get { return (ushort)this.FDisc; }
            set { FSetDisc((byte)value); }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
            set { FSetRating(value); }
        }
		public String Year // Release year
		{
			get { return this.FYear; }
			set { FSetYear(value); }
		}
		public String Genre // Genre name
		{
			get { return this.FGenre; }
			set { FSetGenre(value); }
		}
		public String Comment // Comment
		{
			get { return this.FComment; }
			set { FSetComment(value); }
		}
		public String Copyright // (c)
		{
			get { return this.FCopyright; }
			set { FSetCopyright(value); }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures // (Embedded pictures flags)
        {
            get { return this.FPictures; }
        }


		// Tag ID
		public const String APE_ID = "APETAGEX";							// APE

		// Size constants
		public const byte APE_TAG_FOOTER_SIZE = 32;							// APE tag footer
		public const byte APE_TAG_HEADER_SIZE = 32;							// APE tag header

		// First version of APE tag
		public const int APE_VERSION_1_0 = 1000;

		// Max. number of supported tag fields
		public const byte APE_FIELD_COUNT = 12;

		// Names of supported tag fields
        // NB : "preference" is a synonym to "rating" used by certain softwares
		public static String[] APE_FIELD = new String[APE_FIELD_COUNT]
		{
			"Title", "Artist", "Album", "Track", "Year", "Genre",
			"Comment", "Copyright", "Composer", "Cover Art (front)", "rating", "preference" };

		// APE tag data - for internal use
		private class TagInfo
		{
			// Real structure of APE footer
			public char[] ID = new char[8];                              // Always "APETAGEX"
			public int Version;			                                       // Tag version
			public int Size;				                     // Tag size including footer
			public int Fields;					                          // Number of fields
			public int Flags;						                             // Tag flags
			public char [] Reserved = new char[8];                  // Reserved for later use
			// Extended data
			public byte DataShift;		                           // Used if ID3v1 tag found
			public int FileSize;		                                 // File size (bytes)
			public String[] Field = new String[APE_FIELD_COUNT];   // Information from fields

            public void Reset()
            {
                Array.Clear(ID,0,ID.Length);
			    Version = 0;
			    Flags = 0;
			    Fields = 0;
			    Size = 0;
			    Array.Clear(Reserved,0,Reserved.Length);
			    DataShift = 0;
			    FileSize = 0;
			    for (int i=0; i<Field.Length; i++) Field[i] = "";
            }
		}

		// ********************* Auxiliary functions & voids ********************

        bool ReadFooter(BinaryReader SourceFile, ref TagInfo Tag)
        {	
			char[] tagID = new char[3];
			//int Transferred;
			bool result = true;
            Stream fs = SourceFile.BaseStream;
  
			// Load footer from file to variable
			Tag.FileSize = (int)fs.Length;
		
			// Check for existing ID3v1 tag in order to get the correct offset for APEtag packet
            fs.Seek(Tag.FileSize - TID3v1.ID3V1_TAG_SIZE, SeekOrigin.Begin);
            tagID = StreamUtils.ReadOneByteChars(SourceFile, 3);
            if (StreamUtils.StringEqualsArr(TID3v1.ID3V1_ID, tagID)) Tag.DataShift = TID3v1.ID3V1_TAG_SIZE;

			// Read footer data
            fs.Seek(Tag.FileSize - Tag.DataShift - APE_TAG_FOOTER_SIZE, SeekOrigin.Begin);

            Tag.ID = StreamUtils.ReadOneByteChars(SourceFile, 8);
            if (StreamUtils.StringEqualsArr(APE_ID, Tag.ID))
            {
                Tag.Version = SourceFile.ReadInt32(); //
                Tag.Size = SourceFile.ReadInt32();
                Tag.Fields = SourceFile.ReadInt32();
                Tag.Flags = SourceFile.ReadInt32();
                Tag.Reserved = StreamUtils.ReadOneByteChars(SourceFile, 8);
            }
            else
            {
                result = false;
            }

			//Source.ReadChars(BlockRead(SourceFile, Tag, APE_TAG_FOOTER_SIZE, Transferred);
		
			// if transfer is not complete
			//if (Transferred < APE_TAG_FOOTER_SIZE) result = false;

			return result;
		}

		void SetTagItem(String FieldName, String FieldValue, ref TagInfo Tag)
		{		
			// Set tag item if supported field found
			for (int Iterator=0; Iterator < APE_FIELD_COUNT; Iterator++)		
				if ( FieldName.Replace("\0","").ToUpper() == APE_FIELD[Iterator].ToUpper() ) 
					if (Tag.Version > APE_VERSION_1_0)
						Tag.Field[Iterator] = FieldValue;
					else
						Tag.Field[Iterator] = FieldValue;		
		}

		// ---------------------------------------------------------------------------

		void ReadFields(BinaryReader SourceFile, ref TagInfo Tag)
		{		
			String FieldName;
            String strValue;
			int ValueSize;
			long ValuePosition;
			int FieldFlags;
            Stream fs = SourceFile.BaseStream;

			fs.Seek(Tag.FileSize - Tag.DataShift - Tag.Size,SeekOrigin.Begin);
			// Read all stored fields
			for (int iterator=0; iterator < Tag.Fields; iterator++)
			{
				ValueSize = SourceFile.ReadInt32();
				FieldFlags = SourceFile.ReadInt32();
                FieldName = StreamUtils.ReadNullTerminatedString(SourceFile,0);
					
				ValuePosition = fs.Position;

                if (ValueSize <= 500)
                {
                    strValue = Encoding.UTF8.GetString(SourceFile.ReadBytes(ValueSize));
                    SetTagItem(FieldName.Trim(), strValue.Trim(), ref Tag);
                }
                else
                {
                    if (FieldName.Trim().ToUpper().Equals("COVER ART (FRONT)"))
                    {
                        Pictures.Add(MetaReaderFactory.PIC_CODE.Front);
                        if (FPictureStreamHandler != null)
                        {
                            String description = StreamUtils.ReadNullTerminatedString(SourceFile,0);
                            MemoryStream mem = new MemoryStream(ValueSize-description.Length-1);
                            StreamUtils.CopyMemoryStreamFrom(mem, SourceFile, ValueSize-description.Length-1);
                            FPictureStreamHandler(ref mem);
                            mem.Close();
                        }
                    }
                }
                fs.Seek(ValuePosition + ValueSize, SeekOrigin.Begin);
			}			
		}
        
		// ********************** Private functions & voids *********************

		void FSetTitle(String newTrack)
		{
			// Set song title
			FTitle = newTrack.Trim();	
		}
		void FSetArtist(String NewArtist)
		{
			// Set artist name
			FArtist = NewArtist.Trim();
		}
        void FSetComposer(String NewComposer)
        {
            // Set artist name
            FComposer = NewComposer.Trim();
        }
		void FSetAlbum(String NewAlbum)
		{
			// Set album title
			FAlbum = NewAlbum.Trim();
		}
		void FSetTrack(ushort NewTrack)
		{
			// Set track number
			FTrack = NewTrack;
		}
        void FSetDisc(byte NewDisc)
        {
            // Set disc number
            FDisc = NewDisc;
        }
        void FSetRating(ushort NewRating)
        {
            // Set rating
            FRating = NewRating;
        }
		void FSetYear(String NewYear)
		{
			// Set release year
			FYear = NewYear.Trim();
		}
		void FSetGenre(String NewGenre)
		{
			// Set genre name
			FGenre = NewGenre.Trim();
		}
		void FSetComment(String NewComment)
		{
			// Set comment
			FComment = NewComment.Trim();
		}
		void FSetCopyright(String NewCopyright)
		{
			// Set copyright information
			FCopyright = NewCopyright.Trim();
		}

		// ********************** Public functions & voids **********************

		public void TAPETag()
		{
			// Create object		
			ResetData();
		}

		// ---------------------------------------------------------------------------

		public void ResetData()
		{
			// Reset all variables
			FExists = false;
			FVersion = 0;
			FSize = 0;
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
			FCopyright = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		// ---------------------------------------------------------------------------

        public bool ReadFromFile(BinaryReader SourceFile, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
			TagInfo Tag = new TagInfo();
            FPictureStreamHandler = pictureStreamHandler;

			// Reset data and load footer from file to variable
			ResetData();
            Tag.Reset();
				
			bool result = ReadFooter(SourceFile, ref Tag);
		
			// Process data if loaded and footer valid
			if ( result )
			{
				FExists = true;
				// Fill properties with footer data
				FVersion = Tag.Version;
				FSize = Tag.Size;
				// Get information from fields
                ReadFields(SourceFile, ref Tag);
				FTitle = Tag.Field[0];
				FArtist = Tag.Field[1];
				FAlbum = Tag.Field[2];
				FTrack = TrackUtils.ExtractTrackNumber(Tag.Field[3]);
				FYear = Tag.Field[4];
				FGenre = Tag.Field[5];
				FComment = Tag.Field[6];
				FCopyright = Tag.Field[7];
                FComposer = Tag.Field[8];

                // Rating can be containted in two fields
                FRatingStr = Utils.ProtectValue(Tag.Field[10]);
                if (0 == FRatingStr.Trim().Length) Utils.ProtectValue(Tag.Field[11]);
                FRating = TrackUtils.ExtractIntRating(FRatingStr);
			}

			return result;
		}

	}
}