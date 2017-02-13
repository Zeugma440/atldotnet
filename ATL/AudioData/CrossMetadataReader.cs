using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// Wrapper for reading multiple tags according to a priority
	/// </summary>
	public class CrossMetadataReader : IMetaDataIO 
	{
        // Contains all IMetaDataIO objects to be read, in priority order (index [0] is the most important)
		private IList<IMetaDataIO> metaReaders = null;

		public CrossMetadataReader(ref IAudioDataIO baseReader, int[] tagPriority)
		{
            metaReaders = new List<IMetaDataIO>();

			for (int i=0; i<tagPriority.Length; i++)
			{
                if ((MetaDataIOFactory.TAG_NATIVE == tagPriority[i]) && (baseReader.HasNativeMeta()))
                {
                    metaReaders.Add(baseReader.NativeTag);
                }
                if ( (MetaDataIOFactory.TAG_ID3V1 == tagPriority[i]) && (baseReader.ID3v1.Exists) )
				{
					metaReaders.Add(baseReader.ID3v1);
				}
				if ( (MetaDataIOFactory.TAG_ID3V2 == tagPriority[i]) && (baseReader.ID3v2.Exists) )
				{
					metaReaders.Add(baseReader.ID3v2);
				}
				if ( (MetaDataIOFactory.TAG_APE == tagPriority[i]) && (baseReader.APEtag.Exists) )
				{
					metaReaders.Add(baseReader.APEtag);
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
        /// Rating
        /// </summary>
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
        /// <summary>
        /// List of picture IDs stored in the tag
        /// </summary>
        public IList<MetaDataIOFactory.PIC_CODE> Pictures
        {
            get
            {
                IList<MetaDataIOFactory.PIC_CODE> pictures = new List<MetaDataIOFactory.PIC_CODE>();
                foreach (IMetaDataIO reader in metaReaders)
                {
                    pictures = reader.Pictures;
                    if (pictures.Count > 0) break;
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

        public long Offset
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool Read(BinaryReader source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler)
        {
            throw new NotImplementedException();
        }

        public bool Write(BinaryReader r, TagData tag)
        {
            throw new NotImplementedException();
        }
    }
}
