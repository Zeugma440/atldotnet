using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using static ATL.PictureInfo;
using ATL.AudioData.IO;

namespace ATL.test.IO.MetaData
{
    /*
     * IMPLEMENTED USE CASES
     *  
     *  1. Single metadata fields
     *                                Read  | Add   | Remove
     *  Supported textual field     |   x   |  x    | x
     *  Unsupported textual field   |   x   |  x    | x
     *  Supported picture           |   x   |  x    | x
     *  Unsupported picture         |   x   |  x    | x
     *  
     *  2. General behaviour
     *  
     *  Whole tag removal
     *  
     *  Conservation of unmodified tag items after tag editing
     *  Conservation of unsupported tag field after tag editing
     *  Conservation of supported pictures after tag editing
     *  Conservation of unsupported pictures after tag editing
     *  
     *  3. Specific behaviour
     *  
     *  Remove single supported picture (from normalized type and index)
     *  Remove single unsupported picture (with multiple pictures; checking if removing pic 2 correctly keeps pics 1 and 3)
     *
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Individual picture removal (from index > 1)
     * 
     * Test multiplicity of field names
     * 
     * 
     * TECHNICAL
     * 
     * Exact picture data conservation after tag editing
     * 
     * 
    */


    [TestClass]
    public class WMA : MetaIOTest
    {
        public WMA()
        {
            emptyFile = "WMA/empty_full.wma";
            notEmptyFile = "WMA/wma.wma";
            tagType = MetaDataIOFactory.TagType.NATIVE;

            testData.Conductor = null;
            testData.Date = DateTime.MinValue;
            testData.SortAlbum = "SortAlbum";
            testData.SortArtist = "SortArtist";
            testData.SortTitle = "SortTitle";
            testData.Group = "Group";
        }

        [TestMethod]
        public void TagIO_R_WMA_simple()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_WMA_Empty()
        {
            new ConsoleLogger();

            // Source : totally metadata-free WMA
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);

            // Construct a new tag
            TagHolder theTag = new TagHolder();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.AlbumArtist = "Mike";
            theTag.Comment = "This is a test";
            theTag.Date = DateTime.Parse("2008/01/01");
            theTag.Genre = "Merengue";
            theTag.TrackNumber = 1;
            theTag.TrackTotal = 1;
            theTag.DiscNumber = 2;
            theTag.Composer = "Me";
            theTag.Popularity = 2.0f / 5;
            theTag.Copyright = "父";
            theTag.Conductor = "John Johnson Jr.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Album", theFile.NativeTag.Album);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("Mike", theFile.NativeTag.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.NativeTag.Comment);
            Assert.AreEqual(2008, theFile.NativeTag.Date.Year);
            Assert.AreEqual("Merengue", theFile.NativeTag.Genre);
            Assert.AreEqual(1, theFile.NativeTag.TrackNumber);
            Assert.AreEqual(2, theFile.NativeTag.DiscNumber);
            Assert.AreEqual((float)(2.0 / 5), theFile.NativeTag.Popularity);
            Assert.AreEqual("Me", theFile.NativeTag.Composer);
            Assert.AreEqual("父", theFile.NativeTag.Copyright);
            Assert.AreEqual("John Johnson Jr.", theFile.NativeTag.Conductor);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TagType.NATIVE));

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
        public void TagIO_RW_WMA_Empty_NonWM()
        {
            new ConsoleLogger();

            // Source : WMA with remaining non-WM metadata used for playback (isVBR, DeviceConformanceTemplate, WMFSDKxxx)
            string location = TestUtils.GetResourceLocationRoot() + "WMA/empty_non-WMFields.wma";
            string testFileLocation = TestUtils.CopyAsTempTestFile("WMA/empty_non-WMFields.wma");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);

            // Construct a new tag
            TagHolder theTag = new TagHolder();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.AlbumArtist = "Mike";
            theTag.Comment = "This is a test";
            theTag.Date = DateTime.Parse("2008/01/01");
            theTag.Genre = "Merengue";
            theTag.TrackNumber = 1;
            theTag.TrackTotal = 1;
            theTag.DiscNumber = 2;
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.Conductor = "John Johnson Jr.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Album", theFile.NativeTag.Album);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("Mike", theFile.NativeTag.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.NativeTag.Comment);
            Assert.AreEqual(2008, theFile.NativeTag.Date.Year);
            Assert.AreEqual("Merengue", theFile.NativeTag.Genre);
            Assert.AreEqual(1, theFile.NativeTag.TrackNumber);
            Assert.AreEqual(2, theFile.NativeTag.DiscNumber);
            Assert.AreEqual("Me", theFile.NativeTag.Composer);
            Assert.AreEqual("父", theFile.NativeTag.Copyright);
            Assert.AreEqual("John Johnson Jr.", theFile.NativeTag.Conductor);


            // Remove the tag and check that it has been indeed removed
            ATL.Settings.ASF_keepNonWMFieldsWhenRemovingTag = true;
            try
            {
                Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TagType.NATIVE));
            }
            finally
            {
                ATL.Settings.ASF_keepNonWMFieldsWhenRemovingTag = false;
            }

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);


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
        public void TagIO_RW_WMA_Existing()
        {
            new ConsoleLogger();

            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (WM/Mood)
            string testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagHolder theTag = new TagHolder();
            theTag.Conductor = "John Jackman";

            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PictureInfo.PIC_TYPE.Back);
            var testPics = theTag.EmbeddedPictures;
            testPics.Add(picInfo);
            theTag.EmbeddedPictures = testPics;


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            readExistingTagsOnFile(theFile, 3);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.NativeTag.Conductor);

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Back))
                {
                    Assert.AreEqual(0x04, pic.NativePicCode);
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    break;
                }
            }


            // Remove the additional supported field
            theTag = new TagHolder();
            theTag.Conductor = "";

            // Remove additional picture
            picInfo = new PictureInfo(PictureInfo.PIC_TYPE.Back);
            picInfo.MarkedForDeletion = true;
            testPics.Add(picInfo);
            theTag.EmbeddedPictures = testPics;

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            readExistingTagsOnFile(theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.NativeTag.Conductor);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            /* Not possible yet due to zone order differences
                        FileInfo originalFileInfo = new FileInfo(location);
                        FileInfo testFileInfo = new FileInfo(testFileLocation);

                        Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                        string originalMD5 = TestUtils.GetFileMD5Hash(location);
                        string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                        Assert.IsTrue(originalMD5.Equals(testMD5));
            */
            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_WMA_Unsupported_Empty()
        {
            // Source : tag-free file
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST2", "This is another test 父"));

            // Add new unsupported pictures
            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TagType.NATIVE, 0xAA);
            theTag.Pictures.Add(picInfo);
            picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TagType.NATIVE, 0xAB);
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            Assert.AreEqual(2, theFile.NativeTag.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAA))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
                else if (pic.PicType.Equals(PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAB))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(290, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
            }

            Assert.AreEqual(2, found);

            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new PictureInfo(MetaDataIOFactory.TagType.NATIVE, 0xAA);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            // Pictures
            Assert.AreEqual(1, theFile.NativeTag.EmbeddedPictures.Count);

            found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAB))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(290, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
            }

            Assert.AreEqual(1, found);


            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_WMA_Unsupported_Classes_1_6()
        {
            // Source : file with classes 1 and 6 fields (MediaClassPrimaryID, LeakyBucketPairs)
            string testFileLocation = TestUtils.CopyAsTempTestFile("WMA/classes1&6.wma");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            string mediaClassPrimaryIdValue64 = "vH1g0SPj4kuGoUikKihEHg==";
            string leakyValue64 = "AADAXQAAEY8kADB1AAB3mhwAyK8AAP/+EQCQ4gAAtz0NAADCAQBpEgUAgKkDAEbIAAAwVwUAzAAAACChBwCPAAAAkCMLAGIAAABAQg8ARwAAAMBcFQAzAAAAIAsgACIAAABAS0wADgAAAICWmAAHAAAA";

            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(mediaClassPrimaryIdValue64, theFile.NativeTag.AdditionalFields["WM/MediaClassPrimaryID"]);
            Assert.AreEqual(leakyValue64, theFile.NativeTag.AdditionalFields["ASFLeakyBucketPairs"]);

            TagHolder theTag = new TagHolder();
            theTag.Conductor = "John Jackman";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            
            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(mediaClassPrimaryIdValue64, theFile.NativeTag.AdditionalFields["WM/MediaClassPrimaryID"]);
            Assert.AreEqual(leakyValue64, theFile.NativeTag.AdditionalFields["ASFLeakyBucketPairs"]);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_R_WMA_Rating()
        {
            assumeRatingInFile("_Ratings/windows7/0.wma", 0, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/windows7/1.wma", 1.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/windows7/2.wma", 2.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/windows7/3.wma", 3.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/windows7/4.wma", 4.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/windows7/5.wma", 1, MetaDataIOFactory.TagType.NATIVE);
        }

        [TestMethod]
        public void TagIO_RW_WMA_ID3v1()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V1);
        }

        [TestMethod]
        public void TagIO_RW_WMA_ID3v2()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V2);
        }

        [TestMethod]
        public void TagIO_RW_WMA_APE()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.APE);
        }
    }
}
