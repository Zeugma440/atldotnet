using ATL.Playlist;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class M3UIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_M3U_simple()
        {
            PLIO_R("playlist_simple.m3u", TestUtils.GetResourceLocationRoot(), 2);
        }

        [TestMethod]
        public void PLIO_R_M3U_extended()
        {
            var replacements = new List<KeyValuePair<string, string>>();
            var resourceRoot = TestUtils.GetResourceLocationRoot(false);

            // No disk path => on Windows this skips drive name, e.g. "C:" (not required on *nix)
            var noDiskPath = Path.DirectorySeparatorChar != '\\'
                ? resourceRoot
                : resourceRoot.Substring(2, resourceRoot.Length - 2);

            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", noDiskPath));

            PLIO_R("playlist_fullPath.m3u", replacements, 4);
        }

        [TestMethod]
        public void PLIO_W_M3U_Simple()
        {
            bool defaultFormatSetting = ATL.Settings.M3U_useExtendedFormat;
            bool defaultPathSetting = ATL.Settings.PlaylistWriteAbsolutePath;

            string testFileLocation = TestUtils.CreateTempTestFile("test.m3u");
            try
            {
                ATL.Settings.M3U_useExtendedFormat = false;
                ATL.Settings.PlaylistWriteAbsolutePath = true;

                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.FilePaths = pathsToWrite;
                pls.Save();

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
                ATL.Settings.M3U_useExtendedFormat = defaultFormatSetting;
                ATL.Settings.PlaylistWriteAbsolutePath = defaultPathSetting;
            }
        }

        [TestMethod]
        public void PLIO_W_M3U_Extended()
        {
            bool defaultSetting = ATL.Settings.M3U_useExtendedFormat;

            string testFileLocation = TestUtils.CreateTempTestFile("test.m3u");
            bool defaultPathSetting = ATL.Settings.PlaylistWriteAbsolutePath;
            try
            {
                ATL.Settings.M3U_useExtendedFormat = true;

                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing + absolute formatting
                ATL.Settings.PlaylistWriteAbsolutePath = true;
                pls.FilePaths = pathsToWrite;
                pls.Save();

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
                ATL.Settings.PlaylistWriteAbsolutePath = false;
                pls.Tracks = tracksToWrite;
                pls.Save();

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
                ATL.Settings.PlaylistWriteAbsolutePath = defaultPathSetting;
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
                ATL.Settings.M3U_useExtendedFormat = defaultSetting;
            }
        }

        [TestMethod]
        public void PLIO_PLIO_RW_Absolute_Relative_Path_M3U()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("m3u");
            try
            {
                int nbEntries = 1;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("#EXTM3U", sr.ReadLine());

                    for (int i = 0; i < 3; i++)
                    {
                        var title = sr.ReadLine();
                        if (2 == nbEntries) Assert.IsTrue(title.EndsWith(NEW_TITLE));

                        string path;
                        switch (nbEntries)
                        {
                            case 1:
                                path = remoteFilePath;
                                break;
                            case 2:
                                path = localFilePath1;
                                break;
                            case 3:
                                path = TestUtils.MakePathRelative(testFileLocation, localFilePath2);
                                break;
                            default:
                                path = "";
                                break;
                        }
                        Assert.AreEqual(path, sr.ReadLine());
                        nbEntries++;
                    }
                }
                Assert.AreEqual(4, nbEntries);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }
    }
}
