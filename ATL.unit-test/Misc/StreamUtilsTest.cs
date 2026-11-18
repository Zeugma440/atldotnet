using Commons;
using System;
using System.Buffers.Binary;

namespace ATL.test
{
    [TestClass]
    public class StreamUtilsTest
    {
        [TestMethod]
        public void StreamUtils_CopyStream_limited()
        {
            IList<byte> list1 = new List<byte>();
            for (byte i = 0; i < 20; i++) list1.Add(i);

            IList<byte> list2 = new List<byte>();
            for (byte i = 0; i < 20; i++) list2.Add(i);

            using (MemoryStream stream1 = new MemoryStream(20))
            using (MemoryStream stream2 = new MemoryStream(20))
            {
                foreach (byte i in list1) stream1.WriteByte(i);
                foreach (byte i in list2) stream2.WriteByte(i);

                stream1.Seek(1, SeekOrigin.Begin);
                stream2.Seek(5, SeekOrigin.Begin);

                StreamUtils.CopyStreamAsync(stream1, stream2, 5).Wait();

                // Build expected result
                for (int i = 5; i < 10; i++) list2[i] = Convert.ToByte(i - 4);

                // Test expected result
                stream2.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 20; i++) Assert.AreEqual(list2[i], stream2.ReadByte());
            }
        }

        [TestMethod]
        public void StreamUtils_CopyStream_unlimited()
        {
            IList<byte> list1 = new List<byte>();
            for (byte i = 0; i < 20; i++) list1.Add(i);

            IList<byte> list2 = new List<byte>();
            for (byte i = 0; i < 20; i++) list2.Add(i);

            using (MemoryStream stream1 = new MemoryStream(20))
            using (MemoryStream stream2 = new MemoryStream(20))
            {
                foreach (byte i in list1) stream1.WriteByte(i);
                foreach (byte i in list2) stream2.WriteByte(i);

                stream1.Seek(1, SeekOrigin.Begin);
                stream2.Seek(5, SeekOrigin.Begin);

                StreamUtils.CopyStreamAsync(stream1, stream2).Wait();

                // Build expected result
                for (int i = 5; i < 20; i++) list2[i] = Convert.ToByte(i - 4);

                // Test expected result
                stream2.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 20; i++) Assert.AreEqual(list2[i], stream2.ReadByte());
            }
        }

        [TestMethod]
        public void StreamUtils_ShortenStream()
        {
            IList<byte> list = new List<byte>();
            for (byte i = 0; i < 13; i++) list.Add(i);

            using (MemoryStream stream = new MemoryStream(12))
            {
                foreach (byte i in list) stream.WriteByte(i);
                StreamUtils.ShortenStreamAsync(stream, 6, 2).Wait();
                list.RemoveAt(4);
                list.RemoveAt(4);
                stream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 11; i++) Assert.AreEqual(list[i], stream.ReadByte());
            }
        }

        [TestMethod]
        public void StreamUtils_LengthenStream()
        {
            IList<byte> list = new List<byte>();
            for (byte i = 0; i < 13; i++) list.Add(i);

            using (MemoryStream stream = new MemoryStream(12))
            {
                foreach (byte i in list) stream.WriteByte(i);
                StreamUtils.LengthenStreamAsync(stream, 6, 2, true).Wait();
                list.Insert(6, 0);
                list.Insert(6, 0);
                stream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 15; i++) Assert.AreEqual(list[i], stream.ReadByte());
            }

        }

        [TestMethod]
        public void StreamUtils_EncodeDecodeSafeSynchInt()
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            int ticks = rnd.Next(0, (int)(Math.Floor(Math.Pow(2, 28) / 2) - 1));

            // 4-byte synchsafe (28 bits)
            byte[] encoded = StreamUtils.EncodeSynchSafeInt(ticks, 4);
            int decoded = StreamUtils.DecodeSynchSafeInt(encoded);

            Assert.AreEqual(ticks, decoded);

            ticks = rnd.Next(0, (int)(Math.Floor(Math.Pow(2, 32) / 2) - 1));

            // 5-byte synchsafe (32 bits)
            encoded = StreamUtils.EncodeSynchSafeInt(ticks, 5);
            decoded = StreamUtils.DecodeSynchSafeInt(encoded);

            Assert.AreEqual(ticks, decoded);

            int test = 0x0000FFFF;
            encoded = StreamUtils.EncodeSynchSafeInt(test, 4);
            Assert.AreEqual(0x7F, encoded[3]);
            Assert.AreEqual(0x7F, encoded[2]);
            Assert.AreEqual(0x03, encoded[1]);
            Assert.AreEqual(0x00, encoded[0]);
            Assert.AreEqual(test, StreamUtils.DecodeSynchSafeInt(encoded));

            test = 0x04ADD3AC;
            encoded = StreamUtils.EncodeSynchSafeInt32(test);
            Assert.AreEqual(0x2C, encoded[3]);
            Assert.AreEqual(0x27, encoded[2]);
            Assert.AreEqual(0x37, encoded[1]);
            Assert.AreEqual(0x25, encoded[0]);
            Assert.AreEqual(test, StreamUtils.DecodeSynchSafeInt32(encoded));
        }

        [TestMethod]
        public void StreamUtils_FindSequence()
        {
            string sequence1 = "ftypmp42";
            string sequence2 = "trak";
            string sequence3 = "jstsd";

            using (FileStream fs = new FileStream(TestUtils.GetResourceLocationRoot() + "MP4/mp4.m4a", FileMode.Open, FileAccess.Read))
            {
                Assert.AreEqual(true, StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(sequence1)));
                Assert.AreEqual(12, fs.Position);

                Assert.AreEqual(true, StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(sequence2)));
                Assert.AreEqual(156, fs.Position);

                Assert.AreEqual(true, StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(sequence3)));
                Assert.AreEqual(416, fs.Position);

                fs.Seek(20, SeekOrigin.Begin);

                Assert.AreEqual(false, StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes(sequence3), 100));
            }
        }

        [TestMethod]
        public void StreamUtils_CopySameStream()
        {
            byte[] finalListForward = { 0, 1, 2, 3, 2, 3, 4, 5, 6, 7 };
            byte[] finalListBackward = { 0, 1, 4, 5, 6, 7, 8, 9, 8, 9 };

            using (MemoryStream stream = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }))
            {
                StreamUtils.CopySameStreamAsync(stream, 2, 4, 6, 3).Wait();
                Assert.IsTrue(finalListForward.SequenceEqual(stream.ToArray()));
            }

            using (MemoryStream stream = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }))
            {
                StreamUtils.CopySameStreamAsync(stream, 4, 2, 6, 3).Wait();
                Assert.IsTrue(finalListBackward.SequenceEqual(stream.ToArray()));
            }
        }

        [TestMethod]
        public void StreamUtils_ReadBitsLE()
        {
            byte value1 = 0b10110110;
            byte value2 = 0b11010101;

            ReadOnlySpan<byte> span1 = new byte[] { value1 };
            ReadOnlySpan<byte> span2 = new byte[] { value2, value1 };
            ReadOnlySpan<byte> span4 = new byte[] { 0b110, 0b101, 0b001, 0b010 };
            ReadOnlySpan<byte> span5 = new byte[] { 0b111, 0b110, 0b101, 0b001, 0b010 };

            // One byte only
            Assert.AreEqual(2, StreamUtils.ReadBits(span1, 0, 2));
            Assert.AreEqual(6, StreamUtils.ReadBits(span1, 0, 3));
            Assert.AreEqual(6, StreamUtils.ReadBits(span1, 0, 4));
            Assert.AreEqual(182, StreamUtils.ReadBits(span1, 0, 8));

            // Check offset
            Assert.AreEqual(1, StreamUtils.ReadBits(span1, 2, 2));
            Assert.AreEqual(3, StreamUtils.ReadBits(span1, 4, 2));

            Assert.AreEqual(1, StreamUtils.ReadBits(span2, 4, 2));
            Assert.AreEqual(5, StreamUtils.ReadBits(span2, 4, 3));
            Assert.AreEqual(13, StreamUtils.ReadBits(span2, 4, 4));
            Assert.AreEqual(109, StreamUtils.ReadBits(span2, 4, 8)); // 0110 1101

            // Check reading multi-byte numbers
            Assert.AreEqual(46805, StreamUtils.ReadBits(span2, 0, 16));
            Assert.AreEqual(1543, StreamUtils.ReadBits(span5, 0, 16)); // 0b111 0b110, read in little-endian convention
            Assert.AreEqual(33621254, StreamUtils.ReadBits(span4, 0, 32));

            // Check sources longer than 4
            Assert.AreEqual(7, StreamUtils.ReadBits(span5, 0, 3));
        }

        [TestMethod]
        public void StreamUtils_ReadBitsBE()
        {
            ReadOnlySpan<byte> spanReal = new byte[] { 0b00010001, 0b10111000, 0b01010110, 0b11100101, 0b0 };
            Assert.AreEqual(2u, StreamUtils.ReadBitsBE(spanReal, 0, 5));
            Assert.AreEqual(3u, StreamUtils.ReadBitsBE(spanReal, 5, 4));

            ReadOnlySpan<byte> span4 = new byte[] { 0b110, 0b101, 0b001, 0b010 };
            ReadOnlySpan<byte> span5 = new byte[] { 0b111, 0b110, 0b101, 0b001, 0b010 };

            Assert.AreEqual(1798u, StreamUtils.ReadBitsBE(span5, 0, 16)); // 0b111 0b110, read in big-endian convention
            Assert.AreEqual(100991234u, StreamUtils.ReadBitsBE(span4, 0, 32));
        }

        [TestMethod]
        public void StreamUtils_BEUInt24Converters()
        {
            uint intValue = 0x00873529;

            Assert.AreEqual((uint)0x00FFFFFF, StreamUtils.DecodeBEUInt24(new byte[] { 0xFF, 0xFF, 0xFF }));

            byte[] byteValue = StreamUtils.EncodeBEUInt24(intValue);
            Assert.AreEqual(intValue, StreamUtils.DecodeBEUInt24(byteValue));
        }

        [TestMethod]
        public void StreamUtils_BEInt16Converters()
        {
            short intValue = 0x3529;

            Assert.AreEqual((short)0x00FF, BinaryPrimitives.ReadInt16BigEndian(new byte[2] { 0x00, 0xFF }));

            byte[] byteValue = StreamUtils.EncodeBEInt16(intValue);
            Assert.AreEqual(intValue, BinaryPrimitives.ReadInt16BigEndian(byteValue));
        }

        [TestMethod]
        public void StreamUtils_LongConverters()
        {
            ulong longValue = 0x45976248;

            byte[] byteValue = BitConverter.GetBytes(longValue);
            Assert.AreEqual(longValue, BinaryPrimitives.ReadUInt64LittleEndian(byteValue));

            byteValue = StreamUtils.EncodeBEUInt64(longValue);
            Assert.AreEqual((long)longValue, BinaryPrimitives.ReadInt64BigEndian(byteValue));
        }

        [TestMethod]
        public void StreamUtils_Exceptions()
        {
            try
            {
                BinaryPrimitives.ReadUInt16BigEndian(new byte[1]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadUInt16LittleEndian(new byte[1]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadInt16LittleEndian(new byte[1]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadInt16BigEndian(new byte[1]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }


            try
            {
                StreamUtils.DecodeBEInt24(new byte[2]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.DecodeBEUInt24(new byte[2]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.EncodeBEUInt24(0x01FFFFFF);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }


            try
            {
                BinaryPrimitives.ReadUInt32BigEndian(new byte[3]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadUInt32LittleEndian(new byte[3]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadInt32BigEndian(new byte[3]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadInt32LittleEndian(new byte[3]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }


            try
            {
                BinaryPrimitives.ReadUInt64LittleEndian(new byte[7]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                BinaryPrimitives.ReadInt64BigEndian(new byte[7]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }


            try
            {
                StreamUtils.DecodeSynchSafeInt(new byte[6]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.DecodeSynchSafeInt32(new byte[6]);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.EncodeSynchSafeInt(1, 0);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.EncodeSynchSafeInt(1, 6);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.ReadBEBits(new MemoryStream(), 0, 0);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }

            try
            {
                StreamUtils.ReadBEBits(new MemoryStream(), 0, 33);
                Assert.Fail();
            }
            catch
            {
                // Nothing
            }
        }
    }
}
