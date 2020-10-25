using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using ATL.AudioData;

namespace ATL.test
{
    [TestClass]
    public class FileStructureHelperTest
    {
        private readonly FileStructureHelper structureHelper = new FileStructureHelper();

        private void init(BinaryWriter w)
        {
            w.Write((ulong)10);                         // 1st size descriptor; position 0
            w.Write((byte)2);                           // Counter; position 8
            w.Write((uint)5);                           // 1st sub-size descriptor; position 9
            w.Write(new byte[5] { 1, 2, 3, 4, 5 });     // Data chunk 1; position 13
            w.Write((uint)5);                           // 2nd sub-size descriptor; position 18
            w.Write(new byte[5] { 6, 7, 8, 9, 10 });    // Data chunk 2; position 22

            structureHelper.AddZone(13, 5, "zone1");
            structureHelper.AddSize(0, (ulong)10, "zone1");
            structureHelper.AddCounter(8, (byte)2, "zone1");
            structureHelper.AddSize(9, (uint)5, "zone1");

            structureHelper.AddZone(22, 5, "zone2");
            structureHelper.AddSize(0, (ulong)10, "zone2");
            structureHelper.AddCounter(8, (byte)2, "zone2");
            structureHelper.AddSize(18, (uint)5, "zone2");

            structureHelper.AddZone(27, 0, "zone3");
            structureHelper.AddSize(0, (ulong)10, "zone3");
            structureHelper.AddCounter(8, (byte)2, "zone3");
        }

        [TestMethod]
        public void FSH_Edit()
        {
            using (Stream s = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(s))
            using (BinaryReader r = new BinaryReader(s))
            {
                init(w);

                StreamUtils.ShortenStream(s, 18, 2);
                structureHelper.RewriteHeaders(w, null, -2, FileStructureHelper.ACTION.Edit, "zone1");

                r.BaseStream.Seek(0, SeekOrigin.Begin);
                Assert.AreEqual((ulong)8, r.ReadUInt64());

                r.BaseStream.Seek(8, SeekOrigin.Begin);
                Assert.AreEqual((byte)2, r.ReadByte());

                r.BaseStream.Seek(9, SeekOrigin.Begin);
                Assert.AreEqual((uint)3, r.ReadUInt32());

                r.BaseStream.Seek(16, SeekOrigin.Begin);
                Assert.AreEqual((uint)5, r.ReadUInt32());


                StreamUtils.ShortenStream(s, 25, 2);
                structureHelper.RewriteHeaders(w, null, -2, FileStructureHelper.ACTION.Edit, "zone2");

                r.BaseStream.Seek(0, SeekOrigin.Begin);
                Assert.AreEqual((ulong)6, r.ReadUInt64());

                r.BaseStream.Seek(8, SeekOrigin.Begin);
                Assert.AreEqual((byte)2, r.ReadByte());

                r.BaseStream.Seek(9, SeekOrigin.Begin);
                Assert.AreEqual((uint)3, r.ReadUInt32());

                r.BaseStream.Seek(16, SeekOrigin.Begin);
                Assert.AreEqual((uint)3, r.ReadUInt32());
            }
        }

        [TestMethod]
        public void FSH_Remove()
        {
            using (Stream s = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(s))
            using (BinaryReader r = new BinaryReader(s))
            {
                init(w);

                structureHelper.RewriteHeaders(w, null, -5, FileStructureHelper.ACTION.Delete, "zone1");
                StreamUtils.ShortenStream(s, 18, 5);

                r.BaseStream.Seek(0, SeekOrigin.Begin);
                Assert.AreEqual((ulong)5, r.ReadUInt64());

                r.BaseStream.Seek(8, SeekOrigin.Begin);
                Assert.AreEqual((byte)1, r.ReadByte());

                r.BaseStream.Seek(9, SeekOrigin.Begin);
                Assert.AreEqual((uint)0, r.ReadUInt32());

                r.BaseStream.Seek(13, SeekOrigin.Begin);
                Assert.AreEqual((uint)5, r.ReadUInt32());
            }
        }

        [TestMethod]
        public void FSH_Add()
        {
            using (Stream s = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(s))
            using (BinaryReader r = new BinaryReader(s))
            {
                init(w);

                structureHelper.RewriteHeaders(w, null, 5, FileStructureHelper.ACTION.Add, "zone3");
                StreamUtils.LengthenStream(s, 27, 5);

                r.BaseStream.Seek(0, SeekOrigin.Begin);
                Assert.AreEqual((ulong)15, r.ReadUInt64());

                r.BaseStream.Seek(8, SeekOrigin.Begin);
                Assert.AreEqual((byte)3, r.ReadByte());

                r.BaseStream.Seek(9, SeekOrigin.Begin);
                Assert.AreEqual((uint)5, r.ReadUInt32());

                r.BaseStream.Seek(18, SeekOrigin.Begin);
                Assert.AreEqual((uint)5, r.ReadUInt32());
            }
        }
    }
}
