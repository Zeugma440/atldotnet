using System;
using ATL.AudioReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace ATL.test
{
    [TestClass]
    public class FactoryTest
    {
        [TestMethod]
        public void TestGetPlaylistFormats()
        {
            StringBuilder filter = new StringBuilder("");

            foreach (Format f in ATL.PlaylistReaders.PlaylistReaderFactory.GetInstance().getFormats())
            {
                if (f.Readable)
                {
                    foreach (String extension in f)
                    {
                        filter.Append(extension).Append(";");
                    }
                }
            }
            // Removes the last separator
            filter.Remove(filter.Length - 1, 1);

            Assert.AreEqual(".PLS;.M3U;.M3U8;.FPL", filter.ToString());
        }
    }
}
