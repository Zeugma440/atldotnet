using ATL.Playlist;
using System;
using System.IO;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class SMILIO : PlaylistIOTest
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

            PLIO_R("playlist.smil", replacements, 4);
        }

        [TestMethod]
        public void PLIO_W_SMIL()
        {
            string testFileLocation = TestUtils.CreateTempTestFile("test.smil");
            bool defaultPathSetting = ATL.Settings.PlaylistWriteAbsolutePath;
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing + absolute formatting
                ATL.Settings.PlaylistWriteAbsolutePath = true;
                pls.FilePaths = pathsToWrite;
                pls.Save();

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

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;

                testFileLocation = TestUtils.CreateTempTestFile("test.smil");
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.Tracks = tracksToWrite;
                pls.Save();

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

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.PlaylistWriteAbsolutePath = defaultPathSetting;
            }
        }

        [TestMethod]
        public void PLIO_PLIO_RW_Absolute_Relative_Path_SMIL()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("smil");
            try
            {
                IList<string> parents = new List<string>();
                int nbEntries = -1;

                using FileStream fs = new FileStream(testFileLocation, FileMode.Open);
                using XmlReader source = XmlReader.Create(fs);
                while (source.Read())
                {
                    if (source.NodeType == XmlNodeType.Element)
                    {
                        if (source.Name.Equals("smil", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                        else if (source.Name.Equals("body", StringComparison.OrdinalIgnoreCase) && parents.Contains("smil")) parents.Add(source.Name);
                        else if (source.Name.Equals("seq", StringComparison.OrdinalIgnoreCase) && parents.Contains("body")) parents.Add(source.Name);
                        else if (source.Name.Equals("media", StringComparison.OrdinalIgnoreCase) && parents.Contains("seq"))
                        {
                            nbEntries++;
                            string expected = "";
                            switch (nbEntries)
                            {
                                case 0:
                                    expected = "file:///" + remoteFilePath1.Replace('\\', '/');
                                    break;
                                case 1:
                                    expected = "file:///" + localFilePath1.Replace('\\', '/');
                                    break;
                                case 2:
                                    expected = TestUtils.MakePathRelative(testFileLocation, localFilePath2);
                                    break;
                                case 3:
                                    expected = "file:///" + remoteFilePath2.Replace('\\', '/');
                                    break;
                            }
                            var actual = source.GetAttribute("src");
                            Assert.AreEqual(expected, actual);
                        }
                    }
                }
                Assert.AreEqual(4, nbEntries + 1);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }
    }
}
