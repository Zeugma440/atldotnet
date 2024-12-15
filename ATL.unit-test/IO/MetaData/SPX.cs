using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class SPX : MetaIOTest
    {
        public SPX()
        {
            emptyFile = "SPX/empty.spx";
            notEmptyFile = "SPX/spx.spx";
            tagType = MetaDataIOFactory.TagType.NATIVE;

            supportsExtraEmbeddedPictures = false;

            testData.EmbeddedPictures.Clear();
            testData.Conductor = null;
            testData.Date = DateTime.Parse("1997-06-20");
            testData.PublishingDate = DateTime.Parse("1998-07-21");
            testData.CatalogNumber = "44887733";

            testData.AdditionalFields = new Dictionary<string, string>
            {
                { "TEST", "xxx" },
                { "VORBIS-VENDOR", "Lavf61.1.100" }
            };
        }

        [TestMethod]
        public void TagIO_R_SPX_simple()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_SPX_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, false);
        }

        [TestMethod]
        public void tagIO_RW_SPX_Existing()
        {
            test_RW_Existing(notEmptyFile, 0);
        }
    }
}
