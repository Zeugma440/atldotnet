using ATL.PlaylistReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test
{
    [TestClass]
    public class PlaylistTest
    {
        private string copyFileAndReplace(string location, string placeholder, string replacement)
        {
            string testFileLocation = location.Substring(0, location.LastIndexOf('.')) + "_test" + location.Substring(location.LastIndexOf('.'), location.Length- location.LastIndexOf('.'));

            using (StreamWriter s = File.CreateText(testFileLocation))
            {
                foreach (string line in File.ReadLines(location))
                {
                    s.WriteLine(line.Replace(placeholder, replacement));
                }
            }

            return testFileLocation;
        }


        [TestMethod]
        public void TestM3U()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader("../../Resources/playlist.m3u");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(4, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }
        }

        [TestMethod]
        public void TestXSPF()
        {
            string testFileLocation = copyFileAndReplace("../../Resources/playlist.xspf", "$PATH", Path.GetFullPath("../../Resources"));

            try
            {
                IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
                Assert.AreEqual(5, theReader.GetFiles().Count);
                foreach (string s in theReader.GetFiles())
                {
                    System.Console.WriteLine(s);
                    Assert.IsTrue(System.IO.File.Exists(s));
                }
            }
            finally
            {
                File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void TestSMIL()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader("../../Resources/playlist.smil");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(2, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }
        }

        [TestMethod]
        public void TestASX()
        {
            string testFileLocation = copyFileAndReplace("../../Resources/playlist.asx", "$PATH", Path.GetFullPath("../../Resources").Replace("\\","/"));

            try
            {

                IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
                Assert.AreEqual(4, theReader.GetFiles().Count);
                foreach (string s in theReader.GetFiles())
                {
                    System.Console.WriteLine(s);
                    Assert.IsTrue(System.IO.File.Exists(s));
                }
            }
            finally
            {
                File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void TestB4S()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader("../../Resources/playlist.b4s");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(4, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }
        }
    }
}
