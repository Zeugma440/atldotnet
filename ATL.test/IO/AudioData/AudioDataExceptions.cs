using ATL.AudioData;
using ATL.Logging;
using Commons;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.Logging.Log;

namespace ATL.test
{
    [TestClass]
    public class AudioDataIOExceptions
    {
        private void audio_X_AAC_MP4_Atom(string atom, string atomCaption = null)
        {
            if (null == atomCaption) atomCaption = atom;

            ArrayLogger log = new ArrayLogger();
            string resource = "AAC/mp4.m4a";
            string location = TestUtils.GetResourceLocationRoot() + resource;
            string testFileLocation = TestUtils.DuplicateTempTestFile(resource);

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(atom));
                fs.Seek(-1, SeekOrigin.Current);
                fs.WriteByte(0);

                Track theTrack = new Track(fs, ".mp4");
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
            audio_X_AAC_MP4_Atom("udta");
            audio_X_AAC_MP4_Atom("ilst");
            audio_X_AAC_MP4_Atom("mdat");
        }
    }
}