using ATL.AudioData.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// Wrapper for reading multiple tags according to a priority
    /// 
    /// Rule : The first non-empty field of the most prioritized tag becomes the "cross-detected" field
    /// There is no "field blending" across collections (pictures, additional fields) : the first non-empty collection is kept
	/// </summary>
	internal class CrossMetadataReader : IMetaDataIO 
	{
        // Contains all IMetaDataIO objects to be read, in priority order (index [0] is the most important)
		private IList<IMetaDataIO> metaReaders = null;

		public CrossMetadataReader(AudioDataManager audioManager, int[] tagPriority)
		{
            metaReaders = new List<IMetaDataIO>();

			for (int i=0; i<tagPriority.Length; i++)
			{
                if ((MetaDataIOFactory.TAG_NATIVE == tagPriority[i]) && (audioManager.HasNativeMeta()) && (audioManager.NativeTag != null))
                {
                    metaReaders.Add(audioManager.NativeTag);
                }
                if ( (MetaDataIOFactory.TAG_ID3V1 == tagPriority[i]) && (audioManager.ID3v1.Exists) )
				{
					metaReaders.Add(audioManager.ID3v1);
				}
				if ( (MetaDataIOFactory.TAG_ID3V2 == tagPriority[i]) && (audioManager.ID3v2.Exists) )
				{
					metaReaders.Add(audioManager.ID3v2);
				}
				if ( (MetaDataIOFactory.TAG_APE == tagPriority[i]) && (audioManager.APEtag.Exists) )
				{
					metaReaders.Add(audioManager.APEtag);
				}
			}
		}

		/// <summary>
		/// Returns true if this kind of metadata exists in the file, false if not
		/// </summary>
		public bool Exists
		{
			get { return (metaReaders.Count > 0); }
		}
		/// <summary>
		/// Title of the track
		/// </summary>
		public String Title
		{
			get
			{
				String title = "";
				foreach(IMetaDataIO reader in metaReaders)
				{
					title = reader.Title;
					if (title != "") break;
				}
				return title;
			}
		}
		/// <summary>
		/// Artist
		/// </summary>
		public String Artist
		{
			get
			{
				String artist = "";
				foreach(IMetaDataIO reader in metaReaders)
				{
					artist = reader.Artist;
					if (artist != "") break;
				}
				return artist;
			}
		}
        /// <summary>
        /// Composer
        /// </summary>
        public String Composer
        {
            get
            {
                String composer = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    composer = reader.Composer;
                    if (composer != "") break;
                }
                return composer;
            }
        }
		/// <summary>
		/// Comments
		/// </summary>
		public String Comment
		{
			get
			{
				String comment = "";
				foreach(IMetaDataIO reader in metaReaders)
				{
					comment = reader.Comment;
					if (comment != "") break;
				}
				return comment;
			}
		}
		/// <summary>
		/// Genre
		/// </summary>
		public String Genre
		{
			get
			{
				String genre = "";
				foreach(IMetaDataIO reader in metaReaders)
				{
					genre = reader.Genre;
					if (genre != "") break;
				}
				return genre;
			}
		}
		/// <summary>
		/// Track number
		/// </summary>
		public ushort Track
		{
			get
			{
				ushort track = 0;
				foreach(IMetaDataIO reader in metaReaders)
				{
					track = reader.Track;
					if (track != 0) break;
				}
				return track;
			}
		}
        /// <summary>
        /// Disc number
        /// </summary>
        public ushort Disc
        {
            get
            {
                ushort disc = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    disc = reader.Disc;
                    if (disc != 0) break;
                }
                return disc;
            }
        }
		/// <summary>
		/// Year
		/// </summary>
		public String Year
		{
			get
			{
				String year = "";
				foreach(IMetaDataIO reader in metaReaders)
				{
					year = reader.Year;
					if (year != "") break;
				}
				return year;
			}
		}
		/// <summary>
		/// Title of the album
		/// </summary>
		public String Album
		{
			get 
			{
				String album = "";
				foreach(IMetaDataIO reader in metaReaders)
				{
					album = reader.Album;
					if (album != "") break;
				}
				return album;
			}
		}
        /// <summary>
		/// Copyright
		/// </summary>
		public String Copyright
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Copyright;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <summary>
		/// Album Arist
		/// </summary>
		public String AlbumArtist
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.AlbumArtist;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <summary>
        /// Conductor
        /// </summary>
        public String Conductor
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Conductor;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <summary>
        /// Publisher
        /// </summary>
        public String Publisher
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Publisher;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <summary>
        /// General description
        /// </summary>
        public String GeneralDescription
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.GeneralDescription;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <summary>
        /// Original artist
        /// </summary>
        public String OriginalArtist
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.OriginalArtist;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <summary>
        /// Original album
        /// </summary>
        public String OriginalAlbum
        {
            get
            {
                String result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.OriginalAlbum;
                    if (result != "") break;
                }
                return result;
            }
        }
        public ushort Rating
        {
            get
            {
                ushort rating = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    rating = reader.Rating;
                    if (rating != 0) break;
                }
                return rating;
            }
        }
        public float Popularity
        {
            get
            {
                float result = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Popularity;
                    if (result != 0) break;
                }
                return result;
            }
        }
        /// <summary>
        /// List of picture IDs stored in the tag
        /// </summary>
        public IList<PictureInfo> PictureTokens
        {
            get
            {
                IList<PictureInfo> pictures = new List<PictureInfo>();
                foreach (IMetaDataIO reader in metaReaders)
                {
                    if (reader.PictureTokens.Count > 0)
                    {
                        pictures = reader.PictureTokens;
                        break;
                    }
                }
                return pictures;
            }
        }

        /// <summary>
        /// Any other metadata field that is not represented among above getters
        /// </summary>
        public IDictionary<string,string> AdditionalFields
        {
            get
            {
                IDictionary<string,string> result = new Dictionary<string, string>();
                foreach (IMetaDataIO reader in metaReaders)
                {
                    IDictionary<string, string> readerAdditionalFields = reader.AdditionalFields;
                    if (readerAdditionalFields.Count > 0)
                    {
                        foreach (string s in readerAdditionalFields.Keys)
                        {
                            if (!result.ContainsKey(s)) result.Add(s, readerAdditionalFields[s]);
                        }
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Chapters
        /// </summary>
        public IList<ChapterInfo> Chapters
        {
            get
            {
                IList<ChapterInfo> chapters = new List<ChapterInfo>();
                foreach (IMetaDataIO reader in metaReaders)
                {
                    if (reader.Chapters != null && reader.Chapters.Count > 0)
                    {
                        foreach(ChapterInfo chapter in reader.Chapters)
                        {
                            chapters.Add(chapter);
                        }
                        break;
                    }
                }
                return chapters;
            }
        }

        /// <summary>
        /// Embedded pictures
        /// </summary>
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                IList<PictureInfo> pictures = new List<PictureInfo>();
                foreach (IMetaDataIO reader in metaReaders)
                {
                    if (reader.EmbeddedPictures != null && reader.EmbeddedPictures.Count > 0)
                    {
                        foreach (PictureInfo picture in reader.EmbeddedPictures)
                        {
                            pictures.Add(picture);
                        }
                        break;
                    }
                }
                return pictures;
            }
        }

        public int Size
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams) { throw new NotImplementedException(); }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag) { throw new NotImplementedException(); }

        public bool Remove(BinaryWriter w) { throw new NotImplementedException(); }

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
