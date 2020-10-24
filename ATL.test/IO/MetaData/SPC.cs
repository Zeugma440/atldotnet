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
    public class SPC : MetaIOTest
    {
        public SPC()
        {
            emptyFile = "SPC/empty.spc";
            notEmptyFile = "SPC/spc.spc";
        }

        [TestMethod]
        public void TagIO_R_SPC_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile);
        }
        
        [TestMethod]
        public void TagIO_RW_SPC_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free SPC
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
            theTag.Comment = "This is a test";
            theTag.RecordingYear = "2008";
            theTag.TrackNumber = "01/01";
            theTag.DiscNumber = "2";

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
            Assert.AreEqual(1, theFile.NativeTag.Track);
            Assert.AreEqual(2, theFile.NativeTag.Disc);

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
        public void tagIO_RW_SPC_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag incl. unsupported field (dumper)
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Publisher = "Square-Enix";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile, "Square-Enix");


            // Remove the additional supported field
            theTag = new TagData();
            theTag.Publisher = "Square";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            /*
             * Not possible yet due to field order differences
             *
            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));
            */

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_SPC_Unsupported_Empty()
        {
            // Source : tag-free file
            String testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation) );


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            // Assert.IsFalse(theFile.NativeTag.Exists); Tag data contains information required for playback => can never be nonexistent


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "55", "This is a test"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "56", "This is another test"));


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(6, theFile.NativeTag.AdditionalFields.Count); // 6 and not 2 because of header tag info

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("55"));
            Assert.AreEqual("This is a test", theFile.NativeTag.AdditionalFields["55"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("56"));
            Assert.AreEqual("This is another test", theFile.NativeTag.AdditionalFields["56"]);


            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "55");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(5, theFile.NativeTag.AdditionalFields.Count); // 5 instead of 1 because of the header tag info
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("56"));
            Assert.AreEqual("This is another test", theFile.NativeTag.AdditionalFields["56"]);


            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_SPC_APE()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_NATIVE, MetaDataIOFactory.TAG_APE, false);
        }


        private void readExistingTagsOnFile(AudioDataManager theFile, string publisherStr = "Square")
        {
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Supported fields
            Assert.AreEqual("Confusing Melody", theFile.NativeTag.Title);
            Assert.AreEqual("Chrono Trigger", theFile.NativeTag.Album);
            Assert.AreEqual("Yasunori Mitsuda", theFile.NativeTag.Artist);
            Assert.AreEqual("", theFile.NativeTag.Comment);
            Assert.AreEqual("1995", theFile.NativeTag.Year);
            Assert.AreEqual(23, theFile.NativeTag.Track);
            Assert.AreEqual(2, theFile.NativeTag.Disc);
            Assert.AreEqual(publisherStr, theFile.NativeTag.Publisher);

            // Unsupported field (Dumper)
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("162"));
            Assert.AreEqual("Datschge", theFile.NativeTag.AdditionalFields["162"]);
        }
    }
}
