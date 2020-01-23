using ATL.Logging;
using Commons;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using static ATL.Logging.Log;

namespace ATL.test.IO
{
    [TestClass]
    public class AudioDataExceptions
    {
        private void audio_X_AAC_MP4_Atom(string atom, string atomCaption = null)
        {
            if (null == atomCaption) atomCaption = atom;

            ArrayLogger log = new ArrayLogger();
            string resource = "AAC/mp4.m4a";
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(atom));
                fs.Seek(-1, SeekOrigin.Current);
                fs.WriteByte(0);

                new Track(fs, ".mp4");
                IList<LogItem> logItems = log.GetAllItems(Log.LV_ERROR);
                Assert.AreEqual(1, logItems.Count);
                Assert.AreEqual(atomCaption + " atom could not be found; aborting read", logItems[0].Message);
            }

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void Audio_X_AAC_MP4()
        {
            audio_X_AAC_MP4_Atom("moov");
            audio_X_AAC_MP4_Atom("mvhd");
            audio_X_AAC_MP4_Atom("trak");
            audio_X_AAC_MP4_Atom("mdia");
            //            audio_X_AAC_MP4_Atom("mdhd", "mdia.mdhd");
            audio_X_AAC_MP4_Atom("hdlr", "mdia.hdlr");
            audio_X_AAC_MP4_Atom("minf", "mdia.minf");
            audio_X_AAC_MP4_Atom("stbl", "mdia.minf.stbl");
            audio_X_AAC_MP4_Atom("stsd");
            //            audio_X_AAC_MP4_Atom("stts");
            //            audio_X_AAC_MP4_Atom("stsc");
            audio_X_AAC_MP4_Atom("stsz");
//            audio_X_AAC_MP4_Atom("udta"); optional atim
//            audio_X_AAC_MP4_Atom("ilst"); optional atom
            audio_X_AAC_MP4_Atom("mdat");
        }

        [TestMethod]
        public void Audio_X_APE_invalid()
        {
            // Source : APE with invalid header
            ArrayLogger log = new ArrayLogger();
            string location = TestUtils.GetResourceLocationRoot() + "MP3/invalidApeHeader.mp3";

            new Track(location);

            IList<LogItem> logItems = log.GetAllItems(LV_ERROR);
            Assert.AreEqual(1, logItems.Count);
            Assert.AreEqual("Invalid value found while reading APEtag frame", logItems[0].Message);
        }
    }
}