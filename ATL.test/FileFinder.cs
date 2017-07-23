using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATL.test
{
    [TestClass]
    public class FileFinder
    {
        [TestMethod, TestCategory("mass")]
        public void FF_RecursiveExplore()
        {
            //String dirName = "E:/Music/XXX";
            String dirName = "E:/temp/id3v2";
            String filter = "*.mp3";

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

    }
}
