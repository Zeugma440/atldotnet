using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

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

        [TestMethod, TestCategory("mass")]
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

        [TestMethod, TestCategory("mass")]
        public void FF_ReadOneFile()
        {
            Track t = new Track("E:/temp/id3v2/XXX");
        }

        [TestMethod, TestCategory("mass")]
        public void USR_FilterAndDisplayAudioFiles()
        {
            string folder = TestUtils.GetResourceLocationRoot();
            string[] files = Directory.GetFiles(folder);

            Track t;
            
            foreach (string file in files)
            {
                if (isFormatSupported(file))
                {
                    t = new Track(file);
                    Console.WriteLine(t.Path + "......." + Commons.Utils.FormatTime(t.Duration) + " | " + t.SampleRate + " (" + t.Bitrate + " kpbs)");
                }
            }
        }

    }
}
