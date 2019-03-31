using Commons;

namespace ATL
{
    public class ChapterInfo
    {
        public class UrlInfo
        {
            public string Description;
            public string Url;

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

        public uint StartTime = 0;          // Start time (ms)
        public uint EndTime = 0;            // End time (ms)
        public uint StartOffset = 0;        // Start offset (bytes)
        public uint EndOffset = 0;          // End offset (bytes)
        public bool UseOffset = false;      // True if StartOffset / EndOffset are usable, false if not

        public string Title = "";

        public string UniqueID = ""; // Specific to ID3v2
        public string Subtitle = ""; // Specific to ID3v2
        public UrlInfo Url; // Specific to ID3v2
        public PictureInfo Picture = null; // Specific to ID3v2


        // ---------------- CONSTRUCTORS

        public ChapterInfo() { }

        public ChapterInfo(ChapterInfo chapter)
        {
            StartTime = chapter.StartTime; EndTime = chapter.EndTime; StartOffset = chapter.StartOffset; EndOffset = chapter.EndOffset; Title = chapter.Title; Subtitle = chapter.Subtitle; Url = chapter.Url; UniqueID = chapter.UniqueID;

            if (chapter.Url != null) Url = new UrlInfo(chapter.Url);
            if (chapter.Picture != null) Picture = new PictureInfo(chapter.Picture);
        }
    }
}
