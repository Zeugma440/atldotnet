using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpawnDev.EBML.Elements;
using System.Reflection;

namespace SpawnDev.EBML
{
    public class EBMLSchemaSet
    {
        public const string EBML = "ebml";
        public Dictionary<string, EBMLSchema> Schemas { get; } = new Dictionary<string, EBMLSchema>();
        public List<EBMLSchema> ParseXML(string xml)
        {
            var schemas = EBMLSchema.FromXML(xml);
            foreach (var schema in schemas)
            {
                Schemas[schema.DocType] = schema;
            }
            return schemas;
        }
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
                default: return null;
            }
        }
        public Dictionary<ulong, EBMLSchemaElement> GetElements(string docType = EBML)
        {
            var ret = docType != EBML && Schemas.TryGetValue(EBML, out var ebmlSchema) ? new Dictionary<ulong, EBMLSchemaElement>(ebmlSchema.Elements) : new Dictionary<ulong, EBMLSchemaElement>();
            if (!string.IsNullOrEmpty(docType) && Schemas.TryGetValue(docType, out var schema))
            {
                foreach (var kvp in schema.Elements)
                {
                    ret[kvp.Key] = kvp.Value;
                }
            }
            return ret;
        }
        public EBMLSchemaElement? GetEBMLSchemaElement(ulong id, string docType = EBML)
        {
            if (!string.IsNullOrEmpty(docType) && Schemas.TryGetValue(docType, out var schema) && schema.Elements.TryGetValue(id, out var element)) return element;
            return docType != EBML ? GetEBMLSchemaElement(id) : null;
        }
        public EBMLSchemaElement? GetEBMLSchemaElement(string name, string docType = EBML)
        {
            if (!string.IsNullOrEmpty(docType) && Schemas.TryGetValue(docType, out var schema))
            {
                var tmp = schema.Elements.Values.FirstOrDefault(o => o.Name == name);
                if (tmp != null) return tmp;
            }
            return docType != EBML ? GetEBMLSchemaElement(name) : null;
        }
        /// <summary>
        /// Returns true if the MasterElement can contain the schema element
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="schemaElement"></param>
        /// <returns></returns>
        public bool CheckParent(MasterElement? parent, EBMLSchemaElement? schemaElement)
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
        public List<MasterElement> CheckParents(List<MasterElement> parents, EBMLSchemaElement? schemaElement)
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
        public List<EBMLSchema> LoadExecutingAssemblyEmbeddedSchemaXMLs(Func<string, bool>? predicate = null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            return LoadEmbeddedSchemaXMLs(assembly, predicate);
        }
        public List<EBMLSchema> LoadCallingAssemblyEmbeddedSchemaXMLs(Func<string, bool>? predicate = null)
        {
            var assembly = Assembly.GetCallingAssembly();
            return LoadEmbeddedSchemaXMLs(assembly, predicate);
        }
        public List<EBMLSchema> LoadEmbeddedSchemaXMLs(Assembly assembly, Func<string, bool>? predicate = null)
        {
            var ret = new List<EBMLSchema>();
            var resourceNames = GetEmbeddedSchemasXMLResourceNames(assembly);
            if (predicate != null) resourceNames = resourceNames.Where(predicate).ToArray();
            foreach (var resourceName in resourceNames)
            {
                var tmp = LoadEmbeddedSchemaXML(assembly, resourceName);
                ret.AddRange(tmp);
            }
            return ret;
        }
        public List<EBMLSchema> LoadEmbeddedSchemaXML(Assembly assembly, string resourceName)
        {
            var xml = ReadEmbeddedResourceString(assembly, resourceName);
            return string.IsNullOrEmpty(xml) ? new List<EBMLSchema>() : ParseXML(xml);
        }
        public List<EBMLSchema> LoadExecutingAssemblyEmbeddedSchemaXML(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            return LoadEmbeddedSchemaXML(assembly, resourceName);
        }
        public string[] GetExecutingAssemblyEmbeddedSchemasXMLResourceNames()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return GetEmbeddedSchemasXMLResourceNames(assembly);
        }
        public List<EBMLSchema> LoadCallingAssemblyEmbeddedSchemaXML(string resourceName)
        {
            var assembly = Assembly.GetCallingAssembly();
            return LoadEmbeddedSchemaXML(assembly, resourceName);
        }
        public string[] GetCallingAssemblyEmbeddedSchemasXMLResourceNames()
        {
            var assembly = Assembly.GetCallingAssembly();
            return GetEmbeddedSchemasXMLResourceNames(assembly);
        }
        public string[] GetEmbeddedSchemasXMLResourceNames(Assembly assembly)
        {
            var ret = new List<string>();
            var temp = assembly.GetManifestResourceNames();
            foreach (var name in temp)
            {
                if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
                ret.Add(name);
            }
            return ret.ToArray();
        }
        public string? ReadEmbeddedResourceString(Assembly assembly, string resourceName)
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
        /// <summary>
        /// Parses a stream returning EBML documents as they are found
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public IEnumerable<EBMLDocument> Parse(Stream stream)
        {
            var startPos = stream.Position;
            while (startPos < stream.Length)
            {
                stream.Position = startPos;
                var doc = new EBMLDocument(stream, this);
                if (doc.Data.Count() == 0)
                {
                    yield break;
                }
                var docSize = doc.TotalSize;
                startPos = startPos + (long)docSize;
                yield return doc;
            }
        }
        private List<EBMLDocumentEngineInfo> _EBMLDocumentEngines { get; } = new List<EBMLDocumentEngineInfo>();
        /// <summary>
        /// Document engines can handle document events and provide additional functionality
        /// </summary>
        public IEnumerable<EBMLDocumentEngineInfo> EBMLDocumentEngines => _EBMLDocumentEngines;
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine(Type engineType) 
        {
            var ebmlDocumentParserInfo = new EBMLDocumentEngineInfo(engineType);
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine<TEBMLDocumentEngine>() where TEBMLDocumentEngine : EBMLDocumentEngine
        {
            var ebmlDocumentParserInfo = new EBMLDocumentEngineInfo(typeof(TEBMLDocumentEngine));
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine<TEBMLDocumentEngine>(Func<EBMLDocument, EBMLDocumentEngine> factory) where TEBMLDocumentEngine : EBMLDocumentEngine
        {
            var ebmlDocumentParserInfo = new EBMLDocumentEngineInfo(typeof(TEBMLDocumentEngine), factory);
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
        /// <summary>
        /// Register a document engine that can handle document events and provide additional tools for a document
        /// </summary>
        public void RegisterDocumentEngine(Type engineType, Func<EBMLDocument, EBMLDocumentEngine> factory) 
        {
            var ebmlDocumentParserInfo = new EBMLDocumentEngineInfo(engineType, factory);
            _EBMLDocumentEngines.Add(ebmlDocumentParserInfo);
        }
    }
}
