using ATL.Playlist;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class DPLIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_DPL()
        {
            PLIO_R("playlist.dpl", TestUtils.GetResourceLocationRoot(false), 5);
        }

        [TestMethod]
        public void PLIO_W_DPL()
        {
            string testFileLocation = TestUtils.CreateTempTestFile("test.dpl");
            bool defaultPathSetting = ATL.Settings.PlaylistWriteAbsolutePath;
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing + absolute formatting
                ATL.Settings.PlaylistWriteAbsolutePath = true;
                pls.FilePaths = pathsToWrite;
                pls.Save();

                double totalDuration = 0;
                for (int i = 0; i < pathsToWrite.Count; i++)
                {
                    var t = new Track(pathsToWrite[i]);
                    totalDuration += t.DurationMs;
                }

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                {
                    // Test if the default UTF-8 BOM has been written at the beginning of the file
                    byte[] bom = new byte[3];
                    fs.Read(bom, 0, 3);
                    Assert.IsTrue(bom.SequenceEqual(PlaylistIO.BOM_UTF8));
                    fs.Seek(0, SeekOrigin.Begin);

                    using (StreamReader sr = new StreamReader(fs))
                    {
                        Assert.AreEqual("DAUMPLAYLIST", sr.ReadLine());
                        Assert.AreEqual("topindex=0", sr.ReadLine());
                        Assert.AreEqual("saveplaypos=0", sr.ReadLine());
                        Assert.AreEqual("playtime=" + (long)totalDuration, sr.ReadLine());
                        for (int i = 0; i < pathsToWrite.Count; i++)
                        {
                            var t = new Track(pathsToWrite[i]);
                            Assert.AreEqual(i + 1 + "*file*" + pathsToWrite[i], sr.ReadLine());
                            Assert.AreEqual(i + 1 + "*title*" + t.Title, sr.ReadLine());
                            if (t.DurationMs > 0)
                                Assert.AreEqual(i + 1 + "*duration2*" + (long)t.DurationMs, sr.ReadLine());
                        }
                        Assert.IsTrue(sr.EndOfStream);
                    }
                }
                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;

                testFileLocation = TestUtils.CreateTempTestFile("test.dpl");
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.Tracks = tracksToWrite;
                pls.Save();

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("DAUMPLAYLIST", sr.ReadLine());
                    Assert.AreEqual("topindex=0", sr.ReadLine());
                    Assert.AreEqual("saveplaypos=0", sr.ReadLine());
                    Assert.AreEqual("playtime=334106", sr.ReadLine());
                    int counter = 1;
                    foreach (Track t in tracksToWrite)
                    {
                        Assert.AreEqual(counter + "*file*" + TestUtils.MakePathRelative(testFileLocation, t.Path), sr.ReadLine());
                        Assert.AreEqual(counter + "*title*" + t.Title, sr.ReadLine());
                        if (t.DurationMs > 0)
                            Assert.AreEqual(counter + "*duration2*" + (long)t.DurationMs, sr.ReadLine());
                        counter++;
                    }
                    sr.ReadLine();
                    Assert.IsTrue(sr.EndOfStream);
                }

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(tracksToWrite.Count, tracks.Count);
                for (int i = 0; i < tracksToWrite.Count; i++) Assert.AreEqual(tracksToWrite[i].Path, tracks[i].Path);
                Assert.IsTrue(tracks[0].Duration > 0);
                Assert.IsTrue(tracks[1].Duration > 0);

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.PlaylistWriteAbsolutePath = defaultPathSetting;
            }
        }

        [TestMethod]
        public void PLIO_PLIO_RW_Absolute_Relative_Path_DPL()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("dpl");
            try
            {
                int nbEntries = 1;
                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    // Skip file header
                    sr.ReadLine();
                    sr.ReadLine();
                    sr.ReadLine();
                    sr.ReadLine();
                    for (int i = 0; i < 4; i++)
                    {
                        string path;
                        switch (nbEntries)
                        {
                            case 1:
                                path = remoteFilePath1;
                                break;
                            case 2:
                                path = localFilePath1;
                                break;
                            case 3:
                                path = TestUtils.MakePathRelative(testFileLocation, localFilePath2);
                                break;
                            case 4:
                                path = remoteFilePath2;
                                break;
                            default:
                                path = "";
                                break;
                        }
                        Assert.AreEqual(nbEntries + "*file*" + path, sr.ReadLine());
                        var title = sr.ReadLine();
                        if (2 == nbEntries) Assert.AreEqual(nbEntries + "*title*" + NEW_TITLE, title);
                        sr.ReadLine(); // Duration
                        nbEntries++;
                    }
                    var hop = sr.ReadLine();
                    Assert.IsTrue(sr.EndOfStream);
                }
                Assert.AreEqual(4, nbEntries - 1);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }
    }
}
