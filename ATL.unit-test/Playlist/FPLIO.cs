using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class FPLIO
    {
        [TestMethod]
        public void PLIO_R_FPL()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.fpl", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(4, theReader.FilePaths.Count);
                foreach (string s in theReader.FilePaths) Assert.IsTrue(System.IO.File.Exists(s));
                foreach (Track t in theReader.Tracks) Assert.IsTrue(t.Duration > 0); // Ensures the track has been parsed
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }
    }
}
