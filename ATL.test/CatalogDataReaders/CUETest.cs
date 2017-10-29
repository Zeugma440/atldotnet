using ATL.CatalogDataReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test
{
    [TestClass]
    public class CUETest
    {
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
               Assert.AreEqual("GENRE Game"+Settings.InternalValueSeparator+"DATE 1984", theReader.Comments);

                Assert.AreEqual(2, theReader.Tracks.Count);
               Assert.AreEqual("Title Screen", theReader.Tracks[0].Title);
               Assert.AreEqual("Title Screen (reprise)", theReader.Tracks[1].Title);
            }
            finally
            {
                File.Delete(testFileLocation);
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
                Assert.AreEqual("GENRE Electronica" + Settings.InternalValueSeparator + "DATE 1998", theReader.Comments);

                Assert.AreEqual(8, theReader.Tracks.Count);
                Assert.AreEqual("Reverence", theReader.Tracks[0].Title);
                Assert.AreEqual("Faithless", theReader.Tracks[0].Artist);
                Assert.AreEqual("comment11" + Settings.InternalValueSeparator + "comment12", theReader.Tracks[0].Comment);

                Assert.AreEqual("She's My Baby", theReader.Tracks[1].Title);
                Assert.AreEqual("Faithless", theReader.Tracks[1].Artist);
                Assert.AreEqual("comment21" + Settings.InternalValueSeparator + "comment22", theReader.Tracks[1].Comment);
            }
            finally
            {
                File.Delete(testFileLocation);
            }
        }

    }
}
