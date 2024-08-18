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

            supportsDateOrYear = true;
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
    }
}
