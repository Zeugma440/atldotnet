using ATL.Logging;
using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using static ATL.Logging.Log;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_Format_Init()
        {
            PlaylistFormat tempFmt = new PlaylistFormat(0, "M3U");
            tempFmt.AddMimeType("blah");
            tempFmt.AddExtension(".m3u");

            PlaylistFormat testFmt = new PlaylistFormat(tempFmt);
            Assert.AreEqual(tempFmt.ID, testFmt.ID);
            Assert.AreEqual(tempFmt.Name, testFmt.Name);
            Assert.AreEqual(tempFmt.LocationFormat, testFmt.LocationFormat);
            Assert.AreEqual(tempFmt.MimeList.Count, testFmt.MimeList.Count);
            Assert.AreEqual(tempFmt.Readable, testFmt.Readable);
        }

        [TestMethod]
        public void PLIO_R_NoFormat()
        {
            IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.xyz");
            Assert.IsInstanceOfType(pls, typeof(ATL.Playlist.IO.DummyIO));
        }

        [TestMethod]
        public void PLIO_R_Common()
        {
            IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.AreEqual(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u", pls.Path);

            ArrayLogger log = new ArrayLogger();
            try
            {
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/efiufhziuefizeub.m3u");
                IList<string> files = pls.FilePaths;
                Assert.Fail();
            }
            catch
            {
                IList<LogItem> logItems = log.GetAllItems(Log.LV_ERROR);
                Assert.AreEqual(1, logItems.Count);
                Assert.IsTrue(logItems[0].Message.Contains("efiufhziuefizeub.m3u")); // Can't do much more than than because the exception message is localized
            }
        }

        [TestMethod]
        public void PL_Common()
        {
            IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.AreEqual(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u", theReader.Path);

            ArrayLogger log = new ArrayLogger();
            try
            {
                theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/efiufhziuefizeub.m3u");
                IList<string> files = theReader.FilePaths;
                Assert.Fail();
            }
            catch
            {
                IList<LogItem> logItems = log.GetAllItems(Log.LV_ERROR);
                Assert.AreEqual(1, logItems.Count);
                Assert.IsTrue(logItems[0].Message.Contains("efiufhziuefizeub.m3u")); // Can't do much more than than because the exception message is localized
            }
        }

        [TestMethod]
        public void PL_M3U()
        {
            IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.AreEqual(1, theReader.FilePaths.Count);
            foreach (string s in theReader.FilePaths)
            {
                Assert.IsTrue(System.IO.File.Exists(s));
            }

            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            string resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", resourceRoot.Substring(2, resourceRoot.Length - 2)));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_fullPath.m3u", replacements);
            try
            {
                theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(3, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PL_XSPF()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.xspf", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PL_SMIL()
        {
            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            string resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", resourceRoot.Substring(2, resourceRoot.Length - 2)));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.smil", replacements);
            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(3, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PL_ASX()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.asx", "$PATH", TestUtils.GetResourceLocationRoot(false).Replace("\\", "/"));

            try
            {

                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PL_B4S()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.b4s", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(3, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PL_PLS()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.pls", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PL_FPL()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.fpl", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }
    }
}
