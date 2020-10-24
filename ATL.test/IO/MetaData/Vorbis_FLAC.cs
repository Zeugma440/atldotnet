using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using Commons;
using static ATL.PictureInfo;

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
    public class Vorbis_FLAC : MetaIOTest
    {
        public Vorbis_FLAC()
        {
            emptyFile = "FLAC/empty.flac";
            notEmptyFile = "FLAC/flac.flac";
            tagType = MetaDataIOFactory.TAG_NATIVE;

            testData.Conductor = null;
            testData.RecordingDate = "1997-06-20";
        }

        [TestMethod]
        public void TagIO_R_VorbisFLAC_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_R_VorbisFLAC_dirtyTrackDiscNumbering()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + "FLAC/flac_dirtyTrackDiscNumbering.flac";
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 2);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free OGG
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
            theTag.AlbumArtist = "Mike";
            theTag.Comment = "This is a test";
            theTag.RecordingYear = "2008";
            theTag.RecordingDate = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";
            theTag.DiscNumber = "2";
            theTag.Rating = 2.5.ToString();
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.Conductor = "John Johnson Jr.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Album", theFile.NativeTag.Album);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("Mike", theFile.NativeTag.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.NativeTag.Comment);
            Assert.AreEqual("2008", theFile.NativeTag.Year);
            Assert.AreEqual("Merengue", theFile.NativeTag.Genre);
            Assert.AreEqual(1, theFile.NativeTag.Track);
            Assert.AreEqual(2, theFile.NativeTag.Disc);
            Assert.AreEqual((float)(2.5 / 5), theFile.NativeTag.Popularity);
            Assert.AreEqual("Me", theFile.NativeTag.Composer);
            Assert.AreEqual("父", theFile.NativeTag.Copyright);
            Assert.AreEqual("John Johnson Jr.", theFile.NativeTag.Conductor);


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
        public void TagIO_RW_VorbisFLAC_Existing()
        {
            tagIO_RW_VorbisFLAC_Existing(notEmptyFile, 2);
        }

        private void tagIO_RW_VorbisFLAC_Existing(string fileName, int initialNbPictures, bool deleteTempFile = true)
        {
            ATL.Settings.AddNewPadding = true;
            ATL.Settings.PaddingSize = 4063; // Default padding of the sample FLAC file

            try
            {
                ConsoleLogger log = new ConsoleLogger();

                // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
                string location = TestUtils.GetResourceLocationRoot() + fileName;
                string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
                AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                // Add a new supported field and a new supported picture
                Assert.IsTrue(theFile.ReadFromFile());

                TagData theTag = new TagData();
                theTag.Conductor = "John Jackman";

                PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PictureInfo.PIC_TYPE.CD);
                theTag.Pictures.Add(picInfo);


                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

                readExistingTagsOnFile(theFile, initialNbPictures + 1);

                // Additional supported field
                Assert.AreEqual("John Jackman", theFile.NativeTag.Conductor);

                int nbFound = 0;
                foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
                {
                    if (pic.PicType.Equals(PictureInfo.PIC_TYPE.CD))
                    {
                        Assert.AreEqual(0x06, pic.NativePicCode);
                        using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                        {
                            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                            Assert.AreEqual(600, picture.Height);
                            Assert.AreEqual(900, picture.Width);
                        }
                        nbFound++;
                        break;
                    }
                }

                Assert.AreEqual(1, nbFound);

                // Remove the additional supported field
                theTag = new TagData();
                theTag.Conductor = "";

                // Remove additional picture
                picInfo = new PictureInfo(PictureInfo.PIC_TYPE.CD);
                picInfo.MarkedForDeletion = true;
                theTag.Pictures.Add(picInfo);

                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

                readExistingTagsOnFile(theFile, initialNbPictures);

                // Additional removed field
                Assert.AreEqual("", theFile.NativeTag.Conductor);


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
                if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            } finally {
                ATL.Settings.AddNewPadding = false;
                ATL.Settings.PaddingSize = 2048;
            }
        }

        private void checkTrackDiscZeroes(FileStream fs)
        {
            using (BinaryReader r = new BinaryReader(fs))
            {
                byte[] bytes = new byte[20];
                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("TRACKNUMBER=")));
                String s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("06", s.Substring(0, s.Length - 1));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("TRACKTOTAL=")));
                s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("06", s.Substring(0, s.Length - 1));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("DISCNUMBER=")));
                s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("03", s.Substring(0, s.Length - 1));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("DISCTOTAL=")));
                s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("04", s.Substring(0, s.Length - 1));
            }
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_UpdateKeepTrackDiscZeroes()
        {
            StreamDelegate dlg = new StreamDelegate(checkTrackDiscZeroes);
            test_RW_UpdateTrackDiscZeroes(notEmptyFile, false, false, dlg);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_UpdateFormatTrackDiscZeroes()
        {
            StreamDelegate dlg = new StreamDelegate(checkTrackDiscZeroes);
            test_RW_UpdateTrackDiscZeroes(notEmptyFile, true, true, dlg);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_Unsupported_Empty()
        {
            // Source : tag-free file
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST2", "This is another test 父"));

            // Add new unsupported pictures
            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TAG_NATIVE, 0x0A);
            theTag.Pictures.Add(picInfo);
            picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TAG_NATIVE, 0x0B);
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(3, theFile.NativeTag.AdditionalFields.Count); // 3 instead of 2 because of the VENDOR field... (specific to VorbisTag)

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            Assert.AreEqual(2, theFile.NativeTag.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0x0A))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
                else if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0x0B))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(290, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
            }

            Assert.AreEqual(2, found);

            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new PictureInfo(MetaDataIOFactory.TAG_NATIVE, 0x0A);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count); // 2 instead of 1 because of the VENDOR field...
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            // Pictures
            Assert.AreEqual(1, theFile.NativeTag.EmbeddedPictures.Count);

            found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0x0B))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
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
        public void TagIO_RW_VorbisFLAC_ID3v2()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_NATIVE, MetaDataIOFactory.TAG_ID3V2);
        }
    }
}
