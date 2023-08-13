using ATL.AudioData.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ATL.AudioData
{
    /// <summary>
    /// Wrapper for reading multiple tags according to a priority
    /// 
    /// Rule : The first non-empty field of the most prioritized tag becomes the "cross-detected" field
    /// There is no "field blending" across collections (pictures, additional fields) : the first non-empty collection is kept
    /// </summary>
    internal partial class CrossMetadataReader : IMetaDataIO
    {
        // Contains all IMetaDataIO objects to be read, in priority order (index [0] is the most important)
        private readonly IList<IMetaDataIO> metaReaders = null;

        public CrossMetadataReader(AudioDataManager audioManager, MetaDataIOFactory.TagType[] tagPriority)
        {
            metaReaders = new List<IMetaDataIO>();

            for (int i = 0; i < tagPriority.Length; i++)
            {
                if ((MetaDataIOFactory.TagType.NATIVE == tagPriority[i]) && (audioManager.HasNativeMeta()) && (audioManager.NativeTag != null))
                {
                    metaReaders.Add(audioManager.NativeTag);
                }
                if ((MetaDataIOFactory.TagType.ID3V1 == tagPriority[i]) && (audioManager.ID3v1.Exists))
                {
                    metaReaders.Add(audioManager.ID3v1);
                }
                if ((MetaDataIOFactory.TagType.ID3V2 == tagPriority[i]) && (audioManager.ID3v2.Exists))
                {
                    metaReaders.Add(audioManager.ID3v2);
                }
                if ((MetaDataIOFactory.TagType.APE == tagPriority[i]) && (audioManager.APEtag.Exists))
                {
                    metaReaders.Add(audioManager.APEtag);
                }
            }
        }

        /// <inheritdoc/>
        public bool Exists
        {
            get { return metaReaders.Count > 0; }
        }
        /// <inheritdoc/>
        public IList<Format> MetadataFormats
        {
            get
            {
                IList<Format> result = new List<Format>();
                foreach (IMetaDataIO reader in metaReaders)
                {
                    foreach (Format f in reader.MetadataFormats)
                    {
                        result.Add(f);
                    }
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string Title
        {
            get
            {
                string title = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    title = reader.Title;
                    if (title != "") break;
                }
                return title;
            }
        }
        /// <inheritdoc/>
        public string Artist
        {
            get
            {
                string artist = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    artist = reader.Artist;
                    if (artist != "") break;
                }
                return artist;
            }
        }
        /// <inheritdoc/>
        public string Composer
        {
            get
            {
                string composer = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    composer = reader.Composer;
                    if (composer != "") break;
                }
                return composer;
            }
        }
        /// <inheritdoc/>
        public string Comment
        {
            get
            {
                string comment = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    comment = reader.Comment;
                    if (comment != "") break;
                }
                return comment;
            }
        }
        /// <inheritdoc/>
        public string Genre
        {
            get
            {
                string genre = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    genre = reader.Genre;
                    if (genre != "") break;
                }
                return genre;
            }
        }
        /// <inheritdoc/>
        public ushort TrackNumber
        {
            get
            {
                ushort track = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    track = reader.TrackNumber;
                    if (track != 0) break;
                }
                return track;
            }
        }
        /// <inheritdoc/>
		public ushort TrackTotal
        {
            get
            {
                ushort trackTotal = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    trackTotal = reader.TrackTotal;
                    if (trackTotal != 0) break;
                }
                return trackTotal;
            }
        }
        /// <inheritdoc/>
        public ushort DiscNumber
        {
            get
            {
                ushort disc = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    disc = reader.DiscNumber;
                    if (disc != 0) break;
                }
                return disc;
            }
        }
        /// <inheritdoc/>
        public ushort DiscTotal
        {
            get
            {
                ushort discTotal = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    discTotal = reader.DiscTotal;
                    if (discTotal != 0) break;
                }
                return discTotal;
            }
        }
        /// <inheritdoc/>
        public DateTime Date
        {
            get
            {
                DateTime date = DateTime.MinValue;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    date = reader.Date;
                    if (date != DateTime.MinValue) break;
                }
                return date;
            }
        }

        /// <inheritdoc/>
        public bool IsDateYearOnly
        {
            get
            {
                foreach (IMetaDataIO reader in metaReaders)
                {
                    if (reader.Date != DateTime.MinValue) return reader.IsDateYearOnly;
                }
                return false;
            }
        }

        /// <inheritdoc/>
        public string Album
        {
            get
            {
                string album = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    album = reader.Album;
                    if (album != "") break;
                }
                return album;
            }
        }
        /// <inheritdoc/>
		public string Copyright
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Copyright;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
		public string AlbumArtist
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.AlbumArtist;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string Conductor
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Conductor;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string Publisher
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Publisher;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public DateTime PublishingDate
        {
            get
            {
                DateTime date = DateTime.MinValue;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    date = reader.PublishingDate;
                    if (date != DateTime.MinValue) break;
                }
                return date;
            }
        }
        /// <inheritdoc/>
        public string GeneralDescription
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.GeneralDescription;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string OriginalArtist
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.OriginalArtist;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string OriginalAlbum
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.OriginalAlbum;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string ProductId
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.ProductId;
                    if (result != "") break;
                }
                return result;
            }
        }
        public string SortAlbum
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.SortAlbum;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string SortAlbumArtist
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.SortAlbumArtist;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string SortArtist
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.SortArtist;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string SortTitle
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.SortTitle;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string Group
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Group;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string SeriesTitle
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.SeriesTitle;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string SeriesPart
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.SeriesPart;
                    if (result != "") break;
                }
                return result;
            }
        }

        public string LongDescription
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.LongDescription;
                    if (result != "") break;
                }
                return result;
            }
        }

        public int? BPM
        {
            get
            {
                int? result = null;
                if (!Settings.NullAbsentValues) result = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.BPM;
                    if (result.HasValue && (Settings.NullAbsentValues || result.Value > 0)) break;
                }
                return result;
            }
        }

        /// <inheritdoc/>
        public float? Popularity
        {
            get
            {
                float? result = null;
                if (!Settings.NullAbsentValues) result = 0;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Popularity;
                    if (result.HasValue && (Settings.NullAbsentValues || result.Value > 0)) break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public long PaddingSize
        {
            get
            {
                long result = 0;
                foreach (IMetaDataIO reader in metaReaders) result += reader.PaddingSize;
                return result;
            }
        }
        /// <inheritdoc/>
        public string ChaptersTableDescription
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.ChaptersTableDescription;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                IDictionary<string, string> result = new Dictionary<string, string>();

                foreach (var (readerAdditionalFields, s) in from IMetaDataIO reader in metaReaders
                                                            let readerAdditionalFields = reader.AdditionalFields
                                                            where readerAdditionalFields.Count > 0
                                                            from string s in readerAdditionalFields.Keys
                                                            where !result.ContainsKey(s)
                                                            select (readerAdditionalFields, s))
                {
                    result.Add(s, readerAdditionalFields[s]);
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public IList<ChapterInfo> Chapters
        {
            get
            {
                IList<ChapterInfo> chapters = new List<ChapterInfo>();

                IMetaDataIO reader = metaReaders.FirstOrDefault(r => r.Chapters != null && r.Chapters.Count > 0);
                if (reader != null)
                    foreach (ChapterInfo chapter in reader.Chapters) chapters.Add(chapter);

                return chapters;
            }
        }

        /// <inheritdoc/>
        public LyricsInfo Lyrics
        {
            get
            {
                IMetaDataIO reader = metaReaders.FirstOrDefault(r => r.Lyrics != null && r.Lyrics.Exists());
                if (reader != null) return new LyricsInfo(reader.Lyrics);
                return new LyricsInfo();
            }
        }

        /// <inheritdoc/>
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                IList<PictureInfo> pictures = new List<PictureInfo>();

                IMetaDataIO reader = metaReaders.FirstOrDefault(r => r.EmbeddedPictures != null && r.EmbeddedPictures.Count > 0);
                if (reader != null)
                    foreach (PictureInfo picture in reader.EmbeddedPictures) pictures.Add(picture);

                return pictures;
            }
        }

        /// <inheritdoc/>
        public long Size
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        public bool Read(Stream source, MetaDataIO.ReadTagParams readTagParams) { throw new NotImplementedException(); }

        /// <inheritdoc/>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public Task<bool> WriteAsync(Stream s, TagData tag, ProgressToken<float> writeProgress = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool Remove(Stream s) { throw new NotImplementedException(); }

        /// <inheritdoc/>
        public Task<bool> RemoveAsync(Stream s) { throw new NotImplementedException(); }

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
