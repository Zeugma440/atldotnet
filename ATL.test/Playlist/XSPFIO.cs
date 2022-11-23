using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class XSPFIO
    {
        [TestMethod]
        public void PLIO_R_XSPF()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.xspf", "$PATH", TestUtils.GetResourceLocationRoot(false).Replace('\\', '/'));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(5, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths)
                {
                    if (!s.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(File.Exists(s));
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
        public void PLIO_W_XSPF()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add(TestUtils.GetResourceLocationRoot() + "aaa.mp3");
            pathsToWrite.Add(TestUtils.GetResourceLocationRoot() + "bbb.mp3");
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MP3", "empty.mp3")));
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MOD", "mod.mod")));
            tracksToWrite.Add(new Track("http://this-is-a-stre.am:8405/live"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.xspf");
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
                    Assert.IsTrue(StreamUtils.ArrEqualsArr(bom, PlaylistIO.BOM_UTF8));
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


                // Test Track writing
                pls.Tracks = tracksToWrite;
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
                                if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Path);
                                else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Title);
                                else if (source.Name.Equals("creator", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Artist);
                                else if (source.Name.Equals("album", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Album);
                                else if (source.Name.Equals("annotation", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Comment);
                                else if (source.Name.Equals("trackNum", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].TrackNumber);
                                else if (source.Name.Equals("duration", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), ((long)Math.Round(tracksToWrite[index].DurationMs)).ToString());
                            }
                        }
                    }
                }
                Assert.AreEqual(5, parents.Count);

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(tracksToWrite.Count, tracks.Count);
                for (int i = 0; i < tracksToWrite.Count; i++) Assert.AreEqual(tracksToWrite[i].Path, tracks[i].Path);
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
