using ATL.Playlist;
using System;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class XSPFIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_XSPF()
        {
            PLIO_R("playlist.xspf", TestUtils.GetResourceLocationRoot(false).Replace("\\", "/"), 5);
        }

        [TestMethod]
        public void PLIO_W_XSPF()
        {
            string testFileLocation = TestUtils.CreateTempTestFile("test.xspf");
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
                                if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                                else if (source.Name.Equals("tracklist", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist")) parents.Add(source.Name);
                                else if (source.Name.Equals("track", StringComparison.OrdinalIgnoreCase) && parents.Contains("trackList"))
                                {
                                    parents.Add(source.Name);
                                    index++;
                                }
                                else if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase) &&
                                         parents.Contains("track"))
                                {
                                    string sourceStr = getXmlValue(source);
                                    if (sourceStr.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                                        Assert.AreEqual(pathsToWrite[index], sourceStr);
                                    else
                                        Assert.AreEqual(pathsToWrite[index], sourceStr.Replace('/', Path.DirectorySeparatorChar));
                                }
                            }
                        }
                    }
                }

                Assert.AreEqual(5, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));

                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


                // Test Track writing + relative formatting
                ATL.Settings.PlaylistWriteAbsolutePath = false;

                testFileLocation = TestUtils.CreateTempTestFile("test.xspf");
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
                        if (source.NodeType == XmlNodeType.Element)
                        {
                            if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                            else if (source.Name.Equals("trackList", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist")) parents.Add(source.Name);
                            else if (source.Name.Equals("track", StringComparison.OrdinalIgnoreCase) && parents.Contains("trackList"))
                            {
                                parents.Add(source.Name);
                                index++;
                            }
                            else if (parents.Contains("track"))
                            {
                                if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), TestUtils.MakePathRelative(testFileLocation, tracksToWrite[index].Path));
                                else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Title);
                                else if (source.Name.Equals("creator", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Artist);
                                else if (source.Name.Equals("album", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Album);
                                else if (source.Name.Equals("annotation", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Comment);
                                else if (source.Name.Equals("trackNum", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].TrackNumber.ToString());
                                else if (source.Name.Equals("duration", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), ((long)Math.Round(tracksToWrite[index].DurationMs)).ToString());
                            }
                        }
                    }
                }
                Assert.AreEqual(5, parents.Count);

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
        public void PLIO_PLIO_RW_Absolute_Relative_Path_XSPF()
        {
            var testFileLocation = PLIO_RW_Absolute_Relative_Path("xspf");
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
                        if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase))
                            parents.Add(source.Name);
                        else if (source.Name.Equals("trackList", StringComparison.OrdinalIgnoreCase) &&
                                 parents.Contains("playlist")) parents.Add(source.Name);
                        else if (source.Name.Equals("track", StringComparison.OrdinalIgnoreCase) &&
                                 parents.Contains("trackList"))
                        {
                            parents.Add(source.Name);
                            nbEntries++;
                        }
                        else if (parents.Contains("track"))
                        {
                            string expected = "";
                            switch (nbEntries)
                            {
                                case 0:
                                    expected = remoteFilePath;
                                    break;
                                case 1:
                                    expected = localFilePath1;
                                    break;
                                case 2:
                                    expected = TestUtils.MakePathRelative(testFileLocation, localFilePath2);
                                    break;
                            }

                            if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase))
                                Assert.AreEqual(getXmlValue(source), expected);
                            else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase) && 1 == nbEntries)
                            {
                                Assert.AreEqual(getXmlValue(source), NEW_TITLE);
                            }
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
