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

        public static int ToStream(Stream w, bool isLittleEndian, MetaDataHolder meta)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_IXML));

            long sizePos = w.Position;
            w.Write(StreamUtils.EncodeInt32(0)); // Placeholder for chunk size that will be rewritten at the end of the method

            XmlArray xmlArray = createXmlArray();
            int result = xmlArray.ToStream(w, meta);

            long finalPos = w.Position;

            // Add the extra padding byte if needed
            long paddingSize = (finalPos - sizePos) % 2;
            if (paddingSize > 0) w.WriteByte(0);

            w.Seek(sizePos, SeekOrigin.Begin);
            w.Write(isLittleEndian
                ? StreamUtils.EncodeInt32((int)(finalPos - sizePos - 4))
                : StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4))
                );

            return result;
        }
    }
}
