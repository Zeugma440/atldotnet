using ATL.AudioData;
using ATL.AudioData.IO;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class GYM : MetaIOTest
    {
        public GYM()
        {
            emptyFile = "GYM/empty.gym";
            notEmptyFile = "GYM/gym.gym";
            tagType = MetaDataIOFactory.TagType.NATIVE;

            canMetaNotExist = false;
            supportsExtraEmbeddedPictures = false;

            // Initialize specific test data
            testData = new TagHolder();

            testData.Title = "The Source of Evilness";
            testData.Album = "Arrow Flash";
            testData.Comment = "Last Stage Music";
            testData.Copyright = "1990 Sega";

            testData.AdditionalFields.Add(new KeyValuePair<string, string>("EMULATOR", "Magasis"));
            testData.AdditionalFields.Add(new KeyValuePair<string, string>("DUMPER", "Guy"));
        }

        [TestMethod]
        public void TagIO_R_GYM_simple()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_GYM_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void tagIO_RW_GYM_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, true);
        }
    }
}
