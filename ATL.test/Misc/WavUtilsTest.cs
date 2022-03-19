using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;
using System.Text;
using System.IO;
using ATL.AudioData.IO;
using System.Collections.Generic;

namespace ATL.test
{
    [TestClass]
    public class WavUtilsTest
    {
        [TestMethod]
        public void WavUtils_writeFixedFieldTextValue()
        {
            IDictionary<string, string> additionalFields = new Dictionary<string, string>();
            additionalFields.Add("aaa", "bbb");

            using (MemoryStream stream = new MemoryStream(20))
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                // Init stream values
                for (int i = 0; i < 20; i++) w.Write((byte)0);

                // Write test values to stream
                stream.Seek(0, SeekOrigin.Begin);
                WavHelper.writeFixedFieldTextValue("aaa", 3, additionalFields, w, 1);
                WavHelper.writeFixedFieldTextValue("ccc", 2, additionalFields, w, 1);

                // Test expected result
                stream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 3; i++) Assert.AreEqual(0x62, stream.ReadByte());
                for (int i = 0; i < 2; i++) Assert.AreEqual(1, stream.ReadByte());

                for (long i = stream.Position; i < 20; i++) Assert.AreEqual(0, stream.ReadByte());
            }
        }

        [TestMethod]
        public void WavUtils_writeFieldIntValue()
        {
            IDictionary<string, string> additionalFields = new Dictionary<string, string>();
            additionalFields.Add("aaa", "2");

            using (MemoryStream stream = new MemoryStream(20))
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                // Init stream values
                for (int i = 0; i < 20; i++) w.Write((byte)0);

                // Write test values to stream
                stream.Seek(0, SeekOrigin.Begin);
                WavHelper.writeFieldIntValue("aaa", additionalFields, w, (byte)1);

                WavHelper.writeFieldIntValue("ccc", additionalFields, w, (byte)1);
                WavHelper.writeFieldIntValue("ccc", additionalFields, w, (sbyte)1);
                WavHelper.writeFieldIntValue("ccc", additionalFields, w, (short)1);
                WavHelper.writeFieldIntValue("ccc", additionalFields, w, (ushort)1);
                WavHelper.writeFieldIntValue("ccc", additionalFields, w, 1);
                WavHelper.writeFieldIntValue("ccc", additionalFields, w, (ulong)1);

                // Test expected result
                stream.Seek(0, SeekOrigin.Begin);
                byte[] data = new byte[8];
                Assert.AreEqual(2, stream.ReadByte());
                Assert.AreEqual(1, stream.ReadByte());
                Assert.AreEqual(1, stream.ReadByte());
                stream.Read(data, 0, 2);
                Assert.AreEqual(1, StreamUtils.DecodeInt16(data));
                stream.Read(data, 0, 2);
                Assert.AreEqual(1, StreamUtils.DecodeUInt16(data));
                stream.Read(data, 0, 4);
                Assert.AreEqual(1, StreamUtils.DecodeInt32(data));
                stream.Read(data, 0, 8);
                Assert.AreEqual((ulong)1, StreamUtils.DecodeUInt64(data));

                for (long i = stream.Position; i < 20; i++) Assert.AreEqual(0, stream.ReadByte());
            }
        }

        [TestMethod]
        public void WavUtils_writeField100DecimalValue()
        {
            IDictionary<string, string> additionalFields = new Dictionary<string, string>();
            additionalFields.Add("aaa", (252.0/100).ToString());

            using (MemoryStream stream = new MemoryStream(20))
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                // Init stream values
                for (int i = 0; i < 20; i++) w.Write((byte)0);

                // Write test values to stream
                stream.Seek(0, SeekOrigin.Begin);
                WavHelper.writeField100DecimalValue("aaa", additionalFields, w, (short)1);

                WavHelper.writeField100DecimalValue("ccc", additionalFields, w, (short)1);

                // Test expected result
                stream.Seek(0, SeekOrigin.Begin);
                byte[] data = new byte[8];
                stream.Read(data, 0, 2);
                Assert.AreEqual(252, StreamUtils.DecodeInt16(data));
                stream.Read(data, 0, 2);
                Assert.AreEqual(1, StreamUtils.DecodeUInt16(data));

                for (long i = stream.Position; i < 20; i++) Assert.AreEqual(0, stream.ReadByte());
            }
        }
    }
}
