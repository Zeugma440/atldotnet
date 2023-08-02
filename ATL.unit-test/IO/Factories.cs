using ATL.AudioData;
using ATL.AudioData.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ATL.test.IO
{
    [TestClass]
    public class Factories
    {
        [TestMethod]
        public void Factories_FormatCpy()
        {
            Format f1 = new Format(1, "AAA");
            f1.AddMimeType("ab/cd");
            f1.AddExtension("aa");

            Format f2 = new Format(f1);

            f1.AddMimeType("ef/gh");
            f1.ID = 2;
            f1.AddExtension("bb");
            f1.Name = "AA";

            Assert.AreEqual("AAA", f2.Name);
            Assert.AreEqual(1, f2.ID);
            Assert.AreEqual(1, f2.MimeList.Count);
            Assert.AreEqual(false, f2.IsValidExtension("bb"));
        }

        [TestMethod]
        public void Factories_InstanciateExt()
        {
            ICollection<string> extensions = new List<string>()
            { "mp1", "ogg", "mpc", "asf", "aac", "mp4", "ac3", "dts", "vqf", "flac", "ape", "ofr", "wv", "wav", "mid", "dsf", "psf", "spc", "tta", "tak", "mod", "s3m", "xm", "it", "aif", "vgm", "gym", "aa", "caf"};

            foreach (string ext in extensions)
            {
                IAudioDataIO audioIo = AudioDataIOFactory.GetInstance().GetFromMimeType("." + ext, "");
                Assert.IsNotInstanceOfType(audioIo, typeof(DummyReader));
            }
        }
    }
}
