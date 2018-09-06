using ATL.PlaylistReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATL.test.IO
{
    [TestClass]
    public class Factories
    {
        [TestMethod]
        public void Factories_GetFormats()
        {
            int fmtCount = 0;
            foreach (Format f in PlaylistReaderFactory.GetInstance().getFormats())
            {
                if (f.Readable)
                {
                    if (f.Name.Equals("PLS"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".pls"));
                    } else if (f.Name.Equals("M3U"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".m3u"));
                        Assert.IsTrue(f.IsValidExtension(".m3u8"));
                    }
                    else if (f.Name.Equals("FPL (experimental)"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".fpl"));
                    }
                    else if (f.Name.Equals("XSPF (spiff)"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".xspf"));
                    }
                    else if (f.Name.Equals("SMIL"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".smil"));
                        Assert.IsTrue(f.IsValidExtension(".smi"));
                        Assert.IsTrue(f.IsValidExtension(".zpl"));
                        Assert.IsTrue(f.IsValidExtension(".wpl"));

                    }
                    else if (f.Name.Equals("ASX"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".asx"));
                        Assert.IsTrue(f.IsValidExtension(".wax"));
                        Assert.IsTrue(f.IsValidExtension(".wvx"));
                    }
                    else if (f.Name.Equals("B4S"))
                    {
                        fmtCount++;
                        Assert.IsTrue(f.IsValidExtension(".b4s"));
                    }
                }
            }
            Assert.AreEqual(7, fmtCount);
        }

    }
}
