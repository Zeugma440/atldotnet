using ATL.AudioData;
using ATL.AudioData.IO;

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
            // Hash check NOT POSSIBLE YET mainly due to tag order and integer encoding size differences
            test_RW_Existing(notEmptyFile, 2, true, false, false);
        }

        [TestMethod]
        public void TagIO_RW_MKA_Chapters()
        {
            new ConsoleLogger();

            // Source : empty MP3
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);

            Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

            TagHolder theTag = new TagHolder();
            theTag.ChaptersTableDescription = "Content֍";
            IList<ChapterInfo> testChapters = new List<ChapterInfo>();
            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 123;
            ch.EndTime = 789;
            ch.UniqueID = "";
            ch.Title = "aaa";

            testChapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 1230;
            ch.EndTime = 7890;
            ch.UniqueID = "002";
            ch.Title = "aaa0";

            testChapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            theTag.Chapters = testChapters;

            // Check if they are persisted properly
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Content֍", theFile.NativeTag.ChaptersTableDescription);
            Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

            // Check if values are the same
            int found = 0;
            foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
            {
                if (expectedChaps.ContainsKey(chap.StartTime))
                {
                    found++;
                    Assert.AreEqual(chap.UniqueID, expectedChaps[chap.StartTime].UniqueID);
                    Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                    Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                }
                else
                {
                    System.Console.WriteLine(chap.StartTime);
                }
            }
            Assert.AreEqual(2, found);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }
    }
}
