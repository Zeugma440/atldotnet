using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;
using Commons;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// DPL (Daum Playlist / PotPlayer) playlist manager
    /// </summary>
    public class DPLIO : PlaylistIO
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public DPLIO(string filePath) : base(filePath)
        {
        }

        /// <inheritdoc/>
        protected override void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks)
        {
            Encoding encoding = StreamUtils.GetEncodingFromFileBOM(fs);

            int currentIndex = -1;
            string title = "";
            FileLocation location = null;

            using TextReader source = new StreamReader(fs, encoding);
            string s = source.ReadLine();
            while (!string.IsNullOrEmpty(s))
            {
                var parts = s.Split('*');
                if (parts.Length > 2 && Utils.IsNumeric(parts[0]))
                {
                    var index = int.Parse(parts[0]);
                    if (-1 == currentIndex) currentIndex = index;

                    if (index != currentIndex) // Record previous track
                    {
                        if (location != null) addTrack(location, title, locations, tracks);
                        title = "";
                        location = null;
                        currentIndex = index;
                    }

                    switch (parts[1])
                    {
                        case "file":
                            location = decodeLocation(parts[2]);
                            break;
                        case "title":
                            title = parts[2];
                            break;
                    }
                }

                s = source.ReadLine();
            }
            if (location != null) addTrack(location, title, locations, tracks);
        }

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            Encoding encoding = System.Text.Encoding.UTF8;

            long totalDuration = (long)Math.Floor(tracks.Sum(s => s.DurationMs));

            using TextWriter w = new StreamWriter(fs, encoding);
            w.WriteLine("DAUMPLAYLIST");
            w.WriteLine("topindex=0");
            w.WriteLine("saveplaypos=0");
            w.WriteLine("playtime=" + totalDuration);
            // playname not supported

            int counter = 1;
            foreach (Track t in tracks)
            {
                w.Write(counter);
                w.Write("*file*");
                w.WriteLine(encodeLocation(t.Path)); // Can be rooted or not

                if (!string.IsNullOrEmpty(t.Title))
                {
                    w.Write(counter);
                    w.Write("*title*");
                    w.WriteLine(t.Title);
                }

                if (t.DurationMs > 0)
                {
                    w.Write(counter);
                    w.Write("*duration2*");
                    w.WriteLine((long)t.DurationMs);
                }

                // *start* and *played* are not supported

                counter++;
            }
        }
    }
}
