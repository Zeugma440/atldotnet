using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpawnDev.EBML
{
    /// <summary>
    /// https://github.com/ietf-wg-cellar/ebml-specification/blob/master/specification.markdown#element-element
    /// </summary>
    public class EBMLSchemaElement
    {
        /// <summary>
        /// Within an EBML Schema, the XPath of the @name attribute is /EBMLSchema/element/@name.<br/>
        /// The name provides the human-readable name of the EBML Element.The value of the name MUST be in the form of characters "A" to "Z", "a" to "z", "0" to "9", "-", and ".". The first character of the name MUST be in the form of an "A" to "Z", "a" to "z", or "0" to "9" character.<br/>
        /// The name attribute is REQUIRED.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Within an EBML Schema, the XPath of the @path attribute is /EBMLSchema/element/@path.<br/>
        /// https://github.com/ietf-wg-cellar/ebml-specification/blob/master/specification.markdown#path
        /// </summary>
        public string Path { get; set; }
        public string DocType { get; set; }
        public ulong Id { get; set; }
        public string Type { get; set; }
        public string? Default { get; set; }
        public string? Range { get; set; }
        public string? MinVer { get; set; }
        public string? MaxVer { get; set; }
        public bool Recurring { get; set; }
        public bool Recursive { get; set; }
        public string? Length { get; set; }
        /// <summary>
        /// The position the element must be set to<br/>
        /// == -1 - last element<br/>
        /// >=  0 - first element<br/>
        /// If multiple elements exist in the same container with the same position value the greater PositionWeight will gt the Position and the others will be next to<br/>
        /// Non-standard metadata added by Todd Tanner
        /// </summary>
        public int? Position { get; set; }
        public int PositionWeight { get; set; }
        /// <summary>
        /// Within an EBML Schema, the XPath of the @minOccurs attribute is /EBMLSchema/element/@minOccurs.<br/>
        /// minOccurs is a nonnegative integer expressing the minimum permitted number of occurrences of this EBML Element within its Parent Element.
        /// </summary>
        public int MinOccurs { get; set; }
        /// <summary>
        /// Within an EBML Schema, the XPath of the @maxOccurs attribute is /EBMLSchema/element/@maxOccurs.<br/>
        /// maxOccurs is a nonnegative integer expressing the maximum permitted number of occurrences of this EBML Element within its Parent Element.
        /// </summary>
        public int MaxOccurs { get; set; }
        public string Definition => Definitions.Values.FirstOrDefault() ?? "";
        public int MinDepth { get; set; } = 0;
        public bool IsGlobal { get; set; } = false;
        public Dictionary<string, string> Definitions { get; } = new Dictionary<string, string>();
        public EBMLSchemaElement(string docType, ulong id, XElement node)
        {
            DocType = docType;
            Id = id;
            Name = node.Attribute("name")!.Value;
            Path = node.Attribute("path")!.Value;
            Type = node.Attribute("type")!.Value;
            Default = node.Attribute("default")?.Value;
            MinVer = node.Attribute("minVer")?.Value;
            MaxVer = node.Attribute("maxVer")?.Value;
            Recurring = node.Attribute("recurring")?.Value == "1";
            Recursive = node.Attribute("recursive")?.Value == "1";
            Length = node.Attribute("length")?.Value;// == null ? null : int.Parse(node.Attribute("length")!.Value);
            MaxOccurs = node.Attribute("maxOccurs")?.Value == null ? 0 : int.Parse(node.Attribute("maxOccurs")!.Value);
            MinOccurs = node.Attribute("minOccurs")?.Value == null ? 0 : int.Parse(node.Attribute("minOccurs")!.Value);
            Range = node.Attribute("range")?.Value;
            if (null == node.Attribute("position")?.Value) Position = null;
            else Position = int.Parse(node.Attribute("position")!.Value);

            PositionWeight = node.Attribute("positionWeight")?.Value == null ? 0 : int.Parse(node.Attribute("positionWeight")!.Value);
            var minDepthMatch = Regex.Match(Path, $@"^\\\(([0-9]+)-\\\){Name}$");
            if (minDepthMatch.Success)
            {
                var minDepthStr = minDepthMatch.Groups[1].Value;
                MinDepth = int.Parse(minDepthStr);
                IsGlobal = true;
            }
            else
            {
                IsGlobal = Regex.IsMatch(Path, $@"^\\\(-\\\){Name}$");
            }
            var childElements = node.Elements();
            foreach (var childEl in childElements)
            {
                if (childEl.Name.LocalName == "documentation" && childEl.Attribute("purpose")?.Value == "definition")
                {
                    var lang = childEl.Attribute("lang")?.Value ?? "";
                    Definitions[lang] = childEl.Value;
                }
            }
        }
    }
}
