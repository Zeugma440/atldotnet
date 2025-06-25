
namespace ATL.test.IO
{
    [TestClass]
    public class TrackTest
    {
        [TestMethod]
        public void Track_CopyMetaTo()
        {
            Track track = new Track();
            track.Title = "aaa";
            track.Year = 1997;
            var fields = new Dictionary<string, string>
            {
                { "AA", "bb" },
                { "CC", "dd" }
            };
            track.AdditionalFields = fields;
            PictureInfo referencePic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"), PictureInfo.PIC_TYPE.Front);
            referencePic.ComputePicHash();
            PictureInfo picture1 = referencePic;
            track.EmbeddedPictures.Add(picture1);
            LyricsInfo info = new LyricsInfo
            {
                UnsynchronizedLyrics = "lalala"
            };
            var lyrics = new List<LyricsInfo>() { info };
            track.Lyrics = lyrics;

            // Test initial file is empty
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/empty.mp3");
            Track track2 = new Track(testFileLocation);

            Assert.AreEqual("", track2.Artist);
            Assert.AreEqual(0, track2.Year);
            Assert.AreEqual(0, track2.AdditionalFields.Count);
            Assert.AreEqual(0, track2.EmbeddedPictures.Count);
            Assert.AreEqual(false, track2.Lyrics.Count > 0);

            // Test in-memory data after copy
            track.CopyMetadataTo(track2);
            testCopiedMetadata(track2, referencePic, lyrics);

            // Test deep copy
            track.AdditionalFields["CC"] = "ee";
            track.EmbeddedPictures[0] = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpeg"), PictureInfo.PIC_TYPE.Front);
            testCopiedMetadata(track2, referencePic, lyrics);

            // Test after saving
            track2.Save();
            testCopiedMetadata(track2, referencePic, lyrics);

            // Get rid of the working file
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private void testCopiedMetadata(Track track, PictureInfo refPic, IList<LyricsInfo> refLyrics)
        {
            Assert.AreEqual("aaa", track.Title);
            Assert.AreEqual(1997, track.Year);
            Assert.AreEqual(2, track.AdditionalFields.Count);
            Assert.AreEqual("bb", track.AdditionalFields["AA"]);
            Assert.AreEqual("dd", track.AdditionalFields["CC"]);
            Assert.AreEqual(1, track.EmbeddedPictures.Count);
            PictureInfo image = track.EmbeddedPictures[0];
            Assert.AreEqual(refPic.PictureHash, image.ComputePicHash());
            Assert.AreEqual(refLyrics.Count, track.Lyrics.Count);
            for (int i = 0; i < refLyrics.Count; i++)
            {
                Assert.AreEqual(refLyrics[i].UnsynchronizedLyrics, track.Lyrics[i].UnsynchronizedLyrics);
            }
        }
    }
}
