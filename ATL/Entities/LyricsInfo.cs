using Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public sealed class LyricsPhrase : IComparable<LyricsPhrase>, IEquatable<LyricsPhrase>
        {
            /// <summary>
            /// Timestamp of the phrase, in milliseconds
            /// </summary>
            public int TimestampMs { get; }
            /// <summary>
            /// Text
            /// </summary>
            public string Text { get; }

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
            
            /// <summary>
            /// Construct a lyrics phrase by copying data from the given LyricsPhrase object
            /// </summary>
            /// <param name="phrase">Object to copy data from</param>
            public LyricsPhrase(LyricsPhrase phrase)
            {
                TimestampMs = phrase.TimestampMs;
                Text = phrase.Text;
            }
            
            /// <summary>
            /// Compares this with other
            /// </summary>
            /// <param name="other">The LyricsPhrase object to compare to</param>
            /// <returns>-1 if this is less than other. 0 if this is equal to other. 1 if this is greater than other</returns>
            /// <exception cref="NullReferenceException">Thrown if other is null</exception>
            public int CompareTo(LyricsPhrase other)
            {
                if (this < other)
                {
                    return -1;
                }
                if (this == other)
                {
                    return 0;
                }
                return 1;
            }

            /// <summary>
            /// Gets whether or not an object is equal to this LyricsPhrase
            /// </summary>
            /// <param name="obj">The object to compare</param>
            /// <returns>True if equals, else false</returns>
            public override bool Equals(object obj)
            {
                if (obj is LyricsPhrase toCompare)
                {
                    return Equals(toCompare);
                }
                return false;
            }

            /// <summary>
            /// Gets whether or not an object is equal to this LyricsPhrase
            /// </summary>
            /// <param name="toCompare">The LyricsPhrase object to compare</param>
            /// <returns>True if equals, else false</returns>
            public bool Equals(LyricsPhrase toCompare) => !ReferenceEquals(toCompare, null) && TimestampMs == toCompare.TimestampMs && Text == toCompare.Text;

            /// <summary>
            /// Gets a hash code for the object
            /// </summary>
            /// <returns>The object's hash code</returns>
            public override int GetHashCode() => TimestampMs ^ Text.GetHashCode();

            /// <summary>
            /// Compares two LyricsPhrase objects by equals
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a == b, else false</returns>
            public static bool operator ==(LyricsPhrase a, LyricsPhrase b)
            {
                if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
                {
                    return true;
                }
                return !ReferenceEquals(a, null) && a.Equals(b);
            }

            /// <summary>
            /// Compares two LyricsPhrase objects by not-equals
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a != b, else false</returns>
            public static bool operator !=(LyricsPhrase a, LyricsPhrase b)
            {
                if ((!ReferenceEquals(a, null) && ReferenceEquals(b, null)) || (ReferenceEquals(a, null) && !ReferenceEquals(b, null)))
                {
                    return true;
                }
                return !ReferenceEquals(a, null) && !a.Equals(b);
            }

            /// <summary>
            /// Compares two LyricsPhrase objects by inferior
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is greater than b, else false</returns>
            public static bool operator <(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampMs < b.TimestampMs && a.Text.CompareTo(b.Text) < 0;

            /// <summary>
            /// Compares two LyricsPhrase objects by superior
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is less than b, else false</returns>
            public static bool operator >(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampMs > b.TimestampMs && a.Text.CompareTo(b.Text) > 0;

            /// <summary>
            /// Compares two LyricsPhrase objects by inferior-or-equals
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is greater than b, else false</returns>
            public static bool operator <=(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampMs <= b.TimestampMs && a.Text.CompareTo(b.Text) <= 0;

            /// <summary>
            /// Compares two LyricsPhrase objects by superior-or-equals
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is less than b, else false</returns>
            public static bool operator >=(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampMs >= b.TimestampMs && a.Text.CompareTo(b.Text) >= 0;
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
        /// Data of unsynchronized (i.e. without associated timestamp) lyrics
        /// </summary>
        public string UnsynchronizedLyrics { get; set; }
        /// <summary>
        /// Data of synchronized (i.e. with associated timestamps) lyrics
        /// </summary>
        public IList<LyricsPhrase> SynchronizedLyrics { get; set; }
        /// <summary>
        /// Metadata of synchronized lyrics (e.g. LRC metadata)
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }
        /// <summary>
        /// Indicate if this object is marked for removal
        /// </summary>
        public bool IsMarkedForRemoval { get; private set; }


        // ---------------- CONSTRUCTORS

        /// <summary>
        /// Create a new object marked for removal
        /// </summary>
        /// <returns>New object marked for removal</returns>
        public static LyricsInfo ForRemoval()
        {
            LyricsInfo result = new LyricsInfo
            {
                IsMarkedForRemoval = true
            };
            return result;
        }

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
            Metadata = new Dictionary<string, string>();
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
            SynchronizedLyrics = info.SynchronizedLyrics.Select(x => new LyricsPhrase(x)).ToList();
            Metadata = info.Metadata.ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Clear data
        /// </summary>
        public void Clear()
        {
            Description = "";
            LanguageCode = "";
            UnsynchronizedLyrics = "";
            IsMarkedForRemoval = false;
            ContentType = LyricsType.LYRICS;
            SynchronizedLyrics.Clear();
            Metadata.Clear();
        }

        /// <summary>
        /// Indicate if the structure contains any data
        /// </summary>
        public bool Exists()
        {
            return Description.Length > 0 || UnsynchronizedLyrics.Length > 0 || SynchronizedLyrics.Count > 0 || Metadata.Count > 0;
        }

        /// <summary>
        /// Parse the given unsynchronized LRC string into synchronized lyrics
        /// </summary>
        public void ParseLRC(string data)
        {
            List<string> lines = data.Split('\n').Select(l => l.Trim()).ToList();
            foreach (string line in lines)
            {
                int endIndex = line.IndexOf(']');
                if (endIndex < 0) continue;
                if (endIndex == line.Length - 1)
                {
                    int metaIndex = line.IndexOf(':');
                    if (metaIndex < 0) continue;
                    string key = line.Substring(1, metaIndex - 1);
                    string value = line.Substring(metaIndex + 1, endIndex - metaIndex - 1);
                    Metadata[key.Trim()] = value.Trim();
                }
                else
                {
                    string timestamp = line.Substring(1, endIndex - 1);
                    string lyrics = line.Substring(endIndex + 1);
                    SynchronizedLyrics.Add(new LyricsPhrase(timestamp, lyrics.Trim()));
                }
            }
        }

        /// <summary>
        /// Format Metadata and Synchronized lyrics to LRC block of text
        /// </summary>
        public string FormatSynchToLRC()
        {
            StringBuilder sb = new StringBuilder();

            // Metadata
            foreach (var meta in Metadata)
            {
                sb.Append('[').Append(meta.Key).Append(':').Append(meta.Value).Append("]\n");
            }

            sb.Append('\n');

            // Lyrics
            foreach (var line in SynchronizedLyrics)
            {
                sb.Append('[').Append(Utils.EncodeTimecode_ms(line.TimestampMs)).Append(']').Append(line.Text).Append('\n');
            }

            return sb.ToString();
        }
    }
}
