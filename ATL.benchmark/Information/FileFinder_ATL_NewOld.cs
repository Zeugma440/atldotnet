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
                            testDifference(w, f.FullName, "Exception", e.Message, "ok");
                            continue;
                        }

                        try
                        {
                            tOld = new Track(f.FullName, true);
                        } catch (Exception e)
                        {
                            testDifference(w, f.FullName, "Exception", "ok", e.Message);
                            continue;
                        }

                        testDifference(w, f.FullName, "Album", t.Album, tOld.Album);
                        testDifference(w, f.FullName, "Artist", t.Artist, tOld.Artist);
                        testDifference(w, f.FullName, "Bitrate", t.Bitrate, tOld.Bitrate);
                        testDifference(w, f.FullName, "CodecFamily", t.CodecFamily, tOld.CodecFamily);
                        testDifference(w, f.FullName, "Comment", t.Comment, tOld.Comment);
                        testDifference(w, f.FullName, "Composer", t.Composer, tOld.Composer);
                        testDifference(w, f.FullName, "DiscNumber", t.DiscNumber, tOld.DiscNumber);
                        testDifference(w, f.FullName, "Duration", t.Duration, tOld.Duration);
                        testDifference(w, f.FullName, "Genre", t.Genre, tOld.Genre);
                        testDifference(w, f.FullName, "IsVBR", t.IsVBR, tOld.IsVBR);
                        if (t.PictureTokens != null && tOld.PictureTokens != null)
                            testDifference(w, f.FullName, "PictureTokens", t.PictureTokens.Count, tOld.PictureTokens.Count);
                        testDifference(w, f.FullName, "Rating", t.Rating, tOld.Rating);
                        //testDifference(f.FullName, "Genre", t.SampleRate, tOld.SampleRate);
                        testDifference(w, f.FullName, "Title", t.Title, tOld.Title);
                        testDifference(w, f.FullName, "TrackNumber", t.TrackNumber, tOld.TrackNumber);
                        testDifference(w, f.FullName, "Year", t.Year, tOld.Year);
                    }
                }
            }
        }

        private void testDifference(StreamWriter w, string fileName, string field, object newValue, object oldValue)
        {
            if (!oldValue.ToString().Equals(newValue.ToString()) && !oldValue.ToString().Equals("0") && !oldValue.ToString().Equals(""))
            {
                bool filtered = false;
                // Filter known bugfix : duration increase on MP3s
                filtered = filtered || (field.Equals("Duration") && fileName.Substring(fileName.LastIndexOf('.') + 1, 3).ToLower().Equals("mp3") && ((int)newValue - (int)oldValue < 11) && ((int)newValue - (int)oldValue > 0));

                if (!filtered) w.WriteLine(fileName + ";" + field + ";" + oldValue + ";" + newValue);
            }
        }

    }
}
