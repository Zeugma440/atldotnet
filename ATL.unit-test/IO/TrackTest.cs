using ATL.AudioData;

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
            var fields = new Dictionary<string, string>();
            fields.Add("aa", "bb");
            fields.Add("cc", "dd");
            track.AdditionalFields = fields;


            Track track2 = new Track();

            Assert.IsNull(track2.Artist);
            Assert.AreEqual(0, track2.Year);
            Assert.IsNull(track2.AdditionalFields);

            track.CopyMetadataTo(track2);

            Assert.AreEqual("aaa", track2.Title);
            Assert.AreEqual(1997, track2.Year);
            Assert.AreEqual(2, track2.AdditionalFields.Count);
            Assert.AreEqual("dd", track2.AdditionalFields["cc"]);

            track.AdditionalFields["cc"] = "ee";

            Assert.AreEqual("dd", track2.AdditionalFields["cc"]); // Test deep copy
        }
    }
}
