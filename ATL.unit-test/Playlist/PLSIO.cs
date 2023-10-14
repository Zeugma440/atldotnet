using ATL.Playlist;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class PLSIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_PLS()
        {
            PLIO_R("playlist.pls", TestUtils.GetResourceLocationRoot(false), 5);
        }

        [TestMethod]
        public void PLIO_W_PLS()
        {
            string testFileLocation = TestUtils.CreateTempTestFile("test.pls");
            bool defaultPathSetting = ATL.Settings.PlaylistWriteAbsolutePath;
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing + absolute formatting
                ATL.Settings.PlaylistWriteAbsolutePath = true;
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
                        Assert.AreEqual("[playlist]", sr.ReadLine());
                        sr.ReadLine();

                        int counter = 1;
                        foreach (string s in pathsToWrite)
                        {
                            Assert.AreEqual("File" + counter + "=" + s, sr.ReadLine());
                            Assert.AreEqual("Title" + counter + "=" + System.IO.Path.GetFileNameWithoutExtension(s), sr.ReadLine());
                            Assert.AreEqual("Length" + counter + "=-1", sr.ReadLine());
                            sr.ReadLine();
                            counter++;
                        }

                        Assert.AreEqual("NumberOfEntries=" + pathsToWrite.Count, sr.ReadLine());
                        Assert.AreEqual("Version=2", sr.ReadLine());
                        Assert.IsTrue(sr.EndOfStream);
                    }
                }
                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;

                testFileLocation = TestUtils.CreateTempTestFile("test.pls");
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.Tracks = tracksToWrite;
                pls.Save();

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("[playlist]", sr.ReadLine());
                    int counter = 1;
                    foreach (Track t in tracksToWrite)
                    {
                        sr.ReadLine();
                        Assert.AreEqual("File" + counter + "=" + TestUtils.MakePathRelative(testFileLocation, t.Path), sr.ReadLine());
                        Assert.AreEqual("Title" + counter + "=" + t.Title, sr.ReadLine());
                        Assert.AreEqual("Length" + counter + "=" + (t.Duration > 0 ? t.Duration : -1), sr.ReadLine());
                        counter++;
                    }
                    sr.ReadLine();
                    Assert.AreEqual("NumberOfEntries=" + tracksToWrite.Count, sr.ReadLine());
                    Assert.AreEqual("Version=2", sr.ReadLine());
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
        public void PLIO_PLIO_RW_Absolute_Relative_Path_PLS()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("pls");
            try
            {
                int nbEntries = 1;
                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    sr.ReadLine();
                    for (int i = 0; i < 3; i++)
                    {
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
                        sr.ReadLine();
                        Assert.AreEqual("File" + nbEntries + "=" + path, sr.ReadLine());
                        var title = sr.ReadLine();
                        if (2 == nbEntries) Assert.AreEqual("Title" + nbEntries + "=" + NEW_TITLE, title);
                        sr.ReadLine(); // Duration
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
