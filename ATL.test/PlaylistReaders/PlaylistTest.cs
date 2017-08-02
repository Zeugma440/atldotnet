using ATL.PlaylistReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test
{
    [TestClass]
    public class PlaylistTest
    {
        private static string copyFileAndReplace(string location, string placeholder, string replacement)
        {
            string testFileLocation = location.Substring(0, location.LastIndexOf('.')) + "_test" + location.Substring(location.LastIndexOf('.'), location.Length - location.LastIndexOf('.'));

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
        public void PL_TestM3U()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(TestUtils.GetResourceLocationRoot() + "playlist_simple.m3u");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(4, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }

            string testFileLocation = copyFileAndReplace(TestUtils.GetResourceLocationRoot() + "playlist_fullPath.m3u", "$PATH", TestUtils.GetResourceLocationRoot());
            try
            {
                theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
                Assert.AreEqual(3, theReader.GetFiles().Count);
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
        public void PL_TestXSPF()
        {
            string testFileLocation = copyFileAndReplace(TestUtils.GetResourceLocationRoot() + "playlist.xspf", "$PATH", TestUtils.GetResourceLocationRoot());

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
        public void PL_TestSMIL()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(TestUtils.GetResourceLocationRoot() + "playlist.smil");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(2, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }
        }

        [TestMethod]
        public void PL_TestASX()
        {
            string testFileLocation = copyFileAndReplace(TestUtils.GetResourceLocationRoot() + "playlist.asx", "$PATH", TestUtils.GetResourceLocationRoot().Replace("\\", "/"));

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
        public void PL_TestB4S()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(TestUtils.GetResourceLocationRoot() + "playlist.b4s");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(4, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }
        }

        [TestMethod]
        public void PL_TestPLS()
        {
            string testFileLocation = copyFileAndReplace(TestUtils.GetResourceLocationRoot() + "playlist.pls", "$PATH", TestUtils.GetResourceLocationRoot());

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
        public void PL_TestFPL()
        {
            string testFileLocation = copyFileAndReplace(TestUtils.GetResourceLocationRoot() + "playlist.fpl", "$PATH", TestUtils.GetResourceLocationRoot());

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
    }
}
