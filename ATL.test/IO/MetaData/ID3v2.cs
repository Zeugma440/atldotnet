 using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

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
     *  4. Technical
     *  
     *  Cohabitation with ID3v1 and APE
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Individual picture removal (from index > 1)
     * 
     * Extended ID3v2 header compliance cases incl. limit cases
     * 
     * 
     * TECHNICAL
     * 
     * Add a standard unsupported field => persisted as standard field in tag
     * Add a non-standard unsupported field => persisted as TXXX field
     * Exact picture data conservation after tag editing
     * 
    */


    [TestClass]
    public class ID3v2 : MetaIOTest
    {
        public ID3v2()
        {
            emptyFile = "MP3/empty.mp3";
            notEmptyFile = "MP3/ID3v2.2 UTF16.mp3";
        }

        [TestMethod]
        public void TagIO_R_ID3v22_simple()
        {
            string location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 ANSI charset only.mp3";
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location) );

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("noTagnoTag", theFile.ID3v2.Title);
            Assert.AreEqual("ALBUM!", theFile.ID3v2.Album);
            Assert.AreEqual("ARTIST", theFile.ID3v2.Artist);
            Assert.AreEqual("ALBUMARTIST", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("I have no IDE and i must code", theFile.ID3v2.Comment);
            Assert.AreEqual("1997", theFile.ID3v2.Year);
            Assert.AreEqual("House", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual("COMP!", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);

            // Pictures
            Assert.AreEqual(1, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(TagData.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 656);
                    Assert.AreEqual(picture.Width, 552);
                    found++;
                }
            }

            Assert.AreEqual(1, found);
        }

        [TestMethod]
        public void TagIO_R_ID3v22_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 UTF16.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location));

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("﻿bébé", theFile.ID3v2.Title);
            Assert.AreEqual("ALBUM!", theFile.ID3v2.Album);
            Assert.AreEqual("﻿父", theFile.ID3v2.Artist);
            Assert.AreEqual("ALBUMARTIST", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("﻿I have no IDE and i must code bébé 父", theFile.ID3v2.Comment);
            Assert.AreEqual("1997", theFile.ID3v2.Year);
            Assert.AreEqual("House", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual("COMP!", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);

            // Pictures
            Assert.AreEqual(1, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(TagData.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 656);
                    Assert.AreEqual(picture.Width, 552);
                    found++;
                }
            }

            Assert.AreEqual(1, found);
        }

        [TestMethod]
        public void TagIO_R_ID3v22_3pictures()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 3 pictures.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location));

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);


            // Pictures
            Assert.AreEqual(3, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(TagData.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
                    Assert.AreEqual(picture.Height, 256);
                    Assert.AreEqual(picture.Width, 256);
                    found++;
                }
            }

            Assert.AreEqual(3, found);
        }

        [TestMethod]
        public void TagIO_R_ID3v23_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/id3v2.3_UTF16.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location));

            readExistingTagsOnFile(ref theFile);
        }

        [TestMethod]
        public void TagIO_R_ID3v24_UTF8()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/id3v2.4_UTF8.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location));

            readExistingTagsOnFile(ref theFile);
        }

        [TestMethod]
        public void TagIO_RW_ID3v24_Extended()
        {
            ArrayLogger logger = new ArrayLogger();

            // Source : MP3 with extended tag properties (tag restrictions)
            string testFileLocation = TestUtils.GetTempTestFile("MP3/id3v2.4_UTF8_extendedTag.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));

            // Check that the presence of an extended tag does not disrupt field reading
            readExistingTagsOnFile(ref theFile);

            Settings.ID3v2_useExtendedHeaderRestrictions = true;

            try
            {
                // Insert a very long field while tag restrictions specify that string shouldn't be longer than 30 characters
                TagData theTag = new TagData();
                theTag.Conductor = "Veeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeery long field";

                // Insert a large picture while tag restrictions specify that pictures shouldn't be larger than 64x64pixels AND tag size shouldn't be larger than 4 KB
                TagData.PictureInfo picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, TagData.PIC_TYPE.Back);
                picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
                theTag.Pictures.Add(picInfo);


                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));
            }
            finally
            {
                Settings.ID3v2_useExtendedHeaderRestrictions = false;
            }

            bool isAlertFieldLength = false;
            bool isAlertTagSize = false;
            bool isAlertNbFrames = false;
            bool isAlertPicDimension = false;

            foreach (Logging.Log.LogItem logItem in logger.items)
            {
                if (logItem.Message.Contains("is longer than authorized")) isAlertFieldLength = true;
                if (logItem.Message.StartsWith("Tag is too large")) isAlertTagSize = true;
                if (logItem.Message.StartsWith("Tag has too many frames")) isAlertNbFrames = true;
                if (logItem.Message.EndsWith("does not respect ID3v2 restrictions (exactly 64x64)")) isAlertPicDimension = true;
            }

            Assert.IsTrue(isAlertFieldLength);
            Assert.IsTrue(isAlertTagSize);
            //Assert.IsTrue(isAlertNbFrames);
            Assert.IsTrue(isAlertPicDimension);

        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Empty()
        {
            // Source : tag-free MP3
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.GetTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


            // Construct a new tag
            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.AlbumArtist = "Mike";
            theTag.Comment = "This is a test";
            theTag.RecordingYear = "2008";
            theTag.RecordingDate = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";
            theTag.DiscNumber = "2";
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.OriginalArtist = "Bob";
            theTag.OriginalAlbum = "Hey Hey";
            theTag.GeneralDescription = "That's right";
            theTag.Publisher = "Test Media Inc.";
            theTag.Conductor = "John Johnson Jr.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual("Test !!", theFile.ID3v2.Title);
            Assert.AreEqual("Album", theFile.ID3v2.Album);
            Assert.AreEqual("Artist", theFile.ID3v2.Artist);
            Assert.AreEqual("Mike", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.ID3v2.Comment);
            Assert.AreEqual("2008", theFile.ID3v2.Year);
            Assert.AreEqual("Merengue", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual(2, theFile.ID3v2.Disc);
            Assert.AreEqual("Me", theFile.ID3v2.Composer);
            Assert.AreEqual("父", theFile.ID3v2.Copyright);
            Assert.AreEqual("Bob", theFile.ID3v2.OriginalArtist);
            Assert.AreEqual("Hey Hey", theFile.ID3v2.OriginalAlbum);
            Assert.AreEqual("That's right", theFile.ID3v2.GeneralDescription);
            Assert.AreEqual("Test Media Inc.", theFile.ID3v2.Publisher);
            Assert.AreEqual("John Johnson Jr.", theFile.ID3v2.Conductor);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


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

        [TestMethod]
        public void TagIO_RW_ID3v2_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String testFileLocation = TestUtils.GetTempTestFile("MP3/id3v2.3_UTF16.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Conductor = "John Jackman";

            TagData.PictureInfo picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, TagData.PIC_TYPE.Back);
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot()+ "_Images/pic1.jpg");
            theTag.Pictures.Add(picInfo);


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            readExistingTagsOnFile(ref theFile, 3);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.ID3v2.Conductor);

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Back))
                {
                    Assert.AreEqual(pic.Value.NativeCodeInt, 0x04);
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    break;
                }
            }


            // Remove the additional supported field
            theTag = new TagData();
            theTag.Conductor = "";

            // Remove additional picture
            picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, TagData.PIC_TYPE.Back);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            readExistingTagsOnFile(ref theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.ID3v2.Conductor);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)

/* NOT POSSIBLE YET mainly due to tag order and padding differences
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));
*/
            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Unsupported_Empty()
        {
            // Source : tag-free MP3
            String testFileLocation = TestUtils.GetTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation) );


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "TES2", "This is another test 父"));

            // Add new unsupported pictures
            TagData.PictureInfo picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, MetaDataIOFactory.TAG_ID3V2, 0x0A);
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
            theTag.Pictures.Add(picInfo);
            picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, MetaDataIOFactory.TAG_ID3V2, 0x0B);
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg");
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2);

            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual(2, theFile.ID3v2.AdditionalFields.Count);

            Assert.IsTrue(theFile.ID3v2.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.ID3v2.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.ID3v2.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test 父", theFile.ID3v2.AdditionalFields["TES2"]);

            Assert.AreEqual(2, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.NativeCodeInt.Equals(0x0A))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
                else if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.NativeCodeInt.Equals(0x0B))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 290);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
            }

            Assert.AreEqual(2, found);

            // Remove the additional unsupported field
            theTag = new TagData();
            TagData.MetaFieldInfo fieldInfo = new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, MetaDataIOFactory.TAG_ID3V2, 0x0A);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.ID3v2.AdditionalFields.Count);
            Assert.IsTrue(theFile.ID3v2.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test 父", theFile.ID3v2.AdditionalFields["TES2"]);

            // Pictures
            Assert.AreEqual(1, pictures.Count);

            found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.NativeCodeInt.Equals(0x0B))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 290);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
            }

            Assert.AreEqual(1, found);


            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_ID3v1()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_ID3V2, MetaDataIOFactory.TAG_ID3V1);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_APE()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_ID3V2, MetaDataIOFactory.TAG_APE);
        }

        private void readExistingTagsOnFile(ref AudioDataManager theFile, int nbPictures = 2)
        {
            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("Title", theFile.ID3v2.Title);
            Assert.AreEqual("父", theFile.ID3v2.Album);
            Assert.AreEqual("Artist", theFile.ID3v2.Artist);
            Assert.AreEqual("Test!", theFile.ID3v2.Comment);
            Assert.AreEqual("2017", theFile.ID3v2.Year);
            Assert.AreEqual("Test", theFile.ID3v2.Genre);
            Assert.AreEqual(22, theFile.ID3v2.Track);
            Assert.AreEqual("Me", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);

            // Unsupported field (MOOD)
            Assert.IsTrue(theFile.ID3v2.AdditionalFields.Keys.Contains("MOOD"));
            Assert.AreEqual("xxx", theFile.ID3v2.AdditionalFields["MOOD"]);


            // Pictures
            Assert.AreEqual(nbPictures, pictures.Count);

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(TagData.PIC_TYPE.Front)) // Supported picture
                {
                    Assert.AreEqual(pic.Value.NativeCodeInt, 0x03);
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 150);
                    Assert.AreEqual(picture.Width, 150);
                }
                else if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported))  // Unsupported picture
                {
                    Assert.AreEqual(pic.Value.NativeCodeInt, 0x09);
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
                    Assert.AreEqual(picture.Height, 168);
                    Assert.AreEqual(picture.Width, 175);
                }
            }
        }
    }
}
