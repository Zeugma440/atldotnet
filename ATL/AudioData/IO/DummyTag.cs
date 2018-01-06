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
        public DummyTag()
        {
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Instancing a Dummy Meta Data Reader");
        }

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
        public float Popularity
        {
            get { return 0; }
        }
        public int Size
        {
            get { return 0; }
        }
        public IList<PictureInfo> PictureTokens
        {
            get { return new List<PictureInfo>(); }
        }

        public string Copyright
        {
            get { return ""; }
        }

        public string OriginalArtist
        {
            get { return ""; }
        }

        public string OriginalAlbum
        {
            get { return ""; }
        }

        public string GeneralDescription
        {
            get { return ""; }
        }

        public string Publisher
        {
            get { return ""; }
        }

        public string AlbumArtist
        {
            get { return ""; }
        }

        public string Conductor
        {
            get { return ""; }
        }

        public IDictionary<string, string> AdditionalFields
        {
            get { return new Dictionary<string, string>();  }
        }

        public IList<ChapterInfo> Chapters
        {
            get { return new List<ChapterInfo>(); }
        }

        public IList<PictureInfo> EmbeddedPictures
        {
            get { return new List<PictureInfo>(); }
        }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return true;
        }

        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            throw new NotImplementedException();
        }

        public bool Remove(BinaryWriter w)
        {
            throw new NotImplementedException();
        }

        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
