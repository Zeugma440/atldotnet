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
        private void audio_X_MP4_Atom(string resourceName, string atom, int logLevel = Log.LV_ERROR, string atomCaption = null)
        {
            if (null == atomCaption) atomCaption = atom;

            ArrayLogger log = new ArrayLogger();
            string resource = "MP4/" + resourceName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(atom));
                fs.Seek(-1, SeekOrigin.Current);
                fs.WriteByte(0);
                if (StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(atom)))
                {
                    fs.Seek(-1, SeekOrigin.Current);
                    fs.WriteByte(0);
                }

                new Track(fs, ".mp4");
                IList<LogItem> logItems = log.GetAllItems(logLevel);
                Assert.IsTrue(logItems.Count > 0);
                bool found = false;
                foreach(LogItem l in logItems)
                {
                    if (l.Message.Contains(atomCaption + " atom could not be found")) found = true;
                }
                Assert.IsTrue(found);
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void Audio_X_MP4()
        {
            audio_X_MP4_Atom("mp4.m4a","moov");
            audio_X_MP4_Atom("mp4.m4a", "mvhd");
//            audio_X_MP4_Atom("mp4.m4a", "trak", Log.LV_DEBUG);
            audio_X_MP4_Atom("mp4.m4a", "mdia", Log.LV_DEBUG);
            audio_X_MP4_Atom("mp4.m4a", "hdlr", Log.LV_DEBUG, "mdia.hdlr");
            audio_X_MP4_Atom("mp4.m4a", "minf", Log.LV_DEBUG, "mdia.minf");
            audio_X_MP4_Atom("mp4.m4a", "stbl", Log.LV_DEBUG, "mdia.minf.stbl");
            audio_X_MP4_Atom("mp4.m4a", "stsd", Log.LV_DEBUG);
            audio_X_MP4_Atom("mp4.m4a", "stsz");
            audio_X_MP4_Atom("mp4.m4a", "udta", Log.LV_INFO);
            audio_X_MP4_Atom("mp4.m4a", "ilst", Log.LV_WARNING);
            audio_X_MP4_Atom("mp4.m4a", "mdat");

            audio_X_MP4_Atom("chapters_QT.m4v", "mdhd", Log.LV_DEBUG);
            audio_X_MP4_Atom("chapters_QT.m4v", "stts");
            audio_X_MP4_Atom("chapters_QT.m4v", "stsc");
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