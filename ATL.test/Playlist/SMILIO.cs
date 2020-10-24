using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class SMILIO
    {
        [TestMethod]
        public void PLIO_R_SMIL()
        {
            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            string resourceRoot = TestUtils.GetResourceLocationRoot(false);
            replacements.Add(new KeyValuePair<string, string>("$PATH", resourceRoot));
            replacements.Add(new KeyValuePair<string, string>("$NODISK_PATH", resourceRoot.Substring(2, resourceRoot.Length - 2)));

            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.smil", replacements);
            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(3, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
                foreach (Track t in theReader.Tracks) Assert.IsTrue(t.Duration > 0);
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

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MP3\\empty.mp3"));
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MOD\\mod.mod"));


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
                    Assert.IsTrue(StreamUtils.ArrEqualsArr(bom, PlaylistIO.BOM_UTF8));
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
                Assert.AreEqual(3, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(2, filePaths.Count);
                Assert.IsTrue(filePaths[0].EndsWith(pathsToWrite[0]));
                Assert.IsTrue(filePaths[1].EndsWith(pathsToWrite[1]));


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
                                Assert.AreEqual("file:///"+tracksToWrite[index].Path.Replace('\\', '/'), source.GetAttribute("src"));
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
    }
}
