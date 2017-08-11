using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;
using System.IO;
using ATL.AudioData;

namespace ATL.test
{
    [TestClass]
    public class FileStructureHelperTest
    {
        private FileStructureHelper structureHelper = new FileStructureHelper();

        private void init(ref BinaryWriter w)
        {
            w.Write((ulong)10);                         // 1st size descriptor; position 0
            w.Write((byte)2);                           // Counter; position 8
            w.Write((uint)5);                           // 1st sub-size descriptor; position 9
            w.Write(new byte[5] { 1, 2, 3, 4, 5 });     // Data chunk; position 13
            w.Write((uint)5);                           // 2nd sub-size descriptor; position 18
            w.Write(new byte[5] { 6, 7, 8, 9, 10 });    // Data chunk; position 22

            structureHelper.AddZone(13, 10, "zone1");
            structureHelper.AddSize(0, (ulong)10, "zone1");
            structureHelper.AddCounter(8, (byte)2, "zone1");
            structureHelper.AddSize(9, (uint)5, "zone1");

            structureHelper.AddZone(23, 10, "zone2");
            structureHelper.AddSize(0, (ulong)10, "zone2");
            structureHelper.AddCounter(8, (byte)2, "zone2");
            structureHelper.AddSize(18, (uint)5, "zone2");

            structureHelper.AddZone(27, 0, "zone3");
            structureHelper.AddSize(0, (ulong)10, "zone3");
            structureHelper.AddCounter(8, (byte)2, "zone3");
        }

        private void test(ref BinaryReader r, ulong header, byte counter, uint subHeader1, uint subHeader2)
        {
            r.BaseStream.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual(header, r.ReadUInt64());

            r.BaseStream.Seek(8, SeekOrigin.Begin);
            Assert.AreEqual(counter, r.ReadByte());

            r.BaseStream.Seek(9, SeekOrigin.Begin);
            Assert.AreEqual(subHeader1, r.ReadUInt32());

            r.BaseStream.Seek(18, SeekOrigin.Begin);
            Assert.AreEqual(subHeader2, r.ReadUInt32());
        }

        [TestMethod]
        public void FSH_Edit()
        {
            Stream s = new MemoryStream();

            BinaryWriter w = new BinaryWriter(s);
            BinaryReader r = new BinaryReader(s);
            try
            {
                init(ref w);

                structureHelper.RewriteMarkers(ref w, -2, FileStructureHelper.ACTION_EDIT, "zone1");

                test(ref r, 8, 2, 3, 5);

                structureHelper.RewriteMarkers(ref w, -2, FileStructureHelper.ACTION_EDIT, "zone2");

                test(ref r, 6, 2, 3, 3);
            }
            finally
            {
                w.Close();
            }
        }

        [TestMethod]
        public void FSH_Remove()
        {
            Stream s = new MemoryStream();

            BinaryWriter w = new BinaryWriter(s);
            BinaryReader r = new BinaryReader(s);
            try
            {
                init(ref w);

                structureHelper.RewriteMarkers(ref w, -5, FileStructureHelper.ACTION_DELETE, "zone1");

                test(ref r, 5, 1, 0, 5);
            }
            finally
            {
                w.Close();
            }
        }

        [TestMethod]
        public void FSH_Add()
        {
            Stream s = new MemoryStream();

            BinaryWriter w = new BinaryWriter(s);
            BinaryReader r = new BinaryReader(s);
            try
            {
                init(ref w);

                structureHelper.RewriteMarkers(ref w, 5, FileStructureHelper.ACTION_ADD, "zone3");

                test(ref r, 15, 3, 5, 5);
            }
            finally
            {
                w.Close();
            }
        }
    }
}
