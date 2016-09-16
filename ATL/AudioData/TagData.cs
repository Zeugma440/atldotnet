using ATL.AudioReaders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// Track description
	/// </summary>
	public class TagData
	{
		public TagData()
        {
            Pictures = new Dictionary<MetaDataIOFactory.PIC_CODE, Image>();
        }
        /*
        public static String META_TITLE = "TITLE";
        public static String META_ARTIST = "ARTIST";
        public static String META_COMPOSER = "COMPOSER";
        public static String META_COMMENT = "COMMENT";
        public static String META_GENRE = "GENRE";
        public static String META_ALBUM = "ALBUM";
        public static String META_DATE = "DATE";
        public static String META_TRACKNUM = "TRACKNUM";
        public static String META_DISCNUM = "DISCNUM";
        public static String META_RATING = "RATING";
        */

        public String Title = "";
		public String Artist = "";
        public String Composer = "";
		public String Comment = "";
        public String Genre = "";
        public String Album = "";
		public String Date = "";
		public String TrackNumber = "";
        public String DiscNumber = "";
        public String Rating = "";
        public IDictionary<MetaDataIOFactory.PIC_CODE, Image> Pictures;

        protected void readImageData(ref MemoryStream s, MetaDataIOFactory.PIC_CODE picCode)
        {
            if (Pictures.ContainsKey(picCode))
            {
                Pictures.Remove(picCode);
            }
            Pictures.Add(picCode, Image.FromStream(s));
        }
	}
}
