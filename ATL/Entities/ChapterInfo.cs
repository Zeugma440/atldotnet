namespace ATL
{
    public class ChapterInfo
    {
        public class UrlInfo
        {
            public string Description { get; set; }
            public string Url { get; set; }

            public UrlInfo(UrlInfo url)
            {
                Description = url.Description;
                Url = url.Url;
            }

            public UrlInfo(string description, string url)
            {
                Description = description;
                Url = url;
            }

            public UrlInfo(string url)
            {
                Description = "";
                Url = url;
            }

            public override string ToString()
            {
                return Description + Settings.InternalValueSeparator + Url;
            }
        }

        public uint StartTime { get; set; }     // Start time (ms)
        public uint EndTime { get; set; }       // End time (ms)
        public uint StartOffset { get; set; }   // Start offset (bytes)
        public uint EndOffset { get; set; }     // End offset (bytes)
        public bool UseOffset { get; set; }     // True if StartOffset / EndOffset are usable, false if not

        public string Title { get; set; }

        public string UniqueID { get; set; }    // Specific to ID3v2
        public string Subtitle { get; set; }    // Specific to ID3v2
        public UrlInfo Url { get; set; }        // Specific to ID3v2
        public PictureInfo Picture { get; set; }// Specific to ID3v2


        // ---------------- CONSTRUCTORS

        public ChapterInfo()
        {
            StartTime = 0;
            EndTime = 0;
            StartOffset = 0;
            EndOffset = 0;
            UseOffset = false;
            Title = "";

            UniqueID = "";
            Subtitle = "";
            Picture = null;
        }

        public ChapterInfo(ChapterInfo chapter)
        {
            StartTime = chapter.StartTime; EndTime = chapter.EndTime; StartOffset = chapter.StartOffset; EndOffset = chapter.EndOffset; Title = chapter.Title; Subtitle = chapter.Subtitle; Url = chapter.Url; UniqueID = chapter.UniqueID;

            if (chapter.Url != null) Url = new UrlInfo(chapter.Url);
            if (chapter.Picture != null) Picture = new PictureInfo(chapter.Picture);
        }
    }
}
