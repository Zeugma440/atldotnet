using ATL.Playlist;
using System;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class M3UIO
    {
        [TestMethod]
        public void PLIO_R_M3U()
        {
            var pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.IsNotInstanceOfType(pls, typeof(ATL.Playlist.IO.DummyIO));
            Assert.AreEqual(1, pls.FilePaths.Count);
            foreach (var s in pls.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
            foreach (var t in pls.Tracks) Assert.IsTrue(t.Duration > 0); // Ensures the track has been parsed

            var replacements = new List<KeyValuePair<string, string>>();
            var resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));

            // No disk path => on Windows this skips drive name, e.g. "C:" (not required on *nix)
            var noDiskPath = Path.DirectorySeparatorChar != '\\'
                ? resourceRoot
                : resourceRoot.Substring(2, resourceRoot.Length - 2);

            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", noDiskPath));

            var testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_fullPath.m3u", replacements);
            bool foundHttp = false;
            try
            {
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(pls, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(4, pls.FilePaths.Count);
                foreach (string s in pls.FilePaths)
                {
                    if (!s.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(File.Exists(s));
                    else foundHttp = true;
                }
                Assert.IsTrue(foundHttp);
                foreach (Track t in pls.Tracks)
                {
                    // Ensure the track has been parsed when it points to a file
                    if (!t.Path.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(t.Duration > 0);
                }
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PLIO_W_M3U_Simple()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            bool defaultSetting = ATL.Settings.M3U_useExtendedFormat;

            string testFileLocation = TestUtils.CreateTempTestFile("test.m3u");
            try
            {
                ATL.Settings.M3U_useExtendedFormat = false;
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.FilePaths = pathsToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                {
                    // Test if the default UTF-8 BOM has been written at the beginning of the file
                    byte[] bom = new byte[3];
                    fs.Read(bom, 0, 3);
                    Assert.IsTrue(bom.SequenceEqual(PlaylistIO.BOM_UTF8));
                    fs.Seek(0, SeekOrigin.Begin);

                    using (StreamReader sr = new StreamReader(fs))
                    {
                        foreach (string s in pathsToWrite) Assert.AreEqual(s, sr.ReadLine());
                        Assert.IsTrue(sr.EndOfStream);
                    }
                }

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
                ATL.Settings.M3U_useExtendedFormat = defaultSetting;
            }
        }

        [TestMethod]
        public void PLIO_W_M3U_Extended()
        {
            bool defaultSetting = ATL.Settings.M3U_useExtendedFormat;

            IList<string> pathsToWrite = new List<string>();
            string testTrackLocation1 = TestUtils.CopyAsTempTestFile("MP3/empty.mp3");
            string testTrackLocation2 = TestUtils.CopyAsTempTestFile("MOD/mod.mod");
            pathsToWrite.Add(testTrackLocation1);
            pathsToWrite.Add(testTrackLocation2);
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            IList<Track> tracksToWrite = new List<Track>();
            foreach (var s in pathsToWrite) tracksToWrite.Add(new Track(s));


            string testFileLocation = TestUtils.CreateTempTestFile("test.m3u");
            bool defaultPathSetting = ATL.Settings.PlaylistUseAbsolutePath;
            try
            {
                ATL.Settings.M3U_useExtendedFormat = true;

                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing + absolute formatting
                ATL.Settings.PlaylistUseAbsolutePath = true;
                pls.FilePaths = pathsToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("#EXTM3U", sr.ReadLine());
                    foreach (string s in pathsToWrite)
                    {
                        Assert.AreEqual("#EXTINF:-1," + Path.GetFileNameWithoutExtension(s), sr.ReadLine());
                        Assert.AreEqual(s, sr.ReadLine());
                    }
                    Assert.IsTrue(sr.EndOfStream);
                }

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistUseAbsolutePath = false;
                pls.Tracks = tracksToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("#EXTM3U", sr.ReadLine());
                    foreach (Track t in tracksToWrite)
                    {
                        string line = "#EXTINF:" + (t.Duration > 0 ? t.Duration : -1) + ",";
                        if (!string.IsNullOrEmpty(t.Artist)) line += t.Artist + " - ";
                        line += t.Title;
                        Assert.AreEqual(line, sr.ReadLine());
                        Assert.AreEqual(TestUtils.MakePathRelative(testFileLocation, t.Path), sr.ReadLine());
                    }
                    Assert.IsTrue(sr.EndOfStream);
                }

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(tracksToWrite.Count, tracks.Count);
                for (int i = 0; i < tracksToWrite.Count; i++) Assert.AreEqual(tracksToWrite[i].Path, tracks[i].Path);
                Assert.IsTrue(tracks[0].Duration > 0);
                Assert.IsTrue(tracks[1].Duration > 0);
            }
            finally
            {
                ATL.Settings.PlaylistUseAbsolutePath = defaultPathSetting;
                if (Settings.DeleteAfterSuccess)
                {
                    File.Delete(testTrackLocation1);
                    File.Delete(testTrackLocation2);
                    File.Delete(testFileLocation);
                }
                ATL.Settings.M3U_useExtendedFormat = defaultSetting;
            }
        }
    }
}
