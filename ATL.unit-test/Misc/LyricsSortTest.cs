using Commons;
using System.Text;

namespace ATL.test
{
    [TestClass]
    public class LyricsSortTest
    {
        private LyricsInfo.LyricsPhrase lyrics1 = new(1000, "AAA");
        private LyricsInfo.LyricsPhrase lyrics1b = new(1000, "AAA");
        private LyricsInfo.LyricsPhrase lyrics2 = new(2000, "ZZZ");
        private LyricsInfo.LyricsPhrase lyrics3 = null;

        [TestMethod]
        public void LyricsSort_Equality()
        {
            Assert.IsTrue(lyrics1 != lyrics2);
            Assert.IsTrue(lyrics1 != null);
            Assert.IsTrue(lyrics1 == lyrics1b);
            Assert.IsTrue(lyrics3 == null);
            Assert.IsTrue(lyrics1 >= lyrics1b);
            Assert.IsTrue(lyrics1 <= lyrics1b);
            Assert.IsTrue(lyrics1 == new LyricsInfo.LyricsPhrase(lyrics1));
        }

        [TestMethod]
        public void LyricsSort_Superior()
        {
            Assert.IsTrue(lyrics2 > lyrics1);
            Assert.IsTrue(lyrics2 >= lyrics1);
            Assert.IsFalse(new LyricsInfo.LyricsPhrase(2000, "AAA") > lyrics1);
            Assert.IsFalse(new LyricsInfo.LyricsPhrase(1000, "ZZZ") > lyrics1);
            Assert.IsTrue(lyrics2 > lyrics1);
        }

        [TestMethod]
        public void LyricsSort_Inferior()
        {
            Assert.IsTrue(lyrics1 < lyrics2);
            Assert.IsTrue(lyrics1 <= lyrics2);
            Assert.IsFalse(lyrics1 < new LyricsInfo.LyricsPhrase(2000, "AAA"));
            Assert.IsFalse(lyrics1 < new LyricsInfo.LyricsPhrase(1000, "ZZZ"));
            Assert.IsTrue(lyrics1 < lyrics2);
        }

        [TestMethod]
        public void LyricsSort_Equals()
        {
            Assert.IsTrue(lyrics1.Equals(lyrics1b));
            object obj = (object)new LyricsInfo.LyricsPhrase(1000, "AAA");
            Assert.IsTrue(lyrics1.Equals(obj));
        }

        [TestMethod]
        public void LyricsSort_Compare()
        {
            Assert.IsTrue(0 == lyrics1.CompareTo(lyrics1b));
            Assert.IsTrue(lyrics1.CompareTo(lyrics2) < 0);
            Assert.IsTrue(lyrics2.CompareTo(lyrics1) > 0);
        }
    }
}
