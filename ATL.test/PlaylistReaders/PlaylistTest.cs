using ATL.PlaylistReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATL.test
{
    [TestClass]
    public class PlaylistTest
    {
        [TestMethod]
        public void TestM3U()
        {
            IPlaylistReader theReader = PlaylistReaders.PlaylistReaderFactory.GetInstance().GetPlaylistReader("../../Resources/playlist.m3u");

            Assert.IsNotInstanceOfType(theReader, typeof(PlaylistReaders.BinaryLogic.DummyReader));
            Assert.AreEqual(4, theReader.GetFiles().Count);
            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
                Assert.IsTrue(System.IO.File.Exists(s));
            }
        }
    }
}
