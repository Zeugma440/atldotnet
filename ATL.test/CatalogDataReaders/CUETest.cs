using ATL.CatalogDataReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test
{
    [TestClass]
    public class CUETest
    {
        [TestMethod]
        public void Cue_ReadNoFormat()
        {
            string testFileLocation = TestUtils.GetResourceLocationRoot() + "_Cuesheet/cue.xyz";
            ICatalogDataReader theReader = CatalogDataReaderFactory.GetInstance().GetCatalogDataReader(testFileLocation);
            Assert.IsInstanceOfType(theReader, typeof(CatalogDataReaders.BinaryLogic.DummyReader));
        }

        [TestMethod]
        public void Cue_ReadTracks1()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Cuesheet/cue.cue", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                ICatalogDataReader theReader = CatalogDataReaderFactory.GetInstance().GetCatalogDataReader(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(CatalogDataReaders.BinaryLogic.DummyReader));
                Assert.AreEqual("Nintendo Sound Team", theReader.Artist);
                Assert.AreEqual("Duck Hunt", theReader.Title);
                Assert.AreEqual("GENRE Game" + ATL.Settings.InternalValueSeparator + "DATE 1984", theReader.Comments);

                Assert.AreEqual(2, theReader.Tracks.Count);
                Assert.AreEqual("Title Screen", theReader.Tracks[0].Title);
                Assert.AreEqual("Nintendo Sound Team", theReader.Tracks[0].Artist);
                Assert.AreEqual(4, theReader.Tracks[0].Duration);
                Assert.AreEqual("Title Screen (reprise)", theReader.Tracks[1].Title);
                Assert.AreEqual("Nintendo Sound Team", theReader.Tracks[1].Artist);
                Assert.AreEqual(4, theReader.Tracks[1].Duration);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void Cue_ReadTracks2()
        {
            string testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Cuesheet/cue2.cue", "$PATH", TestUtils.GetResourceLocationRoot(false));

            try
            {
                ICatalogDataReader theReader = CatalogDataReaderFactory.GetInstance().GetCatalogDataReader(testFileLocation);

                Assert.IsNotInstanceOfType(theReader, typeof(CatalogDataReaders.BinaryLogic.DummyReader));
                Assert.AreEqual("Faithless", theReader.Artist);
                Assert.AreEqual("Fake sample Cuesheet", theReader.Title);
                Assert.AreEqual("GENRE Electronica" + ATL.Settings.InternalValueSeparator + "DATE 1998", theReader.Comments);

                Assert.AreEqual(8, theReader.Tracks.Count);
                Assert.AreEqual("Reverence", theReader.Tracks[0].Title);
                Assert.AreEqual("Faithless1", theReader.Tracks[0].Artist);
                Assert.AreEqual(403, theReader.Tracks[0].Duration);
                Assert.AreEqual("comment11" + ATL.Settings.InternalValueSeparator + "comment12", theReader.Tracks[0].Comment);

                Assert.AreEqual("She's My Baby", theReader.Tracks[1].Title);
                Assert.AreEqual("Faithless", theReader.Tracks[1].Artist);
                Assert.AreEqual(253, theReader.Tracks[1].Duration);
                Assert.AreEqual("comment21" + ATL.Settings.InternalValueSeparator + "comment22", theReader.Tracks[1].Comment);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

    }
}
