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
	public class TID3v1 : MetaDataReader
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
	
		private byte FGenreID;
			
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

		// ********************** Public functions & voids **********************

		public TID3v1()
		{		
			ResetData();
		}

		// ---------------------------------------------------------------------------

		public override void ResetData()
		{
            base.ResetData();
			FVersion = TAG_VERSION_1_0;
			FGenreID = DEFAULT_GENRE;
		}

		// ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler = null)
        {
			TagRecord TagData = new TagRecord();
	
			// Reset and load tag data from file to variable
			ResetData();
            bool result = ReadTag(source, ref TagData);

			// Process data if loaded successfuly
			if (result)
			{
				FExists = true;
                FSize = ID3V1_TAG_SIZE;
				FVersion = GetTagVersion(TagData);
				// Fill properties with tag data
                FTitle = TagData.Title;
                FArtist = TagData.Artist;
                FAlbum = TagData.Album;
                FYear = TagData.Year;
				if (TAG_VERSION_1_0 == FVersion)
				{
                    FComment = TagData.Comment + Utils.StripZeroChars(TagData.EndComment);
				}
				else
				{
                    FComment = TagData.Comment;
					FTrack = (byte)TagData.EndComment[1];
				}
				FGenreID = TagData.Genre;
                FGenre = (FGenreID < MAX_MUSIC_GENRES) ? MusicGenre[FGenreID] : "";
			}
			return result;
		}
	}
}