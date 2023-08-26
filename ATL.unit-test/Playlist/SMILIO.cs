using ATL.Playlist;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class SMILIO
    {
        [TestMethod]
        public void PLIO_R_SMIL()
        {
            var replacements = new List<KeyValuePair<string, string>>();
            var resourceRoot = TestUtils.GetResourceLocationRoot(false);

            // No disk path => on Windows this skips drive name, e.g. "C:" (not required on *nix)
            var noDiskPath = Path.DirectorySeparatorChar != '\\'
                ? resourceRoot
                : resourceRoot.Substring(2, resourceRoot.Length - 2);

            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", noDiskPath));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.smil", replacements);
            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(4, theReader.FilePaths.Count);
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
        public void PLIO_W_SMIL()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MP3", "empty.mp3")));
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MOD", "mod.mod")));
            tracksToWrite.Add(new Track("http://this-is-a-stre.am:8405/live"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.smil");
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing
                pls.FilePaths = pathsToWrite;
                IList<string> parents = new List<string>();
                int index = -1;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                {
                    // Test if the default UTF-8 BOM has been written at the beginning of the file
                    byte[] bom = new byte[3];
                    fs.Read(bom, 0, 3);
                    Assert.IsTrue(bom.SequenceEqual(PlaylistIO.BOM_UTF8));
                    fs.Seek(0, SeekOrigin.Begin);

                    using (XmlReader source = XmlReader.Create(fs))
                    {
                        while (source.Read())
                        {
                            if (source.NodeType == XmlNodeType.Element)
                            {
                                if (source.Name.Equals("smil", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                                else if (source.Name.Equals("body", StringComparison.OrdinalIgnoreCase) && parents.Contains("smil")) parents.Add(source.Name);
                                else if (source.Name.Equals("seq", StringComparison.OrdinalIgnoreCase) && parents.Contains("body")) parents.Add(source.Name);
                                else if (source.Name.Equals("media", StringComparison.OrdinalIgnoreCase) && parents.Contains("seq"))
                                {
                                    index++;
                                    Assert.AreEqual(pathsToWrite[index], source.GetAttribute("src"));
                                }
                            }
                        }
                    }
                }
                Assert.AreEqual(pathsToWrite.Count, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));


                // Test Track writing
                pls.Tracks = tracksToWrite;
                parents.Clear();
                index = -1;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (XmlReader source = XmlReader.Create(fs))
                {
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.Element)
                        {
                            if (source.Name.Equals("smil", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                            else if (source.Name.Equals("body", StringComparison.OrdinalIgnoreCase) && parents.Contains("smil")) parents.Add(source.Name);
                            else if (source.Name.Equals("seq", StringComparison.OrdinalIgnoreCase) && parents.Contains("body")) parents.Add(source.Name);
                            else if (source.Name.Equals("media", StringComparison.OrdinalIgnoreCase) && parents.Contains("seq"))
                            {
                                index++;
                                var expected = tracksToWrite[index].Path;
                                if (!expected.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // fix file:////, which is happening on *nix systems
                                    expected = ("file:///" + expected.Replace('\\', '/')).Replace("file:////", "file:///");
                                }
                                var actual = source.GetAttribute("src");
                                Assert.AreEqual(expected, actual);
                            }
                        }
                    }
                }
                Assert.AreEqual(tracksToWrite.Count, parents.Count);

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
