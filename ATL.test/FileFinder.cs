using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATL.test
{
    [TestClass]
    public class FileFinder
    {
        [TestMethod]
        public void FF_RecursiveExplore()
        {
            //String dirName = "E:/Music/Classique";
            //String dirName = "E:/Music/Films & TV";
            //String dirName = "E:/Music/René";
            //String dirName = "E:/Music/Divers";
            //String dirName = "E:/Music/Anime";
            //String dirName = "E:/Music/VGM";
            String dirName = "E:/temp/id3v2";
            String filter = "*.mp3";

            DirectoryInfo dirInfo = new DirectoryInfo(dirName);

            foreach (FileInfo f in dirInfo.EnumerateFiles(filter,SearchOption.AllDirectories))
            {
                Track t = new Track(f.FullName);
                System.Console.WriteLine(f.FullName);
            }
        }

        [TestMethod]
        public void FF_ReadOneFile()
        {
            Track t = new Track("E:/temp/id3v2/01 - Opening_unsynch.mp3");
        }

    }
}
