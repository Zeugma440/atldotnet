using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ATL.AudioData.IO.MetaDataIO;

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
        private readonly IList<IMetaDataIO> metaReaders;

        public CrossMetadataReader(AudioDataManager audioManager, MetaDataIOFactory.TagType[] tagPriority)
        {
            metaReaders = new List<IMetaDataIO>();

            foreach (var t in tagPriority)
            {
                if (MetaDataIOFactory.TagType.NATIVE == t && audioManager.HasNativeMeta() && audioManager.NativeTag != null)
                {
                    metaReaders.Add(audioManager.NativeTag);
                }
                if (MetaDataIOFactory.TagType.ID3V1 == t && audioManager.ID3v1.Exists)
                {
                    metaReaders.Add(audioManager.ID3v1);
                }
                if (MetaDataIOFactory.TagType.ID3V2 == t && audioManager.ID3v2.Exists)
                {
                    metaReaders.Add(audioManager.ID3v2);
                }
                if (MetaDataIOFactory.TagType.APE == t && audioManager.APEtag.Exists)
                {
                    metaReaders.Add(audioManager.APEtag);
                }
            }
        }

        /// <inheritdoc/>
        public bool Exists => metaReaders.Count > 0;

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
        public string TrackNumber
        {
            get
            {
                string value = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    value = reader.TrackNumber;
                    if (!string.IsNullOrEmpty(value)) break;
                }
                return value;
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
        public DateTime OriginalReleaseDate
        {
            get
            {
                DateTime date = DateTime.MinValue;
                foreach (IMetaDataIO reader in metaReaders)
                {
                    date = reader.OriginalReleaseDate;
                    if (date != DateTime.MinValue) break;
                }
                return date;
            }
        }
        /// <inheritdoc/>
        public bool IsOriginalReleaseDateYearOnly
        {
            get
            {
                foreach (IMetaDataIO reader in metaReaders)
                {
                    if (reader.OriginalReleaseDate != DateTime.MinValue) return reader.IsOriginalReleaseDateYearOnly;
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
        public string Language
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Language;
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
        public string Lyricist
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Lyricist;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string InvolvedPeople
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.InvolvedPeople;
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
        /// <inheritdoc/>
        public string ISRC
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.ISRC;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string CatalogNumber
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.CatalogNumber;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public string AudioSourceUrl
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.AudioSourceUrl;
                    if (result != "") break;
                }
                return result;
            }
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public float? BPM
        {
            get
            {
                float? result = null;
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
        public string EncodedBy
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.EncodedBy;
                    if (result != "") break;
                }

                return result;
            }
        }
        /// <inheritdoc/>
        public string Encoder
        {
            get
            {
                string result = "";
                foreach (IMetaDataIO reader in metaReaders)
                {
                    result = reader.Encoder;
                    if (result != "") break;
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

                IMetaDataIO reader = metaReaders.FirstOrDefault(r => r.Chapters is { Count: > 0 });
                if (reader == null) return chapters;
                foreach (ChapterInfo chapter in reader.Chapters) chapters.Add(chapter);

                return chapters;
            }
        }

        /// <inheritdoc/>
        public IList<LyricsInfo> Lyrics
        {
            get
            {
                IList<LyricsInfo> result = new List<LyricsInfo>();

                IMetaDataIO reader = metaReaders.FirstOrDefault(r => r.Lyrics is { Count: > 0 });
                if (reader == null) return result;
                foreach (LyricsInfo l in reader.Lyrics) result.Add(l);

                return result;
            }
        }

        /// <inheritdoc/>
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                IList<PictureInfo> pictures = new List<PictureInfo>();

                IMetaDataIO reader = metaReaders.FirstOrDefault(r => r.EmbeddedPictures is { Count: > 0 });
                if (reader == null) return pictures;
                foreach (PictureInfo picture in reader.EmbeddedPictures) pictures.Add(picture);

                return pictures;
            }
        }

        /// <inheritdoc/>
        public long Size => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool Read(Stream source, ReadTagParams readTagParams) { throw new NotImplementedException(); }

        /// <inheritdoc/>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public Task<bool> WriteAsync(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public Task<bool> RemoveAsync(Stream s, WriteTagParams args) { throw new NotImplementedException(); }

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
