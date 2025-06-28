namespace ATL
{
    /// <summary>
    /// Information describing a chapter
    /// </summary>
    public class ChapterInfo
    {
        /// <summary>
        /// Technical format
        /// </summary>
        public enum FORMAT
        {
            /// <summary>
            /// MP4 Quicktime chapters
            /// </summary>
            QT,
            /// <summary>
            /// MP4 Nero Chapters
            /// </summary>
            Nero,
            /// <summary>
            /// Other native formats (e.g. Vorbis CHAPTER, Matroska Chapters)
            /// </summary>
            Native
        }

        /// <summary>
        /// Information describing an URL
        /// </summary>
        public class UrlInfo
        {
            /// <summary>
            /// Description
            /// </summary>
            public string Description { get; set; }
            /// <summary>
            /// The URL itself
            /// </summary>
            public string Url { get; set; }

            /// <summary>
            /// Construct by copying data from the given UrlInfo
            /// </summary>
            /// <param name="url">Object to copy data from</param>
            public UrlInfo(UrlInfo url)
            {
                Description = url.Description;
                Url = url.Url;
            }

            /// <summary>
            /// Construct the structure from the URL and its description
            /// </summary>
            /// <param name="description">Description</param>
            /// <param name="url">The URL itself</param>
            public UrlInfo(string description, string url)
            {
                Description = description;
                Url = url;
            }

            /// <summary>
            /// Construct the structure from a single URL
            /// </summary>
            /// <param name="url">The URL itself</param>
            public UrlInfo(string url)
            {
                Description = "";
                Url = url;
            }

            /// <summary>
            /// Internal string representation of the structure
            /// </summary>
            /// <returns>Internal string representation of the structure</returns>
            public override string ToString()
            {
                return Description + Settings.InternalValueSeparator + Url;
            }
        }


        /// <summary>
        /// Format the chapter has been persisted with
        /// </summary>
        public FORMAT Format { get; set; }
        /// <summary>
        /// Start time (ms)
        /// NB : Only used when <see cref="UseOffset"/> is false
        /// </summary>
        public uint StartTime { get; set; }
        /// <summary>
        /// End time (ms)
        /// NB : Only used when <see cref="UseOffset"/> is false
        /// </summary>
        public uint EndTime { get; set; }

        /// <summary>
        /// Start offset (bytes)
        /// NB1 : Only used when <see cref="UseOffset"/> is true
        /// NB2 : Only supported by ID3v2
        /// </summary>
        public uint StartOffset { get; set; }
        /// <summary>
        /// End offset (bytes)
        /// NB1 : Only used when <see cref="UseOffset"/> is true
        /// NB2 : Only supported by ID3v2
        /// </summary>
        public uint EndOffset { get; set; }

        /// <summary>
        /// True to use StartOffset / EndOffset instead of StartTime / EndTime
        /// NB : Only supported by ID3v2
        /// Default : false
        /// </summary>
        public bool UseOffset { get; set; }

        /// <summary>
        /// Title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Unique ID, alphanumeric
        /// ID3v2 : Unique ID
        /// Vorbis : Chapter index (0,1,2...) [deprecated]
        /// MKV : ChapterStringUID
        /// </summary>
        public string UniqueID { get; set; }
        /// <summary>
        /// Unique ID, numeric
        /// Vorbis : Chapter index (0,1,2...)
        /// MKV : ChapterStringUID
        /// </summary>
        public uint UniqueNumericID { get; set; }
        /// <summary>
        /// Subtitle
        /// NB : Only supported by ID3v2
        /// </summary>
        public string Subtitle { get; set; }
        /// <summary>
        /// Associated URL
        /// NB : Only supported by ID3v2
        /// </summary>
        public UrlInfo Url { get; set; }
        /// <summary>
        /// Associated picture
        /// NB : Only supported by ID3v2 and MP4/M4A
        /// </summary>
        public PictureInfo Picture { get; set; }


        // ---------------- CONSTRUCTORS

        /// <summary>
        /// Construct an empty structure
        /// </summary>
        public ChapterInfo(uint startTime = 0, string title = "")
        {
            Format = FORMAT.Native;
            StartTime = startTime;
            EndTime = 0;
            StartOffset = 0;
            EndOffset = 0;
            UseOffset = false;
            Title = title;

            UniqueNumericID = 0;
            UniqueID = "";
            Subtitle = "";
            Picture = null;
        }

        /// <summary>
        /// Construct a structure by copying informatio from the given ChapterInfo
        /// </summary>
        /// <param name="chapter">Structure to copy information from</param>
        public ChapterInfo(ChapterInfo chapter)
        {
            Format = chapter.Format; StartTime = chapter.StartTime; EndTime = chapter.EndTime; StartOffset = chapter.StartOffset; EndOffset = chapter.EndOffset; Title = chapter.Title; Subtitle = chapter.Subtitle; Url = chapter.Url; UniqueID = chapter.UniqueID; UniqueNumericID = chapter.UniqueNumericID;

            if (chapter.Url != null) Url = new UrlInfo(chapter.Url);
            if (chapter.Picture != null) Picture = new PictureInfo(chapter.Picture);
        }
    }
}
