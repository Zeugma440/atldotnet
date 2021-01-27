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

        /// <inheritdoc/>
        public bool Exists
        {
            get { return true; }
        }
        /// <inheritdoc/>
        public IList<Format> MetadataFormats
        {
            get { return new List<Format>(new Format[1] { Factory.UNKNOWN_FORMAT }); }
        }
        /// <inheritdoc/>
        public String Title
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public String Artist
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public String Composer
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public String Comment
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public String Genre
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public ushort Track
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public ushort TrackTotal
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public ushort Disc
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public ushort DiscTotal
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public DateTime Date
        {
            get { return DateTime.MinValue; }
        }
        /// <inheritdoc/>
        public String Year
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public String Album
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public float Popularity
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public long Size
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public IList<PictureInfo> PictureTokens
        {
            get { return new List<PictureInfo>(); }
        }
        /// <inheritdoc/>
        public string Copyright
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public string OriginalArtist
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public string OriginalAlbum
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public string GeneralDescription
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public string Publisher
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public DateTime PublishingDate
        {
            get { return DateTime.MinValue; }
        }
        /// <inheritdoc/>
        public string AlbumArtist
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public string Conductor
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public long PaddingSize
        {
            get { return 0; }
        }
        /// <inheritdoc/>
        public string ChaptersTableDescription
        {
            get { return ""; }
        }
        /// <inheritdoc/>
        public IDictionary<string, string> AdditionalFields
        {
            get { return new Dictionary<string, string>(); }
        }
        /// <inheritdoc/>
        public IList<ChapterInfo> Chapters
        {
            get { return new List<ChapterInfo>(); }
        }
        /// <inheritdoc/>
        public LyricsInfo Lyrics
        {
            get { return new LyricsInfo(); }
        }
        /// <inheritdoc/>
        public IList<PictureInfo> EmbeddedPictures
        {
            get { return new List<PictureInfo>(); }
        }
        /// <inheritdoc/>
        public bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            return true;
        }
        /// <inheritdoc/>
        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool Remove(BinaryWriter w)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
