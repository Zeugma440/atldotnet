using ATL.AudioData;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL
{
	/// <summary>
	/// High-level class for audio file manipulation
	/// </summary>
	public class Track
	{
        /// <summary>
        /// Basic constructor; does nothing else than instanciating the Track object
        /// </summary>
		public Track() {}

        /// <summary>
        /// Loads the file at the given path
        /// Only works with local paths; http, ftp and the like do not work.
        /// </summary>
        /// <param name="Path">Path of the local file to be loaded</param>
        public Track(string Path)
        {
            this.Path = Path;
            stream = null;
            Update();
        }

        /// <summary>
        /// Loads the raw data in the given stream according to the given MIME-type
        /// </summary>
        /// <param name="stream">Stream containing the raw data to be loaded</param>
        /// <param name="mimeType">MIME-type (e.g. "audio/mp3" or file extension (e.g. ".mp3") of the content</param>
        public Track(Stream stream, String mimeType)
        {
            this.stream = stream;
            this.mimeType = mimeType;
            Path = "In-memory";
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
		public string Title;
        /// <summary>
		/// Artist
		/// </summary>
		public string Artist;
        /// <summary>
        /// Composer
        /// </summary>
        public string Composer;
        /// <summary>
		/// Comments
		/// </summary>
		public string Comment;
        /// <summary>
		/// Genre
		/// </summary>
		public string Genre;
        /// <summary>
		/// Title of the album
		/// </summary>
		public string Album;
        /// <summary>
        /// Title of the original album
        /// </summary>
        public string OriginalAlbum;
        /// <summary>
        /// Original artist
        /// </summary>
        public string OriginalArtist;
        /// <summary>
        /// Copyright
        /// </summary>
        public string Copyright;
        /// <summary>
        /// General description
        /// </summary>
        public string Description;
        /// <summary>
        /// Publisher
        /// </summary>
        public string Publisher;
        /// <summary>
        /// Album Artist
        /// </summary>
        public string AlbumArtist;
        /// <summary>
        /// Conductor
        /// </summary>
        public string Conductor;
        /// <summary>
		/// Recording Year
		/// </summary>
        public int Year;
        /// <summary>
		/// Track number
		/// </summary>
        public int TrackNumber;
        /// <summary>
		/// Disc number
		/// </summary>
        public int DiscNumber;
        /// <summary>
		/// Rating (1 to 5)
		/// </summary>
        [Obsolete("Use Popularity")]
        public int Rating;
        /// <summary>
		/// Popularity (0% = 0 stars to 100% = 5 stars)
        /// e.g. 3.5 stars = 70%
		/// </summary>
        public float Popularity;
        /// <summary>
        /// List of picture IDs stored in the tag
        ///     PictureInfo.PIC_TYPE : internal, normalized picture type
        ///     PictureInfo.NativePicCode : native picture code (useful when exploiting the UNSUPPORTED picture type)
        ///     NB : PictureInfo.PictureData (raw binary picture data) is _not_ valued here; see EmbeddedPictures field
        /// </summary>
        public IList<PictureInfo> PictureTokens = null;
        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        public IList<ChapterInfo> Chapters;


        //=== PHYSICAL PROPERTIES

        /// <summary>
        /// Bitrate (kilobytes per second)
        /// </summary>
		public int Bitrate;
        /// <summary>
		/// Sample rate (Hz)
		/// </summary>
        public double SampleRate;
        /// <summary>
        /// Returns true if the bitrate is variable; false if not
        /// </summary>
        public bool IsVBR;
        /// <summary>
        /// Family of the audio codec (See AudioDataIOFactory)
        /// 0=Streamed, lossy data
        /// 1=Streamed, lossless data
        /// 2=Sequenced with embedded sound library
        /// 3=Sequenced with codec or hardware-dependent sound library
        /// </summary>
		public int CodecFamily;
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
		public double DurationMs;

        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        public IDictionary<string, string> AdditionalFields;
        private ICollection<string> initialAdditionalFields; // Initial fields, used to identify removed ones

        private IList<PictureInfo> embeddedPictures = null;
        private ICollection<PictureInfo> initialEmbeddedPictures; // Initial fields, used to identify removed ones

        /// <summary>
        /// List of pictures stored in the tag
        /// NB : PictureInfo.PictureData (raw binary picture data) is valued
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
        private readonly String mimeType;
        private AudioFileIO fileIO;


        // ========== METHODS

        private IList<PictureInfo> getEmbeddedPictures()
        {
            if (null == embeddedPictures)
            {
                embeddedPictures = new List<PictureInfo>();
                initialEmbeddedPictures = new List<PictureInfo>();

                Update(true);
            }

            return embeddedPictures;
        }

        protected void Update(bool readEmbeddedPictures = false)
        {
            if ((null == Path) || (0 == Path.Length)) return;

            // TODO when tag is not available, customize by naming options // tracks (...)
            if (null == stream) fileIO = new AudioFileIO(Path, readEmbeddedPictures, Settings.ReadAllMetaFrames);
            else fileIO = new AudioFileIO(stream, mimeType, readEmbeddedPictures, Settings.ReadAllMetaFrames);

            Title = fileIO.Title;
            if (Settings.UseFileNameWhenNoTitle && (null == Title || "" == Title) )
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
            AlbumArtist = Utils.ProtectValue(fileIO.AlbumArtist);
            Conductor = Utils.ProtectValue(fileIO.Conductor);
            Year = fileIO.IntYear;
            Album = fileIO.Album;
            TrackNumber = fileIO.Track;
            DiscNumber = fileIO.Disc;
            Bitrate = fileIO.IntBitRate;
            CodecFamily = fileIO.CodecFamily;
            DurationMs = fileIO.Duration;
            Rating = fileIO.Rating;
            Popularity = fileIO.Popularity;
            IsVBR = fileIO.IsVBR;
            SampleRate = fileIO.SampleRate;

            Chapters = fileIO.Chapters;

            AdditionalFields = fileIO.AdditionalFields;
            initialAdditionalFields = fileIO.AdditionalFields.Keys;

            PictureTokens = new List<PictureInfo>(fileIO.PictureTokens);

            if (readEmbeddedPictures)
            {
                foreach(PictureInfo picInfo in fileIO.EmbeddedPictures)
                {
                    picInfo.ComputePicHash();
                    embeddedPictures.Add(picInfo);

                    PictureInfo initialPicInfo = new PictureInfo(picInfo, false);
                    initialEmbeddedPictures.Add(initialPicInfo);
                }
            }

            if (!readEmbeddedPictures && embeddedPictures != null)
            {
                embeddedPictures.Clear();
                initialEmbeddedPictures.Clear();
                embeddedPictures = null;
                initialEmbeddedPictures = null;
            }
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
            result.Copyright = Copyright;
            result.Publisher = Publisher;
            result.AlbumArtist = AlbumArtist;
            result.Conductor = Conductor;
            result.RecordingYear = Year.ToString();
            result.Album = Album;
            result.TrackNumber = TrackNumber.ToString();
            result.DiscNumber = DiscNumber.ToString();
            result.Rating = (Popularity * 5).ToString();

            if (Chapters.Count > 0)
            {
                result.Chapters = new List<ChapterInfo>();
                foreach (ChapterInfo chapter in Chapters)
                {
                    result.Chapters.Add(new ChapterInfo(chapter));
                }
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
            if (embeddedPictures != null) foreach (PictureInfo targetPic in embeddedPictures) targetPic.Flag = 0;

            if (initialEmbeddedPictures != null)
            {
                foreach (PictureInfo picInfo in initialEmbeddedPictures)
                {
                    // Detect and tag deleted pictures (=those which were in initialEmbeddedPictures and do not appear in embeddedPictures anymore)
                    if (!embeddedPictures.Contains(picInfo))
                    {
                        PictureInfo picToDelete = new PictureInfo(picInfo);
                        picToDelete.MarkedForDeletion = true;
                        result.Pictures.Add(picToDelete);
                    }
                    else // Only add new additions (pictures identical to initial list will be kept, and do not have to make it to the list, or else a duplicate will be created)
                    {
                        foreach (PictureInfo targetPic in embeddedPictures)
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

                                targetPic.Flag = 1;
                            }
                        }
                    }
                }

                if (embeddedPictures != null)
                {
                    foreach (PictureInfo targetPic in embeddedPictures)
                    {
                        if (0 == targetPic.Flag) // Entirely new pictures without equivalent in initialEmbeddedPictures
                        {
                            result.Pictures.Add(targetPic);
                        }
                    }
                }
            }

            return result;
        }

        public void Save()
        {
            fileIO.Save(toTagData());
            Update();
        }

        public void Remove(int tagType = MetaDataIOFactory.TAG_ANY)
        {
            fileIO.Remove(tagType);
            Update();
        }
    }
}
