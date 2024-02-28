using System;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    internal static class XmpTag
    {
        public const string CHUNK_XMP = "_PMX";
        public const string UUID_XMP = "BE7ACFCB97A942E89C71999491E3AFAC"; // XMP data unique ID

        // Keys that will always be serialized as attributes
        private static readonly ISet<string> ATTRIBUTES = new HashSet<string>
        {
            "x:xmptk",
            "xml:lang",
            "rdf:about",
            "rdf:id",
            "rdf:nodeId",
            "rdf:dataType",
            "rdf:parseType"
        };

        // Default namespaces
        private static readonly IDictionary<string, string> DEFAULT_NAMESPACES = new Dictionary<string, string>
        {
            {"x", "adobe:ns:meta/"},
            {"rdf","http://www.w3.org/1999/02/22-rdf-syntax-ns#"},
            {"xmp","http://ns.adobe.com/xap/1.0/"},
            {"xmpDM","http://ns.adobe.com/xmp/1.0/DynamicMedia/"},
            {"stDim","http://ns.adobe.com/xap/1.0/sType/Dimensions#"},
            {"tiff","http://ns.adobe.com/tiff/1.0/"},
            {"xmpMM","http://ns.adobe.com/xap/1.0/mm/"},
            {"stEvt","http://ns.adobe.com/xap/1.0/sType/ResourceEvent#"},
            {"stRef","http://ns.adobe.com/xap/1.0/sType/ResourceRef#"},
            {"dc","http://purl.org/dc/elements/1.1/"},
            {"tmi", "http://titus.com/tmi/1.0/"}
        };


        private static XmlArray createXmlArray()
        {
            var result = new XmlArray(
                "x:xmpmeta",
                "xmp",
                e => (
                    e.Equals("RDF:BAG", StringComparison.OrdinalIgnoreCase)
                    || e.Equals("RDF:SEQ", StringComparison.OrdinalIgnoreCase)
                    || e.Equals("RDF:ALT", StringComparison.OrdinalIgnoreCase)
                ),
                _ => false
            );
            result.setStructuralAttributes(ATTRIBUTES);
            result.setDefaultNamespaces(DEFAULT_NAMESPACES);
            return result;
        }

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            XmlArray xmlArray = createXmlArray();
            xmlArray.FromStream(source, meta, readTagParams, chunkSize);
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            return WavHelper.IsDataEligible(meta, "xmp.");
        }

        public static int ToStream(BinaryWriter w, MetaDataIO meta, bool isLittleEndian = false, bool wavEmbed = false)
        {
            long sizePos = w.BaseStream.Position;
            // Placeholder for chunk size that will be rewritten at the end of the method
            if (wavEmbed) w.Write(0);

            XmlArray xmlArray = createXmlArray();
            int result = xmlArray.ToStream(w, meta);

            if (wavEmbed) // Add the extra padding byte if needed
            {
                long finalPos = w.BaseStream.Position;
                long paddingSize = (finalPos - sizePos) % 2;
                if (paddingSize > 0) w.BaseStream.WriteByte(0);

                w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                if (isLittleEndian) w.Write((int)(finalPos - sizePos - 4));
                else w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));
            }

            return result;
        }

    }
}
