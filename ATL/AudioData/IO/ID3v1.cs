using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for ID3v1.0-1.1 tags manipulation
    /// </summary>
	public class ID3v1 : MetaDataIO
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
			public String Header = "";                // Tag header - must be "TAG"
            public String Title = "";
            public String Artist = "";
            public String Album = "";
            public String Year = "";
            public String Comment = "";
            public byte[] EndComment = new byte[2];
			public byte Genre;                                                 // Genre data

			public void Reset()
			{
                Header = "";
                Title = "";
                Album = "";
                Artist = "";
                Year = "";
                Comment = "";
				Genre = 0;
			}
		}

		// ********************* Auxiliary functions & voids ********************

        private bool ReadTag(BinaryReader source, ref TagRecord TagData)
        {
            bool result = false;

            // Read tag
                source.BaseStream.Seek(-ID3V1_TAG_SIZE, SeekOrigin.End);
            FOffset = source.BaseStream.Position;

#if DEBUG
//            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, System.DateTime.Now.ToString("hh:mm:ss.ffff") + " ID3v1-seeked");
#endif

			// ID3v1 tags are C-String(null-terminated)-based tags
			// they are not unicode-encoded, hence the use of ReadOneByteChars
            TagData.Header = FEncoding.GetString(source.ReadBytes(3), 0, 3);
            if (ID3V1_ID == TagData.Header)
            {
                TagData.Title = Utils.StripZeroChars(FEncoding.GetString(source.ReadBytes(30), 0, 30));
                TagData.Artist = Utils.StripZeroChars(FEncoding.GetString(source.ReadBytes(30), 0, 30));
                TagData.Album = Utils.StripZeroChars(FEncoding.GetString(source.ReadBytes(30), 0, 30));
                TagData.Year = Utils.StripZeroChars(FEncoding.GetString(source.ReadBytes(4), 0, 4));
                TagData.Comment = Utils.StripZeroChars(FEncoding.GetString(source.ReadBytes(28), 0, 28));
                TagData.EndComment = source.ReadBytes(2);
                TagData.Genre = source.ReadByte();
                result = true;
            }

            return result;
		}


		// ---------------------------------------------------------------------------

		private static byte GetTagVersion(TagRecord TagData)
		{
			byte result = TAG_VERSION_1_0;
			// Terms for ID3v1.1
            if (((0 == TagData.EndComment[0]) && (0 != TagData.EndComment[1])) ||
                ((32 == TagData.EndComment[0]) && (32 != TagData.EndComment[1])))
				result = TAG_VERSION_1_1;

			return result;
		}

		// ********************** Public functions & voids **********************

		public ID3v1()
		{		
			ResetData();
		}

		// ---------------------------------------------------------------------------

		public override void ResetData()
		{
            base.ResetData();
			FVersion = TAG_VERSION_1_0;
			FGenreID = DEFAULT_GENRE;
            FEncoding = Encoding.GetEncoding("ISO-8859-1");
        }

		// ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler = null, bool storeUnsupportedMetaFields = false)
        {
			TagRecord tagData = new TagRecord();
	
			// Reset and load tag data from file to variable
			ResetData();
            bool result = ReadTag(source, ref tagData);

			// Process data if loaded successfuly
			if (result)
			{
				FExists = true;
                FSize = ID3V1_TAG_SIZE;
				FVersion = GetTagVersion(tagData);
				// Fill properties with tag data
                FTitle = tagData.Title;
                FArtist = tagData.Artist;
                FAlbum = tagData.Album;
                FYear = tagData.Year;
				if (TAG_VERSION_1_0 == FVersion)
				{
                    FComment = tagData.Comment + Utils.StripZeroChars(FEncoding.GetString(tagData.EndComment,0,2));
				}
				else
				{
                    FComment = tagData.Comment;
					FTrack = tagData.EndComment[1];
				}
				FGenreID = tagData.Genre;
                FGenre = (FGenreID < MAX_MUSIC_GENRES) ? MusicGenre[FGenreID] : "";

                if (storeUnsupportedMetaFields)
                {
                    unsupportedTagFields = new Dictionary<String, String>();
                    
                    // N/A since ID3v1 fields are all implemented by TagData
                }
			}
			return result;
		}


        public override bool Write(TagData tag, BinaryWriter w)
        {
            bool result = true;

            // ID3v1 tags are C-String(null-terminated)-based tags
            // they are not unicode-encoded, hence the use of ReadOneByteChars
            w.Write(ID3V1_ID.ToCharArray());

            w.Write(Utils.BuildStrictLengthString(tag.Title,30,'\0').ToCharArray());
            w.Write(Utils.BuildStrictLengthString(tag.Artist, 30, '\0').ToCharArray());
            w.Write(Utils.BuildStrictLengthString(tag.Album, 30, '\0').ToCharArray());
            // ID3v1 standard requires the year
            w.Write(Utils.BuildStrictLengthString( TrackUtils.ExtractStrYear(tag.Date), 4, '\0').ToCharArray());
            w.Write(Utils.BuildStrictLengthString(tag.Comment, 28, '\0').ToCharArray());
            
            // ID3v1.1 standard
            w.Write('\0');
            w.Write(TrackUtils.ExtractTrackNumber(tag.TrackNumber));

            byte genre = 0;
            for (byte i = 0; i < MAX_MUSIC_GENRES; i++)
            {
                if (tag.Genre.Equals(MusicGenre[i]))
                {
                    genre = i;
                    break;
                }
            }
            w.Write(genre);
            w.BaseStream.SetLength(ID3V1_TAG_SIZE);

            return result;
        }

        protected override int getDefaultTagOffset()
        {
            return TO_EOF;
        }

    }
}