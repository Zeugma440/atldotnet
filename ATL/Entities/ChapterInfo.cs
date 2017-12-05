namespace ATL
{
    public class ChapterInfo
    {
        public uint StartTime = 0;          // Start time (ms)
        public uint EndTime = 0;            // End time (ms)
        public uint StartOffset = 0;        // Start offset (bytes)
        public uint EndOffset = 0;          // End offset (bytes)
        public bool UseOffset = false;      // True if StartOffset / EndOffset are usable, false if not

        public string UniqueID = ""; // Specific to ID3v2

        public string Title = "";
        public string Subtitle = "";
        public string Url = "";
        public PictureInfo Picture = null;


        // ---------------- CONSTRUCTORS

        public ChapterInfo() { }

        public ChapterInfo(ChapterInfo chapter)
        {
            StartTime = chapter.StartTime; EndTime = chapter.EndTime; StartOffset = chapter.StartOffset; EndOffset = chapter.EndOffset; Title = chapter.Title; Subtitle = chapter.Subtitle; Url = chapter.Url; UniqueID = chapter.UniqueID;

            if (chapter.Picture != null) Picture = new PictureInfo(chapter.Picture);
        }
    }
}
