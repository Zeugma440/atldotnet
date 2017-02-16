using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;

namespace ATL.test.IO
{
    [TestClass]
    public class HighLevel
    {
        [TestMethod]
        public void TagIO_R_Single_ID3v1()
        {
            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at aff if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = false;                            // default behaviour anyway
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V1, 1);  // Should be entirely read
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V2, 2);  // Should not be read, since behaviour is single tag reading
            /* end set options */

            // TODO SWITCH OLD CODE TO NEW CODE
            Track theTrack = new Track("../../Resources/01 - Title Screen.mp3");

            Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
            Assert.AreEqual(0, theTrack.Year); // Specifically tagged as empty on the ID3v1 tag
        }

        [TestMethod]
        public void TagIO_R_Multi()
        {
            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at aff if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = true;
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V1, 1);  // Should be the main source except for the Year field (empty on ID3v1)
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V2, 2);  // Should be used for the Year field (valuated on ID3v2)
            /* end set options */

            // TODO SWITCH OLD CODE TO NEW CODE
            Track theTrack = new Track("../../Resources/01 - Title Screen.mp3");

            Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
            Assert.AreEqual(1984, theTrack.Year); // Empty on the ID3v1 tag => cross-reading should read it on ID3v2
        }

        // TODO test access to native tag on MP4 file
    }
}
