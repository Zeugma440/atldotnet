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
        public void TagIO_R_XM()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 2);
        }
    }
}
