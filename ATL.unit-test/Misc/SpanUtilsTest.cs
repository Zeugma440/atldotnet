using Commons;
using System;
using System.Buffers.Binary;

namespace ATL.test
{
    [TestClass]
    public class SpanUtilsTest
    {
        [TestMethod]
        public void SpanUtils_EncodeDecodeSafeSynchInt()
        {   
            int test = 0x04ADD3AC;
            byte[] encoded = StreamUtils.EncodeSynchSafeInt32(test);
            Assert.AreEqual(0x2C, encoded[3]);
            Assert.AreEqual(0x27, encoded[2]);
            Assert.AreEqual(0x37, encoded[1]);
            Assert.AreEqual(0x25, encoded[0]);
            Assert.AreEqual(test, SpanUtils.DecodeSynchSafeInt32(encoded));
        }


        [TestMethod]
        public void SpanUtils_ReadBitsLE()
        {
            byte value1 = 0b10110110;
            byte value2 = 0b11010101;

            ReadOnlySpan<byte> span1 = new byte[] { value1 };
            ReadOnlySpan<byte> span2 = new byte[] { value2, value1 };
            ReadOnlySpan<byte> span4 = new byte[] { 0b110, 0b101, 0b001, 0b010 };
            ReadOnlySpan<byte> span5 = new byte[] { 0b111, 0b110, 0b101, 0b001, 0b010 };

            // One byte only
            Assert.AreEqual(2, SpanUtils.ReadBits(span1, 0, 2));
            Assert.AreEqual(6, SpanUtils.ReadBits(span1, 0, 3));
            Assert.AreEqual(6, SpanUtils.ReadBits(span1, 0, 4));
            Assert.AreEqual(182, SpanUtils.ReadBits(span1, 0, 8));

            // Check offset
            Assert.AreEqual(1, SpanUtils.ReadBits(span1, 2, 2));
            Assert.AreEqual(3, SpanUtils.ReadBits(span1, 4, 2));

            Assert.AreEqual(1, SpanUtils.ReadBits(span2, 4, 2));
            Assert.AreEqual(5, SpanUtils.ReadBits(span2, 4, 3));
            Assert.AreEqual(13, SpanUtils.ReadBits(span2, 4, 4));
            Assert.AreEqual(109, SpanUtils.ReadBits(span2, 4, 8)); // 0110 1101

            // Check reading multi-byte numbers
            Assert.AreEqual(46805, SpanUtils.ReadBits(span2, 0, 16));
            Assert.AreEqual(1543, SpanUtils.ReadBits(span5, 0, 16)); // 0b111 0b110, read in little-endian convention
            Assert.AreEqual(33621254, SpanUtils.ReadBits(span4, 0, 32));

            // Check sources longer than 4
            Assert.AreEqual(7, SpanUtils.ReadBits(span5, 0, 3));
        }

        [TestMethod]
        public void SpanUtils_ReadBitsBE()
        {
            ReadOnlySpan<byte> spanReal = new byte[] { 0b00010001, 0b10111000, 0b01010110, 0b11100101, 0b0 };
            Assert.AreEqual(2u, SpanUtils.ReadBitsBE(spanReal, 0, 5));
            Assert.AreEqual(3u, SpanUtils.ReadBitsBE(spanReal, 5, 4));

            ReadOnlySpan<byte> span4 = new byte[] { 0b110, 0b101, 0b001, 0b010 };
            ReadOnlySpan<byte> span5 = new byte[] { 0b111, 0b110, 0b101, 0b001, 0b010 };

            Assert.AreEqual(1798u, SpanUtils.ReadBitsBE(span5, 0, 16)); // 0b111 0b110, read in big-endian convention
            Assert.AreEqual(100991234u, SpanUtils.ReadBitsBE(span4, 0, 32));
        }

        [TestMethod]
        public void SpanUtils_BEUInt24Converters()
        {
            uint intValue = 0x00873529;

            Assert.AreEqual((uint)0x00FFFFFF, SpanUtils.DecodeBEUInt24(new byte[] { 0xFF, 0xFF, 0xFF }));

            byte[] byteValue = StreamUtils.EncodeBEUInt24(intValue);
            Assert.AreEqual(intValue, SpanUtils.DecodeBEUInt24(byteValue));
        }

        [TestMethod]
        public void SpanUtils_Exceptions()
        {
            try
            {
                SpanUtils.DecodeBEUInt24(new byte[2]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                SpanUtils.DecodeSynchSafeInt32(new byte[6]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }
        }
    }
}
