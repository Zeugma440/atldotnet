using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Commons;

namespace ATL.test
{
    [TestClass]
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

        [TestMethod, TestCategory("manual")]
        public void FF_RecursiveExplore()
        {
            //String dirName = "E:/Music/XXX";
            string dirName = "E:/temp/id3v2";
            string filter = "*.mp3";

            DirectoryInfo dirInfo = new DirectoryInfo(dirName);

            foreach (FileInfo f in dirInfo.EnumerateFiles(filter,SearchOption.AllDirectories))
            {
                Track t = new Track(f.FullName);
                System.Console.WriteLine(f.FullName);
            }
        }

        [TestMethod, TestCategory("manual")]
        public void FF_ReadOneFile()
        {
            //Track t = new Track(@"E:\temp\wma\a.wma");
            //Track t = new Track(TestUtils.GetResourceLocationRoot() + "/OGG/ogg_bigPicture.ogg");
        }

        [TestMethod, TestCategory("manual")]
        public void FF_FilterAndDisplayAudioFiles()
        {
            FF_FilterAndDisplayAudioFiles(null, false);
        }

        public void FF_FilterAndDisplayAudioFiles(string path, bool useOldImplementation = false)
        {
            //string folder = TestUtils.GetResourceLocationRoot();
            string folder = (null == path) ? @"E:\temp\wma" : path;
            string[] files = Directory.GetFiles(folder);

            Track t;

            foreach (string file in files)
            {
                if (isFormatSupported(file))
                {
                    t = new Track(file, useOldImplementation);
                    Console.WriteLine(t.Path + "......." + Commons.Utils.FormatTime(t.Duration) + " | " + t.SampleRate + " (" + t.Bitrate + " kpbs" + (t.IsVBR?" VBR)":")") );
                    Console.WriteLine(Utils.BuildStrictLengthString("",t.Path.Length,'.') + "......." + t.DiscNumber + " | " + t.TrackNumber + " | " + t.Title + " | " + t.Artist + " | " + t.Album + " | " + t.Year + ( (t.PictureTokens != null && t.PictureTokens.Count>0)?" ("+ t.PictureTokens.Count+" picture(s))":""));
                }
            }
        }

    }
}
