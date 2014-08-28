using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for ID3v1.0-1.1 tags manipulation
    /// </summary>
	public class TID3v1 : IMetaDataReader
	{
		public const int MAX_MUSIC_GENRES = 148;        // Max. number of music genres
		public const int DEFAULT_GENRE = 255;               // Index for default genre

        public const int ID3V1_TAG_SIZE = 128;
        public const String ID3V1_ID = "TAG";

		// Used with VersionID property
		public const byte TAG_VERSION_1_0 = 1;                // Index for ID3v1.0 tag
		public const byte TAG_VERSION_1_1 = 2;                // Index for ID3v1.1 tag

		#region music genres
		public static String[] MusicGenre = new string[MAX_MUSIC_GENRES] 		// Genre names
		{	// Standard genres
			"Blues",
			"Classic Rock",
			"Country",
			"Dance",
			"Disco",
			"Funk",
			"Grunge",
			"Hip-Hop",
			"Jazz",
			"Metal",
			"New Age",
			"Oldies",
			"Other",
			"Pop",
			"R&B",
			"Rap",
			"Reggae",
			"Rock",
			"Techno",
			"Industrial",
			"Alternative",
			"Ska",
			"Death Metal",
			"Pranks",
			"Soundtrack",
			"Euro-Techno",
			"Ambient",
			"Trip-Hop",
			"Vocal",
			"Jazz+Funk",
			"Fusion",
			"Trance",
			"Classical",
			"Instrumental",
			"Acid",
			"House",
			"Game",
			"Sound Clip",
			"Gospel",
			"Noise",
			"AlternRock",
			"Bass",
			"Soul",
			"Punk",
			"Space",
			"Meditative",
			"Instrumental Pop",
			"Instrumental Rock",
			"Ethnic",
			"Gothic",
			"Darkwave",
			"Techno-Industrial",
			"Electronic",
			"Pop-Folk",
			"Eurodance",
			"Dream",
			"Southern Rock",
			"Comedy",
			"Cult",
			"Gangsta",
			"Top 40",
			"Christian Rap",
			"Pop/Funk",
			"Jungle",
			"Native American",
			"Cabaret",
			"New Wave",
			"Psychadelic",
			"Rave",
			"Showtunes",
			"Trailer",
			"Lo-Fi",
			"Tribal",
			"Acid Punk",
			"Acid Jazz",
			"Polka",
			"Retro",
			"Musical",
			"Rock & Roll",
			"Hard Rock",
			// Extended genres
			"Folk",
			"Folk-Rock",
			"National Folk",
			"Swing",
			"Fast Fusion",
			"Bebob",
			"Latin",
			"Revival",
			"Celtic",
			"Bluegrass",
			"Avantgarde",
			"Gothic Rock",
			"Progessive Rock",
			"Psychedelic Rock",
			"Symphonic Rock",
			"Slow Rock",
			"Big Band",
			"Chorus",
			"Easy Listening",
			"Acoustic",
			"Humour",
			"Speech",
			"Chanson",
			"Opera",
			"Chamber Music",
			"Sonata",
			"Symphony",
			"Booty Bass",
			"Primus",
			"Porn Groove",
			"Satire",
			"Slow Jam",
			"Club",
			"Tango",
			"Samba",
			"Folklore",
			"Ballad",
			"Power Ballad",
			"Rhythmic Soul",
			"Freestyle",
			"Duet",
			"Punk Rock",
			"Drum Solo",
			"A capella",
			"Euro-House",
			"Dance Hall",
			"Goa",
			"Drum & Bass",
			"Club-House",
			"Hardcore",
			"Terror",
			"Indie",
			"BritPop",
			"Negerpunk",
			"Polsk Punk",
			"Beat",
			"Christian Gangsta Rap",
			"Heavy Metal",
			"Black Metal",
			"Crossover",
			"Contemporary Christian",
			"Christian Rock",
			"Merengue",
			"Salsa",
			"Trash Metal",
			"Anime",
			"JPop",
			"Synthpop"
		};
		#endregion
	
		private bool FExists;
		private byte FVersionID;
		private String FTitle; // String30
		private String FArtist; // String30
        private String FComposer; // String30
		private String FAlbum; // String30
		private String FYear; // String04
		private String FComment; // String30
		private ushort FTrack;
        private byte FDisc;
		private byte FGenreID;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;
			
		public bool Exists // True if tag found
		{
			get { return this.FExists; }
		}
		public byte VersionID // Version code
		{
			get { return this.FVersionID; }
		}
        public int Size
        {
            get { if (Exists) return ID3V1_TAG_SIZE; else return 0; }
        }
		public String Title // Song title (String30)
		{
			get { return this.FTitle; }
			set { FSetTitle(value); }
		}
		public String Artist // Artist name (String30)
		{
			get { return this.FArtist; }
			set { FSetArtist(value); }
		}
        public String Composer // Composer name (String30)
        {
            get { return this.FComposer; }
            set { FSetComposer(value); }
        }
		public String Album // Album name (String30)
		{
			get { return this.FAlbum; }
			set { FSetAlbum(value); }
		}	
		public String Year // Year (String04)
		{
			get { return this.FYear; }
			set { FSetYear(value); }
		}	
		public String Comment // Comment (String30)
		{
			get { return this.FComment; }
			set { FSetComment(value); }
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
        public ushort Rating // Rating -- not part of ID3v1 standard
        {
            get { return 0; }
            set {  }
        }
		public byte GenreID // Genre code
		{
			get { return this.FGenreID; }
			set { FSetGenreID(value); }
		}
		public String Genre // Genre name
		{
			get { return FGetGenre(); }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating the presence of embedded pictures
        {
            get { return this.FPictures; }
        }

		// Real structure of ID3v1 tag
		private class TagRecord
		{
			public char[] Header = new char[3];                // Tag header - must be "TAG"
            public String Title = "";
            public String Artist = "";
            public String Album = "";
            public String Year = "";
            public String Comment = "";
            public String EndComment = "";
			public byte Genre;                                                 // Genre data

			public void Reset()
			{
                Title = "";
                Artist = "";
                Year = "";
                Comment = "";
                EndComment = "";
				Genre = 0;
			}
		}

		// ********************* Auxiliary functions & voids ********************

        private bool ReadTag(BinaryReader source, ref TagRecord TagData)
        {
			bool result;
            Encoding encoding = Encoding.GetEncoding("ISO-8859-1"); // aka ISO Latin-1

            result = true;
			
			// Read tag
            source.BaseStream.Seek(-ID3V1_TAG_SIZE, SeekOrigin.End);
			
			// ID3v1 tags are C-String(null-terminated)-based tags
			// they are not unicode-encoded, hence the use of ReadOneByteChars
			TagData.Header = StreamUtils.ReadOneByteChars(source,3);
            if (StreamUtils.StringEqualsArr(ID3V1_ID, TagData.Header))
            {
                TagData.Title = StreamUtils.ReadNullTerminatedStringFixed(source, encoding, 30);
                TagData.Artist = StreamUtils.ReadNullTerminatedStringFixed(source, encoding, 30);
                TagData.Album = StreamUtils.ReadNullTerminatedStringFixed(source, encoding, 30);
                TagData.Year = StreamUtils.ReadNullTerminatedStringFixed(source, encoding, 4);
                TagData.Comment = StreamUtils.ReadNullTerminatedStringFixed(source, encoding, 28);
                TagData.EndComment = new String(StreamUtils.ReadOneByteChars(source, 2));
                TagData.Genre = source.ReadByte();
            }
            else
            {
                result = false;
            }

            return result;
		}


		// ---------------------------------------------------------------------------

		byte GetTagVersion(TagRecord TagData)
		{
			byte result = TAG_VERSION_1_0;
			// Terms for ID3v1.1
            if ((('\0' == TagData.EndComment[0]) && ('\0' != TagData.EndComment[1])) ||
                ((32 == (byte)TagData.EndComment[0]) && (32 != (byte)TagData.EndComment[1])))
				result = TAG_VERSION_1_1;

			return result;
		}

		// ********************** Private functions & voids *********************

		void FSetTitle(String newTrack)
		{
			FTitle = newTrack.TrimEnd();
		}
		void FSetArtist(String NewArtist)
		{
			FArtist = NewArtist.TrimEnd();
		}
        void FSetComposer(String NewComposer)
        {
            FComposer = NewComposer.TrimEnd();
        }
        void FSetAlbum(String NewAlbum)
		{
			FAlbum = NewAlbum.TrimEnd();
		}
		void FSetYear(String NewYear)
		{
			FYear = NewYear.TrimEnd();
		}
		void FSetComment(String NewComment)
		{
			FComment = NewComment.TrimEnd();
		}
		void FSetTrack(ushort NewTrack)
		{
			FTrack = NewTrack;
		}
        void FSetDisc(byte NewDisc)
        {
            FDisc = NewDisc;
        }
		void FSetGenreID(byte NewGenreID)
		{
			FGenreID = NewGenreID;
		}
		string FGetGenre()
		{
			String result = "";
			// Return an empty string if the current GenreID is not valid
			if ( FGenreID < MAX_MUSIC_GENRES ) result = MusicGenre[FGenreID];
			return result;
		}

		// ********************** Public functions & voids **********************

		public TID3v1()
		{		
			ResetData();
		}

		// ---------------------------------------------------------------------------

		public void ResetData()
		{
			FExists = false;
			FVersionID = TAG_VERSION_1_0;
			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FYear = "";
			FComment = "";
			FTrack = 0;
            FDisc = 0;
			FGenreID = DEFAULT_GENRE;
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		// ---------------------------------------------------------------------------

        public bool ReadFromFile(BinaryReader SourceFile)
        {
			TagRecord TagData = new TagRecord();
	
			// Reset and load tag data from file to variable
			ResetData();
            bool result = ReadTag(SourceFile, ref TagData);

			// Process data if loaded successfuly
			if (result)
			{
				FExists = true;
				FVersionID = GetTagVersion(TagData);
				// Fill properties with tag data
                FTitle = TagData.Title;
                FArtist = TagData.Artist;
                FAlbum = TagData.Album;
                FYear = TagData.Year;
				if (TAG_VERSION_1_0 == FVersionID)
				{
                    FComment = TagData.Comment + Utils.StripZeroChars(TagData.EndComment);
				}
				else
				{
                    FComment = TagData.Comment;
					FTrack = (byte)TagData.EndComment[1];
				}
				FGenreID = TagData.Genre;
			}
			return result;
		}
	}
}