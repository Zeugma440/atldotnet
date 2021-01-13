using ATL.AudioData;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.ChannelsArrangements;

namespace ATL
{
    /// <summary>
    /// High-level class for audio file manipulation
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
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        /// <param name="load">True to load the file when running this constructor (default : true)</param>
        public Track(string path, IProgress<float> writeProgress = null, bool load = true)
        {
            this.Path = path;
            stream = null;
            this.writeProgress = writeProgress;
            if (load) Update();
        }

        /// <summary>
        /// Loads the file at the given path
        /// Only works with local paths; http, ftp and the like do not work.
        /// </summary>
        /// <param name="path">Path of the local file to be loaded</param>
        /// <param name="load">True to load the file when running this constructor (default : true)</param>
        public Track(string path, bool load)
        {
            this.Path = path;
            stream = null;
            this.writeProgress = null;
            if (load) Update();
        }

        /// <summary>
        /// Loads the raw data in the given stream according to the given MIME-type
        /// </summary>
        /// <param name="stream">Stream containing the raw data to be loaded</param>
        /// <param name="mimeType">MIME-type (e.g. "audio/mp3") or file extension (e.g. ".mp3") of the content</param>
        /// <param name="writeProgress">Callback that will be called multiple times when saving changes, as saving progresses (default : null = no callback)</param>
        public Track(Stream stream, string mimeType, IProgress<float> writeProgress = null)
        {
            this.stream = stream;
            this.mimeType = mimeType;
            Path = InMemoryPath;
            this.writeProgress = writeProgress;
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
        /// Publishing date
        /// </summary>
        public DateTime PublishingDate { get; set; }
        /// <summary>
        /// Album Artist
        /// </summary>
        public string AlbumArtist { get; set; }
        /// <summary>
        /// Conductor
        /// </summary>
        public string Conductor { get; set; }
        /// <summary>
		/// Recording Date
		/// </summary>
        public DateTime Date { get; set; }
        /// <summary>
		/// Recording Year
		/// </summary>
        public int Year { get; set; }
        /// <summary>
		/// Track number
		/// </summary>
        public int TrackNumber { get; set; }
        /// <summary>
		/// Total track number
		/// </summary>
        public int TrackTotal { get; set; }
        /// <summary>
		/// Disc number
		/// </summary>
        public int DiscNumber { get; set; }
        /// <summary>
		/// Total disc number
		/// </summary>
        public int DiscTotal { get; set; }
        /// <summary>
		/// Popularity (0% = 0 stars to 100% = 5 stars)
        /// e.g. 3.5 stars = 70%
		/// </summary>
        public float Popularity { get; set; }
        /// <summary>
        /// List of picture IDs stored in the tag
        ///     PictureInfo.PIC_TYPE : internal, normalized picture type
        ///     PictureInfo.NativePicCode : native picture code (useful when exploiting the UNSUPPORTED picture type)
        ///     NB : PictureInfo.PictureData (raw binary picture data) is _not_ valued here; see EmbeddedPictures field
        /// </summary>
        public IList<PictureInfo> PictureTokens { get; set; } = null;
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


        //=== PHYSICAL PROPERTIES

        /// <summary>
        /// Bitrate (kilobytes per second)
        /// </summary>
		public int Bitrate { get; set; }
        /// <summary>
		/// Sample rate (Hz)
		/// </summary>
        public double SampleRate { get; set; }
        /// <summary>
        /// Returns true if the bitrate is variable; false if not
        /// </summary>
        public bool IsVBR { get; set; }
        /// <summary>
        /// Family of the audio codec (See AudioDataIOFactory)
        /// 0=Streamed, lossy data
        /// 1=Streamed, lossless data
        /// 2=Sequenced with embedded sound library
        /// 3=Sequenced with codec or hardware-dependent sound library
        /// </summary>
		public int CodecFamily { get; set; }
        /// <summary>
        /// Format of the audio data
        /// </summary>
        public Format AudioFormat { get; set; }
        /// <summary>
        /// Format of the tagging systems
        /// </summary>
        public IList<Format> MetadataFormats { get; set; }
        /// <summary>
        /// Duration (seconds)
        /// </summary>
        public int Duration
        {
            get
            {
                return (int)Math.Round(DurationMs / 1000.0);
            }
        }
        /// <summary>
		/// Duration (milliseconds)
		/// </summary>
		public double DurationMs { get; set; }
        /// <summary>
		/// Channels arrangement
		/// </summary>
		public ChannelsArrangement ChannelsArrangement { get; set; }

        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        public IDictionary<string, string> AdditionalFields { get; set; }
        private ICollection<string> initialAdditionalFields; // Initial fields, used to identify removed ones

        private IList<PictureInfo> currentEmbeddedPictures { get; set; } = null;
        private ICollection<PictureInfo> initialEmbeddedPictures; // Initial fields, used to identify removed ones


        //=== TECHNICAL
        private readonly IProgress<float> writeProgress;


        /// <summary>
        /// List of pictures stored in the tag
        /// NB1 : PictureInfo.PictureData (raw binary picture data) is valued
        /// NB2 : Also allows to value embedded pictures inside chapters
        /// </summary>
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                return getEmbeddedPictures();
            }
        }

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

        protected void Update(bool onlyReadEmbeddedPictures = false)
        {
            if (string.IsNullOrEmpty(Path)) return;

            // TODO when tag is not available, customize by naming options // tracks (...)
            if (null == stream) fileIO = new AudioFileIO(Path, onlyReadEmbeddedPictures, Settings.ReadAllMetaFrames, writeProgress);
            else fileIO = new AudioFileIO(stream, mimeType, onlyReadEmbeddedPictures, Settings.ReadAllMetaFrames, writeProgress);

            if (onlyReadEmbeddedPictures)
            {
                foreach (PictureInfo picInfo in fileIO.EmbeddedPictures)
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

            MetadataFormats = new List<Format>(fileIO.MetadataFormats);

            Title = fileIO.Title;
            if (Settings.UseFileNameWhenNoTitle && (null == Title || "" == Title) && Path != InMemoryPath)
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(Path);
            }
            Artist = Utils.ProtectValue(fileIO.Artist);
            Composer = Utils.ProtectValue(fileIO.Composer);
            Comment = Utils.ProtectValue(fileIO.Comment);
            Genre = Utils.ProtectValue(fileIO.Genre);
            OriginalArtist = Utils.ProtectValue(fileIO.OriginalArtist);
            OriginalAlbum = Utils.ProtectValue(fileIO.OriginalAlbum);
            Description = Utils.ProtectValue(fileIO.GeneralDescription);
            Copyright = Utils.ProtectValue(fileIO.Copyright);
            Publisher = Utils.ProtectValue(fileIO.Publisher);
            PublishingDate = fileIO.PublishingDate;
            AlbumArtist = Utils.ProtectValue(fileIO.AlbumArtist);
            Conductor = Utils.ProtectValue(fileIO.Conductor);
            Date = fileIO.Date;
            Year = fileIO.IntYear;
            Album = fileIO.Album;
            TrackNumber = fileIO.Track;
            TrackTotal = fileIO.TrackTotal;
            DiscNumber = fileIO.Disc;
            DiscTotal = fileIO.DiscTotal;
            ChaptersTableDescription = Utils.ProtectValue(fileIO.ChaptersTableDescription);

            Bitrate = fileIO.IntBitRate;
            CodecFamily = fileIO.CodecFamily;
            AudioFormat = fileIO.AudioFormat;

            DurationMs = fileIO.Duration;
            Popularity = fileIO.Popularity;
            IsVBR = fileIO.IsVBR;
            SampleRate = fileIO.SampleRate;
            ChannelsArrangement = fileIO.ChannelsArrangement;

            Chapters = fileIO.Chapters;
            Lyrics = fileIO.Lyrics;

            AdditionalFields = fileIO.AdditionalFields;
            initialAdditionalFields = fileIO.AdditionalFields.Keys;

            PictureTokens = new List<PictureInfo>(fileIO.PictureTokens);
        }

        private TagData toTagData()
        {
            TagData result = new TagData();

            result.Title = Title;
            result.Artist = Artist;
            result.Composer = Composer;
            result.Comment = Comment;
            result.Genre = Genre;
            result.OriginalArtist = OriginalArtist;
            result.OriginalAlbum = OriginalAlbum;
            result.GeneralDescription = Description;
            result.Rating = (Popularity * 5).ToString();
            result.Copyright = Copyright;
            result.Publisher = Publisher;
            if (!PublishingDate.Equals(DateTime.MinValue)) result.PublishingDate = TrackUtils.FormatISOTimestamp(PublishingDate);
            result.AlbumArtist = AlbumArtist;
            result.Conductor = Conductor;
            if (!Date.Equals(DateTime.MinValue)) result.RecordingDate = TrackUtils.FormatISOTimestamp(Date);
            result.RecordingYear = Year.ToString();
            result.Album = Album;
            result.TrackNumber = TrackNumber.ToString();
            result.TrackTotal = TrackTotal.ToString();
            result.DiscNumber = DiscNumber.ToString();
            result.DiscTotal = DiscTotal.ToString();
            result.ChaptersTableDescription = ChaptersTableDescription.ToString();

            result.Chapters = new List<ChapterInfo>();
            foreach (ChapterInfo chapter in Chapters)
            {
                result.Chapters.Add(new ChapterInfo(chapter));
            }

            if (Lyrics != null)
            {
                result.Lyrics = new LyricsInfo(Lyrics);
            }

            foreach (string s in AdditionalFields.Keys)
            {
                result.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, s, AdditionalFields[s]));
            }

            // Detect and tag deleted Additional fields (=those which were in initialAdditionalFields and do not appear in AdditionalFields anymore)
            foreach (string s in initialAdditionalFields)
            {
                if (!AdditionalFields.ContainsKey(s))
                {
                    MetaFieldInfo metaFieldToDelete = new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, s, "");
                    metaFieldToDelete.MarkedForDeletion = true;
                    result.AdditionalFields.Add(metaFieldToDelete);
                }
            }

            result.Pictures = new List<PictureInfo>();
            if (currentEmbeddedPictures != null) foreach (PictureInfo targetPic in currentEmbeddedPictures) targetPic.TransientFlag = 0;

            if (initialEmbeddedPictures != null && currentEmbeddedPictures != null)
            {
                foreach (PictureInfo picInfo in initialEmbeddedPictures)
                {
                    // Detect and tag deleted pictures (=those which were in initialEmbeddedPictures and do not appear in embeddedPictures anymore)
                    if (!currentEmbeddedPictures.Contains(picInfo))
                    {
                        PictureInfo picToDelete = new PictureInfo(picInfo);
                        picToDelete.MarkedForDeletion = true;
                        result.Pictures.Add(picToDelete);
                    }
                    else // Only add new additions (pictures identical to initial list will be kept, and do not have to make it to the list, or else a duplicate will be created)
                    {
                        foreach (PictureInfo targetPic in currentEmbeddedPictures)
                        {
                            if (targetPic.Equals(picInfo))
                            {
                                // Compare picture contents
                                targetPic.ComputePicHash();

                                if (targetPic.PictureHash != picInfo.PictureHash)
                                {
                                    // A new picture content has been defined for an existing location
                                    result.Pictures.Add(targetPic);

                                    PictureInfo picToDelete = new PictureInfo(picInfo);
                                    picToDelete.MarkedForDeletion = true;
                                    result.Pictures.Add(picToDelete);
                                }

                                targetPic.TransientFlag = 1;
                            }
                        }
                    }
                }

                if (currentEmbeddedPictures != null)
                {
                    foreach (PictureInfo targetPic in currentEmbeddedPictures)
                    {
                        if (0 == targetPic.TransientFlag) // Entirely new pictures without equivalent in initialEmbeddedPictures
                        {
                            result.Pictures.Add(targetPic);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Save Track to disk
        /// </summary>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool Save()
        {
            bool result = fileIO.Save(toTagData());
            if (result) Update();

            return result;
        }

        /// <summary>
        /// Remove the given tag type from the Track
        /// </summary>
        /// <param name="tagType">Tag type to remove (see MetaDataIOFactory.TAG_XX values)</param>
        /// <see cref="MetaDataIOFactory"/>
        /// <returns>True if removal succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        public bool Remove(int tagType = MetaDataIOFactory.TAG_ANY)
        {
            bool result = fileIO.Remove(tagType);
            if (result) Update();

            return result;
        }
    }
}
