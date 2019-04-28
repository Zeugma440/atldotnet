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
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.xspf", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
                foreach (Track t in theReader.Tracks) Assert.IsTrue(t.Duration > 0);
            }
            finally
            {
                File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PLIO_W_XSPF()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MP3/empty.mp3"));
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MOD/mod.mod"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.xspf");
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing
                pls.FilePaths = pathsToWrite;
                IList<string> parents = new List<string>();
                int index = -1;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (XmlReader source = XmlReader.Create(fs))
                {
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.Element)
                        {
                            if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                            else if (source.Name.Equals("tracklist", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist")) parents.Add(source.Name);
                            else if (source.Name.Equals("track", StringComparison.OrdinalIgnoreCase) && parents.Contains("tracklist"))
                            {
                                parents.Add(source.Name);
                                index++;
                            }
                            else if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase) && parents.Contains("track")) Assert.AreEqual(getXmlValue(source), pathsToWrite[index]);
                        }
                    }
                }

                Assert.AreEqual(4, parents.Count);

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
                            else if (source.Name.Equals("tracklist", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist")) parents.Add(source.Name);
                            else if (source.Name.Equals("track", StringComparison.OrdinalIgnoreCase) && parents.Contains("tracklist"))
                            {
                                parents.Add(source.Name);
                                index++;
                            }
                            else if (parents.Contains("track"))
                            {
                                if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(getXmlValue(source), tracksToWrite[index].Path.Replace('\\','/'));
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
                Assert.AreEqual(4, parents.Count);
            }
            finally
            {
                File.Delete(testFileLocation);
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
