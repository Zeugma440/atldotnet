using ATL.Playlist;
using System;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class B4SIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_B4S()
        {
            PLIO_R("playlist.b4s", TestUtils.GetResourceLocationRoot(false), 4);
        }

        [TestMethod]
        public void PLIO_W_B4S()
        {
            string testFileLocation = TestUtils.CreateTempTestFile("test.b4s");
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
                    // Test if _no_ UTF-8 BOM has been written at the beginning of the file
                    byte[] bom = new byte[3];
                    fs.Read(bom, 0, 3);
                    Assert.IsFalse(bom.SequenceEqual(PlaylistIO.BOM_UTF8));
                    fs.Seek(0, SeekOrigin.Begin);

                    using (XmlReader source = XmlReader.Create(fs))
                    {
                        while (source.Read())
                        {
                            if (source.NodeType == XmlNodeType.Element)
                            {
                                if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                                else if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase) && parents.Contains("WinampXML")) parents.Add(source.Name);
                                else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist"))
                                {
                                    parents.Add(source.Name);
                                    index++;
                                    string src = source.GetAttribute("Playstring") ?? "";
                                    if (src.StartsWith("http")) Assert.AreEqual(pathsToWrite[index], src);
                                    else Assert.AreEqual("file:///" + pathsToWrite[index].Replace('\\', '/'), src);
                                }
                            }
                        }
                    }
                }

                Assert.AreEqual(5, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;
                pls.Tracks = tracksToWrite;
                pls.Save();

                index = -1;
                parents.Clear();

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (XmlReader source = XmlReader.Create(fs))
                {
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.Element)
                        {
                            if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                            else if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase) && parents.Contains("WinampXML")) parents.Add(source.Name);
                            else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist"))
                            {
                                parents.Add(source.Name);
                                index++;
                                Assert.IsTrue(source.GetAttribute("Playstring").EndsWith(TestUtils.MakePathRelative(testFileLocation, tracksToWrite[index].Path).Replace('\\', '/')));
                            }
                            else if (parents.Contains("entry"))
                            {
                                if (source.Name.Equals("Name", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Title, getXmlValue(source));
                                else if (source.Name.Equals("Length", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(((long)Math.Round(tracksToWrite[index].DurationMs)).ToString(), getXmlValue(source));
                            }
                        }
                    }
                }
                Assert.AreEqual(5, parents.Count);

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(tracksToWrite.Count, tracks.Count);
                //for (int i = 0; i < tracksToWrite.Count; i++) Assert.AreEqual(TestUtils.MakePathRelative(testFileLocation, tracksToWrite[i].Path), tracks[i].Path);
                Assert.IsTrue(tracks[0].Duration > 0);
                Assert.IsTrue(tracks[1].Duration > 0);
            }
            finally
            {
                ATL.Settings.PlaylistWriteAbsolutePath = defaultPathSetting;
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }


        [TestMethod]
        public void PLIO_PLIO_RW_Absolute_Relative_Path_B4S()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("b4s");
            try
            {
                IList<string> parents = new List<string>();
                int nbEntries = -1;

                using FileStream fs = new FileStream(testFileLocation, FileMode.Open);
                using XmlReader source = XmlReader.Create(fs);
                while (source.Read())
                {
                    if (source.NodeType != XmlNodeType.Element) continue;

                    if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                    else if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase) && parents.Contains("WinampXML")) parents.Add(source.Name);
                    else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist"))
                    {
                        parents.Add(source.Name);
                        nbEntries++;
                        var writtenPath = source.GetAttribute("Playstring");
                        switch (nbEntries)
                        {
                            case 0:
                                Assert.AreEqual("file:///" + remoteFilePath.Replace('\\', '/'), writtenPath);
                                break;
                            case 1:
                                Assert.AreEqual(writtenPath, "file:///" + localFilePath1.Replace('\\', '/'), writtenPath);
                                break;
                            case 2:
                                // B4S doesn't support relative paths
                                Assert.AreEqual("file:///" + localFilePath2.Replace('\\', '/'), writtenPath);
                                break;
                        }
                    }
                    else if (parents.Contains("entry"))
                    {
                        if (source.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) && 1 == nbEntries)
                        {
                            Assert.AreEqual(NEW_TITLE, getXmlValue(source));
                        }
                    }
                }
                Assert.AreEqual(3, nbEntries + 1);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        private static string getXmlValue(XmlReader source)
        {
            source.Read();
            if (source.NodeType == XmlNodeType.Text)
            {
                return source.Value;
            }
            return "";
        }
    }
}
