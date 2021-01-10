using System;
using System.IO;
using System.Collections.Generic;
using Commons;
using System.Threading;
using System.Threading.Tasks;

namespace ATL.benchmark
{
    public class FileFinder
    {
        static ICollection<Format> supportedFormats;

        static FileFinder()
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

        private string lookForLyrics(Track t, string key)
        {
            string value = t.AdditionalFields[key];
            if (value.Length > 50) return key; else return "";
        }

        public void FF_RecursiveExplore(string dirName, string filter)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirName);

            foreach (FileInfo f in dirInfo.EnumerateFiles(filter, SearchOption.AllDirectories))
            {
                Track t = new Track(f.FullName);
                string found = "";
                if (t.AdditionalFields != null)
                {
                    if (t.AdditionalFields.Keys.Contains("uslt")) found = lookForLyrics(t, "uslt");
                    if (t.AdditionalFields.Keys.Contains("USLT")) found = lookForLyrics(t, "USLT");
                    if (t.AdditionalFields.Keys.Contains("sylt")) found = lookForLyrics(t, "sylt");
                    if (t.AdditionalFields.Keys.Contains("SYLT")) found = lookForLyrics(t, "uslt");

                    if (found.Length > 0) Console.WriteLine("FOUND " + found + " IN " + f.FullName);
                }
            }
        }

        public void FF_RecursiveRead(string dirName, string filter, int nbThreads)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirName);

            int nb = 0;
            foreach (FileInfo f in dirInfo.EnumerateFiles(filter, SearchOption.AllDirectories))
            {
                ReadThread thread = new ReadThread(f.FullName);
                Thread t = new Thread(new ThreadStart(thread.ThreadProc));
                t.Start();
                nb++;
                if (nbThreads == nb) break;
            }
        }

        public void FF_WriteAllInFolder(string dirName, string filter, int nbThreads)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirName);

            ATL.Settings.ID3v2_tagSubVersion = 3;
            ATL.Settings.MP4_createNeroChapters = true;
            ATL.Settings.MP4_createQuicktimeChapters = true;

            int nb = 0;
            foreach (FileInfo f in dirInfo.EnumerateFiles(filter, SearchOption.TopDirectoryOnly))
            {
                WriteThread thread = new WriteThread(f.FullName);
                Thread t = new Thread(new ThreadStart(thread.ThreadProc));
                t.Start();
                nb++;
                if (nbThreads == nb) break;
            }
        }

        public void FF_ReadOneFile()
        {
            //Track t = new Track(@"E:\temp\wma\a.wma");
            //Track t = new Track(TestUtils.GetResourceLocationRoot() + "/OGG/ogg_bigPicture.ogg");
        }

        public void FF_FilterAndDisplayAudioFiles()
        {
            FF_BrowseATLAudioFiles(null);
        }

        public void FF_BrowseATLAudioFiles(string path, bool fetchPicture = false, bool display = true)
        {
            //string folder = TestUtils.GetResourceLocationRoot();
            string folder = (null == path) ? @"E:\temp\wma" : path;
            string[] files = Directory.GetFiles(folder);
            IList<PictureInfo> pictures;

            Track t;

            foreach (string file in files)
            {
                if (isFormatSupported(file))
                {
                    t = new Track(file);
                    if (fetchPicture) pictures = t.EmbeddedPictures;
                    if (display)
                    {
                        Console.WriteLine(t.Path + "......." + Commons.Utils.EncodeTimecode_s(t.Duration) + " | " + t.SampleRate + " (" + t.Bitrate + " kpbs" + (t.IsVBR ? " VBR)" : ")" + " " + t.ChannelsArrangement));
                        Console.WriteLine(Utils.BuildStrictLengthString("", t.Path.Length, '.') + "......." + t.DiscNumber + " | " + t.TrackNumber + " | " + t.Title + " | " + t.Artist + " | " + t.Album + " | " + t.Year + ((t.PictureTokens != null && t.PictureTokens.Count > 0) ? " (" + t.PictureTokens.Count + " picture(s))" : ""));
                    }
                }
            }
        }

        public void FF_BrowseTagLibAudioFiles(string path, bool fetchPicture = false, bool display = true)
        {
            string folder = (null == path) ? @"E:\temp\wma" : path;
            string[] files = Directory.GetFiles(folder);

            TagLib.File tagFile;

            foreach (string file in files)
            {
                if (isFormatSupported(file))
                {
                    tagFile = TagLib.File.Create(file);
                    if (display)
                    {
                        Console.WriteLine(tagFile.Name + "......." + tagFile.Properties.Duration + " | " + tagFile.Properties.AudioSampleRate + " (" + tagFile.Properties.AudioBitrate + " kpbs)");// + (tagFile. ? " VBR)" : ")"));
                        Console.WriteLine(Utils.BuildStrictLengthString("", tagFile.Name.Length, '.') + "......." + tagFile.Tag.Disc + " | " + tagFile.Tag.Track + " | " + tagFile.Tag.Title + " | " + tagFile.Tag.FirstPerformer + " | " + tagFile.Tag.Album + " | " + tagFile.Tag.Year + ((tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0) ? " (" + tagFile.Tag.Pictures.Length + " picture(s))" : ""));
                    }
                }
            }
        }

    }

    public class ReadThread
    {
        private string fileName;

        public ReadThread(string fileName)
        {
            this.fileName = fileName;
        }

        public void ThreadProc()
        {
            Track t = new Track(fileName);
            System.Console.WriteLine(t.Title + "[" + Utils.EncodeTimecode_s(t.Duration) + "]");
        }
    }


    public class WriteThread
    {
        private string fileName;

        public WriteThread(string fileName)
        {
            this.fileName = fileName;
        }

        public void ThreadProc()
        {
            Track t = new Track(fileName);
            System.Console.WriteLine(t.Title + "[" + Utils.EncodeTimecode_s(t.Duration) + "]");

            t.Chapters.Clear();

            for (int i = 0; i < 20; i++)
            {
                ChapterInfo chi = new ATL.ChapterInfo();
                chi.StartTime = (uint)Math.Round(t.DurationMs / 20.0 * i);
                chi.EndTime = (uint)Math.Round(t.DurationMs / 20.0 * i + 1);
                chi.Title = "Chap" + (i + 1);
                t.Chapters.Add(chi);
            }

            t.Save();
            System.Console.WriteLine(t.Title + "[" + Utils.EncodeTimecode_s(t.Duration) + "] -> DONE");
        }
    }
}
