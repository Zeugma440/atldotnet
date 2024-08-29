using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class MKA : MetaIOTest
    {
        public MKA()
        {
            emptyFile = "MKA/empty.mka";
            notEmptyFile = "MKA/mka.mka";
            tagType = MetaDataIOFactory.TagType.NATIVE;
            titleFieldCode = "title";

            var testAddFields = new Dictionary<string, string>();
            testAddFields["track.test"] = "xxx";
            testData.AdditionalFields = testAddFields;

            testData.EmbeddedPictures[0].NativePicCodeStr = "cover.jpg";
            testData.EmbeddedPictures[1].NativePicCodeStr = "Other.png";

            // TODO it actually does support extra embedded pics, but detection of added pic fails as MKA is the only support with no picture type numeric code
            supportsExtraEmbeddedPictures = false; 
        }

        [TestMethod]
        public void TagIO_R_MKA()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 2);
        }

        [TestMethod]
        public void TagIO_R_MKA_Chapters()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + "MKA/chapters.mka";
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            Assert.AreEqual(2, meta.Chapters.Count);
            Assert.AreEqual("Chapter 01", meta.Chapters[0].Title);
            Assert.AreEqual(1000L, meta.Chapters[0].StartTime);
            Assert.AreEqual("Chapter 02", meta.Chapters[1].Title);
            Assert.AreEqual(2000L, meta.Chapters[1].StartTime);
        }

        [TestMethod]
        public void TagIO_RW_MKA_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_MKA_Existing()
        {
            // Hash check NOT POSSIBLE YET mainly due to tag order differences
            test_RW_Existing(notEmptyFile, 2, true, false, false);
        }
    }
}
