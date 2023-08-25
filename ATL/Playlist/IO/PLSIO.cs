using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// PLS playlist manager
    /// </summary>
    public class PLSIO : PlaylistIO
    {
        /// <inheritdoc/>
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            Encoding encoding = StreamUtils.GetEncodingFromFileBOM(fs);

            using (TextReader source = new StreamReader(fs, encoding))
            {
                string s = source.ReadLine();
                int equalIndex;
                while (s != null)
                {
                    // If the read line isn't a metadata, it's a file path
                    if (s.Length > 3 && s.Substring(0, 4).Equals("FILE", System.StringComparison.OrdinalIgnoreCase))
                    {
                        equalIndex = s.IndexOf('=') + 1;
                        s = s.Substring(equalIndex, s.Length - equalIndex);
                        result.Add(decodeLocation(s));
                    }
                    s = source.ReadLine();
                }
            }
        }

        /// <inheritdoc/>
        protected override void setTracks(FileStream fs, IList<Track> result)
        {
            Encoding encoding = UTF8_NO_BOM;

            using (TextWriter w = new StreamWriter(fs, encoding))
            {
                w.WriteLine("[playlist]");

                int counter = 1;
                foreach (Track t in result)
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
                w.WriteLine(result.Count);

                w.Write("Version=2");
            }
        }
    }
}
