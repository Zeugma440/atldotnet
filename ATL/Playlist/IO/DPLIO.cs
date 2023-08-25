using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// DPL (Daum Playlist / PotPlayer) playlist manager
    /// </summary>
    public class DPLIO : PlaylistIO
    {
        /// <inheritdoc/>
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            Encoding encoding = StreamUtils.GetEncodingFromFileBOM(fs);

            using (TextReader source = new StreamReader(fs, encoding))
            {
                string s = source.ReadLine();
                int fileIndex;
                while (s != null)
                {
                    fileIndex = s.IndexOf("*file*");
                    if (fileIndex > -1)
                    {
                        s = s.Substring(fileIndex + 6, s.Length - fileIndex - 6);
                        result.Add(decodeLocation(s));
                    }
                    s = source.ReadLine();
                }
            }
        }

        /// <inheritdoc/>
        protected override void setTracks(FileStream fs, IList<Track> result)
        {
            Encoding encoding = System.Text.Encoding.UTF8;

            long totalDuration = (long)Math.Floor(result.Sum(s => s.DurationMs));

            using (TextWriter w = new StreamWriter(fs, encoding))
            {
                w.WriteLine("DAUMPLAYLIST");
                w.WriteLine("topindex=0");
                w.WriteLine("saveplaypos=0");
                w.WriteLine("playtime=" + totalDuration);
                // playname not supported

                int counter = 1;
                foreach (Track t in result)
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
}
