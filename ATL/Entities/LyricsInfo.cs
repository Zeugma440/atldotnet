using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ATL
{
    /// <summary>
    /// Information describing lyrics
    /// </summary>
    public class LyricsInfo
    {
        // LRC A2 beat pattern
        private static readonly Lazy<Regex> rxLRCA2Beat = new(() => new Regex("<\\d+:\\d+\\.\\d+>", RegexOptions.None, TimeSpan.FromMilliseconds(100)));

        /// <summary>
        /// Type (contents) of lyrics data
        /// NB : Directly inspired by ID3v2 format
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
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
        /// Format of lyrics data
        /// </summary>
        public enum LyricsFormat
        {
            /// <summary>
            /// LRC
            /// </summary>
            LRC = 0,
            /// <summary>
            /// LRC A2
            /// </summary>
            LRC_A2 = 1,
            /// <summary>
            /// SRT
            /// </summary>
            SRT = 2,
            /// <summary>
            /// Native synchronized
            /// </summary>
            SYNCHRONIZED = 97,
            /// <summary>
            /// Unsynchronized
            /// </summary>
            UNSYNCHRONIZED = 98,
            /// <summary>
            /// Other / non supported
            /// </summary>
            OTHER = 99
        }

        /// <summary>
        /// Phrase ("line") inside lyrics
        /// </summary>
        public sealed class LyricsPhrase : IComparable<LyricsPhrase>, IEquatable<LyricsPhrase>
        {
            /// <summary>
            /// Start timestamp of the phrase, in milliseconds
            /// </summary>
            public int TimestampStart { get; }
            /// <summary>
            /// End timestamp of the phrase, in milliseconds
            /// </summary>
            public int TimestampEnd { get; set; }
            /// <summary>
            /// Text
            /// </summary>
            public string Text { get; }
            /// <summary>
            /// Beats
            /// </summary>
            public List<LyricsPhrase> Beats { get; }

            /// <summary>
            /// Construct a lyrics phrase from its parts
            /// </summary>
            /// <param name="timestampStart">Start timestamp, in milliseconds</param>
            /// <param name="text">Text</param>
            /// <param name="timestampEnd">End timestamp, in milliseconds</param>
            /// <param name="beats">Timed beats (optional)</param>
            public LyricsPhrase(
                int timestampStart,
                string text,
                int timestampEnd = -1,
                List<LyricsPhrase> beats = null)
            {
                TimestampStart = timestampStart;
                TimestampEnd = timestampEnd;
                Text = text;
                if (beats != null) Beats = beats.Select(b => new LyricsPhrase(b)).ToList();
            }

            /// <summary>
            /// Construct a lyrics phrase from its parts
            /// </summary>
            /// <param name="timestampStart">Start timestamp, in the form of a timecode
            /// Supported formats : hh:mm, hh:mm:ss.ddd, mm:ss, hh:mm:ss and mm:ss.ddd</param>
            /// <param name="text">Text</param>
            /// <param name="timestampEnd">End timestamp, in the form of a timecode</param>
            /// <param name="beats">Timed beats (optional)</param>
            public LyricsPhrase(
                string timestampStart,
                string text,
                string timestampEnd = "",
                List<LyricsPhrase> beats = null)
            {
                TimestampStart = Utils.DecodeTimecodeToMs(timestampStart);
                TimestampEnd = Utils.DecodeTimecodeToMs(timestampEnd);
                Text = text;
                if (beats != null) Beats = beats.Select(b => new LyricsPhrase(b)).ToList();
            }

            /// <summary>
            /// Construct a lyrics phrase by copying data from the given LyricsPhrase object
            /// </summary>
            /// <param name="phrase">Object to copy data from</param>
            public LyricsPhrase(LyricsPhrase phrase)
            {
                TimestampStart = phrase.TimestampStart;
                TimestampEnd = phrase.TimestampEnd;
                Text = phrase.Text;
                if (phrase.Beats != null) Beats = phrase.Beats.Select(b => new LyricsPhrase(b)).ToList();
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
            public bool Equals(LyricsPhrase toCompare) => !ReferenceEquals(toCompare, null)
                && TimestampStart == toCompare.TimestampStart
                && TimestampEnd == toCompare.TimestampEnd
                && Text == toCompare.Text
                && (Beats == toCompare.Beats
                || Beats.Count == toCompare.Beats.Count); // TODO do better than that

            /// <summary>
            /// Gets a hash code for the object
            /// </summary>
            /// <returns>The object's hash code</returns>
            public override int GetHashCode() => TimestampStart ^ Text.GetHashCode();

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
            public static bool operator <(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampStart < b.TimestampStart && a.Text.CompareTo(b.Text) < 0;

            /// <summary>
            /// Compares two LyricsPhrase objects by superior
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is less than b, else false</returns>
            public static bool operator >(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampStart > b.TimestampStart && a.Text.CompareTo(b.Text) > 0;

            /// <summary>
            /// Compares two LyricsPhrase objects by inferior-or-equals
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is greater than b, else false</returns>
            public static bool operator <=(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampStart <= b.TimestampStart && a.Text.CompareTo(b.Text) <= 0;

            /// <summary>
            /// Compares two LyricsPhrase objects by superior-or-equals
            /// </summary>
            /// <param name="a">The first LyricsPhrase object</param>
            /// <param name="b">The second LyricsPhrase object</param>
            /// <returns>True if a is less than b, else false</returns>
            public static bool operator >=(LyricsPhrase a, LyricsPhrase b) => !ReferenceEquals(a, null) && !ReferenceEquals(b, null) && a.TimestampStart >= b.TimestampStart && a.Text.CompareTo(b.Text) >= 0;
        }

        /// <summary>
        /// Type
        /// </summary>
        public LyricsType ContentType { get; set; }
        /// <summary>
        /// Format
        /// </summary>
        public LyricsFormat Format { get; set; }
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
        // TODO maintain ??
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
            Format = LyricsFormat.OTHER;
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
            Format = info.Format;
            SynchronizedLyrics = info.SynchronizedLyrics != null ? info.SynchronizedLyrics.Select(x => new LyricsPhrase(x)).ToList()
                : new List<LyricsPhrase>();
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
        /// Guess and set the format
        /// </summary>
        public void GuessFormat()
        {
            if (Format != LyricsFormat.OTHER) return;
            if (UnsynchronizedLyrics.Length > 0 && SynchronizedLyrics.Count == 0) Format = LyricsFormat.UNSYNCHRONIZED;
            else if (SynchronizedLyrics.Count > 0)
            {
                if (SynchronizedLyrics.Any(sl => sl.Beats != null && sl.Beats.Count > 0)) Format = LyricsFormat.LRC_A2;
                else if (Metadata.Count > 0) Format = LyricsFormat.LRC;
                else
                {
                    int prevEndTimestamp = -1;
                    foreach (LyricsPhrase sl in SynchronizedLyrics)
                    {
                        if (prevEndTimestamp > -1 && prevEndTimestamp < sl.TimestampStart)
                        {
                            Format = LyricsFormat.SRT;
                            break;
                        }
                        prevEndTimestamp = sl.TimestampEnd;
                    }
                }
            }
        }

        /// <summary>
        /// Parse the given data into lyrics (synchronized and, if nothing is recognized, unsynchronized)
        /// </summary>
        /// <param name="data">Data to parse</param>
        public void Parse(string data)
        {
            try
            {
                if (data.Contains("[0") && ParseLRC(data)) return;
                if ((data.Contains("--> 0") || data.Contains("-->0")) && ParseSRT(data)) return;
            }
            catch (Exception e)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, e.Message + "\n" + e.StackTrace);
            }
            UnsynchronizedLyrics = data;
            Format = LyricsFormat.UNSYNCHRONIZED;
        }

        /// <summary>
        /// Parse the given unsynchronized LRC or LRC A2 string into synchronized lyrics
        /// </summary>
        private bool ParseLRC(string data)
        {
            List<string> lines = data.Split('\n').Select(l => l.Trim()).ToList();
            bool hasLrcA2 = false;
            foreach (string line in lines)
            {
                int endIndex = line.IndexOf(']');
                if (endIndex < 0) continue;
                if (endIndex == line.Length - 1)
                {
                    // Metadata
                    int metaIndex = line.IndexOf(':');
                    if (metaIndex < 0) continue;
                    string key = line.Substring(1, metaIndex - 1);
                    string value = line.Substring(metaIndex + 1, endIndex - metaIndex - 1);
                    Metadata[key.Trim()] = value.Trim();
                }
                else
                {
                    // Regular lyrics
                    string start = line.Substring(1, endIndex - 1);
                    string lyrics = line.Substring(endIndex + 1).Trim();
                    LyricsPhrase phrase;

                    // Look for beats (LRC A2)
                    if (lyrics.Contains('<') && lyrics.Contains('>') && rxLRCA2Beat.Value.Match(lyrics).Success)
                    {
                        hasLrcA2 = true;
                        var beats = new List<LyricsPhrase>();
                        string[] parts = lyrics.Split('<').Select(s => s.Trim()).ToArray();
                        foreach (string part in parts)
                        {
                            int endIdx = part.IndexOf('>');
                            if (endIdx < 0) continue;
                            string beatStart = part.Substring(1, endIdx - 1);
                            string beatLyrics = part.Substring(endIdx + 1).Trim();
                            if (0 == beatLyrics.Length)
                            {
                                // Timestamp end of last beat
                                int timestampEnd = Utils.DecodeTimecodeToMs(beatStart);
                                if (timestampEnd > 0)
                                {
                                    // Duplicate with timestamp end
                                    LyricsPhrase lastBeat = beats[^1];
                                    lastBeat = new LyricsPhrase(lastBeat.TimestampStart, lastBeat.Text, timestampEnd);
                                    beats[^1] = lastBeat;
                                }
                            }
                            else
                            {
                                var newBeat = new LyricsPhrase(beatStart, beatLyrics);
                                // Set previous beat's timestamp end
                                if (beats.Count > 0) beats[^1].TimestampEnd = newBeat.TimestampStart;
                                beats.Add(newBeat);
                            }
                        }
                        lyrics = string.Join(' ', beats.Select(b => b.Text));
                        phrase = new LyricsPhrase(start, lyrics, beats: beats);
                    }
                    else
                    {
                        phrase = new LyricsPhrase(start, lyrics);

                    }
                    SynchronizedLyrics.Add(phrase);
                }
            }
            Format = hasLrcA2 ? LyricsFormat.LRC_A2 : LyricsFormat.LRC;
            return true;
        }

        /// <summary>
        /// Parse the given unsynchronized SRT string into synchronized lyrics
        /// </summary>
        private bool ParseSRT(string data)
        {
            List<string> lines = data.Split('\n').Select(l => l.Trim()).ToList();
            bool insideLyric = false;
            string start = "";
            string end = "";
            bool isFirstLine = false;
            StringBuilder text = new StringBuilder();
            foreach (string line in lines)
            {
                if (line.Contains("-->"))
                {
                    insideLyric = true;
                    isFirstLine = true;
                    int arrowIdx = line.IndexOf("-->");
                    start = line.Substring(0, arrowIdx - 1).Trim();
                    end = line.Substring(arrowIdx + 3).Trim();
                }
                else if (0 == line.Length)
                {
                    if (text.Length > 0) SynchronizedLyrics.Add(new LyricsPhrase(start, text.ToString(), end));
                    insideLyric = false;
                    text.Clear();
                }
                else if (insideLyric)
                {
                    if (!isFirstLine) text.Append('\n');
                    else isFirstLine = false;
                    text.Append(line);
                }
            }
            // May happen when the stream ends without an empty line
            if (insideLyric && text.Length > 0) SynchronizedLyrics.Add(new LyricsPhrase(start, text.ToString(), end));
            Format = LyricsFormat.SRT;
            return true;
        }

        /// <summary>
        /// Format synchronized lyrics to a string following Format
        /// </summary>
        public string FormatSynch()
        {
            switch (Format)
            {
                case LyricsFormat.SRT: return FormatSynchToSRT();
                default: return FormatSynchToLRC();
            }
        }

        /// <summary>
        /// Format Metadata and Synchronized lyrics to LRC / LRC A2 block of text
        /// </summary>
        private string FormatSynchToLRC()
        {
            StringBuilder sb = new StringBuilder();

            // Metadata
            foreach (var meta in Metadata)
            {
                sb.Append('[').Append(meta.Key).Append(':').Append(meta.Value).Append("]\n");
            }

            if (SynchronizedLyrics.Count > 0) sb.Append('\n');

            // Lyrics
            foreach (var line in SynchronizedLyrics)
            {
                sb.Append('[').Append(Utils.EncodeTimecode_ms(line.TimestampStart, true)).Append(']');
                if (line.Beats is { Count: > 0 })
                {
                    // LRC A2
                    foreach (var beat in line.Beats)
                    {
                        sb.Append('<').Append(Utils.EncodeTimecode_ms(beat.TimestampStart, true)).Append('>').Append(beat.Text);
                        if (beat.TimestampEnd > -1) sb.Append('<').Append(Utils.EncodeTimecode_ms(beat.TimestampStart, true)).Append('>');
                    }
                }
                else
                {
                    // Plain LRC
                    sb.Append(line.Text);
                }
                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format Metadata and Synchronized lyrics to SRT block of text
        /// </summary>
        private string FormatSynchToSRT()
        {
            StringBuilder sb = new StringBuilder();

            var index = 1;
            bool isFirstLine = true;
            foreach (var line in SynchronizedLyrics)
            {
                if (!isFirstLine) sb.Append('\n');
                else isFirstLine = false;

                // Index
                sb.Append(index++).Append('\n');
                // Timecodes
                sb.Append(Utils.EncodeTimecode_ms(line.TimestampStart, true).Replace('.', ','));
                sb.Append(" --> ");
                sb.Append(Utils.EncodeTimecode_ms(line.TimestampEnd, true).Replace('.', ','));
                sb.Append('\n');
                // Text
                sb.Append(line.Text).Append('\n');
            }

            return sb.ToString();
        }
    }
}
