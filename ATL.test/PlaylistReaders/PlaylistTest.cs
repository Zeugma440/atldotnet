using ATL.Logging;
using ATL.PlaylistReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using static ATL.Logging.Log;

namespace ATL.test
{
    [TestClass]
    public class PlaylistTest
    {
        [TestMethod]
        public void PL_TestCommon()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.AreEqual(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u", theReader.Path);

            ArrayLogger log = new ArrayLogger();
            try
            {
                theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(TestUtils.GetResourceLocationRoot() + "_Playlists/efiufhziuefizeub.m3u");
                theReader.GetFiles();
                Assert.Fail();
            } catch {
                IList<LogItem> logItems = log.GetAllItems(Log.LV_ERROR);
                Assert.AreEqual(1, logItems.Count);
                Assert.IsTrue(logItems[0].Message.Contains("efiufhziuefizeub.m3u")); // Can't do much more than than because the exception message is localized
            }
        }

        [TestMethod]
        public void PL_TestM3U()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(1, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }

            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            string resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", resourceRoot.Substring(2,resourceRoot.Length-2)));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_fullPath.m3u", replacements);
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
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.xspf", "$PATH", TestUtils.GetResourceLocationRoot(false));

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
        public void PL_TestSMIL()
        {
            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            string resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", resourceRoot.Substring(2, resourceRoot.Length - 2)));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.smil", replacements);
            try
            {
                IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(testFileLocation);

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
        public void PL_TestASX()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.asx", "$PATH", TestUtils.GetResourceLocationRoot(false).Replace("\\", "/"));

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
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.b4s", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try {
                IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(testFileLocation);

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
        public void PL_TestPLS()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.pls", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader(testFileLocation);

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
        public void PL_TestFPL()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.fpl", "$PATH", TestUtils.GetResourceLocationRoot(false));

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
