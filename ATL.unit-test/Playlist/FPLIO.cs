using ATL.Playlist;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class FPLIO : PlaylistIOTest
    {
        [TestMethod]
        public void PLIO_R_FPL()
        {
            PLIO_R("playlist.fpl", TestUtils.GetResourceLocationRoot(false), 5);
        }
    }
}
