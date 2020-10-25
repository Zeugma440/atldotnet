using Commons;
using System.Collections.Generic;

namespace ATL
{
    /// <summary>
    /// Information describing lyrics
    /// </summary>
    public class LyricsInfo
    {
        /// <summary>
        /// Type (contents) of lyrics data
        /// NB : Directly inspired by ID3v2 format
        /// </summary>
        public enum LyricsType
        {
            /// <summary>
            /// Other (i.e. none of the other types of this enum)
            /// </summary>
            OTHER = 0,
            /// <summary>
            /// Lyrical data
            /// </summary>
            LYRICS = 1,
            /// <summary>
            /// Transcription
            /// </summary>
            TRANSCRIPTION = 2,
            /// <summary>
            /// List of the movements in the piece
            /// </summary>
            MOVEMENT_NAME = 3,
            /// <summary>
            /// Events that occur
            /// </summary>
            EVENT = 4,
            /// <summary>
            /// Chord changes that occur in the music
            /// </summary>
            CHORD = 5,
            /// <summary>
            /// Trivia or "pop up" information about the media
            /// </summary>
            TRIVIA = 6,
            /// <summary>
            /// URLs for relevant webpages
            /// </summary>
            WEBPAGE_URL = 7,
            /// <summary>
            /// URLs for relevant images
            /// </summary>
            IMAGE_URL = 8
        }

        /// <summary>
        /// Phrase ("line") inside lyrics
        /// </summary>
        public class LyricsPhrase
        {
            /// <summary>
            /// Timestamp of the phrase, in milliseconds
            /// </summary>
            public int TimestampMs { get; set; }
            /// <summary>
            /// Text
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// Construct a lyrics phrase from its parts
            /// </summary>
            /// <param name="timestampMs">Timestamp, in milliseconds</param>
            /// <param name="text">Text</param>
            public LyricsPhrase(int timestampMs, string text)
            {
                TimestampMs = timestampMs;
                Text = text;
            }

            /// <summary>
            /// Construct a lyrics phrase from its parts
            /// </summary>
            /// <param name="timestamp">Timestamp, in the form of a timecode
            /// Supported formats : hh:mm, hh:mm:ss.ddd, mm:ss, hh:mm:ss and mm:ss.ddd</param>
            /// <param name="text">Text</param>
            public LyricsPhrase(string timestamp, string text)
            {
                TimestampMs = Utils.DecodeTimecodeToMs(timestamp);
                Text = text;
            }
        }

        /// <summary>
        /// Type
        /// </summary>
        public LyricsType ContentType { get; set; }
        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Language code
        /// </summary>
        public string LanguageCode { get; set; } // TODO - handle lyrics in multiple languages
        /// <summary>
        /// Data of unsynhronized (i.e. without associated timestamp) lyrics
        /// </summary>
        public string UnsynchronizedLyrics { get; set; }
        /// <summary>
        /// Data of unsynhronized (i.e. with associated timestamps) lyrics
        /// </summary>
        public IList<LyricsPhrase> SynchronizedLyrics { get; set; }

        // ---------------- CONSTRUCTORS

        /// <summary>
        /// Construct empty lyrics information
        /// </summary>
        public LyricsInfo()
        {
            Description = "";
            LanguageCode = "";
            UnsynchronizedLyrics = "";
            ContentType = LyricsType.LYRICS;
            SynchronizedLyrics = new List<LyricsPhrase>();
        }

        /// <summary>
        /// Construct lyrics information by copying data from the given LyricsInfo object
        /// </summary>
        /// <param name="info">Object to copy data from</param>
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
