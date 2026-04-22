using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v2_DFF : MetaIOTest
    {
        public ID3v2_DFF()
        {
            emptyFile = "DSF/empty.dff";
            notEmptyFile = "DSF/dff.dff";
            testData.BPM = 0;
            testData.Title = ".05 sec 1kHz";
            testData.Album = "Test Tones";
            testData.Artist = "dff";
            testData.Comment = "Created with sox dsd";
            testData.TrackNumber = "1";
            testData.AlbumArtist = "";
            testData.Composer = "";
            testData.Date = DateTime.Parse("01/01/2025 00:00:00");
            testData.Genre = "Test";
            testData.TrackTotal = 0;
            testData.DiscNumber = 0;
            testData.DiscTotal = 0;
            testData.AdditionalFields = new Dictionary<string, string>();
            testData.EmbeddedPictures = new List<PictureInfo>();
            tagType = MetaDataIOFactory.TagType.ID3V2;
        }

        [TestMethod]
        public void TagIO_R_DFF_ID3v2()
        {
            String location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_DFF_ID3v2_Existing()
        {
            // Can't test size because the original files is padded at the end
            test_RW_Existing(notEmptyFile, 0, true, false);
        }

        [TestMethod]
        public void TagIO_RW_DFF_ID3v2_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, true);
        }
    }
}
