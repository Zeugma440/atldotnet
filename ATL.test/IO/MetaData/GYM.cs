using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.Collections.Generic;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class GYM : MetaIOTest
    {
        public GYM()
        {
            emptyFile = "GYM/empty.gym";
            notEmptyFile = "GYM/gym.gym";
            tagType = MetaDataIOFactory.TAG_NATIVE;

            canMetaNotExist = false;

            // Initialize specific test data
            testData = new TagData();

            testData.Title = "The Source of Evilness";
            testData.Album = "Arrow Flash";
            testData.Comment = "Last Stage Music";
            testData.Copyright = "1990 Sega";

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "EMULATOR", "Magasis"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "DUMPER", "Guy"));
        }

        [TestMethod]
        public void TagIO_R_GYM_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile);
        }
        
        [TestMethod]
        public void TagIO_RW_GYM_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void tagIO_RW_GYM_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, true);
        }
    }
}
