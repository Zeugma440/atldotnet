using ATL.CatalogDataReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATL.test
{
    [TestClass]
    public class CUETest
    {
        [TestMethod]
        public void TestReadTracks()
        {
           ICatalogDataReader theReader = CatalogDataReaderFactory.GetInstance().GetCatalogDataReader(TestHelper.getResourceLocationRoot()+"duck hunt.cue");

           System.Console.WriteLine(theReader.Artist);
           System.Console.WriteLine(theReader.Title);
           foreach(Track t in theReader.Tracks)
           {
               System.Console.WriteLine(">" + t.Title);
           }

           Assert.IsNotInstanceOfType(theReader, typeof(CatalogDataReaders.BinaryLogic.DummyReader));
           Assert.AreEqual("Nintendo Sound Team", theReader.Artist);
           Assert.AreEqual("Duck Hunt", theReader.Title);
           Assert.AreEqual(2, theReader.Tracks.Count);
           Assert.AreEqual("Title Screen", theReader.Tracks[0].Title);
           Assert.AreEqual("Title Screen (reprise)", theReader.Tracks[1].Title);
        }

    }
}
