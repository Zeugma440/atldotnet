using System;
using Commons;
using System.IO;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Represents a Broadcast Wave Format iXML metadata set
    /// </summary>
    internal static class IXmlTag
    {
        public const string CHUNK_IXML = "iXML";

        private static XmlArray createXmlArray()
        {
            var result = new XmlArray(
                "BWFXML",
                "ixml",
                e => e.EndsWith("LIST", StringComparison.OrdinalIgnoreCase),
                e => e.EndsWith("COUNT", StringComparison.OrdinalIgnoreCase)
            );
            return result;
        }

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            XmlArray xmlArray = createXmlArray();
            xmlArray.FromStream(source, meta, readTagParams, chunkSize);
        }

        public static bool IsDataEligible(MetaDataHolder meta)
        {
            return WavHelper.IsDataEligible(meta, "ixml.");
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataHolder meta)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_IXML));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            XmlArray xmlArray = createXmlArray();
            int result = xmlArray.ToStream(w, meta);

            long finalPos = w.BaseStream.Position;

            // Add the extra padding byte if needed
            long paddingSize = (finalPos - sizePos) % 2;
            if (paddingSize > 0) w.BaseStream.WriteByte(0);

            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            if (isLittleEndian) w.Write((int)(finalPos - sizePos - 4));
            else w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));

            return result;
        }
    }
}
