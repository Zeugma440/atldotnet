using ATL.AudioData;
using ATL.AudioData.IO;
using Commons;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v2_WAV : MetaIOTest
    {
        public ID3v2_WAV()
        {
            emptyFile = "WAV/empty.wav";
            notEmptyFile = "WAV/audacityTags.wav";
            tagType = MetaDataIOFactory.TagType.ID3V2;

            // Initialize specific test data
            testData = new TagHolder();

            testData.Title = "yeah";
            testData.Artist = "artist";
            testData.AdditionalFields.Add(new KeyValuePair<string, string>("TES2", "Test父"));
        }

        [TestMethod]
        public void TagIO_R_WAV_ID3v2()
        {
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_WAV_ID3v2_Existing()
        {
            // Not same size after edit because original ID3v2.3 is remplaced by ATL ID3v2.4
            test_RW_Existing(notEmptyFile, 0, true, false, false);
        }

        [TestMethod]
        public void TagIO_RW_WAV_ID3v2_Delete()
        {
            new ConsoleLogger();

            // Source : file with embedded ID3v2 ('id3 ' chunk)
            string testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Remove the ID3v2 tag
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TagType.ID3V2));

            // Check if the 'id3 ' chunk has been removed
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open))
            using (BinaryReader r = new BinaryReader(s))
            {
                Assert.IsFalse(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("id3 ")));
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_WAV_ID3v2_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_R_WAV_ID3v2_Illegal()
        {
            new ConsoleLogger();

            // Source : file illegally tagged with ID3v2 tag (slapped at offset 0 instead of being embedded with an 'id3 ' chunk)
            string testFileLocation = TestUtils.CopyAsTempTestFile("WAV/illegal_id3v2.wav");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(MetaDataIOFactory.TagType.ID3V2));
            var meta = theFile.getMeta(MetaDataIOFactory.TagType.ID3V2);
            Assert.IsTrue(meta.Exists);

            Assert.AreEqual("Freestyle", meta.Title);
            Assert.AreEqual("Playboi Carti x A$AP Rocky x A$AP Ferg", meta.Artist);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_Edit_WAV_ID3v2_Illegal()
        {
            new ConsoleLogger();

            // Source : file illegally tagged with ID3v2 tag (slapped at offset 0 instead of being embedded with an 'id3 ' chunk)
            string testFileLocation = TestUtils.CopyAsTempTestFile("WAV/illegal_id3v2.wav");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
            Assert.IsTrue(theFile.ReadFromFile());

            var tag = new TagHolder();
            tag.Title = "blah";
            Assert.IsTrue(theFile.UpdateTagInFile(tag.tagData, MetaDataIOFactory.TagType.ID3V2));

            using (FileStream s = new FileStream(testFileLocation, FileMode.Open))
            using (BinaryReader r = new BinaryReader(s))
            {
                // Check if the leading tag has been removed
                var lead = Utils.Latin1Encoding.GetString(r.ReadBytes(4));
                Assert.AreEqual("RIFF",lead);

                // Check if an "id3 " chunk has been added that contains the target title
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("id3 ")));
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("blah")));
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_Delete_WAV_ID3v2_Illegal()
        {
            new ConsoleLogger();

            // Source : file illegally tagged with ID3v2 tag (slapped at offset 0 instead of being embedded with an 'id3 ' chunk)
            string location = TestUtils.GetResourceLocationRoot() + "WAV/illegal_id3v2.wav";
            string testFileLocation = TestUtils.CopyAsTempTestFile("WAV/illegal_id3v2.wav");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TagType.ID3V2));

            // Check if illegal tagging has been removed
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);
            Assert.AreEqual(1103, originalFileInfo.Length - testFileInfo.Length);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }
    }
}
