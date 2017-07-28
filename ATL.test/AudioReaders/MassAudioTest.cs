using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test.AudioReaders
{
    [TestClass]
    public class MassAudioTest
    {
        [TestMethod, TestCategory("mass")]
        public void MassTest()
        {
            String folder = "../../Resources";
            Track t;

            string[] files = Directory.GetFiles(folder);

            foreach (String file in files)
            {
                t = new Track(file);

                Console.WriteLine(t.Path + "......."+ Commons.Utils.FormatTime(t.Duration) + " | " +t.SampleRate + " ("+t.Bitrate+" kpbs)");
            }
        }
    }
}
