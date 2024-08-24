using Commons;
using System.Text;
using ATL.AudioData;
using System.Reflection.PortableExecutable;

namespace ATL.test
{
    [TestClass]
    public class EBMLTest
    {
        private void EBML_decodeVint(byte[] binary, int result)

        {
            EBMLReader reader = new EBMLReader(new MemoryStream(binary));
            Assert.AreEqual(result, reader.readVint());
        }

        [TestMethod]
        public void EBML_DecodeVint()
        {
            EBML_decodeVint(new byte[] { 0x82 }, 2);
            EBML_decodeVint(new byte[] { 0x40, 0x02 }, 2);
            EBML_decodeVint(new byte[] { 0x20, 0x00, 0x02 }, 2);
            EBML_decodeVint(new byte[] { 0x10, 0x00, 0x00, 0x02 }, 2);
            EBML_decodeVint(new byte[] { 0x40, 0x7F }, 127);
            EBML_decodeVint(new byte[] { 0x41, 0xA6 }, 422);
            EBML_decodeVint(new byte[] { 0x20, 0x3F, 0xFF }, 16383);

            // Unknown size
            EBML_decodeVint(new byte[] { 0xFF }, -1);
            EBML_decodeVint(new byte[] { 0x7F, 0xFF }, -1);
        }

        private void EBML_encodeVint(byte[] expected, byte[] result)

        {
            Assert.AreEqual(expected.Length, result.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], result[i]);
            }
        }

        [TestMethod]
        public void EBML_EncodeVint()
        {
            EBML_encodeVint(new byte[] { 0b10000001 }, EBMLHelper.EncodeVint(1));
            EBML_encodeVint(new byte[] { 0b10000011 }, EBMLHelper.EncodeVint(3));
            EBML_encodeVint(new byte[] { 0x40, 0x7F }, EBMLHelper.EncodeVint(127));
            EBML_encodeVint(new byte[] { 0x41, 0xA6 }, EBMLHelper.EncodeVint(422));
            EBML_encodeVint(new byte[] { 0x20, 0x3F, 0xFF }, EBMLHelper.EncodeVint(16383));
            EBML_encodeVint(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, 0xFF }, EBMLHelper.EncodeVint(16383, false));
            EBML_encodeVint(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7F }, EBMLHelper.EncodeVint(127, false));
        }
    }
}
