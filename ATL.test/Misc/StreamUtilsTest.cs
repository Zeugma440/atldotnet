using Commons;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.test
{
    [TestClass]
    public class StreamUtilsTest
    {
        [TestMethod]
        public void StreamUtils_ShortenStream()
        {
            IList<byte> list = new List<byte>();
            for (byte i = 0; i < 13; i++) list.Add(i);

            using (MemoryStream stream = new MemoryStream(12))
            {
                foreach (byte i in list) stream.WriteByte(i);
                StreamUtils.ShortenStream(stream, 6, 2);
                list.RemoveAt(4);
                list.RemoveAt(4);
                stream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 11; i++) Assert.AreEqual(list[i],stream.ReadByte());
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
                StreamUtils.LengthenStream(stream, 6, 2, true);
                list.Insert(6, 0);
                list.Insert(6, 0);
                stream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 15; i++) Assert.AreEqual(list[i],stream.ReadByte());
            }

        }

        [TestMethod]
        public void StreamUtils_EncodeDecodeSafeSynchInt()
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            int ticks = rnd.Next(0, (int)(Math.Floor(Math.Pow(2, 28)/2)-1));

            // 4-byte synchsafe (28 bits)
            byte[] encoded = StreamUtils.EncodeSynchSafeInt(ticks,4);
            int decoded = StreamUtils.DecodeSynchSafeInt(encoded);

            Assert.AreEqual(ticks, decoded);

            ticks = rnd.Next(0, (int)(Math.Floor(Math.Pow(2, 32) / 2)-1));

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
            string sequence1 = "<ASX VERSION";
            string sequence2 = "$PATH/MID";

            FileStream fs = new FileStream(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.asx", FileMode.Open, FileAccess.Read);
            BinaryReader r = new BinaryReader(fs);

            try
            {
                Assert.AreEqual(true,StreamUtils.FindSequence(ref r, Utils.Latin1Encoding.GetBytes(sequence1)));
                Assert.AreEqual(12, fs.Position);

                Assert.AreEqual(true, StreamUtils.FindSequence(ref r, Utils.Latin1Encoding.GetBytes(sequence2)));
                Assert.AreEqual(261, fs.Position);

                fs.Seek(20, SeekOrigin.Begin);

                Assert.AreEqual(false, StreamUtils.FindSequence(ref r, Utils.Latin1Encoding.GetBytes(sequence2),true, 100));

                Assert.AreEqual(true, StreamUtils.FindSequence(ref r, Utils.Latin1Encoding.GetBytes(sequence1), false));
                Assert.AreEqual(12, fs.Position);
            }
            finally
            {
                r.Close();
            }
        }

        [TestMethod]
        public void StreamUtils_CopySameStream()
        {
            byte[] finalListForward =       new byte[10] { 0, 1, 2, 3, 2, 3, 4, 5, 6, 7 };
            byte[] finalListBackward =      new byte[10] { 0, 1, 4, 5, 6, 7, 8, 9, 8, 9 };

            using (MemoryStream stream = new MemoryStream(new byte[10] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }))
            {
                StreamUtils.CopySameStream(stream, 2, 4, 6, 3);
                Assert.IsTrue(StreamUtils.ArrEqualsArr(finalListForward, stream.ToArray()));
            }

            using (MemoryStream stream = new MemoryStream(new byte[10] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }))
            {
                StreamUtils.CopySameStream(stream, 4, 2, 6, 3);
                Assert.IsTrue(StreamUtils.ArrEqualsArr(finalListBackward, stream.ToArray()));
            }
        }
    }
}
