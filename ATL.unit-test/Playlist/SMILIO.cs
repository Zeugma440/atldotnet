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

                bool foundHttp = false;
                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths)
                {
                    if (!s.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(System.IO.File.Exists(s));
                    else foundHttp = true;
                }
                Assert.IsTrue(foundHttp);
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
            string testTrackLocation1 = TestUtils.CopyAsTempTestFile("MP3/empty.mp3");
            string testTrackLocation2 = TestUtils.CopyAsTempTestFile("MOD/mod.mod");
            pathsToWrite.Add(testTrackLocation1);
            pathsToWrite.Add(testTrackLocation2);
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            IList<Track> tracksToWrite = new List<Track>();
            foreach (var s in pathsToWrite) tracksToWrite.Add(new Track(s));


            string testFileLocation = TestUtils.CreateTempTestFile("test.smil");
            bool defaultPathSetting = ATL.Settings.PlaylistWriteAbsolutePath;
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing + absolute formatting
                ATL.Settings.PlaylistWriteAbsolutePath = true;
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
                                    string src = source.GetAttribute("src") ?? "";
                                    if (src.StartsWith("http")) Assert.AreEqual(pathsToWrite[index], src);
                                    else Assert.AreEqual("file:///" + pathsToWrite[index].Replace('\\', '/'), src);
                                }
                            }
                        }
                    }
                }
                Assert.AreEqual(pathsToWrite.Count, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;
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
                                var expected = TestUtils.MakePathRelative(testFileLocation, tracksToWrite[index].Path);
                                if (!expected.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // fix file:////, which is happening on *nix systems
                                    expected = expected.Replace('\\', '/').Replace("file:////", "file:///");
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
                Assert.IsTrue(tracks[0].Duration > 0);
                Assert.IsTrue(tracks[1].Duration > 0);
            }
            finally
            {
                ATL.Settings.PlaylistWriteAbsolutePath = defaultPathSetting;
                if (Settings.DeleteAfterSuccess)
                {
                    File.Delete(testTrackLocation1);
                    File.Delete(testTrackLocation2);
                    File.Delete(testFileLocation);
                }
            }
        }
    }
}
