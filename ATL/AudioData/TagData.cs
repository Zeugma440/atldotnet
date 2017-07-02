using ATL.AudioReaders;
using Commons;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// Basic metadata fields container
	/// </summary>
	public class TagData
	{
		public TagData()
        {
            Pictures = new Dictionary<MetaDataIOFactory.PIC_TYPE, Image>();
        }

        /* Not useful so far
        public TagData(IMetaDataIO meta)
        {
            Title = meta.Title;
            Artist = meta.Artist;
            Composer = meta.Composer;
            Genre = meta.Genre;
            Album = meta.Album;
            Date = meta.Year;
            TrackNumber = meta.Track.ToString();
            DiscNumber = meta.Disc.ToString();
            Rating = meta.Rating.ToString();

            Pictures = new Dictionary<MetaDataIOFactory.PIC_CODE, Image>();

//            AudioFileIO theReader = new AudioFileIO(Path, new StreamUtils.StreamHandlerDelegate(this.readImageData));
        }
        */
        public const byte TAG_FIELD_GENERAL_DESCRIPTION     = 0;
        public const byte TAG_FIELD_TITLE                   = 1;
        public const byte TAG_FIELD_ARTIST                  = 2;
        public const byte TAG_FIELD_COMPOSER                = 3;
        public const byte TAG_FIELD_COMMENT                 = 4; 
        public const byte TAG_FIELD_GENRE                   = 5;
        public const byte TAG_FIELD_ALBUM                   = 6;
        public const byte TAG_FIELD_RECORDING_DATE          = 7;
        public const byte TAG_FIELD_RECORDING_YEAR          = 8;
        public const byte TAG_FIELD_RECORDING_DAYMONTH      = 9;
        public const byte TAG_FIELD_TRACK_NUMBER            = 10;
        public const byte TAG_FIELD_DISC_NUMBER             = 11;
        public const byte TAG_FIELD_RATING                  = 12;
        public const byte TAG_FIELD_PICTURE_DATA            = 13; // TODO ? - Differentiate front, back, CD
        public const byte TAG_FIELD_ORIGINAL_ARTIST         = 14;
        public const byte TAG_FIELD_ORIGINAL_ALBUM          = 15;
        public const byte TAG_FIELD_COPYRIGHT               = 16;
        public const byte TAG_FIELD_ALBUM_ARTIST            = 17;
        public const byte TAG_FIELD_PUBLISHER               = 18;


        public String GeneralDescription = "";
        public String Title = "";
		public String Artist = "";
        public String OriginalArtist = "";
        public String Composer = "";
		public String Comment = "";
        public String Genre = "";
        public String Album = "";
        public String OriginalAlbum = "";
        public String RecordingYear = "";
        public String RecordingDayMonth = "";
        public String RecordingDate = "";
        public String TrackNumber = "";
        public String DiscNumber = "";
        public String Rating = "";
        public String Copyright = "";
        public String AlbumArtist = "";
        public String Publisher = "";
        public IDictionary<MetaDataIOFactory.PIC_TYPE, Image> Pictures;

        protected void readImageData(ref Stream s, MetaDataIOFactory.PIC_TYPE picCode)
        {
            if (Pictures.ContainsKey(picCode))
            {
                Pictures.Remove(picCode);
            }
            Pictures.Add(picCode, Image.FromStream(s));
        }

        public void IntegrateValue(byte key, String value)
        {
            switch (key)
            {
                case TAG_FIELD_GENERAL_DESCRIPTION:     GeneralDescription = value; break;
                case TAG_FIELD_TITLE:                   Title = value; break;
                case TAG_FIELD_ARTIST:                  Artist= value; break;
                case TAG_FIELD_COMPOSER:                Composer = value; break;
                case TAG_FIELD_COMMENT:                 Comment = value; break;
                case TAG_FIELD_GENRE:                   Genre = value; break;
                case TAG_FIELD_ALBUM:                   Album = value; break;
                case TAG_FIELD_RECORDING_DATE:          RecordingDate = value; break;
                case TAG_FIELD_RECORDING_YEAR:          RecordingYear = value; break;
                case TAG_FIELD_RECORDING_DAYMONTH:      RecordingDayMonth = value; break;
                case TAG_FIELD_TRACK_NUMBER:            TrackNumber = value; break;
                case TAG_FIELD_DISC_NUMBER:             DiscNumber = value; break;
                case TAG_FIELD_RATING:                  Rating = value; break;
                    // Picture data integration has a specific routine
                case TAG_FIELD_ORIGINAL_ARTIST:         OriginalArtist = value; break;
                case TAG_FIELD_ORIGINAL_ALBUM:          OriginalAlbum = value; break;
                case TAG_FIELD_COPYRIGHT:               Copyright = value; break;
                case TAG_FIELD_ALBUM_ARTIST:            AlbumArtist = value; break;
                case TAG_FIELD_PUBLISHER:               Publisher = value; break;
            }
        }

        public void IntegrateValues(TagData data)
        {
            // String values
            IDictionary<byte, String> newData = data.ToMap();
            foreach (byte key in newData.Keys)
            {
                IntegrateValue(key, newData[key]);
            }

            // Pictures
            foreach (MetaDataIOFactory.PIC_TYPE picType in data.Pictures.Keys)
            {
                Pictures[picType] = data.Pictures[picType];
            }
        }

        public IDictionary<byte,String> ToMap()
        {
            IDictionary<byte, String> result = new Dictionary<byte, String>();

            addIfConsistent(GeneralDescription, TAG_FIELD_GENERAL_DESCRIPTION, ref result);
            addIfConsistent(Title, TAG_FIELD_TITLE, ref result);
            addIfConsistent(Artist, TAG_FIELD_ARTIST, ref result);
            addIfConsistent(Composer, TAG_FIELD_COMPOSER, ref result);
            addIfConsistent(Comment, TAG_FIELD_COMMENT, ref result);
            addIfConsistent(Genre, TAG_FIELD_GENRE, ref result);
            addIfConsistent(Album, TAG_FIELD_ALBUM, ref result);
            addIfConsistent(RecordingDate, TAG_FIELD_RECORDING_DATE, ref result);
            addIfConsistent(RecordingYear, TAG_FIELD_RECORDING_YEAR, ref result);
            addIfConsistent(RecordingDayMonth, TAG_FIELD_RECORDING_DAYMONTH, ref result);
            addIfConsistent(TrackNumber, TAG_FIELD_TRACK_NUMBER, ref result);
            addIfConsistent(DiscNumber, TAG_FIELD_DISC_NUMBER, ref result);
            addIfConsistent(Rating, TAG_FIELD_RATING, ref result);
            addIfConsistent(OriginalArtist, TAG_FIELD_ORIGINAL_ARTIST, ref result);
            addIfConsistent(OriginalAlbum, TAG_FIELD_ORIGINAL_ALBUM, ref result);
            addIfConsistent(Copyright, TAG_FIELD_COPYRIGHT, ref result);
            addIfConsistent(AlbumArtist, TAG_FIELD_ALBUM_ARTIST, ref result);
            addIfConsistent(Publisher, TAG_FIELD_PUBLISHER, ref result);

            return result;
        }

        private void addIfConsistent(String data, byte id, ref IDictionary<byte,String> map)
        {
            if ((data != null) && (data.Length > 0)) map[id] = data;
        }
    }
}
