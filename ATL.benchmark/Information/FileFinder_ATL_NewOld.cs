using System;
using System.IO;
using System.Collections.Generic;
using Commons;

namespace ATL.benchmark
{
    public class FileFinder_ATL_NewOld
    {
        static ICollection<Format> supportedFormats;

        static FileFinder_ATL_NewOld()
        {
            supportedFormats = AudioData.AudioDataIOFactory.GetInstance().getFormats();
        }


        private bool isFormatSupported(string filePath)
        {
            bool result = false;

            foreach (Format f in supportedFormats)
            {
                if (f.IsValidExtension(Path.GetExtension(filePath)))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        public void FF_RecursiveExplore(string dirName, string filter = "*.*")
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirName);
            Track t = null, tOld = null;

            using (FileStream fs = new FileStream(@"E:\temp\diff." + DateTime.Now.ToLongTimeString().Replace(":", ".") + ".csv", FileMode.OpenOrCreate))
            using (StreamWriter w = new StreamWriter(fs))
            {
                w.WriteLine("fileName" + ";" + "field" + ";" + "oldValue" + ";" + "newValue" + ";" + "KO?");

                foreach (FileInfo f in dirInfo.EnumerateFiles(filter, SearchOption.AllDirectories))
                {
                    if (isFormatSupported(f.FullName))
                    {
                        Console.WriteLine(f.FullName);

                        try
                        {
                            t = new Track(f.FullName);
                        } catch (Exception e)
                        {
                            testDifference(w, f.FullName, "Exception", e.Message, "ok", null);
                            continue;
                        }

                        try
                        {
                            tOld = new Track(f.FullName, true);
                        } catch (Exception e)
                        {
                            testDifference(w, f.FullName, "Exception", "ok", e.Message, null);
                            continue;
                        }

                        testDifference(w, f.FullName, "Album", t.Album, tOld.Album, t);
                        testDifference(w, f.FullName, "Artist", t.Artist, tOld.Artist, t);
                        testDifference(w, f.FullName, "Bitrate", t.Bitrate, tOld.Bitrate, t);
                        testDifference(w, f.FullName, "CodecFamily", t.CodecFamily, tOld.CodecFamily, t);
                        testDifference(w, f.FullName, "Comment", t.Comment, tOld.Comment, t);
                        testDifference(w, f.FullName, "Composer", t.Composer, tOld.Composer, t);
                        testDifference(w, f.FullName, "DiscNumber", t.DiscNumber, tOld.DiscNumber, t);
                        testDifference(w, f.FullName, "Duration", t.Duration, tOld.Duration, t);
                        testDifference(w, f.FullName, "Genre", t.Genre, tOld.Genre, t);
                        testDifference(w, f.FullName, "IsVBR", t.IsVBR, tOld.IsVBR, t);
                        if (t.PictureTokens != null && tOld.PictureTokens != null)
                            testDifference(w, f.FullName, "PictureTokens", t.PictureTokens.Count, tOld.PictureTokens.Count, t);
                        testDifference(w, f.FullName, "Rating", t.Rating, tOld.Rating, t);
                        //testDifference(f.FullName, "Genre", t.SampleRate, tOld.SampleRate);
                        testDifference(w, f.FullName, "Title", t.Title, tOld.Title, t);
                        testDifference(w, f.FullName, "TrackNumber", t.TrackNumber, tOld.TrackNumber, t);
                        testDifference(w, f.FullName, "Year", t.Year, tOld.Year, t);
                    }
                }
            }
        }

        private void testDifference(StreamWriter w, string fileName, string field, object newValue, object oldValue, Track t)
        {
            if (!oldValue.ToString().Equals(newValue.ToString()) && !oldValue.ToString().Equals("0") && !oldValue.ToString().Equals(""))
            {
                bool filtered = false;

                // Filter known bugfixes
                string extension = Path.GetExtension(fileName).ToLower();

                // MP3 duration fix
                filtered = filtered || (field.Equals("Duration") && extension.Equals(".mp3") && ((int)newValue - (int)oldValue < 11) && ((int)newValue - (int)oldValue > 0));

                // WMA tracknumber fix
                filtered = filtered || (field.Equals("TrackNumber") && extension.Equals(".wma") && (1 == (int)newValue - (int)oldValue));

                // ID3v2 remapping (OriginalAlbum != Album)
                filtered = filtered || (field.Equals("Album") && (oldValue.ToString().Equals(t.OriginalAlbum)));

                // ID3v2 remapping (OriginalArtist != Artist)
                filtered = filtered || (field.Equals("Artist") && (oldValue.ToString().Equals(t.OriginalArtist)));

                // APEtag remapping (Description != Comment)
                filtered = filtered || (field.Equals("Comment") && (oldValue.ToString().Equals(t.Description)));

                if (!filtered) w.WriteLine(fileName + ";" + field + ";" + oldValue + ";" + newValue);
            }
        }

    }
}
