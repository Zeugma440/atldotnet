using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for ID3v1.0-1.1 tags manipulation
    /// </summary>
	public class ID3v1 : MetaDataIO
    {
        /// <summary>
        /// Max. number of music genres
        /// </summary>
        public const int MAX_MUSIC_GENRES = 192;

        /// <summary>
        /// Standard size of an ID3v1 tag
        /// </summary>
        public const int ID3V1_TAG_SIZE = 128;
        /// <summary>
        /// Magic number of an ID3v1 tag
        /// </summary>
        public const string ID3V1_ID = "TAG";

        /// <summary>
        /// Index for ID3v1.0 tag
        /// </summary>
        private const byte TAG_VERSION_1_0 = 1;
        /// <summary>
        /// Index for ID3v1.1 tag
        /// </summary>
        private const byte TAG_VERSION_1_1 = 2;

        #region music genres
        /// <summary>
        /// Codified music genres, ordered by numeric code
        /// </summary>
        public static readonly string[] MusicGenre = {	// Standard genres
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
            "Synthpop",
            "Abstract",
            "Art Rock",
            "Baroque",
            "Bhangra",
            "Big Beat",
            "Breakbeat",
            "Chillout",
            "Downtempo",
            "Dub",
            "EBM",
            "Eclectic",
            "Electro",
            "Electroclash",
            "Emo",
            "Experimental",
            "Garage",
            "Global",
            "IDM",
            "Illbient",
            "Industro-Goth",
            "Jam Band",
            "Krautrock",
            "Leftfield",
            "Lounge",
            "Math Rock",
            "New Romantic",
            "Nu-Breakz",
            "Post-Punk",
            "Post-Rock",
            "Psytrance",
            "Shoegaze",
            "Space Rock",
            "Trop Rock",
            "World Music",
            "Neoclassical",
            "Audiobook",
            "Audio Theatre",
            "Neue Deutsche Welle",
            "Podcast",
            "Indie Rock",
            "G-Funk",
            "Dubstep",
            "Garage Rock",
            "Psybient"
        };
        #endregion


        // --------------- OPTIONAL INFORMATIVE OVERRIDES

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                Format format = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("id3v1")[0]);
                format.Name = format.Name + "." + (m_tagVersion - 1);
                format.ID += m_tagVersion - 1;
                return new List<Format>(new[] { format });
            }
        }


        // --------------- MANDATORY INFORMATIVE OVERRIDES

        /// <inheritdoc/>
        protected override int getDefaultTagOffset()
        {
            return TO_EOF;
        }

        /// <inheritdoc/>
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.ID3V1;
        }

        /// <inheritdoc/>
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            throw new NotImplementedException();
        }


        // ********************* Auxiliary functions & voids ********************

        private bool ReadTag(BufferedBinaryReader source)
        {
            if (source.Length < ID3V1_TAG_SIZE) return false;
            // Read tag
            source.Seek(-ID3V1_TAG_SIZE, SeekOrigin.End);

            // ID3v1 tags are C-String(null-terminated)-based tags encoded in ASCII
            byte[] data = new byte[ID3V1_TAG_SIZE];
            if (source.Read(data, 0, ID3V1_TAG_SIZE) < ID3V1_TAG_SIZE) return false;

            string header = Utils.Latin1Encoding.GetString(data, 0, 3);
            if (!header.Equals(ID3V1_ID)) return false;

            byte[] endComment = new byte[2];
            structureHelper.AddZone(source.Position - ID3V1_TAG_SIZE, ID3V1_TAG_SIZE);

            setMetaField(Field.TITLE, Utils.Latin1Encoding.GetString(data, 3, 30).Replace("\0", ""));
            setMetaField(Field.ARTIST, Utils.Latin1Encoding.GetString(data, 33, 30).Replace("\0", ""));
            setMetaField(Field.ALBUM, Utils.Latin1Encoding.GetString(data, 63, 30).Replace("\0", ""));
            setMetaField(Field.RECORDING_YEAR, Utils.Latin1Encoding.GetString(data, 93, 4).Replace("\0", ""));
            string comment = Utils.Latin1Encoding.GetString(data, 97, 28).Replace("\0", "");

            Array.Copy(data, 125, endComment, 0, 2);
            m_tagVersion = GetTagVersion(endComment);

            // Fill properties using tag data
            if (TAG_VERSION_1_0 == m_tagVersion)
            {
                comment += Utils.Latin1Encoding.GetString(endComment, 0, 2).Replace("\0", "");
            }
            else
            {
                setMetaField(Field.TRACK_NUMBER, endComment[1].ToString());
            }

            setMetaField(Field.COMMENT, comment);
            setMetaField(Field.GENRE, (data[127] < MAX_MUSIC_GENRES) ? MusicGenre[data[127]] : "");

            return true;
        }

        private static byte GetTagVersion(byte[] endComment)
        {
            byte result = TAG_VERSION_1_0;
            // Terms for ID3v1.1
            if ((0 == endComment[0] && 0 != endComment[1]) ||
                (32 == endComment[0] && 32 != endComment[1]))
                result = TAG_VERSION_1_1;

            return result;
        }




        // ********************** Public functions & voids **********************

        /// <summary>
        /// Constructor
        /// </summary>
        public ID3v1()
        {
            ResetData();
        }

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            // Reset and load tag data from file to variable
            ResetData();
            m_tagVersion = TAG_VERSION_1_0;

            bool result = ReadTag(reader);

            // Process data if loaded successfuly
            if (!result) ResetData();

            return result;
        }

        /// <inheritdoc/>
        protected override int write(TagData tag, Stream s, string zone)
        {
            // ID3v1 tags are C-String(null-terminated)-based tags
            // they are not unicode-encoded, hence the use of ReadOneByteChars
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(ID3V1_ID));

            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.TITLE], 30, '\0')));
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.ARTIST], 30, '\0')));
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.ALBUM], 30, '\0')));
            // ID3v1 standard requires the year
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(TrackUtils.ExtractStrYear(tag[Field.RECORDING_YEAR]), 4, '\0')));
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.COMMENT], 28, '\0')));

            // ID3v1.1 standard
            s.WriteByte(0);
            s.WriteByte((byte)Math.Min(TrackUtils.ExtractTrackNumber(tag[Field.TRACK_NUMBER]), byte.MaxValue));

            byte genre = byte.MaxValue;
            if (tag[Field.GENRE] != null)
            {
                string genreStr = tag[Field.GENRE];
                for (byte i = 0; i < MAX_MUSIC_GENRES; i++)
                {
                    if (genreStr.Equals(MusicGenre[i], StringComparison.OrdinalIgnoreCase))
                    {
                        genre = i;
                        break;
                    }
                }
            }
            s.WriteByte(genre);

            return 7;
        }
    }
}