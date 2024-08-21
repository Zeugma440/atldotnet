using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SpawnDev.EBML
{
    public class EBMLSchema
    {
        public string DocType { get; private set; }
        public string? Version { get; private set; }
        public Dictionary<ulong, EBMLSchemaElement> Elements { get; } = new Dictionary<ulong, EBMLSchemaElement>();
        public EBMLSchema(string docType, string? version = null)
        {
            DocType = docType;
            Version = version;
        }
        public static List<EBMLSchema> FromXML(string xml)
        {
            var ret = new List<EBMLSchema>();
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
        public static EBMLSchema FromXML(XElement schemaRoot)
        {
            var nodes = schemaRoot.Elements();
            var docType = schemaRoot.Attribute("docType")!.Value;
            var version = schemaRoot.Attribute("version")?.Value;
            var ret = new EBMLSchema(docType, version);
            var tmp = new Dictionary<ulong, EBMLSchemaElement>();
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
                    var el = new EBMLSchemaElement(docType, id, node);
                    tmp.Add(el.Id, el);
                }
            }
            var list = tmp.ToList();
            list.Sort((pair1, pair2) => pair1.Value.Name.CompareTo(pair2.Value.Name));
            foreach (var item in list)
            {
                ret.Elements.Add(item.Key, item.Value);
            }
            return ret;
        }
    }
}
