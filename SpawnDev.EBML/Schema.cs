using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SpawnDev.EBML
{
    /// <summary>
    /// An EBML Schema is a well-formed XML Document [@!XML] that defines the properties, arrangement, and usage of EBML Elements that compose a specific EBML Document Type. The relationship of an EBML Schema to an EBML Document is analogous to the relationship of an XML Schema [@!XML-SCHEMA] to an XML Document [@!XML]. An EBML Schema MUST be clearly associated with one or more EBML Document Types. An EBML Document Type is identified by a string stored within the EBML Header in the DocType Element -- for example, Matroska or WebM (see (#doctype-element)). The DocType value for an EBML Document Type MUST be unique, persistent, and described in the IANA registry (see (#ebml-doctypes-registry)).
    /// </summary>
    public class Schema
    {
        /// <summary>
        /// Within an EBML Schema, the XPath of the @docType attribute is /EBMLSchema/@docType.<br/>
        /// The docType lists the official name of the EBML Document Type that is defined by the EBML Schema; for example, &lt;EBMLSchema docType="matroska">.<br/>
        /// The docType attribute is REQUIRED within the &lt;EBMLSchema> Element.
        /// </summary>
        public string DocType { get; private set; }
        /// <summary>
        /// Within an EBML Schema, the XPath of the @version attribute is /EBMLSchema/@version.
        /// </summary>
        public string? Version { get; private set; }
        /// <summary>
        /// Contains elements defined for this DocType in the parsed XML schema
        /// </summary>
        public Dictionary<ulong, SchemaElement> Elements { get; } = new Dictionary<ulong, SchemaElement>();
        /// <summary>
        /// Creates a new empty schema with the specified DocType
        /// </summary>
        /// <param name="docType"></param>
        /// <param name="version"></param>
        public Schema(string docType, string? version = null)
        {
            DocType = docType;
            Version = version;
        }
        /// <summary>
        /// Returns a list of Schemas found in the specified XML
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static List<Schema> FromXML(string xml)
        {
            var ret = new List<Schema>();
            var xdoc = XDocument.Parse(xml);
            var nodes = xdoc.Elements().ToList();
            foreach (var node in nodes)
            {
                if (node.Name.LocalName == "EBMLSchema")
                {
                    var schema = FromXML(node);
                    if (schema != null) ret.Add(schema);
                }
            }
            return ret;
        }
        /// <summary>
        /// Returns a Schema from the specified XElement
        /// </summary>
        /// <param name="schemaRoot"></param>
        /// <returns></returns>
        public static Schema FromXML(XElement schemaRoot)
        {
            var nodes = schemaRoot.Elements();
            var docType = schemaRoot.Attribute("docType")!.Value;
            var version = schemaRoot.Attribute("version")?.Value;
            var ret = new Schema(docType, version);
            var tmp = new Dictionary<ulong, SchemaElement>();
            foreach (var node in nodes)
            {
                if (node.Name.LocalName == "element")
                {
                    var idHex = node.Attribute("id")?.Value;
                    if (idHex == null) continue;
                    if (idHex.StartsWith("0x")) idHex = idHex.Substring(2);
                    var idBytes = HexMate.Convert.FromHexString(idHex).ToList();
                    idBytes.Reverse();
                    while (idBytes.Count < 8) idBytes.Add(0);
                    var id = BitConverter.ToUInt64(idBytes.ToArray());
                    var el = new SchemaElement(docType, id, node);
                    tmp.Add(el.Id, el);
                }
            }
            var list = tmp.ToList();
            list.Sort((pair1, pair2) => pair1.Value.Name.CompareTo(pair2.Value.Name));
            foreach(var item in list)
            {
                ret.Elements.Add(item.Key, item.Value);
            }
            return ret;
        }
    }
}
