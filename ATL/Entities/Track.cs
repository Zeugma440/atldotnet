using ATL.AudioData;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.ChannelsArrangements;
using System.Linq;
using static ATL.TagData;
using System.Threading.Tasks;

namespace ATL
{
    /// <summary>
    /// High-level class for audio file manipulation
    ///
    /// Track is the "user-friendly" go-to class you should use for basic operations. Advanced operations should use lower level classes.
    /// Fields are mapped at the lowest level of the library (AudioData.IO). From there on, information "bubbles up" to Track.
    /// </summary>
    public class Track
    {
        private const string InMemoryPath = "In-memory";

        /// <summary>
        /// Basic constructor; does nothing else than instanciating the Track object
        /// </summary>
        public Track() { }

        /// <summary>
        /// Loads the file at the given path
        /// Only works with local paths; http, ftp and the like do not work.
        /// </summary>
        /// <param name="path">Path of the local file to be loaded</param>
        /// <param name="load">True to load the file when running this constructor (default : true)</param>
        public Track(string path, bool load = true)
        {
            this.Path = path;
            stream = null;
            if (load) Update();
        }

        /// <summary>
        /// Loads the raw data in the given stream according to the given MIME-type
        /// </summary>
        /// <param name="stream">Stream containing the raw data to be loaded</param>
        /// <param name="mimeType">MIME-type (e.g. "audio/mp3") or file extension (e.g. ".mp3") of the content</param>
        public Track(Stream stream, string mimeType = "")
        {
            this.stream = stream;
            this.mimeType = mimeType;
            Path = InMemoryPath;
            Update();
        }

        //=== METADATA

        /// <summary>
        /// Full path of the underlying file
        /// </summary>
        public readonly string Path;
        /// <summary>
		/// Title
		/// </summary>
		public string Title { get; set; }
        /// <summary>
		/// Artist
		/// </summary>
		public string Artist { get; set; }
        /// <summary>
        /// Composer
        /// </summary>
        public string Composer { get; set; }
        /// <summary>
        /// Comments
        /// </summary>
        public string Comment { get; set; }
        /// <summary>
		/// Genre
		/// </summary>
		public string Genre { get; set; }
        /// <summary>
		/// Title of the album
		/// </summary>
		public string Album { get; set; }
        /// <summary>
        /// Title of the original album
        /// </summary>
        public string OriginalAlbum { get; set; }
        /// <summary>
        /// Original artist
        /// </summary>
        public string OriginalArtist { get; set; }
        /// <summary>
        /// Copyright
        /// </summary>
        public string Copyright { get; set; }
        /// <summary>
        /// General description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Publisher
        /// </summary>
        public string Publisher { get; set; }
        /// <summary>
        /// Publishing date (set to DateTime.MinValue to remove)
        /// </summary>
        public DateTime? PublishingDate { get; set; }
        /// <summary>
        /// Album Artist
        /// </summary>
        public string AlbumArtist { get; set; }
        /// <summary>
        /// Conductor
        /// </summary>
        public string Conductor { get; set; }
        /// <summary>
        /// Product ID
        /// </summary>
        public string ProductId { get; set; }
        /// <summary>
        /// Title sort order
        /// A string which should be used instead of the album for sorting purposes
        /// </summary>
        public string SortAlbum { get; set; }
        /// <summary>
        /// Title sort order
        /// A string which should be used instead of the album artist for sorting purposes
        /// </summary>
        public string SortAlbumArtist { get; set; }
        /// <summary>
        /// Title sort order
        /// A string which should be used instead of the artist for sorting purposes
        /// </summary>
        public string SortArtist { get; set; }
        /// <summary>
        /// Title sort order
        /// A string which should be used instead of the title for sorting purposes
        /// </summary>
        public string SortTitle { get; set; }
        /// <summary>
        /// Content group description
        /// Used if the sound belongs to a larger category of sounds/music.
        /// For example, classical music is often sorted in different musical sections (e.g. "Piano Concerto").
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// Series title / Movement name
        /// </summary>
        public string SeriesTitle { get; set; }
        /// <summary>
        /// Series part / Movement index
        /// </summary>
        public string SeriesPart { get; set; }
        /// <summary>
        /// Long description (may also be called "Podcast description")
        /// </summary>
        public string LongDescription { get; set; }
        /// <summary>
        /// Beats per minute
        /// </summary>
        public int? BPM { get; set; }

        /// <summary>
		/// Recording Date (set to DateTime.MinValue to remove)
		/// </summary>
        public DateTime? Date
        {
            get => date;
            set
            {
                date = value;
                isYearExplicit = false;
            }
        }
        /// <summary>
		/// Recording Year
		/// </summary>
        public int? Year
        {
            get
            {
                if (canUseValue(Date)) return Date.Value.Year;
                if (Settings.NullAbsentValues) return null;
                return 0;
            }
            set
            {
                if (canUseValue(value) && value.Value > DateTime.MinValue.Year) Date = new DateTime(value.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                else if (Settings.NullAbsentValues) Date = null;
                else Date = DateTime.MinValue;
                isYearExplicit = true;
            }
        }
        /// <summary>
		/// Track number
		/// </summary>
        public int? TrackNumber { get; set; }
        /// <summary>
		/// Total track number
		/// </summary>
        public int? TrackTotal { get; set; }
        /// <summary>
		/// Disc number
		/// </summary>
        public int? DiscNumber { get; set; }
        /// <summary>
		/// Total disc number
		/// </summary>
        public int? DiscTotal { get; set; }
        /// <summary>
		/// Popularity (0% = 0 stars to 100% = 5 stars)
        /// e.g. 3.5 stars = 70%
		/// </summary>
        public float? Popularity { get; set; }
        /// <summary>
        /// Chapters table of content description
        /// </summary>
        public string ChaptersTableDescription { get; set; }
        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        public IList<ChapterInfo> Chapters { get; set; }
        /// <summary>
        /// Synchronized and unsynchronized lyrics
        /// </summary>
        public LyricsInfo Lyrics { get; set; }
        private LyricsInfo initialLyrics; // Initial field, used to identify removal

        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// Use MetaDataHolder.DATETIME_PREFIX + DateTime.ToFileTime() to set dates. ATL will format them properly.
        /// </summary>
        public IDictionary<string, string> AdditionalFields { get; set; }
        private ICollection<string> initialAdditionalFields; // Initial fields, used to identify removed ones

        private IList<PictureInfo> currentEmbeddedPictures { get; set; }
        private ICollection<PictureInfo> initialEmbeddedPictures; // Initial fields, used to identify removed ones

        private DateTime? date;
        private bool isYearExplicit;

        /// <summary>
        /// Format of the tagging systems
        /// </summary>
        public IList<Format> MetadataFormats { get; internal set; }


        //=== PHYSICAL PROPERTIES

        /// <summary>
        /// Bitrate (kilobytes per second)
        /// </summary>
		public int Bitrate { get; internal set; }
        /// <summary>
		/// Bit depth (bits per sample)
        /// -1 if bit depth is not relevant to that audio format
		/// </summary>
		public int BitDepth { get; internal set; }
        /// <summary>
		/// Sample rate (Hz)
		/// </summary>
        public double SampleRate { get; internal set; }
        /// <summary>
        /// Returns true if the bitrate is variable; false if not
        /// </summary>
        public bool IsVBR { get; internal set; }
        /// <summary>
        /// Family of the audio codec (See AudioDataIOFactory)
        /// 0=Streamed, lossy data
        /// 1=Streamed, lossless data
        /// 2=Sequenced with embedded sound library
        /// 3=Sequenced with codec or hardware-dependent sound library
        /// </summary>
		public int CodecFamily { get; internal set; }
        /// <summary>
        /// Format of the audio data
        /// </summary>
        public Format AudioFormat { get; internal set; }
        /// <summary>
        /// Duration (seconds)
        /// </summary>
        public int Duration => (int)Math.Round(DurationMs / 1000.0);

        /// <summary>
		/// Duration (milliseconds)
		/// </summary>
		public double DurationMs { get; internal set; }
        /// <summary>
		/// Channels arrangement
		/// </summary>
		public ChannelsArrangement ChannelsArrangement { get; internal set; }
        /// <summary>
        /// Low-level / technical informations about the audio file
        /// </summary>
        public TechnicalInfo TechnicalInformation { get; internal set; }


        /// <summary>
        /// List of pictures stored in the tag
        /// NB1 : PictureInfo.PictureData (raw binary picture data) is valued
        /// NB2 : Also allows to value embedded pictures inside chapters
        /// </summary>
        public IList<PictureInfo> EmbeddedPictures => getEmbeddedPictures();

        /// <summary>
        /// Stream used to access in-memory Track contents (alternative to path, which is used to access on-disk Track contents)
        /// </summary>
        private readonly Stream stream;
        /// <summary>
        /// MIME-type that describes in-memory Track contents (used in conjunction with stream)
        /// </summary>
        private readonly string mimeType;
        private AudioFileIO fileIO;


        // ========== METHODS

        // Used for pictures lazy loading
        private IList<PictureInfo> getEmbeddedPictures()
        {
            if (null == currentEmbeddedPictures)
            {
                currentEmbeddedPictures = new List<PictureInfo>();
                initialEmbeddedPictures = new List<PictureInfo>();

                Update(true);
            }

            return currentEmbeddedPictures;
        }

        /// <summary>
        /// Load all properties from the values stored on disk
        /// </summary>
        /// <param name="onlyReadEmbeddedPictures">True to only read embedded pictures - used for pictures lazy loading (default : false)</param>
        protected void Update(bool onlyReadEmbeddedPictures = false)
        {
            if (string.IsNullOrEmpty(Path)) return;

            // TODO when tag is not available, customize by naming options // tracks (...)
            if (null == stream) fileIO = new AudioFileIO(Path, onlyReadEmbeddedPictures, Settings.ReadAllMetaFrames);
            else fileIO = new AudioFileIO(stream, mimeType, onlyReadEmbeddedPictures, Settings.ReadAllMetaFrames);

            IMetaDataIO metadata = fileIO.Metadata;
            MetadataFormats = new List<Format>(metadata.MetadataFormats);

            if (onlyReadEmbeddedPictures)
            {
                foreach (PictureInfo picInfo in metadata.EmbeddedPictures)
                {
                    picInfo.ComputePicHash();
                    currentEmbeddedPictures.Add(picInfo);

                    PictureInfo initialPicInfo = new PictureInfo(picInfo, false);
                    initialEmbeddedPictures.Add(initialPicInfo);
                }
                // Don't overwrite all the other fields with their initial value 
                // otherwise any value set by the user before calling EmbeddedPictures would be lost
                return;
            }

            if (currentEmbeddedPictures != null)
            {
                currentEmbeddedPictures.Clear();
                initialEmbeddedPictures.Clear();
                currentEmbeddedPictures = null;
                initialEmbeddedPictures = null;
            }

            Title = processString(metadata.Title);
            if (Settings.UseFileNameWhenNoTitle && string.IsNullOrEmpty(Title) && Path != InMemoryPath)
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(Path);
            }
            Artist = Utils.ProtectValue(processString(metadata.Artist));
            Composer = Utils.ProtectValue(processString(metadata.Composer));
            Comment = Utils.ProtectValue(processString(metadata.Comment));
            Genre = Utils.ProtectValue(processString(metadata.Genre));
            OriginalArtist = Utils.ProtectValue(processString(metadata.OriginalArtist));
            OriginalAlbum = Utils.ProtectValue(processString(metadata.OriginalAlbum));
            Description = Utils.ProtectValue(processString(metadata.GeneralDescription));
            Copyright = Utils.ProtectValue(processString(metadata.Copyright));
            Publisher = Utils.ProtectValue(processString(metadata.Publisher));
            AlbumArtist = Utils.ProtectValue(processString(metadata.AlbumArtist));
            Conductor = Utils.ProtectValue(processString(metadata.Conductor));
            ProductId = Utils.ProtectValue(processString(metadata.ProductId));
            SortAlbum = Utils.ProtectValue(processString(metadata.SortAlbum));
            SortAlbumArtist = Utils.ProtectValue(processString(metadata.SortAlbumArtist));
            SortArtist = Utils.ProtectValue(processString(metadata.SortArtist));
            SortTitle = Utils.ProtectValue(processString(metadata.SortTitle));
            Group = Utils.ProtectValue(processString(metadata.Group));
            SeriesTitle = Utils.ProtectValue(processString(metadata.SeriesTitle));
            SeriesPart = Utils.ProtectValue(processString(metadata.SeriesPart));
            LongDescription = Utils.ProtectValue(processString(metadata.LongDescription));
            Album = Utils.ProtectValue(processString(metadata.Album));
            isYearExplicit = metadata.IsDateYearOnly;
            if (metadata.IsDateYearOnly)
            {
                Year = update(metadata.Date.Year);
            }
            else
            {
                Date = update(metadata.Date);
            }
            PublishingDate = update(metadata.PublishingDate);
            TrackNumber = update(metadata.TrackNumber);
            TrackTotal = update(metadata.TrackTotal);
            DiscNumber = update(metadata.DiscNumber);
            DiscTotal = update(metadata.DiscTotal);
            Popularity = metadata.Popularity;
            BPM = metadata.BPM;

            Chapters = metadata.Chapters;
            ChaptersTableDescription = Utils.ProtectValue(metadata.ChaptersTableDescription);
            Lyrics = metadata.Lyrics;
            initialLyrics = metadata.Lyrics;

            // Deep copy
            AdditionalFields = new Dictionary<string, string>();
            foreach (string key in metadata.AdditionalFields.Keys)
            {
                AdditionalFields.Add(key, processString(metadata.AdditionalFields[key]));
            }
            initialAdditionalFields = metadata.AdditionalFields.Keys;

            // Physical information
            Bitrate = fileIO.IntBitRate;
            BitDepth = fileIO.BitDepth;
            CodecFamily = fileIO.CodecFamily;
            AudioFormat = fileIO.AudioFormat;

            DurationMs = fileIO.Duration;

            IsVBR = fileIO.IsVBR;
            SampleRate = fileIO.SampleRate;
            ChannelsArrangement = fileIO.ChannelsArrangement;

            TechnicalInformation = new TechnicalInfo(fileIO.AudioDataOffset, fileIO.AudioDataSize);
        }

        private TagData toTagData()
        {
            TagData result = new TagData();

            result.IntegrateValue(Field.TITLE, Title);
            result.IntegrateValue(Field.ARTIST, Artist);
            result.IntegrateValue(Field.COMPOSER, Composer);
            result.IntegrateValue(Field.COMMENT, Comment);
            result.IntegrateValue(Field.GENRE, Genre);
            result.IntegrateValue(Field.ORIGINAL_ARTIST, OriginalArtist);
            result.IntegrateValue(Field.ORIGINAL_ALBUM, OriginalAlbum);
            result.IntegrateValue(Field.GENERAL_DESCRIPTION, Description);
            if (Popularity.HasValue)
                result.IntegrateValue(Field.RATING, toTagValue(Popularity.Value));
            else result.IntegrateValue(Field.RATING, Settings.NullAbsentValues ? "" : "0");
            result.IntegrateValue(Field.COPYRIGHT, Copyright);
            result.IntegrateValue(Field.PUBLISHER, Publisher);
            result.IntegrateValue(Field.PUBLISHING_DATE, toTagValue(PublishingDate));
            result.IntegrateValue(Field.ALBUM_ARTIST, AlbumArtist);
            result.IntegrateValue(Field.CONDUCTOR, Conductor);
            result.IntegrateValue(Field.PRODUCT_ID, ProductId);
            result.IntegrateValue(Field.SORT_ALBUM, SortAlbum);
            result.IntegrateValue(Field.SORT_ALBUM_ARTIST, SortAlbumArtist);
            result.IntegrateValue(Field.SORT_ARTIST, SortArtist);
            result.IntegrateValue(Field.SORT_TITLE, SortTitle);
            result.IntegrateValue(Field.GROUP, Group);
            result.IntegrateValue(Field.SERIES_TITLE, SeriesTitle);
            result.IntegrateValue(Field.SERIES_PART, SeriesPart);
            result.IntegrateValue(Field.LONG_DESCRIPTION, LongDescription);
            result.IntegrateValue(Field.BPM, toTagValue(BPM));
            if (isYearExplicit)
            {
                result.IntegrateValue(Field.RECORDING_YEAR, toTagValue(Year));
                result.IntegrateValue(Field.RECORDING_DATE, toTagValue(Year));
            }
            else
            {
                result.IntegrateValue(Field.RECORDING_DATE, toTagValue(Date));
                result.IntegrateValue(Field.RECORDING_YEAR, "");
            }
            result.IntegrateValue(Field.ALBUM, Album);
            result.IntegrateValue(Field.TRACK_NUMBER, toTagValue(TrackNumber));
            result.IntegrateValue(Field.TRACK_TOTAL, toTagValue(TrackTotal));
            result.IntegrateValue(Field.DISC_NUMBER, toTagValue(DiscNumber));
            result.IntegrateValue(Field.DISC_TOTAL, toTagValue(DiscTotal));
            result.IntegrateValue(Field.CHAPTERS_TOC_DESCRIPTION, ChaptersTableDescription);

            result.Chapters = new List<ChapterInfo>();
            foreach (ChapterInfo chapter in Chapters)
            {
                result.Chapters.Add(new ChapterInfo(chapter));
            }

            if (Lyrics != null) result.Lyrics = new LyricsInfo(Lyrics);
            else if (initialLyrics != null)
            {
                result.Lyrics = LyricsInfo.ForRemoval();
            }

            foreach (string s in AdditionalFields.Keys)
            {
                MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY;
                if (MetaFieldInfo.IsAdditionalDataNative(s)) tagType = MetaDataIOFactory.TagType.NATIVE;
                result.AdditionalFields.Add(new MetaFieldInfo(tagType, s, AdditionalFields[s]));
            }

            // Detect and tag deleted Additional fields (=those which were in initialAdditionalFields and do not appear in AdditionalFields anymore)
            foreach (string s in initialAdditionalFields)
            {
                if (!AdditionalFields.ContainsKey(s))
                {
                    MetaFieldInfo metaFieldToDelete = new MetaFieldInfo(MetaDataIOFactory.TagType.ANY, s, "");
                    metaFieldToDelete.MarkedForDeletion = true;
                    result.AdditionalFields.Add(metaFieldToDelete);
                }
            }

            result.Pictures = new List<PictureInfo>();

            if (initialEmbeddedPictures != null && currentEmbeddedPictures != null)
            {
                // Process target pictures first, in their specified order
                foreach (PictureInfo targetPic in currentEmbeddedPictures)
                {
                    PictureInfo picInfo = initialEmbeddedPictures.FirstOrDefault(pi => targetPic.EqualsProper(pi));

                    if (picInfo != null)
                    {
                        result.Pictures.Add(targetPic);
                        // Compare picture contents
                        targetPic.ComputePicHash();
                        // A new picture content has been defined for an existing location
                        if (targetPic.PictureHash != picInfo.PictureHash)
                        {
                            PictureInfo picToDelete = new PictureInfo(picInfo);
                            picToDelete.MarkedForDeletion = true;
                            result.Pictures.Add(picToDelete);
                        }
                    }
                    else result.Pictures.Add(targetPic); // Completely new picture
                }

                // Detect and tag deleted pictures (=those which were in initialEmbeddedPictures and do not appear in embeddedPictures anymore)
                foreach (PictureInfo picInfo in initialEmbeddedPictures)
                {
                    PictureInfo targetPic = currentEmbeddedPictures.FirstOrDefault(pi => picInfo.EqualsProper(pi));

                    if (null == targetPic)
                    {
                        PictureInfo picToDelete = new PictureInfo(picInfo);
                        picToDelete.MarkedForDeletion = true;
                        result.Pictures.Add(picToDelete);
                    }
                }
            }

            return result;
        }


        /// <summary>
        /// Save Track to disk using all existing tag types
        /// Use Save instead of SaveAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        /// </summary>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool Save(Action<float> writeProgress = null)
        {
            bool result = fileIO.Save(toTagData(), null, new ProgressToken<float>(writeProgress));
            if (result) Update();

            return result;
        }

        /// <summary>
        /// Save Track to disk using the given tag type
        /// Use Save instead of SaveAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        /// </summary>
        /// <param name="tagType">Tag type to save.
        /// - Use TagType.ANY to save on existing tags (same behaviour as Save(Action&lt;float&gt;))
        /// - Use any other TagType to save on existing tags and on the given TagType, if supported</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool Save(MetaDataIOFactory.TagType tagType, Action<float> writeProgress = null)
        {
            bool result = fileIO.Save(toTagData(), tagType, new ProgressToken<float>(writeProgress));
            if (result) Update();

            return result;
        }

        /// <summary>
        /// Save Track to disk using all existing tag types
        /// Use SaveAsync instead of Save if you need progress feedback
        /// (e.g. Windows Forms app with progress bar that updates one file at a time)
        /// </summary>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public async Task<bool> SaveAsync(IProgress<float> writeProgress = null)
        {
            bool result = await fileIO.SaveAsync(toTagData(), null, new ProgressToken<float>(writeProgress));
            if (result) Update();

            return result;
        }

        /// <summary>
        /// Save Track to disk using the given tag type
        /// Use SaveAsync instead of Save if you need progress feedback
        /// (e.g. Windows Forms app with progress bar that updates one file at a time)
        /// </summary>
        /// <param name="tagType">Tag type to save.
        /// - Use TagType.ANY to save on existing tags (same behaviour as SaveAsync(IProgress&lt;float&gt;))
        /// - Use any other TagType to save on existing tags and on the given TagType, if supported</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public async Task<bool> SaveAsync(MetaDataIOFactory.TagType tagType, IProgress<float> writeProgress = null)
        {
            bool result = await fileIO.SaveAsync(toTagData(), tagType, new ProgressToken<float>(writeProgress));
            if (result) Update();

            return result;
        }

        /// <summary>
        /// Remove the given tag type from the Track
        /// Use Remove instead of RemoveAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        /// </summary>
        /// <param name="tagType">Tag type to remove</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <see cref="MetaDataIOFactory"/>
        /// <returns>True if removal succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool Remove(MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY, Action<float> writeProgress = null)
        {
            bool result = fileIO.Remove(tagType, new ProgressToken<float>(writeProgress));
            if (result) Update();

            return result;
        }

        /// <summary>
        /// Remove the given tag type from the Track
        /// Use RemoveAsync instead of Remove if you need progress feedback
        /// (e.g. Windows Forms app with progress bar that updates one file at a time)
        /// </summary>
        /// <param name="tagType">Tag type to remove</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <see cref="MetaDataIOFactory"/>
        /// <returns>True if removal succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public async Task<bool> RemoveAsync(MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY, IProgress<float> writeProgress = null)
        {
            bool result = await fileIO.RemoveAsync(tagType, new ProgressToken<float>(writeProgress));
            if (result) Update();

            return result;
        }

        /// FORMATTING UTILITIES

        private string processString(string value)
        {
            return value.Replace(Settings.InternalValueSeparator, Settings.DisplayValueSeparator);
        }

        private DateTime? update(DateTime value)
        {
            if (value > DateTime.MinValue || !Settings.NullAbsentValues) return value;
            else return null;
        }

        private int? update(int value)
        {
            if (value != 0 || !Settings.NullAbsentValues) return value;
            else return null;
        }

        private bool canUseValue(DateTime? value)
        {
            return (value.HasValue && (Settings.NullAbsentValues || !value.Equals(DateTime.MinValue)));
        }
        private bool canUseValue(int? value)
        {
            return (value.HasValue && (Settings.NullAbsentValues || value != 0));
        }

        private bool canUseValue(float value)
        {
            return (Settings.NullAbsentValues || value != 0.0);
        }

        private string toTagValue(DateTime? value)
        {
            if (canUseValue(value)) return TrackUtils.FormatISOTimestamp(value.Value);
            else return Settings.NullAbsentValues ? "" : "0";
        }

        private string toTagValue(int? value)
        {
            if (canUseValue(value)) return value.Value.ToString();
            else return Settings.NullAbsentValues ? "" : "0";
        }

        private string toTagValue(float value)
        {
            if (canUseValue(value)) return value.ToString();
            else return Settings.NullAbsentValues ? "" : "0";
        }
    }
}
