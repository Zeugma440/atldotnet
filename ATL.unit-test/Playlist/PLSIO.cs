using ATL.Playlist;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class PLSIO
    {
        [TestMethod]
        public void PLIO_R_PLS()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.pls", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(5, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths)
                {
                    if (!s.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(System.IO.File.Exists(s));
                }
                foreach (Track t in theReader.Tracks)
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
        public void PLIO_W_PLS()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MP3", "empty.mp3")));
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MOD", "mod.mod")));
            tracksToWrite.Add(new Track("http://this-is-a-stre.am:8405/live"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.pls");
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing
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
                        Assert.AreEqual("[playlist]", sr.ReadLine());
                        sr.ReadLine();
                        Assert.AreEqual("File1=aaa.mp3", sr.ReadLine());
                        Assert.AreEqual("Title1=aaa", sr.ReadLine());
                        Assert.AreEqual("Length1=-1", sr.ReadLine());
                        sr.ReadLine();
                        Assert.AreEqual("File2=bbb.mp3", sr.ReadLine());
                        Assert.AreEqual("Title2=bbb", sr.ReadLine());
                        Assert.AreEqual("Length2=-1", sr.ReadLine());
                        sr.ReadLine();
                        Assert.AreEqual("File3=http://this-is-a-stre.am:8405/live", sr.ReadLine());
                        Assert.AreEqual("Title3=live", sr.ReadLine());
                        Assert.AreEqual("Length3=-1", sr.ReadLine());
                        sr.ReadLine();
                        Assert.AreEqual("NumberOfEntries="+pathsToWrite.Count, sr.ReadLine());
                        Assert.AreEqual("Version=2", sr.ReadLine());
                        Assert.IsTrue(sr.EndOfStream);
                    }
                }
                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));


                // Test Track writing
                pls.Tracks = tracksToWrite;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    Assert.AreEqual("[playlist]", sr.ReadLine());
                    int counter = 1;
                    foreach (Track t in tracksToWrite)
                    {
                        sr.ReadLine();
                        Assert.AreEqual("File" + counter + "=" + t.Path, sr.ReadLine());
                        Assert.AreEqual("Title" + counter + "=" + t.Title, sr.ReadLine());
                        Assert.AreEqual("Length" + counter + "=" + ((t.Duration > 0) ? t.Duration : -1), sr.ReadLine());
                        counter++;
                    }
                    sr.ReadLine();
                    Assert.AreEqual("NumberOfEntries="+tracksToWrite.Count, sr.ReadLine());
                    Assert.AreEqual("Version=2", sr.ReadLine());
                    Assert.IsTrue(sr.EndOfStream);
                }

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(tracksToWrite.Count, tracks.Count);
                for (int i = 0; i < tracksToWrite.Count; i++) Assert.AreEqual(tracksToWrite[i].Path, tracks[i].Path);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }
    }
}
