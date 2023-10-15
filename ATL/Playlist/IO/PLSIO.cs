using System.IO;
using System.Text;
using System.Collections.Generic;
using Commons;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// PLS playlist manager
    /// </summary>
    public class PLSIO : PlaylistIO
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public PLSIO(string filePath) : base(filePath)
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
            while (s != null)
            {
                var parts = s.Split('=');
                if (parts.Length > 1)
                {
                    var index = Utils.ParseFirstIntegerPart(parts[0]);
                    if (-1 == currentIndex) currentIndex = index;

                    if (index != currentIndex) // Record previous track
                    {
                        if (location != null) addTrack(location, title, locations, tracks);
                        title = "";
                        location = null;
                        currentIndex = index;
                    }

                    if (parts[0].ToLower().StartsWith("file"))
                    {
                        location = decodeLocation(parts[1]);
                    }
                    else if (parts[0].ToLower().StartsWith("title"))
                    {
                        title = parts[1];
                    }
                }

                s = source.ReadLine();
            }
            if (location != null) addTrack(location, title, locations, tracks);
        }

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            Encoding encoding = UTF8_NO_BOM;

            using TextWriter w = new StreamWriter(fs, encoding);
            w.WriteLine("[playlist]");

            int counter = 1;
            foreach (Track t in tracks)
            {
                string label = "";
                if (!string.IsNullOrEmpty(t.Title)) label = t.Title;
                if (0 == label.Length) label = System.IO.Path.GetFileNameWithoutExtension(t.Path);

                w.WriteLine("");

                w.Write("File");
                w.Write(counter);
                w.Write("=");
                w.WriteLine(encodeLocation(t.Path)); // Can be rooted or not

                w.Write("Title");
                w.Write(counter);
                w.Write("=");
                w.WriteLine(label);

                w.Write("Length");
                w.Write(counter);
                w.Write("=");
                if (t.Duration > 0) w.WriteLine(t.Duration); else w.WriteLine(-1);

                counter++;
            }

            w.WriteLine("");

            w.Write("NumberOfEntries=");
            w.WriteLine(tracks.Count);

            w.Write("Version=2");
        }
    }
}
