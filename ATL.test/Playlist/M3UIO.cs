using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class M3UIO
    {
        [TestMethod]
        public void PLIO_R_M3U()
        {
            IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.IsNotInstanceOfType(pls, typeof(ATL.Playlist.IO.DummyIO));
            Assert.AreEqual(1, pls.FilePaths.Count);
            foreach (string s in pls.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            foreach (Track t in pls.Tracks) Assert.IsTrue(t.Duration > 0); // Ensures the track has been parsed

            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            string resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", resourceRoot.Substring(2, resourceRoot.Length - 2)));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_fullPath.m3u", replacements);
            try
            {
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(pls, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(3, pls.FilePaths.Count);
                foreach (string s in pls.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
                foreach (Track t in pls.Tracks) Assert.IsTrue(t.Duration > 0); // Ensures the track has been parsed
            }
            finally
            {
                File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PLIO_W_M3U_Simple()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");

            bool defaultSetting = Settings.M3U_useExtendedFormat;

            string testFileLocation = TestUtils.CreateTempTestFile("test.m3u");
            try
            {
                Settings.M3U_useExtendedFormat = false;
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.FilePaths = pathsToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("aaa.mp3", sr.ReadLine());
                    Assert.AreEqual("bbb.mp3", sr.ReadLine());
                    Assert.IsTrue(sr.EndOfStream);
                }
            }
            finally
            {
                File.Delete(testFileLocation);
                Settings.M3U_useExtendedFormat = defaultSetting;
            }
        }

        [TestMethod]
        public void PLIO_W_M3U_Extended()
        {
            bool defaultSetting = Settings.M3U_useExtendedFormat;

            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MP3/empty.mp3"));
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MOD/mod.mod"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.m3u");
            try
            {
                Settings.M3U_useExtendedFormat = true;

                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing
                pls.FilePaths = pathsToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("#EXTM3U", sr.ReadLine());
                    Assert.AreEqual("#EXTINF:-1,aaa", sr.ReadLine());
                    Assert.AreEqual("aaa.mp3", sr.ReadLine());
                    Assert.AreEqual("#EXTINF:-1,bbb", sr.ReadLine());
                    Assert.AreEqual("bbb.mp3", sr.ReadLine());
                    Assert.IsTrue(sr.EndOfStream);
                }

                // Test Track writing
                pls.Tracks = tracksToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("#EXTM3U", sr.ReadLine());
                    foreach(Track t in tracksToWrite)
                    {
                        string line = "#EXTINF:" + t.Duration + ",";
                        if (t.Artist != null && t.Artist.Length > 0) line += t.Artist + " - ";
                        line += t.Title;
                        Assert.AreEqual(line, sr.ReadLine());
                        Assert.AreEqual(t.Path, sr.ReadLine());
                    }
                    Assert.IsTrue(sr.EndOfStream);
                }
            }
            finally
            {
                File.Delete(testFileLocation);
                Settings.M3U_useExtendedFormat = defaultSetting;
            }
        }
    }
}
