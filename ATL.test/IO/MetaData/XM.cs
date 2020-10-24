using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class XM
    {
        string notEmptyFile = "XM/xm.xm";
        string emptyFile = "XM/empty.xm";

        [TestMethod]
        public void TagIO_R_XM()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            string comment = theFile.NativeTag.Comment.Replace(ATL.Settings.InternalValueSeparator, '/');

            // Supported fields
            Assert.AreEqual("Chrono Trigger", theFile.NativeTag.Title);
            Assert.AreEqual(@"@Copyright    97-05-16/DreamWorks Design/presents/VeritaS/""Chrono Trigger""/(Ending Theme)/Rendered for the/truth of sequence/music disk./----< STEREO >----/----< Greetz >----/SquareSoft (of course)/Mr.Zenap/DWD/Kenneth.S/Sorry, no more greetz!/----< Se Ya >----/Just close your eyes/and let your mind/float away with the/Music. Let the sounds/create a fantasy world/for you in your mind./----<Enjoy!!!!!!>----/If you want info on/DreamWorks Design/or other releases on/the net. Please/contact me at:/fredrik.lundgren@/mbox302.swipnet.se/---< The enD >---/\I//\I//\I//\I//\I//\I//\I//\I//\I//\I//Remember!!!!!/Use STEREO!!!!!", comment);
        }

        [TestMethod]
        public void TagIO_RW_XM_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            // Assert.IsFalse(theFile.NativeTag.Exists); XM files have embedded comments that prevent from saying that the tag does not exist at all

            // Construct a new tag
            TagData theTag = new TagData();
            theTag.Title = "Test !!";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            // Assert.IsFalse(theFile.NativeTag.Exists); XM files have embedded comments that prevent from saying that the tag does not exist at all


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.AreEqual(originalMD5, testMD5);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void tagIO_RW_XM_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Title = "Test!!";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile);

            // Additional supported field
            Assert.AreEqual("Test!!", theFile.NativeTag.Title);


            // Remove the additional supported field
            theTag = new TagData();
            theTag.Title = "";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.NativeTag.Title);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }
        

        private void readExistingTagsOnFile(AudioDataManager theFile, int nbPictures = 2)
        {
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            string comment = theFile.NativeTag.Comment.Replace(ATL.Settings.InternalValueSeparator, '/');

            // Supported fields
            Assert.AreEqual(@"@Copyright    97-05-16/DreamWorks Design/presents/VeritaS/""Chrono Trigger""/(Ending Theme)/Rendered for the/truth of sequence/music disk./----< STEREO >----/----< Greetz >----/SquareSoft (of course)/Mr.Zenap/DWD/Kenneth.S/Sorry, no more greetz!/----< Se Ya >----/Just close your eyes/and let your mind/float away with the/Music. Let the sounds/create a fantasy world/for you in your mind./----<Enjoy!!!!!!>----/If you want info on/DreamWorks Design/or other releases on/the net. Please/contact me at:/fredrik.lundgren@/mbox302.swipnet.se/---< The enD >---/\I//\I//\I//\I//\I//\I//\I//\I//\I//\I//Remember!!!!!/Use STEREO!!!!!", comment);
        }
    }
}
