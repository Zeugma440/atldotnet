using System;
using System.Collections.Generic;
using System.IO;
using Commons;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Represents an XMP ("Extensible Metadata Platform"; see ISO-16684–1) metadata set
    /// </summary>
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
            {"xml", "http://www.w3.org/XML/1998/namespace"},
            {"rdf","http://www.w3.org/1999/02/22-rdf-syntax-ns#"},
            {"xmp","http://ns.adobe.com/xap/1.0/"},
            {"xmpDM","http://ns.adobe.com/xmp/1.0/DynamicMedia/"},
            {"xmpRights","http://ns.adobe.com/xap/1.0/rights/"},
            {"tiff","http://ns.adobe.com/tiff/1.0/"},
            {"exif","http://ns.adobe.com/exif/1.0"},
            {"xmpMM","http://ns.adobe.com/xap/1.0/mm/"},
            {"stFnt","http://ns.adobe.com/xap/1.0/sType/Font#"},
            {"stDim","http://ns.adobe.com/xap/1.0/sType/Dimensions#"},
            {"stEvt","http://ns.adobe.com/xap/1.0/sType/ResourceEvent#"},
            {"stRef","http://ns.adobe.com/xap/1.0/sType/ResourceRef#"},
            {"Iptc4xmpCore","http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/"},
            {"Iptc4xmpExt","http://iptc.org/std/Iptc4xmpExt/2008-02-29/"},
            {"dc","http://purl.org/dc/elements/1.1/"},
            {"asf","http://ns.adobe.com/asf/1.0/"},
            {"tmi", "http://titus.com/tmi/1.0/"},
            {"photoshop","http://ns.adobe.com/photoshop/1.0/"},
            {"plus","http://ns.useplus.org/ldf/xmp/1.0/"},
            {"adid", "http://ns.ad-id.org/adid/1.0/"},
            {"tsc", "http://www.techsmith.com/xmp/tsc/"},
            {"tscDM", "http://www.techsmith.com/xmp/tscDM/"},
            {"tscSC", "http://www.techsmith.com/xmp/tsc/"},
            {"tscIQ", "http://www.techsmith.com/xmp/tscIQ/"},
            {"tscHS", "http://www.techsmith.com/xmp/tscHS/"},
        };

        // Namespace anchors
        private static readonly IDictionary<string, ISet<string>> NAMESPACE_ANCHORS = new Dictionary<string, ISet<string>>
        {
            { "x:xmpmeta", new HashSet<string>{ "x" } },
            { "rdf:RDF", new HashSet<string> { "" } }
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
            result.setNamespaceAnchors(NAMESPACE_ANCHORS);
            return result;
        }

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            XmlArray xmlArray = createXmlArray();
            xmlArray.FromStream(source, meta, readTagParams, chunkSize);
        }

        public static bool IsDataEligible(MetaDataHolder meta)
        {
            return WavHelper.IsDataEligible(meta, "xmp.");
        }

        public static int ToStream(Stream w, MetaDataHolder meta, bool isLittleEndian = false, bool wavEmbed = false)
        {
            if (wavEmbed) w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_XMP));

            long sizePos = w.Position;
            // Placeholder for chunk size that will be rewritten at the end of the method
            if (wavEmbed) w.Write(StreamUtils.EncodeInt32(0));

            XmlArray xmlArray = createXmlArray();
            int result = xmlArray.ToStream(w, meta);

            if (!wavEmbed) return result;

            // Add the extra padding byte if needed
            long finalPos = w.Position;
            long paddingSize = (finalPos - sizePos) % 2;
            if (paddingSize > 0) w.WriteByte(0);

            w.Seek(sizePos, SeekOrigin.Begin);
            w.Write(isLittleEndian
                ? StreamUtils.EncodeInt32((int)(finalPos - sizePos - 4))
                : StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));

            return result;
        }

    }
}
