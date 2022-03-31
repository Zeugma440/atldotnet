using Commons;
using System.Collections.Generic;
using System.IO;
using System;

namespace ATL.AudioData.IO
{
    class VorbisTagContainer : IMetaDataIO
    {
        protected VorbisTag vorbisTag;


        /// <inheritdoc/>
        public bool Exists
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Exists;
            }
        }
        /// <inheritdoc/>
        public virtual IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0]);
                nativeFormat.Name = "Native / Vorbis";
                return new List<Format>(new Format[1] { nativeFormat });
            }
        }
        /// <inheritdoc/>
        public string Title
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Title;
            }
        }
        /// <inheritdoc/>
        public string Artist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Artist;
            }
        }
        /// <inheritdoc/>
        public string Composer
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Composer;
            }
        }
        /// <inheritdoc/>
        public string Comment
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Comment;
            }
        }
        /// <inheritdoc/>
        public string Genre
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Genre;
            }
        }
        /// <inheritdoc/>
        public ushort Track
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Track;
            }
        }
        /// <inheritdoc/>
        public ushort TrackTotal
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).TrackTotal;
            }
        }
        /// <inheritdoc/>
        public ushort Disc
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Disc;
            }
        }
        /// <inheritdoc/>
        public ushort DiscTotal
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).DiscTotal;
            }
        }
        /// <inheritdoc/>
        public DateTime Date
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Date;
            }
        }
        /// <inheritdoc/>
        public string Album
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Album;
            }
        }
        /// <inheritdoc/>
        public float? Popularity
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Popularity;
            }
        }
        /// <inheritdoc/>
        public string Copyright
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Copyright;
            }
        }
        /// <inheritdoc/>
        public string OriginalArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalArtist;
            }
        }
        /// <inheritdoc/>
        public string OriginalAlbum
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalAlbum;
            }
        }
        /// <inheritdoc/>
        public string GeneralDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).GeneralDescription;
            }
        }
        /// <inheritdoc/>
        public string Publisher
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Publisher;
            }
        }
        /// <inheritdoc/>
        public DateTime PublishingDate
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PublishingDate;
            }
        }
        /// <inheritdoc/>
        public string AlbumArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AlbumArtist;
            }
        }
        /// <inheritdoc/>
        public string Conductor
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Conductor;
            }
        }
        /// <inheritdoc/>
        public string ProductId
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).ProductId;
            }
        }
        /// <inheritdoc/>
        public long PaddingSize
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PaddingSize;
            }
        }
        /// <inheritdoc/>
        public IList<PictureInfo> PictureTokens
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PictureTokens;
            }
        }
        /// <inheritdoc/>
        public long Size
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Size;
            }
        }
        /// <inheritdoc/>
        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AdditionalFields;
            }
        }
        /// <inheritdoc/>
        public string ChaptersTableDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).ChaptersTableDescription;
            }
        }
        /// <inheritdoc/>
        public IList<ChapterInfo> Chapters
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Chapters;
            }
        }
        /// <inheritdoc/>
        public LyricsInfo Lyrics
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Lyrics;
            }
        }
        /// <inheritdoc/>
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).EmbeddedPictures;
            }
        }

        public virtual void Clear()
        {
            throw new NotImplementedException();
        }

        public virtual bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            throw new NotImplementedException();
        }

        public virtual bool Remove(BinaryWriter w)
        {
            throw new NotImplementedException();
        }

        public virtual void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }

        public virtual bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            throw new NotImplementedException();
        }
    }
}
