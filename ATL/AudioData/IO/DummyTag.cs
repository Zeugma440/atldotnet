using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData.IO
{
	/// <summary>
	/// Dummy metadata provider
	/// </summary>
	public class DummyTag : IMetaDataIO
	{
		public bool Exists
		{
			get { return true; }
		}
		public String Title
		{
			get { return ""; }
		}
		public String Artist
		{
			get { return ""; }
		}
        public String Composer
        {
            get { return ""; }
        }
		public String Comment
		{
			get { return ""; }
		}
		public String Genre
		{
			get { return ""; }
		}
		public ushort Track
		{
			get { return 0; }
		}
        public ushort Disc
        {
            get { return 0; }
        }
		public String Year
		{
			get { return ""; }
		}
		public String Album
		{
			get { return ""; }
		}
        public ushort Rating
        {
            get { return 0; }
        }
        public int Size
        {
            get { return 0; }
        }
        public long Offset
        {
            get { return 0; }
        }
        public IList<MetaDataIOFactory.PIC_TYPE> PictureTokens
        {
            get { return new List<MetaDataIOFactory.PIC_TYPE>(); }
        }
        public bool Read(BinaryReader source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames)
        {
            return true;
        }

        public long Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return 0;
        }
        
	}
}
