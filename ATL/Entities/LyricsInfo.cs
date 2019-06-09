using System.Collections.Generic;

namespace ATL
{
    public class LyricsInfo
    {
        public string LanguageCode { get; set; }
        public string UnsynchronizedLyrics { get; set; }
        public IList<string> SynchronizedLyrics { get; set; }

        // ---------------- CONSTRUCTORS

        public LyricsInfo()
        {
            LanguageCode = "";
            UnsynchronizedLyrics = "";
            SynchronizedLyrics = new List<string>();
        }

        public LyricsInfo(LyricsInfo info)
        {
            LanguageCode = info.LanguageCode;
            UnsynchronizedLyrics = info.UnsynchronizedLyrics;
            SynchronizedLyrics = new List<string>(info.SynchronizedLyrics);
        }
    }
}
