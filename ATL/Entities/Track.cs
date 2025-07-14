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
            streamInitialPos = -1;
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
            streamInitialPos = stream.Position;
            this.mimeType = mimeType;
            Path = AudioDataIOFactory.IN_MEMORY;
            Update();
        }

        //=== LOCATION

        /// <summary>
        /// Full path of the underlying file
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Stream used to access in-memory Track contents (alternative to path, which is used to access on-disk Track contents)
        /// </summary>
        private Stream stream;
        /// <summary>
        /// Initial position of stream when passed to constructor
        /// </summary>
        private long streamInitialPos;
        /// <summary>
        /// MIME-type that describes in-memory Track contents (used in conjunction with stream)
        /// </summary>
        private string mimeType;


        private AudioFileIO fileIO;


        //=== METADATA

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
        /// Lyricist
        /// </summary>
        public string Lyricist { get; set; }
        /// <summary>
        /// Mapping between functions (e.g. "producer") and names. Every odd field is a
        /// function and every even is an name or a comma delimited list of names
        /// </summary>
        public string InvolvedPeople { get; set; }
        /// <summary>
        /// Product ID
        /// </summary>
        public string ProductId { get; set; }
        /// <summary>
        /// International Standard Recording Code (ISRC)
        /// </summary>
        public string ISRC { get; set; }
        /// <summary>
        /// Catalog number
        /// </summary>
        public string CatalogNumber { get; set; }
        /// <summary>
        /// Audio source URL
        /// </summary>
        public string AudioSourceUrl { get; set; }
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
        /// Language
        /// </summary>
        public string Language { get; set; }
        /// <summary>
        /// Beats per minute
        /// </summary>
        public float? BPM { get; set; }
        /// <summary>
        /// Person or organization that encoded the file
        /// </summary>
        public string EncodedBy { get; set; }
        /// <summary>
        /// Software that encoded the file, with relevant settings if any
        /// </summary>
        public string Encoder { get; set; }

        /// <summary>
		/// Recording date (set to DateTime.MinValue to remove)
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
		/// Recording year
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
                if (canUseValue(value))
                {
                    if (Utils.TryExtractDateTimeFromDigits(value.ToString(), out var tmpDate))
                    {
                        Date = tmpDate.Value;
                        isYearExplicit = Date.Value is { Day: 1, Month: 1 };
                    }
                    else Date = DateTime.MinValue;
                }
                else if (Settings.NullAbsentValues) Date = null;
                else Date = DateTime.MinValue;
            }
        }
        /// <summary>
        /// Original release date (set to DateTime.MinValue to remove)
        /// </summary>
        public DateTime? OriginalReleaseDate
        {
            get => originalReleaseDate;
            set
            {
                originalReleaseDate = value;
                isORYearExplicit = false;
            }
        }
        /// <summary>
        /// Original release year
        /// </summary>
        public int? OriginalReleaseYear
        {
            get
            {
                if (canUseValue(OriginalReleaseDate)) return OriginalReleaseDate.Value.Year;
                if (Settings.NullAbsentValues) return null;
                return 0;
            }
            set
            {
                if (canUseValue(value))
                {
                    if (Utils.TryExtractDateTimeFromDigits(value.ToString(), out var tmpDate))
                    {
                        OriginalReleaseDate = tmpDate.Value;
                        isORYearExplicit = OriginalReleaseDate.Value is { Day: 1, Month: 1 };
                    }
                    else OriginalReleaseDate = DateTime.MinValue;
                }
                else if (Settings.NullAbsentValues) OriginalReleaseDate = null;
                else OriginalReleaseDate = DateTime.MinValue;
            }
        }
        /// <summary>
		/// Track number in string form
        /// Useful for storing LP tracks (e.g. A1, A2...)
        /// NB : Does not include total notation (e.g. 01/12); only the track number itself
		/// </summary>
        public string TrackNumberStr { get; set; }
        /// <summary>
		/// Track number
		/// </summary>
        public int? TrackNumber
        {
            get
            {
                var result = Utils.ParseFirstIntegerPart(TrackNumberStr);
                if (-1 != result) return result;
                if (Settings.NullAbsentValues) return null;
                return 0;
            }
            set
            {
                if (canUseValue(value) && value.Value > -1) TrackNumberStr = value.ToString();
                else if (Settings.NullAbsentValues) TrackNumberStr = null;
                else TrackNumberStr = "";
            }
        }
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
        public IList<LyricsInfo> Lyrics { get; set; }

        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// Use MetaDataHolder.DATETIME_PREFIX + DateTime.ToFileTime() to set dates. ATL will format them properly.
        /// </summary>
        public IDictionary<string, string> AdditionalFields { get; set; }
        // Initial fields, used to identify removed ones
        private readonly ICollection<string> initialAdditionalFields = new List<string>();

        private IList<PictureInfo> currentEmbeddedPictures { get; set; }
        // Initial fields, used to identify removed ones
        private readonly ICollection<PictureInfo> initialEmbeddedPictures = new List<PictureInfo>();

        private DateTime? date;
        private bool isYearExplicit;

        private DateTime? originalReleaseDate;
        private bool isORYearExplicit;

        /// <summary>
        /// Format of tagging systems present on the file
        /// NB : their IDs match `MetaDataIOFactory.TagType` enum values
        /// </summary>
        public IList<Format> MetadataFormats { get; internal set; }

        /// <summary>
        /// Format of tagging systems supported by the file
        /// NB : their IDs match `MetaDataIOFactory.TagType` enum values
        /// </summary>
        public IList<Format> SupportedMetadataFormats { get; internal set; }


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
        public AudioFormat AudioFormat { get; internal set; }
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


        // ========== METHODS

        // Used for pictures lazy loading
        private IList<PictureInfo> getEmbeddedPictures()
        {
            if (null != currentEmbeddedPictures) return currentEmbeddedPictures;

            currentEmbeddedPictures = new List<PictureInfo>();
            initialEmbeddedPictures.Clear();
            Update(true);

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
            else
            {
                stream.Position = streamInitialPos;
                fileIO = new AudioFileIO(stream, mimeType, onlyReadEmbeddedPictures, Settings.ReadAllMetaFrames);
            }

            IMetaDataIO metadata = fileIO.Metadata;
            MetadataFormats = new List<Format>(metadata.MetadataFormats);
            SupportedMetadataFormats = new List<Format>(fileIO.GetSupportedMetas().Select(f => MetaDataIOFactory.GetInstance().getFormat((int)f)));

            mimeType = fileIO.AudioFormat.MimeList.FirstOrDefault();

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
                currentEmbeddedPictures = null;
            }
            initialEmbeddedPictures.Clear();

            Title = processString(metadata.Title);
            if (Settings.UseFileNameWhenNoTitle && string.IsNullOrEmpty(Title) && Path != AudioDataIOFactory.IN_MEMORY)
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
            Lyricist = Utils.ProtectValue(processString(metadata.Lyricist));
            InvolvedPeople = Utils.ProtectValue(processString(metadata.InvolvedPeople));
            ProductId = Utils.ProtectValue(processString(metadata.ProductId));
            ISRC = Utils.ProtectValue(processString(metadata.ISRC));
            CatalogNumber = Utils.ProtectValue(processString(metadata.CatalogNumber));
            AudioSourceUrl = Utils.ProtectValue(processString(metadata.AudioSourceUrl));
            SortAlbum = Utils.ProtectValue(processString(metadata.SortAlbum));
            SortAlbumArtist = Utils.ProtectValue(processString(metadata.SortAlbumArtist));
            SortArtist = Utils.ProtectValue(processString(metadata.SortArtist));
            SortTitle = Utils.ProtectValue(processString(metadata.SortTitle));
            Group = Utils.ProtectValue(processString(metadata.Group));
            SeriesTitle = Utils.ProtectValue(processString(metadata.SeriesTitle));
            SeriesPart = Utils.ProtectValue(processString(metadata.SeriesPart));
            LongDescription = Utils.ProtectValue(processString(metadata.LongDescription));
            Language = Utils.ProtectValue(processString(metadata.Language));
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
            isORYearExplicit = metadata.IsOriginalReleaseDateYearOnly;
            if (metadata.IsOriginalReleaseDateYearOnly)
            {
                OriginalReleaseYear = update(metadata.OriginalReleaseDate.Year);
            }
            else
            {
                OriginalReleaseDate = update(metadata.OriginalReleaseDate);
            }
            PublishingDate = update(metadata.PublishingDate);
            TrackNumberStr = metadata.TrackNumber;
            TrackTotal = update(metadata.TrackTotal);
            DiscNumber = update(metadata.DiscNumber);
            DiscTotal = update(metadata.DiscTotal);
            Popularity = metadata.Popularity;
            BPM = metadata.BPM;
            EncodedBy = metadata.EncodedBy;
            Encoder = metadata.Encoder;

            Chapters = metadata.Chapters;
            ChaptersTableDescription = Utils.ProtectValue(metadata.ChaptersTableDescription);

            // Deep copy
            Lyrics = new List<LyricsInfo>();
            foreach (LyricsInfo l in metadata.Lyrics)
            {
                Lyrics.Add(new LyricsInfo(l));
            }

            // Deep copy
            AdditionalFields = new Dictionary<string, string>();
            foreach (string key in metadata.AdditionalFields.Keys)
            {
                AdditionalFields.Add(key, processString(metadata.AdditionalFields[key]));
            }
            initialAdditionalFields.Clear();
            foreach (string key in metadata.AdditionalFields.Keys) initialAdditionalFields.Add(key);

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

        internal TagData toTagData()
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
            result.IntegrateValue(Field.LYRICIST, Lyricist);
            result.IntegrateValue(Field.INVOLVED_PEOPLE, InvolvedPeople);
            result.IntegrateValue(Field.PRODUCT_ID, ProductId);
            result.IntegrateValue(Field.ISRC, ISRC);
            result.IntegrateValue(Field.CATALOG_NUMBER, CatalogNumber);
            result.IntegrateValue(Field.AUDIO_SOURCE_URL, AudioSourceUrl);
            result.IntegrateValue(Field.SORT_ALBUM, SortAlbum);
            result.IntegrateValue(Field.SORT_ALBUM_ARTIST, SortAlbumArtist);
            result.IntegrateValue(Field.SORT_ARTIST, SortArtist);
            result.IntegrateValue(Field.SORT_TITLE, SortTitle);
            result.IntegrateValue(Field.GROUP, Group);
            result.IntegrateValue(Field.SERIES_TITLE, SeriesTitle);
            result.IntegrateValue(Field.SERIES_PART, SeriesPart);
            result.IntegrateValue(Field.LONG_DESCRIPTION, LongDescription);
            result.IntegrateValue(Field.LANGUAGE, Language);
            result.IntegrateValue(Field.BPM, toTagValue(BPM));
            result.IntegrateValue(Field.ENCODED_BY, EncodedBy);
            result.IntegrateValue(Field.ENCODER, Encoder);
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
            if (isORYearExplicit)
            {
                result.IntegrateValue(Field.ORIG_RELEASE_YEAR, toTagValue(OriginalReleaseYear));
                result.IntegrateValue(Field.ORIG_RELEASE_DATE, toTagValue(OriginalReleaseYear));
            }
            else
            {
                result.IntegrateValue(Field.ORIG_RELEASE_DATE, toTagValue(OriginalReleaseDate));
                result.IntegrateValue(Field.ORIG_RELEASE_YEAR, "");
            }
            result.IntegrateValue(Field.ALBUM, Album);
            result.IntegrateValue(Field.TRACK_NUMBER, TrackNumberStr);
            result.IntegrateValue(Field.TRACK_TOTAL, toTagValue(TrackTotal));
            result.IntegrateValue(Field.DISC_NUMBER, toTagValue(DiscNumber));
            result.IntegrateValue(Field.DISC_TOTAL, toTagValue(DiscTotal));
            result.IntegrateValue(Field.CHAPTERS_TOC_DESCRIPTION, ChaptersTableDescription);

            result.Chapters = new List<ChapterInfo>();
            foreach (ChapterInfo chapter in Chapters)
            {
                result.Chapters.Add(new ChapterInfo(chapter));
            }

            result.Lyrics = new List<LyricsInfo>();
            if (Lyrics != null)
            {
                foreach (LyricsInfo lyrics in Lyrics)
                {
                    result.Lyrics.Add(new LyricsInfo(lyrics));
                }
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
                if (AdditionalFields.ContainsKey(s)) continue;

                MetaFieldInfo metaFieldToDelete = new MetaFieldInfo(MetaDataIOFactory.TagType.ANY, s)
                {
                    MarkedForDeletion = true
                };
                result.AdditionalFields.Add(metaFieldToDelete);
            }

            result.Pictures = new List<PictureInfo>();

            if (currentEmbeddedPictures != null)
            {
                // Process target pictures first, in their specified order
                foreach (PictureInfo targetPic in currentEmbeddedPictures)
                {
                    PictureInfo picInfo = initialEmbeddedPictures.FirstOrDefault(pi => targetPic.EqualsProper(pi));

                    result.Pictures.Add(targetPic);
                    if (picInfo == null) continue;

                    // Compare picture contents
                    targetPic.ComputePicHash();
                    // A new picture content has been defined for an existing location
                    if (targetPic.PictureHash == picInfo.PictureHash) continue;

                    PictureInfo picToDelete = new PictureInfo(picInfo)
                    {
                        MarkedForDeletion = true
                    };
                    result.Pictures.Add(picToDelete);
                    // Completely new picture
                }

                // Detect and tag deleted pictures (=those which were in initialEmbeddedPictures and do not appear in embeddedPictures anymore)
                foreach (PictureInfo picInfo in initialEmbeddedPictures)
                {
                    PictureInfo targetPic = currentEmbeddedPictures.FirstOrDefault(pi => picInfo.EqualsProper(pi));

                    if (null != targetPic) continue;

                    PictureInfo picToDelete = new PictureInfo(picInfo)
                    {
                        MarkedForDeletion = true
                    };
                    result.Pictures.Add(picToDelete);
                }
            }

            return result;
        }

        /// <summary>
        /// Save Track to the given file using all existing tag types
        /// Use SaveTo instead of SaveToAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        ///
        /// After completion, any further update on this object will be made on the _target_ file.
        ///
        /// Please note that saving to the file you're already using will result in failure.
        /// </summary>
        /// <param name="target">Absolute path of the file to save the Track to</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool SaveTo(string target, Action<float> writeProgress = null)
        {
            if (null == target || target == Path) return false;

            // Copy the contents of the file
            if (null == stream)
            {
                File.Copy(Path, target, true);
            }
            else
            {
                stream.Position = streamInitialPos;
                using FileStream to = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read);
                StreamUtils.CopyStream(stream, to);
            }
            // Write what needs to be written
            bool result = fileIO.Save(toTagData(), null, target, null, new ProgressToken<float>(writeProgress));
            // Update internal references
            if (!result) return false;
            Path = target;
            stream = null;
            Update();

            return true;
        }

        /// <summary>
        /// Save Track to the given target Stream using all existing tag types
        /// Use SaveTo instead of SaveToAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        ///
        /// After completion, any further update on this object will be made on the _target_ Stream.
        ///
        /// Addional notes : 
        /// - Saving to the Stream you're already using will result in failure
        /// - Saving will be done to offset 0 of the target Stream
        /// </summary>
        /// <param name="target">Stream to save to</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool SaveTo(Stream target, Action<float> writeProgress = null)
        {
            if (null == target || target == stream) return false;

            // Copy the contents of the file
            if (null == stream)
            {
                using FileStream from = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                StreamUtils.CopyStream(from, target);
            }
            else
            {
                stream.Position = streamInitialPos;
                StreamUtils.CopyStream(stream, target);
            }
            target.Seek(0, SeekOrigin.Begin);
            // Write what needs to be written
            bool result = fileIO.Save(toTagData(), null, null, target, new ProgressToken<float>(writeProgress));
            // Update internal references
            if (!result) return false;

            stream = target;
            streamInitialPos = 0;
            Path = AudioDataIOFactory.IN_MEMORY;
            Update();

            return true;
        }

        /// <summary>
        /// Save Track to disk using all existing tag types
        /// Use SaveTo instead of SaveToAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        ///
        /// After completion, any further update on this object will be made on the _target_ file.
        ///
        /// Please note that saving to the file you're already using will result in failure.
        /// </summary>
        /// <param name="target">Absolute path of the file to save the Track to</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public async Task<bool> SaveToAsync(string target, Action<float> writeProgress = null)
        {
            if (null == target || target == Path) return false;

            // Copy the contents of the file
            if (null == stream)
            {
                await StreamUtils.CopyFileAsync(Path, target);
            }
            else
            {
                stream.Position = streamInitialPos;
                await using FileStream to = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read);
                await StreamUtils.CopyStreamAsync(stream, to);
            }
            // Write what needs to be written
            bool result = await fileIO.SaveAsync(toTagData(), null, target, null, new ProgressToken<float>(writeProgress));
            // Update internal references
            if (!result) return false;

            Path = target;
            stream = null;
            Update();

            return true;
        }

        /// <summary>
        /// Save Track to the target Stream using all existing tag types
        /// Use SaveTo instead of SaveToAsync if you're looking for pure performance
        /// or if you don't need any progress feedback (e.g. console app, mass-updating files)
        ///
        /// After completion, any further update on this object will be made on the _target_ Stream.
        ///
        /// Addional notes : 
        /// - Saving to the Stream you're already using will result in failure
        /// - Saving will be done to offset 0 of the target Stream
        /// </summary>
        /// <param name="target">Stream to save to</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public async Task<bool> SaveToAsync(Stream target, Action<float> writeProgress = null)
        {
            if (null == target || target == stream) return false;

            // Copy the contents
            if (null == stream)
            {
                await using FileStream from = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                await StreamUtils.CopyStreamAsync(from, target);
            }
            else
            {
                stream.Position = streamInitialPos;
                await StreamUtils.CopyStreamAsync(stream, target);
            }
            target.Seek(0, SeekOrigin.Begin);
            // Write what needs to be written
            bool result = await fileIO.SaveAsync(toTagData(), null, null, target, new ProgressToken<float>(writeProgress));
            // Update internal references
            if (!result) return false;

            stream = target;
            streamInitialPos = 0;
            Path = AudioDataIOFactory.IN_MEMORY;
            Update();

            return true;
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
            bool result = fileIO.Save(toTagData(), null, null, null, new ProgressToken<float>(writeProgress));
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
            bool result = fileIO.Save(toTagData(), tagType, null, null, new ProgressToken<float>(writeProgress));
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
            bool result = await fileIO.SaveAsync(toTagData(), null, null, null, new ProgressToken<float>(writeProgress));
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
            bool result = await fileIO.SaveAsync(toTagData(), tagType, null, null, new ProgressToken<float>(writeProgress));
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

        /// <summary>
        /// Copy all metadata to the given track
        /// NB : Physical information is not copied
        /// </summary>
        /// <param name="t">Track to copy metadata to</param>
        public void CopyMetadataTo(Track t)
        {
            t.currentEmbeddedPictures ??= new List<PictureInfo>();
            t.currentEmbeddedPictures.Clear();
            if (EmbeddedPictures != null)
                foreach (var pic in EmbeddedPictures) t.currentEmbeddedPictures.Add(new PictureInfo(pic));

            t.Title = Title;
            t.Artist = Artist;
            t.Composer = Composer;
            t.Comment = Comment;
            t.Genre = Genre;
            t.OriginalArtist = OriginalArtist;
            t.OriginalAlbum = OriginalAlbum;
            t.Description = Description;
            t.Copyright = Copyright;
            t.Publisher = Publisher;
            t.AlbumArtist = AlbumArtist;
            t.Conductor = Conductor;
            t.ProductId = ProductId;
            t.SortAlbum = SortAlbum;
            t.SortAlbumArtist = SortAlbumArtist;
            t.SortArtist = SortArtist;
            t.SortTitle = SortTitle;
            t.Group = Group;
            t.SeriesTitle = SeriesTitle;
            t.SeriesPart = SeriesPart;
            t.LongDescription = LongDescription;
            t.Album = Album;
            // Year is not set directly as it actually sets Date and isYearExplicit
            t.Date = Date;
            t.isYearExplicit = isYearExplicit;
            t.PublishingDate = PublishingDate;
            t.TrackNumber = TrackNumber;
            t.TrackTotal = TrackTotal;
            t.DiscNumber = DiscNumber;
            t.DiscTotal = DiscTotal;
            t.Popularity = Popularity;
            t.BPM = BPM;
            t.EncodedBy = EncodedBy;

            t.Chapters ??= new List<ChapterInfo>();
            t.Chapters.Clear();
            if (Chapters != null)
                foreach (var chap in Chapters) t.Chapters.Add(new ChapterInfo(chap));
            t.ChaptersTableDescription = ChaptersTableDescription;

            t.Lyrics ??= new List<LyricsInfo>();
            t.Lyrics.Clear();
            if (Lyrics != null)
                foreach (var l in Lyrics) t.Lyrics.Add(new LyricsInfo(l));
            else t.Lyrics = null;

            t.AdditionalFields ??= new Dictionary<string, string>();
            t.AdditionalFields.Clear();
            if (AdditionalFields == null) return;

            foreach (var af in AdditionalFields) t.AdditionalFields.Add(new KeyValuePair<string, string>(af.Key, af.Value));
        }

        /// FORMATTING UTILITIES

        private static string processString(string value)
        {
            return value.Replace(Settings.InternalValueSeparator, Settings.DisplayValueSeparator);
        }

        private static DateTime? update(DateTime value)
        {
            if (value > DateTime.MinValue || !Settings.NullAbsentValues) return value;
            else return null;
        }

        private static int? update(int value)
        {
            if (value != 0 || !Settings.NullAbsentValues) return value;
            else return null;
        }

        private static bool canUseValue(DateTime? value)
        {
            return value.HasValue && (Settings.NullAbsentValues || !value.Equals(DateTime.MinValue));
        }
        private static bool canUseValue(int? value)
        {
            return value.HasValue && (Settings.NullAbsentValues || value != 0);
        }
        private static bool canUseValue(float? value)
        {
            return value.HasValue && (Settings.NullAbsentValues || Math.Abs((double)value) > 0.001);
        }
        private static bool canUseValue(float value)
        {
            return Settings.NullAbsentValues || !Utils.ApproxEquals(value, 0);
        }

        private static string toTagValue(DateTime? value)
        {
            if (canUseValue(value)) return TrackUtils.FormatISOTimestamp(value.Value);
            return Settings.NullAbsentValues ? "" : "0";
        }

        private static string toTagValue(int? value)
        {
            if (canUseValue(value)) return value.Value.ToString();
            return Settings.NullAbsentValues ? "" : "0";
        }

        private static string toTagValue(float value)
        {
            if (canUseValue(value)) return value.ToString();
            return Settings.NullAbsentValues ? "" : "0";
        }

        private static string toTagValue(float? value)
        {
            if (canUseValue(value)) return value.Value.ToString();
            return Settings.NullAbsentValues ? "" : "0";
        }
    }
}
