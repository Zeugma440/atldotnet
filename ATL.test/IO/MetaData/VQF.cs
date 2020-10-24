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
    public class VQF : MetaIOTest
    {
        public VQF()
        {
            emptyFile = "VQF/empty.vqf";
            notEmptyFile = "VQF/vqf.vqf";
        }

        [TestMethod]
        public void TagIO_R_VQF_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile);
        }
        
        [TestMethod]
        public void TagIO_RW_VQF_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free VQF
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            // Assert.IsFalse(theFile.NativeTag.Exists); Tag data contains information required for playback => can never be nonexistent

            // Construct a new tag
            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.Copyright = "父";
            theTag.Comment = "This is a test";
            theTag.RecordingDate = "2008";
            theTag.Genre = "FPS";
            theTag.TrackNumber = "22/23";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Album", theFile.NativeTag.Album);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("父", theFile.NativeTag.Copyright);
            Assert.AreEqual("This is a test", theFile.NativeTag.Comment);
            Assert.AreEqual("2008", theFile.NativeTag.Year);
            Assert.AreEqual("FPS", theFile.NativeTag.Genre);
            Assert.AreEqual(22, theFile.NativeTag.Track);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            // Assert.IsFalse(theFile.NativeTag.Exists); Tag data contains information required for playback => can never be nonexistent


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
        public void tagIO_RW_VQF_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag incl. unsupported field (dumper)
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Copyright = "Squaresoft";


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile, "Squaresoft");

            // Remove the additional supported field
            theTag = new TagData();
            theTag.Copyright = "Alright";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            /*
             * Not possible yet due to field order differences
             
            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));
            */

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_VQF_Unsupported_Empty()
        {
            // Source : tag-free file
            String testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation) );


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST", "This is a test"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TES2", "This is another test"));


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test", theFile.NativeTag.AdditionalFields["TES2"]);


            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test", theFile.NativeTag.AdditionalFields["TES2"]);


            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private void readExistingTagsOnFile(AudioDataManager theFile, string testCopyright = "Alright")
        {
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Supported fields
            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("Bob", theFile.NativeTag.Album);
            Assert.AreEqual("Rock", theFile.NativeTag.Genre);
            Assert.AreEqual("this is a comment", theFile.NativeTag.Comment);
            Assert.AreEqual("2016", theFile.NativeTag.Year);
            Assert.AreEqual(22, theFile.NativeTag.Track);
            Assert.AreEqual(testCopyright, theFile.NativeTag.Copyright);

            // Unsupported field (GERR)
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("GERR"));
            Assert.AreEqual("Bock", theFile.NativeTag.AdditionalFields["GERR"]);
        }
    }
}
