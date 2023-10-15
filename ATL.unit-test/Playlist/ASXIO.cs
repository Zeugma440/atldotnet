using ATL.Playlist;
using System;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class ASXIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_ASX()
        {
            PLIO_R("playlist.asx", TestUtils.GetResourceLocationRoot(false).Replace("\\", "/"), 5);
        }

        [TestMethod]
        public void PLIO_W_ASX()
        {
            string testFileLocation = TestUtils.CreateTempTestFile("test.asx");
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

                    using XmlReader source = XmlReader.Create(fs);
                    // Read file content
                    while (source.Read())
                    {
                        if (source.NodeType != XmlNodeType.Element) continue;

                        if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name.ToLower());
                        else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("asx"))
                        {
                            index++;
                            parents.Add(source.Name.ToLower());
                        }
                        else if (source.Name.Equals("ref", StringComparison.OrdinalIgnoreCase) && parents.Contains("entry")) Assert.AreEqual(pathsToWrite[index], source.GetAttribute("HREF"));
                    }
                }

                Assert.AreEqual(4, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;

                testFileLocation = TestUtils.CreateTempTestFile("test.asx");
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                pls.Tracks = tracksToWrite;
                pls.Save();

                index = -1;
                parents.Clear();

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (XmlReader source = XmlReader.Create(fs))
                {
                    while (source.Read())
                    {
                        if (source.NodeType != XmlNodeType.Element) continue;

                        if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name.ToLower());
                        else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("asx"))
                        {
                            index++;
                            parents.Add(source.Name.ToLower());
                        }
                        else if (parents.Contains("entry"))
                        {
                            if (source.Name.Equals("ref", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(TestUtils.MakePathRelative(testFileLocation, tracksToWrite[index].Path), source.GetAttribute("HREF"));
                            else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Title, getXmlValue(source));
                            else if (source.Name.Equals("author", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Artist, getXmlValue(source));
                        }
                    }
                }
                Assert.AreEqual(4, parents.Count);

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
        public void PLIO_PLIO_RW_Absolute_Relative_Path_ASX()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("asx");
            try
            {
                IList<string> parents = new List<string>();
                int nbEntries = -1;

                using FileStream fs = new FileStream(testFileLocation, FileMode.Open);
                using XmlReader source = XmlReader.Create(fs);
                while (source.Read())
                {
                    if (source.NodeType != XmlNodeType.Element) continue;

                    if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name.ToLower());
                    else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("asx"))
                    {
                        nbEntries++;
                        parents.Add(source.Name.ToLower());
                    }
                    else if (parents.Contains("entry"))
                    {
                        if (source.Name.Equals("ref", StringComparison.OrdinalIgnoreCase))
                        {
                            var writtenPath = source.GetAttribute("HREF");
                            switch (nbEntries)
                            {
                                case 0:
                                    Assert.AreEqual(remoteFilePath1, writtenPath);
                                    break;
                                case 1:
                                    Assert.AreEqual(localFilePath1, writtenPath);
                                    break;
                                case 2:
                                    Assert.AreEqual(TestUtils.MakePathRelative(testFileLocation, localFilePath2),
                                        writtenPath);
                                    break;
                                case 3:
                                    Assert.AreEqual(remoteFilePath2, writtenPath);
                                    break;
                            }
                        }
                        else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase) && 1 == nbEntries)
                        {
                            Assert.AreEqual(NEW_TITLE, getXmlValue(source));
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

        private static string getXmlValue(XmlReader source)
        {
            source.Read();
            return source.NodeType == XmlNodeType.Text ? source.Value : "";
        }
    }
}
