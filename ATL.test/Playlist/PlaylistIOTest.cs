using ATL.Logging;
using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using static ATL.Logging.Log;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_Format_Init()
        {
            PlaylistFormat tempFmt = new PlaylistFormat(0, "M3U");
            tempFmt.AddMimeType("blah");
            tempFmt.AddExtension(".m3u");

            PlaylistFormat testFmt = new PlaylistFormat(tempFmt);
            Assert.AreEqual(tempFmt.ID, testFmt.ID);
            Assert.AreEqual(tempFmt.Name, testFmt.Name);
            Assert.AreEqual(tempFmt.LocationFormat, testFmt.LocationFormat);
            Assert.AreEqual(tempFmt.MimeList.Count, testFmt.MimeList.Count);
            Assert.AreEqual(tempFmt.Readable, testFmt.Readable);
        }

        [TestMethod]
        public void PLIO_R_NoFormat()
        {
            IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.xyz");
            Assert.IsInstanceOfType(pls, typeof(ATL.Playlist.IO.DummyIO));
        }

        [TestMethod]
        public void PLIO_R_Common()
        {
            IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u");

            Assert.AreEqual(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u", pls.Path);

            ArrayLogger log = new ArrayLogger();
            try
            {
                pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(TestUtils.GetResourceLocationRoot() + "_Playlists/efiufhziuefizeub.m3u");
                IList<string> files = pls.FilePaths;
                Assert.Fail();
            }
            catch
            {
                IList<LogItem> logItems = log.GetAllItems(Log.LV_ERROR);
                Assert.AreEqual(1, logItems.Count);
                Assert.IsTrue(logItems[0].Message.Contains("efiufhziuefizeub.m3u")); // Can't do much more than than because the exception message is localized
            }
        }
    }
}
