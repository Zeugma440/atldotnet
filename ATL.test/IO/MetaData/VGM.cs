 using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;

namespace ATL.test.IO.MetaData
{
    /*
     * IMPLEMENTED USE CASES
     *  
     *  1. Single metadata fields
     *                                Read  | Add   | Remove
     *  Supported textual field     |   x   |  x    | x
     *  Unsupported textual field   |   x   |  x    | x
     *  
     *  2. General behaviour
     *  
     *  Whole tag removal
     *  
     *  Conservation of unmodified tag items after tag editing
     *  Conservation of unsupported tag field after tag editing
     *
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Test multiplicity of field names
     * 
    */


    [TestClass]
    public class VGM : MetaIOTest
    {
        public VGM()
        {
            emptyFile = "VGM/empty.vgm";
            notEmptyFile = "VGM/vgm.vgm";
        }

        [TestMethod]
        public void TagIO_R_VGM_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile);
        }
        
        [TestMethod]
        public void TagIO_RW_VGM_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);

            // Construct a new tag
            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.Comment = "This is a test";
            theTag.RecordingYear = "2008";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Album", theFile.NativeTag.Album);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("This is a test", theFile.NativeTag.Comment);
            Assert.AreEqual("2008", theFile.NativeTag.Year);

            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void tagIO_RW_VGM_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag incl. unsupported field (dumper)
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.RecordingYear = "1999";


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile, "1999");


            // Remove the additional supported field
            theTag = new TagData();
            theTag.RecordingYear = "1992";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private void readExistingTagsOnFile(AudioDataManager theFile, string yearStr = "1992")
        {
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Supported fields
            Assert.AreEqual("Jungle", theFile.NativeTag.Title);
            Assert.AreEqual("Taz-Mania", theFile.NativeTag.Album);
            Assert.AreEqual("Satoshi Namekawa, Kouichi Shimamura, Takashi Masuzaki", theFile.NativeTag.Artist);
            Assert.AreEqual("", theFile.NativeTag.Comment);
            Assert.AreEqual(yearStr, theFile.NativeTag.Year);

            // Unsupported fields
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TITLE_J"));
            Assert.AreEqual("", theFile.NativeTag.AdditionalFields["TITLE_J"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("GAME_J"));
            Assert.AreEqual("", theFile.NativeTag.AdditionalFields["GAME_J"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("AUTHOR_J"));
            Assert.AreEqual("", theFile.NativeTag.AdditionalFields["AUTHOR_J"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("SYSTEM"));
            Assert.AreEqual("Sega Master System", theFile.NativeTag.AdditionalFields["SYSTEM"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("SYSTEM_J"));
            Assert.AreEqual("セガマスターシステム", theFile.NativeTag.AdditionalFields["SYSTEM_J"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("DUMPER"));
            Assert.AreEqual("Zeugma440", theFile.NativeTag.AdditionalFields["DUMPER"]);
        }
    }
}
