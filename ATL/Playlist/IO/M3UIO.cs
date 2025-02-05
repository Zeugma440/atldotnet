using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// M3U/M3U8 playlist manager
    /// </summary>
    public class M3UIO : PlaylistIO
    {
        private const string TITLE_SEPARATOR = " - ";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public M3UIO(string filePath) : base(filePath)
        {
        }

        private Encoding getEncoding(FileStream fs)
        {
            if (System.IO.Path.GetExtension(Path).Equals(".m3u8", System.StringComparison.OrdinalIgnoreCase))
            {
                return System.Text.Encoding.UTF8;
            }
            else
            {
                long position = fs.Position;
                Encoding result = StreamUtils.GetEncodingFromFileBOM(fs);
                fs.Position = position;
                return result;
            }
        }

        /// <inheritdoc/>
        protected override void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks)
        {
            Encoding encoding = getEncoding(fs);

            using TextReader source = new StreamReader(fs, encoding);
            string title = "";
            string artist = "";
            string s = source.ReadLine();
            while (s != null)
            {
                if (0 == s.Length) continue;
                // If the read line isn't a metadata, it's a file path
                if (s[0] == '#')
                {
                    if (s.StartsWith("#extinf", StringComparison.InvariantCultureIgnoreCase))
                    {
                        title = "";
                        artist = "";

                        var parts = s.Split(':'); // #EXTINF:duration,artist - Title
                        if (1 == parts.Length) continue;

                        var parts2 = parts[1].Split(','); // duration,artist - Title
                        if (1 == parts2.Length) continue;

                        var artistTitle = parts2[1];
                        if (0 == artistTitle.Length) continue;

                        // Take the first index, in case the title itself contains a " - "
                        var index = artistTitle.IndexOf(TITLE_SEPARATOR, StringComparison.Ordinal);
                        if (index != -1)
                        {
                            artist = artistTitle[..index].Trim();
                            title = artistTitle[(index + TITLE_SEPARATOR.Length)..].Trim();
                        }
                        else title = artistTitle.Trim();
                    }
                }
                else
                {
                    FileLocation location = decodeLocation(s);
                    var track = new Track(location.Path);
                    if (artist.Length > 0) track.Artist = artist;
                    if (title.Length > 0 && title != System.IO.Path.GetFileNameWithoutExtension(location.Path)) track.Title = title;
                    tracks.Add(track);
                    locations.Add(location);
                }
                s = source.ReadLine();
            }
        }

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            Encoding encoding = getEncoding(fs);

            using TextWriter w = new StreamWriter(fs, encoding);
            if (Settings.M3U_useExtendedFormat) w.WriteLine("#EXTM3U");

            foreach (Track t in tracks)
            {
                if (Settings.M3U_useExtendedFormat)
                {
                    w.Write("#EXTINF:");
                    if (t.Duration > 0) w.Write(t.Duration); else w.Write(-1);
                    w.Write(",");
                    string label = "";
                    if (!string.IsNullOrEmpty(t.Artist)) label = t.Artist + TITLE_SEPARATOR;
                    if (!string.IsNullOrEmpty(t.Title)) label += t.Title;
                    if (0 == label.Length) label = System.IO.Path.GetFileNameWithoutExtension(t.Path);
                    w.WriteLine(label);
                    w.WriteLine(encodeLocation(t.Path)); // Can be rooted or not
                }
                else
                {
                    w.WriteLine(encodeLocation(t.Path)); // Can be rooted or not
                }
            }
        }
    }
}
