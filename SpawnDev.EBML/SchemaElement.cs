using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpawnDev.EBML
{
    /// <summary>
    /// Defines an EBML schema element<br/>
    /// https://github.com/ietf-wg-cellar/ebml-specification/blob/master/specification.markdown#element-element
    /// </summary>
    public class SchemaElement
    {
        /// <summary>
        /// Within an EBML Schema, the XPath of the @name attribute is /EBMLSchema/element/@name.<br/>
        /// The name provides the human-readable name of the EBML Element.The value of the name MUST be in the form of characters "A" to "Z", "a" to "z", "0" to "9", "-", and ".". The first character of the name MUST be in the form of an "A" to "Z", "a" to "z", or "0" to "9" character.<br/>
        /// The name attribute is REQUIRED.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The path defines the allowed storage locations of the EBML Element within an EBML Document. This path MUST be defined with the full hierarchy of EBML Elements separated with a \. The top EBML Element in the path hierarchy is the first in the value. The syntax of the path attribute is defined using this Augmented Backus-Naur Form (ABNF) [@!RFC5234] with the case-sensitive update [@!RFC7405] notation:
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// The DocType this element is defined for
        /// </summary>
        public string DocType { get; set; }
        /// <summary>
        /// The Element ID is encoded as a Variable-Size Integer. It is read and stored in big-endian order. In the EBML Schema, it is expressed in hexadecimal notation prefixed by a 0x. To reduce the risk of false positives while parsing EBML Streams, the Element IDs of the Root Element and Top-Level Elements SHOULD be at least 4 octets in length. Element IDs defined for use at Root Level or directly under the Root Level MAY use shorter octet lengths to facilitate padding and optimize edits to EBML Documents; for instance, the Void Element uses an Element ID with a length of one octet to allow its usage in more writing and editing scenarios.
        /// </summary>
        public ulong Id { get; set; }
        /// <summary>
        /// The type MUST be set to one of the following values: integer (signed integer), uinteger (unsigned integer), float, string, date, utf-8, master, or binary. The content of each type is defined in (#ebml-element-types).
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// If an Element is mandatory (has a minOccurs value greater than zero) but not written within its Parent Element or stored as an Empty Element, then the EBML Reader of the EBML Document MUST semantically interpret the EBML Element as present with this specified default value for the EBML Element. An unwritten mandatory Element with a declared default value is semantically equivalent to that Element if written with the default value stored as the Element Data. EBML Elements that are Master Elements MUST NOT declare a default value. EBML Elements with a minOccurs value greater than 1 MUST NOT declare a default value.
        /// </summary>
        public string? Default { get; set; }
        /// <summary>
        /// A numerical range for EBML Elements that are of numerical types (Unsigned Integer, Signed Integer, Float, and Date). If specified, the value of the EBML Element MUST be within the defined range. See (#expression-of-range) for rules applied to expression of range values.
        /// </summary>
        public string? Range { get; set; }
        /// <summary>
        /// The minver (minimum version) attribute stores a nonnegative integer that represents the first version of the docType to support the EBML Element.
        /// </summary>
        public string? MinVer { get; set; }
        /// <summary>
        /// The maxver (maximum version) attribute stores a nonnegative integer that represents the last or most recent version of the docType to support the element. maxver MUST be greater than or equal to minver.
        /// </summary>
        public string? MaxVer { get; set; }
        /// <summary>
        /// This attribute is a boolean to express whether or not an EBML Element is defined as an Identically Recurring Element; see (#identically-recurring-elements).
        /// </summary>
        public bool Recurring { get; set; }
        /// <summary>
        /// This attribute is a boolean to express whether an EBML Element is permitted to be stored recursively. If it is allowed, the EBML Element MAY be stored within another EBML Element that has the same Element ID, which itself can be stored in an EBML Element that has the same Element ID, and so on. EBML Elements that are not Master Elements MUST NOT set recursive to true.
        /// </summary>
        public bool Recursive { get; set; }
        /// <summary>
        /// The length attribute is a value to express the valid length of the Element Data as written, measured in octets. The length provides a constraint in addition to the Length value of the definition of the corresponding EBML Element Type. This length MUST be expressed as either a nonnegative integer or a range (see (#expression-of-range)) that consists of only nonnegative integers and valid operators.
        /// </summary>
        public string? Length { get; set; }
        /// <summary>
        /// The position the element must be set to<br/>
        /// == -1 - last element<br/>
        /// >=  0 - first element<br/>
        /// If multiple elements exist in the same container with the same position value the greater PositionWeight will gt the Position and the others will be next to<br/>
        /// Non-standard metadata added by Todd Tanner
        /// </summary>
        public int? Position { get; set; }
        /// <summary>
        /// If two elements are defined to have the same position value, the element with the greater weight will get the position
        /// </summary>
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
        /// <summary>
        /// Returns the first definition
        /// </summary>
        public string Definition => Definitions.Values.FirstOrDefault() ?? "";
        /// <summary>
        /// The minimum depth this element must have
        /// </summary>
        public int MinDepth { get; set; } = 0;
        /// <summary>
        /// If true, this element is a global element (does not have a specific parent type)
        /// </summary>
        public bool IsGlobal { get; set; } = false;
        /// <summary>
        /// Element definitions (informative)
        /// </summary>
        public Dictionary<string, string> Definitions { get; } = new Dictionary<string, string>();
        /// <summary>
        /// Creates a new SchemaElement
        /// </summary>
        /// <param name="docType"></param>
        /// <param name="id"></param>
        /// <param name="node"></param>
        public SchemaElement(string docType, ulong id, XElement node)
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
            var posStr = node.Attribute("position")?.Value;
            Position = string.IsNullOrEmpty(posStr) ? 0 : int.Parse(posStr);
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
