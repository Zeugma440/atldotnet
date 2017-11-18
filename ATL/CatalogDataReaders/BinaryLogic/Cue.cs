using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.CatalogDataReaders.BinaryLogic
{
    public class Cue : ICatalogDataReader
    {
        private string path = "";
        private string title = "";
        private string artist = "";
        private string comments = "";

        IList<Track> tracks = new List<Track>();


        public string Path
        {
            get { return path; }
            set { path = value; }
        }

        public string Artist
        {
            get { return artist; }
        }

        public string Comments
        {
            get { return comments; }
        }

        public string Title
        {
            get { return title; }
        }

        public IList<Track> Tracks
        {
            get { return tracks; }
        }


        // ----------------------- Constructor

        public Cue(string path)
        {
            this.path = path;
            read();
        }


        // ----------------------- Specific methods

        private string stripBeginEndQuotes(string s)
        {
            if (s.Length < 2) return s;
            if ((s[0] != '"') || (s[s.Length - 1] != '"')) return s;

            return s.Substring(1, s.Length - 2);
        }

        private void read()
        {
            Encoding encoding = System.Text.Encoding.UTF8;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 2048, FileOptions.SequentialScan))
            using (TextReader source = new StreamReader(fs, encoding))
            {
                string s = source.ReadLine();
                Track currentTrack = null;
                Track physicalTrack = null;
                string audioFilePath = "";

                while (s != null)
                {
                    s = s.Trim();
                    int firstBlank = s.IndexOf(' ');
                    string firstWord = s.Substring(0, firstBlank);
                    if (null == currentTrack)
                    {
                        if ("REM".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            if (comments.Length > 0) comments += Settings.InternalValueSeparator;
                            comments += s.Substring(firstBlank + 1, s.Length - firstBlank - 1);
                        }
                        else if ("PERFORMER".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            artist = stripBeginEndQuotes(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                        }
                        else if ("TITLE".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            title = stripBeginEndQuotes(s.Substring(firstBlank + 1, s.Length - firstBlank - 1));
                        }
                        else if ("FILE".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            audioFilePath = s.Substring(firstBlank + 1, s.Length - firstBlank - 1);
                            audioFilePath = audioFilePath.Substring(0, audioFilePath.LastIndexOf(' ')); // Get rid of the last word representing the audio format
                            audioFilePath = stripBeginEndQuotes(audioFilePath);

                            // Strip the ending word representing the audio format
                            if (!System.IO.Path.IsPathRooted(audioFilePath))
                            {
                                audioFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), audioFilePath);
                            }
                            physicalTrack = new Track(audioFilePath);
                        }
                        else if ("TRACK".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            string[] trackInfo = s.Split(' ');

                            currentTrack = new Track();
                            currentTrack.TrackNumber = byte.Parse(trackInfo[1]);
                            currentTrack.Genre = physicalTrack.Genre;
                            currentTrack.IsVBR = physicalTrack.IsVBR;
                            currentTrack.Bitrate = physicalTrack.Bitrate;
                            currentTrack.CodecFamily = physicalTrack.CodecFamily;
                            currentTrack.Year = physicalTrack.Year;
                            currentTrack.PictureTokens = physicalTrack.PictureTokens;
                            currentTrack.DiscNumber = physicalTrack.DiscNumber;
                            currentTrack.Artist = "";
                            currentTrack.Title = "";
                            currentTrack.Comment = "";
                        }
                    } else
                    {
                        if ("TRACK".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            if (0 == currentTrack.Artist.Length) currentTrack.Artist = physicalTrack.Artist;
                            if (0 == currentTrack.Title.Length) currentTrack.Title = physicalTrack.Title;
                            if (0 == currentTrack.Comment.Length) currentTrack.Comment = physicalTrack.Comment;
                            if (0 == currentTrack.TrackNumber) currentTrack.TrackNumber = physicalTrack.TrackNumber;
                            currentTrack.Album = title;

                            tracks.Add(currentTrack);

                            string[] trackInfo = s.Split(' ');

                            currentTrack = new Track();
                            currentTrack.TrackNumber = byte.Parse(trackInfo[1]);
                            currentTrack.Genre = physicalTrack.Genre;
                            currentTrack.IsVBR = physicalTrack.IsVBR;
                            currentTrack.Bitrate = physicalTrack.Bitrate;
                            currentTrack.CodecFamily = physicalTrack.CodecFamily;
                            currentTrack.Year = physicalTrack.Year;
                            currentTrack.PictureTokens = physicalTrack.PictureTokens;
                            currentTrack.DiscNumber = physicalTrack.DiscNumber;
                            currentTrack.Artist = "";
                            currentTrack.Title = "";
                            currentTrack.Comment = "";
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
                        else if ("PREGAP".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            // TODO
                        }
                        else if ("INDEX".Equals(firstWord, StringComparison.OrdinalIgnoreCase))
                        {
                            // TODO
                        }

                    }

                    s = source.ReadLine();
                } // while

                if (currentTrack != null)
                {
                    if (0 == currentTrack.Artist.Length) currentTrack.Artist = physicalTrack.Artist;
                    if (0 == currentTrack.Title.Length) currentTrack.Title = physicalTrack.Title;
                    if (0 == currentTrack.Comment.Length) currentTrack.Comment = physicalTrack.Comment;
                    if (0 == currentTrack.TrackNumber) currentTrack.TrackNumber = physicalTrack.TrackNumber;

                    currentTrack.Album = title;

                    tracks.Add(currentTrack);
                }

            } // using
        }

    }
}
