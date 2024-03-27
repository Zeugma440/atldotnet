using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.CatalogDataReaders.BinaryLogic
{
    /// <summary>
    /// Class for cuesheet files reading (extension .cue)
    /// http://wiki.hydrogenaud.io/index.php?title=Cue_sheet
    /// </summary>
    public class Cue : ICatalogDataReader
    {
        private readonly StringBuilder comments = new StringBuilder();

        /// <inheritdoc/>
        public string Path { get; set; }

        /// <inheritdoc/>
        public string Artist { get; private set; } = "";

        /// <inheritdoc/>
        public string Comments => comments.ToString();

        /// <inheritdoc/>
        public string Title { get; private set; } = "";

        /// <inheritdoc/>
        public IList<Track> Tracks { get; } = new List<Track>();


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path"></param>
        public Cue(string path)
        {
            this.Path = path;
            read();
        }


        // ----------------------- Specific methods

        private static string stripBeginEndQuotes(string s)
        {
            if (s.Length < 2) return s;
            if (s[0] != '"' || s[^1] != '"') return s;

            return s.Substring(1, s.Length - 2);
        }

        private static double decodeTimecodeToMs(string timeCode)
        {
            double result = -1;

            int frames = 0;
            int minutes = 0;
            int seconds = 0;

            if (timeCode.Contains(':'))
            {
                string[] parts = timeCode.Split(':');
                if (parts.Length >= 1) frames = int.Parse(parts[^1]);
                if (parts.Length >= 2) seconds = int.Parse(parts[^2]);
                if (parts.Length >= 3) minutes = int.Parse(parts[^3]);

                result = frames / 75.0; // 75 frames per seconds (CD sectors)
                result += seconds;
                result += minutes * 60;
            }

            return result * 1000;
        }

        private void read()
        {
            using (FileStream fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 2048, FileOptions.SequentialScan))
            {
                // Determine encoding
                Encoding encoding = Utils.GuessTextEncoding(fs);
                fs.Seek(0, SeekOrigin.Begin);

                // Read contents
                using (TextReader source = new StreamReader(fs, encoding))
                {
                    string s = source.ReadLine();
                    Track physicalTrack = null;

                    Track currentTrack = null;
                    Track previousTrack = null;
                    double previousTimeOffset = 0;
                    int indexRelativePosition = 0;

                    while (s != null)
                    {
                        s = s.Trim();
                        int firstBlank = s.IndexOf(' ');
                        string firstWord = s.Substring(0, firstBlank);
                        string[] trackInfo = s.Split(' ');

                        if (null == currentTrack)
                        {
                            if ("REM".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                if (comments.Length > 0) comments.Append(Settings.InternalValueSeparator);
                                comments.Append(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                            }
                            else if ("PERFORMER".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                Artist = stripBeginEndQuotes(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                            }
                            else if ("TITLE".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                Title = stripBeginEndQuotes(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                            }
                            else if ("FILE".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                var audioFilePath = s.Substring(firstBlank + 1, s.Length - firstBlank - 1);
                                audioFilePath = audioFilePath.Substring(0, audioFilePath.LastIndexOf(' ')); // Get rid of the last word representing the audio format
                                audioFilePath = stripBeginEndQuotes(audioFilePath);

                                // Strip the ending word representing the audio format
                                if (!System.IO.Path.IsPathRooted(audioFilePath))
                                {
                                    audioFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), audioFilePath);
                                }
                                physicalTrack = new Track(audioFilePath);
                            }
                            else if ("TRACK".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                currentTrack = new Track();
                                if (trackInfo.Length > 0) currentTrack.TrackNumber = byte.Parse(trackInfo[1]);
                                if (physicalTrack != null)
                                {
                                    currentTrack.Genre = physicalTrack.Genre;
                                    currentTrack.IsVBR = physicalTrack.IsVBR;
                                    currentTrack.Bitrate = physicalTrack.Bitrate;
                                    currentTrack.CodecFamily = physicalTrack.CodecFamily;
                                    currentTrack.Year = physicalTrack.Year;
                                    currentTrack.DiscNumber = physicalTrack.DiscNumber;
                                }
                                currentTrack.Artist = "";
                                currentTrack.Title = "";
                                currentTrack.Comment = "";
                            }
                        }
                        else
                        {
                            if ("TRACK".Equals(firstWord, StringComparison.OrdinalIgnoreCase) && physicalTrack != null)
                            {
                                if (0 == currentTrack.Artist.Length) currentTrack.Artist = Artist;
                                if (0 == currentTrack.Artist.Length) currentTrack.Artist = physicalTrack.Artist;
                                if (0 == currentTrack.Title.Length) currentTrack.Title = physicalTrack.Title;
                                if (0 == currentTrack.Comment.Length) currentTrack.Comment = physicalTrack.Comment;
                                if (0 == currentTrack.TrackNumber) currentTrack.TrackNumber = physicalTrack.TrackNumber;
                                currentTrack.Album = Title;

                                Tracks.Add(currentTrack);

                                previousTrack = currentTrack;
                                currentTrack = new Track();
                                if (trackInfo.Length > 0) currentTrack.TrackNumber = byte.Parse(trackInfo[1]);
                                currentTrack.Genre = physicalTrack.Genre;
                                currentTrack.IsVBR = physicalTrack.IsVBR;
                                currentTrack.Bitrate = physicalTrack.Bitrate;
                                currentTrack.CodecFamily = physicalTrack.CodecFamily;
                                currentTrack.Year = physicalTrack.Year;
                                currentTrack.DiscNumber = physicalTrack.DiscNumber;
                                currentTrack.Artist = "";
                                currentTrack.Title = "";
                                currentTrack.Comment = "";

                                indexRelativePosition = 0;
                            }
                            else if ("REM".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                if (currentTrack.Comment.Length > 0) currentTrack.Comment += Settings.InternalValueSeparator;
                                currentTrack.Comment += s.Substring(firstBlank + 1, s.Length - firstBlank - 1);
                            }
                            else if ("PERFORMER".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                currentTrack.Artist = stripBeginEndQuotes(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                            }
                            else if ("TITLE".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                currentTrack.Title = stripBeginEndQuotes(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                            }
                            else if ("PREGAP".Equals(firstWord, StringComparison.OrdinalIgnoreCase) || ("POSTGAP".Equals(firstWord, StringComparison.OrdinalIgnoreCase)))
                            {
                                if (trackInfo.Length > 0) currentTrack.DurationMs += decodeTimecodeToMs(trackInfo[1]);
                            }
                            else if ("INDEX".Equals(firstWord, StringComparison.OrdinalIgnoreCase) && trackInfo.Length > 1)
                            {
                                double timeOffset = decodeTimecodeToMs(trackInfo[2]);

                                if (0 == indexRelativePosition && previousTrack != null)
                                {
                                    previousTrack.DurationMs += timeOffset - previousTimeOffset;
                                }
                                else
                                {
                                    currentTrack.DurationMs += timeOffset - previousTimeOffset;
                                }
                                previousTimeOffset = timeOffset;

                                indexRelativePosition++;
                            }

                        }

                        s = source.ReadLine();
                    } // while

                    if (currentTrack != null)
                    {
                        currentTrack.Album = Title;
                        if (0 == currentTrack.Artist.Length) currentTrack.Artist = Artist;
                        if (physicalTrack != null)
                        {
                            if (0 == currentTrack.Artist.Length) currentTrack.Artist = physicalTrack.Artist;
                            if (0 == currentTrack.Title.Length) currentTrack.Title = physicalTrack.Title;
                            if (0 == currentTrack.Comment.Length) currentTrack.Comment = physicalTrack.Comment;
                            if (0 == currentTrack.TrackNumber) currentTrack.TrackNumber = physicalTrack.TrackNumber;
                            currentTrack.DurationMs += physicalTrack.DurationMs - previousTimeOffset;
                        }

                        Tracks.Add(currentTrack);
                    }
                }
            } // using

        } // read method

    }
}
