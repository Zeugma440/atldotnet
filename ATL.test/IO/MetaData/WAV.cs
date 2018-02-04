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
    public class WAV : MetaIOTest
    {
        public WAV()
        {
            emptyFile = "WAV/wav.wav";
            notEmptyFile = "WAV/broadcastwave_bext_info.wav";
        }

        [TestMethod]
        public void TagIO_R_WAV_BEXT_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location) );

            readExistingTagsOnFile_bext(theFile);
        }
        
//        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free VQF
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.GetTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));


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
            File.Delete(testFileLocation);
        }

//        [TestMethod]
        public void tagIO_RW_WAV_BEXT_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag incl. unsupported field (dumper)
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            string testFileLocation = TestUtils.GetTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Copyright = "Squaresoft";


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile_bext(theFile, "Squaresoft");

            // Remove the additional supported field
            theTag = new TagData();
            theTag.Copyright = "Alright";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile_bext(theFile);


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
            File.Delete(testFileLocation);
        }

//        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Unsupported_Empty()
        {
            // Source : tag-free file
            String testFileLocation = TestUtils.GetTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation) );


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
            File.Delete(testFileLocation);
        }

        private void readExistingTagsOnFile_bext(AudioDataManager theFile, string testDescription = "bext.description")
        {
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Supported fields
            Assert.AreEqual(testDescription, theFile.NativeTag.GeneralDescription);

            // Unsupported fields
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.originator"));
            Assert.AreEqual("bext.originator", theFile.NativeTag.AdditionalFields["bext.originator"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.originatorReference"));
            Assert.AreEqual("bext.originatorReference", theFile.NativeTag.AdditionalFields["bext.originatorReference"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.originationDate"));
            Assert.AreEqual("2018-01-09", theFile.NativeTag.AdditionalFields["bext.originationDate"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.originationTime"));
            Assert.AreEqual("01:23:45", theFile.NativeTag.AdditionalFields["bext.originationTime"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.timeReference"));
            Assert.AreEqual("110801250", theFile.NativeTag.AdditionalFields["bext.timeReference"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.version"));
            Assert.AreEqual("2", theFile.NativeTag.AdditionalFields["bext.version"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.UMID"));
            Assert.AreEqual("060A2B3401010101010102101300000000000000000000800000000000000000", theFile.NativeTag.AdditionalFields["bext.UMID"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.loudnessValue"));
            Assert.AreEqual((123 / 100.0).ToString(), theFile.NativeTag.AdditionalFields["bext.loudnessValue"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.loudnessRange"));
            Assert.AreEqual((456 / 100.0).ToString(), theFile.NativeTag.AdditionalFields["bext.loudnessRange"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.maxTruePeakLevel"));
            Assert.AreEqual((789 / 100.0).ToString(), theFile.NativeTag.AdditionalFields["bext.maxTruePeakLevel"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.maxMomentaryLoudness"));
            Assert.AreEqual((333 / 100.0).ToString(), theFile.NativeTag.AdditionalFields["bext.maxMomentaryLoudness"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.maxShortTermLoudness"));
            Assert.AreEqual((-333 / 100.0).ToString(), theFile.NativeTag.AdditionalFields["bext.maxShortTermLoudness"]);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("bext.codingHistory"));
            Assert.AreEqual("A=MPEG1L3,F=22050,B=56,W=20,M=dual-mono,T=haha", theFile.NativeTag.AdditionalFields["bext.codingHistory"]);
        }
    }
}
