using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class ASXIO
    {
        [TestMethod]
        public void PLIO_R_ASX()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.asx", "$PATH", TestUtils.GetResourceLocationRoot(false).Replace("\\", "/"));

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
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PLIO_W_ASX()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add(TestUtils.GetResourceLocationRoot() + "aaa.mp3");
            pathsToWrite.Add(TestUtils.GetResourceLocationRoot() + "bbb.mp3");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MP3\\empty.mp3"));
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MOD\\mod.mod"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.asx");
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
                        // Read file content
                        while (source.Read())
                        {
                            if (source.NodeType == XmlNodeType.Element)
                            {
                                if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name.ToLower());
                                else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("asx"))
                                {
                                    index++;
                                    parents.Add(source.Name.ToLower());
                                }
                                else if (source.Name.Equals("ref", StringComparison.OrdinalIgnoreCase) && parents.Contains("entry")) Assert.AreEqual(pathsToWrite[index], source.GetAttribute("HREF"));
                            }
                        }
                    }
                }

                Assert.AreEqual(3, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(2, filePaths.Count);
                Assert.AreEqual(pathsToWrite[0], filePaths[0]);
                Assert.AreEqual(pathsToWrite[1], filePaths[1]);


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
                            if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name.ToLower());
                            else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("asx"))
                            {
                                index++;
                                parents.Add(source.Name.ToLower());
                            }
                            else if (parents.Contains("entry"))
                            {
                                if (source.Name.Equals("ref", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Path, source.GetAttribute("HREF"));
                                else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Title, getXmlValue(source));
                                else if (source.Name.Equals("author", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Artist, getXmlValue(source));
                            }
                        }
                    }
                }
                Assert.AreEqual(3, parents.Count);

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(2, tracks.Count);
                Assert.AreEqual(tracksToWrite[0].Path, tracks[0].Path);
                Assert.AreEqual(tracksToWrite[1].Path, tracks[1].Path);
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
