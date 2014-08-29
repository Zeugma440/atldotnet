using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
	/// <summary>
	/// Dummy metadata provider
	/// </summary>
	public class DummyTag : IMetaDataReader
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
        public IList<MetaReaderFactory.PIC_CODE> Pictures
        {
            get { return new List<MetaReaderFactory.PIC_CODE>(); }
        }
        public bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            return true;
        }

		public DummyTag()
		{			
		}		
	}
}
