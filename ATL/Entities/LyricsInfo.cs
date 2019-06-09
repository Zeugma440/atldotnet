using Commons;
using System.Collections.Generic;

namespace ATL
{
    public class LyricsInfo
    {
        public enum LyricsType
        {
            OTHER = 0,
            LYRICS = 1,
            TRANSCRIPTION = 2,
            MOVEMENT_NAME = 3,
            EVENT = 4,
            CHORD = 5,
            TRIVIA = 6,
            WEBPAGE_URL = 7,
            IMAGE_URL = 8
        }

        public class LyricsPhrase
        {
            public int TimestampMs { get; set; }
            public string Text { get; set; }

            public LyricsPhrase(int timestampMs, string text)
            {
                TimestampMs = timestampMs;
                Text = text;
            }

            public LyricsPhrase(string timestamp, string text)
            {
                TimestampMs = Utils.DecodeTimecodeToMs(timestamp);
                Text = text;
            }
        }

        public string Description { get; set; }
        public string LanguageCode { get; set; } // TODO - handle lyrics in multiple languages
        public string UnsynchronizedLyrics { get; set; }
        public LyricsType ContentType { get; set; }
        public IList<LyricsPhrase> SynchronizedLyrics { get; set; }

        // ---------------- CONSTRUCTORS

        public LyricsInfo()
        {
            Description = "";
            LanguageCode = "";
            UnsynchronizedLyrics = "";
            ContentType = LyricsType.LYRICS;
            SynchronizedLyrics = new List<LyricsPhrase>();
        }

        public LyricsInfo(LyricsInfo info)
        {
            Description = info.Description;
            LanguageCode = info.LanguageCode;
            UnsynchronizedLyrics = info.UnsynchronizedLyrics;
            ContentType = info.ContentType;
            SynchronizedLyrics = new List<LyricsPhrase>(info.SynchronizedLyrics);
        }
    }
}
