using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class OPUS : MetaIOTest
    {
        public OPUS()
        {
            emptyFile = "OPUS/empty.opus";
            notEmptyFile = "OPUS/opus.opus";
            tagType = MetaDataIOFactory.TagType.NATIVE;

            testData.Conductor = null;
            testData.Date = DateTime.Parse("1997-06-20");
            testData.PublishingDate = DateTime.Parse("1998-07-21");
            testData.CatalogNumber = "44887733";
        }

        [TestMethod]
        public void TagIO_R_OPUS_simple()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_R_OPUS_malformedOggHeader()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + "OPUS/malformedOggPage.opus";
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            // Supported fields
            Assert.AreEqual("Union", meta.Title);
            Assert.AreEqual("Vindata", meta.Artist);
        }

        [TestMethod]
        public void TagIO_RW_OPUS_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, false);
        }

        [TestMethod]
        public void tagIO_RW_OPUS_Existing()
        {
            test_RW_Existing(notEmptyFile, 2);
        }
    }
}
