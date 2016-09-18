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
        
        /* Not really useful
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

        protected void readImageData(ref Stream s, MetaDataIOFactory.PIC_CODE picCode)
        {
            if (Pictures.ContainsKey(picCode))
            {
                Pictures.Remove(picCode);
            }
            Pictures.Add(picCode, Image.FromStream(s));
        }
	}
}
