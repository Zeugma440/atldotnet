using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpawnDev.EBML.Elements;
using System.Reflection;

namespace SpawnDev.EBML
{
    /// <summary>
    /// This class can parse EBML schema XML and use the parsed schemas to parse, edit, and create EBML documents
    /// </summary>
    public class EBMLParser
    {
        /// <summary>
        /// EBML string "ebml"
        /// </summary>
        public const string EBML = "ebml";
        /// <summary>
        /// Loaded schemas
        /// </summary>
        public Dictionary<string, Schema> Schemas { get; } = new Dictionary<string, Schema>();
        /// <summary>
        /// Document engines can handle document events and provide additional functionality<br/>
        /// For example:<br/>
        /// The included EBML document engine can keep CRC-32 elements up to date if a document is modified.<br/>
        /// The included Matroska document engine can auto-populate SeekHead elements and keep the data in a SeekHead element up to date if a document is modified.<br/>
        /// </summary>
        public IEnumerable<DocumentEngineInfo> DocumentEngines => _EBMLDocumentEngines;
        /// <summary>
        /// Create a new ShemaSet and load defaults parser configuration
        /// </summary>
        /// <param name="defaultConfig">If true, all included schemas and document engines will be loaded</param>
        public EBMLParser(bool defaultConfig = true)
        {
            if (defaultConfig)
            {
                LoadDefaultSchemas();
                RegisterDocumentEngine<MatroskaDocumentEngine>();
            }
        }
        /// <summary>
        /// Loads schema XMLs that are included with SpawnDev.EBML (currently ebml, matroska, and webm)
        /// </summary>
        /// <param name="predicate">Optional predicate for selective schema loading</param>
        /// <returns></returns>
        public List<Schema> LoadDefaultSchemas(Func<string, bool>? predicate = null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            return LoadEmbeddedSchemaXMLs(assembly, predicate);
        }
        /// <summary>
        /// Parses an xml document into a list of EBML schema
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public List<Schema> ParseSchemas(string xml)
        {
            var schemas = Schema.FromXML(xml);
            foreach (var schema in schemas)
            {
                Schemas[schema.DocType] = schema;
            }
            return schemas;
        }
        /// <summary>
        /// Parses a stream returning EBML documents as they are found<br/>
        /// </summary>
        /// <param name="stream">The stream to read EBMLDocuments from</param>
        /// <returns></returns>
        public IEnumerable<Document> ParseDocuments(Stream stream)
        {
            var startPos = stream.Position;
            while (startPos < stream.Length)
            {
                stream.Position = startPos;
                var doc = new Document(this, stream);
                if (!doc.Data.Any())
                {
                    yield break;
                }
                var docSize = doc.TotalSize;
                startPos = startPos + (long)docSize;
                yield return doc;
            }
        }
        /// <summary>
        /// parses a single EBML document from the stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public Document? ParseDocument(Stream stream)
        {
            var startPos = stream.Position;
            while (startPos < stream.Length)
            {
                stream.Position = startPos;
                var doc = new Document(this, stream);
                if (!doc.Data.Any())
                {
                    break;
                }
                var docSize = doc.TotalSize;
                startPos = startPos + (long)docSize;
                return doc;
            }
            return null;
        }
        /// <summary>
        /// Creates a new EBML document with the specified DocType
        /// </summary>
        /// <param name="docType"></param>
        /// <returns></returns>
        public Document CreateDocument(string docType) => new Document(this, docType);
        /// <summary>
        /// Returns the .Net that represents the specified elementType:<br/>
        /// - master<br/>
        /// - uinteger<br/>
        /// - integer<br/>
        /// - float<br/>
        /// - binary<br/>
        /// - string<br/>
        /// - utf-8<br/>
        /// - date<br/>
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public Type? GetElementType(string elementType)
        {
            switch (elementType)
            {
                case MasterElement.TypeName: return typeof(MasterElement);
                case UintElement.TypeName: return typeof(UintElement);
                case IntElement.TypeName: return typeof(IntElement);
                case FloatElement.TypeName: return typeof(FloatElement);
                case StringElement.TypeName: return typeof(StringElement);
                case UTF8Element.TypeName: return typeof(UTF8Element);
                case BinaryElement.TypeName: return typeof(BinaryElement);
                case DateElement.TypeName: return typeof(DateElement);
                case BlockElement.TypeName: return typeof(BlockElement);
                case SimpleBlockElement.TypeName: return typeof(SimpleBlockElement);
                default: return null;
            }
        }
        /// <summary>
        /// Returns a list of valid schema elements for a specified DocType
        /// </summary>
        /// <param name="docType"></param>
        /// <returns></returns>
        public Dictionary<ulong, SchemaElement> GetElements(string docType = EBML)
        {
            var ret = docType != EBML && Schemas.TryGetValue(EBML, out var ebmlSchema) ? new Dictionary<ulong, SchemaElement>(ebmlSchema.Elements) : new Dictionary<ulong, SchemaElement>();
            if (!string.IsNullOrEmpty(docType) && Schemas.TryGetValue(docType, out var schema))
            {
                foreach (var kvp in schema.Elements)
                {
                    ret[kvp.Key] = kvp.Value;
                }
            }
            return ret;
        }
        /// <summary>
        /// Returns the schema for the given element id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="docType"></param>
        /// <returns></returns>
        public SchemaElement? GetElement(ulong id, string docType = EBML)
        {
            if (!string.IsNullOrEmpty(docType) && Schemas.TryGetValue(docType, out var schema) && schema.Elements.TryGetValue(id, out var element)) return element;
            return docType != EBML ? GetElement(id) : null;
        }
        /// <summary>
        /// Returns the schema for the given element name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="docType"></param>
        /// <returns></returns>
        public SchemaElement? GetElement(string name, string docType = EBML)
        {
            if (!string.IsNullOrEmpty(docType) && Schemas.TryGetValue(docType, out var schema))
            {
                var tmp = schema.Elements.Values.FirstOrDefault(o => o.Name == name);
                if (tmp != null) return tmp;
            }
            return docType != EBML ? GetElement(name) : null;
        }
        /// <summary>
        /// Returns true if the MasterElement can contain the schema element
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="schemaElement"></param>
        /// <returns></returns>
        public bool CheckParent(MasterElement? parent, SchemaElement? schemaElement)
        {
            if (parent == null)
            {
                // must be a top-level allowed object
                return false;
            }
            if (schemaElement == null)
            {
                return false;
            }
            var elementName = schemaElement.Name;
            var parentPath = parent.Path;
            var parentMasterName = parent.Name;
            var path = $@"{parentPath.TrimEnd('\\')}\{elementName}";
            var depth = parent.Depth + 1;
            if (path == schemaElement.Path)
            {
                return true;
            }
            else if (schemaElement.MinDepth > depth)
            {
                return false;
            }
            else if (path == schemaElement.Path.Replace("+", ""))
            {
                // TODO - better check than this
                // this won't work for nested which is what + indicates is possible
                // Tags
                return true;
            }
            return schemaElement.IsGlobal;
        }
        /// <summary>
        /// Given a container chain (nested elements) the parents are checked starting from the last up to the first<br/>
        /// removing invalid parents until a valid parent is found<br/>
        /// Used to determine when an element of unknown size ends
        /// </summary>
        /// <param name="parents"></param>
        /// <param name="schemaElement"></param>
        /// <returns></returns>
        public List<MasterElement> CheckParents(List<MasterElement> parents, SchemaElement? schemaElement)
        {
            if (schemaElement == null)
            {
                return parents;
            }
            var tmp = parents.ToList();
            bool ret = false;
            while (!ret && tmp.Count > 0)
            {
                ret = CheckParent(tmp.LastOrDefault(), schemaElement);
                if (!ret) tmp.RemoveAt(tmp.Count - 1);
            }
            return tmp;
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine(Type engineType) 
        {
            var ebmlDocumentParserInfo = new DocumentEngineInfo(engineType);
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine<TEBMLDocumentEngine>() where TEBMLDocumentEngine : DocumentEngine
        {
            var ebmlDocumentParserInfo = new DocumentEngineInfo(typeof(TEBMLDocumentEngine));
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine<TEBMLDocumentEngine>(Func<Document, DocumentEngine> factory) where TEBMLDocumentEngine : DocumentEngine
        {
            var ebmlDocumentParserInfo = new DocumentEngineInfo(typeof(TEBMLDocumentEngine), factory);
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine(Type engineType, Func<Document, DocumentEngine> factory) 
        {
            var ebmlDocumentParserInfo = new DocumentEngineInfo(engineType, factory);
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        private List<Schema> LoadEmbeddedSchemaXMLs(Assembly assembly, Func<string, bool>? predicate = null)
        {
            var ret = new List<Schema>();
            var resourceNames = GetEmbeddedSchemasXMLResourceNames(assembly);
            if (predicate != null) resourceNames = resourceNames.Where(predicate).ToArray();
            foreach (var resourceName in resourceNames)
            {
                var tmp = LoadEmbeddedSchemaXML(assembly, resourceName);
                ret.AddRange(tmp);
            }
            return ret;
        }
        private List<Schema> LoadEmbeddedSchemaXML(Assembly assembly, string resourceName)
        {
            var xml = ReadEmbeddedResourceString(assembly, resourceName);
            return string.IsNullOrEmpty(xml) ? new List<Schema>() : ParseSchemas(xml);
        }
        private string[] GetEmbeddedSchemasXMLResourceNames(Assembly assembly)
        {
            var temp = assembly.GetManifestResourceNames();
            return temp.Where(name => name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        private string? ReadEmbeddedResourceString(Assembly assembly, string resourceName)
        {
            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }
        private List<DocumentEngineInfo> _EBMLDocumentEngines { get; } = new List<DocumentEngineInfo>();
    }
}
