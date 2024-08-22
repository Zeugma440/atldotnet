using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpawnDev.EBML.Elements;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML
{
    /// <summary>
    /// An EBML document
    /// </summary>
    public class Document : MasterElement, IDisposable
    {
        /// <summary>
        /// Returns tru if this element is a document
        /// </summary>
        public override bool IsDocument { get; } = true;
        /// <summary>
        /// Element path
        /// </summary>
        public override string Path { get; } = "\\";
        /// <summary>
        /// Get or set the Filename. Used for informational purposes only.
        /// </summary>
        public string Filename { get; set; } = "";
        /// <summary>
        /// Returns the EBML header or null if not found
        /// </summary>
        public MasterElement? Header => GetContainer("EBML");
        /// <summary>
        /// Returns \EBML\DocType or null
        /// </summary>
        public override string DocType => Header?.ReadString("DocType") ?? EBMLParser.EBML;
        /// <summary>
        /// Returns the EBML body or null if not found<br/>
        /// EBML body refers to the first element that is not the EBML element, usually right after the EBML element
        /// </summary>
        public MasterElement? Body => Data.FirstOrDefault(o => o.Name != "EBML" && o is MasterElement) as MasterElement;
        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="schemas"></param>
        /// <param name="filename"></param>
        public Document(EBMLParser schemas, Stream stream, string? filename = null) : base(schemas, new StreamSegment(stream))
        {
            if (!string.IsNullOrEmpty(filename)) Filename = filename;
            OnChanged += Document_OnChanged;
            OnElementAdded += Document_OnElementAdded;
            OnElementRemoved += Document_OnElementRemoved;
            LoadEngines();
        }
        /// <summary>
        /// Creates a new instance
        /// </summary>
        public Document(EBMLParser schemas, SegmentSource segmentSource, string? filename = null) : base(schemas, segmentSource)
        {
            if (!string.IsNullOrEmpty(filename)) Filename = filename;
            OnChanged += Document_OnChanged;
            OnElementAdded += Document_OnElementAdded;
            OnElementRemoved += Document_OnElementRemoved;
            LoadEngines();
        }
        /// <summary>
        /// Creates a new instance
        /// </summary>
        public Document(EBMLParser schemas, string docType, string? filename = null) : base(schemas)
        {
            if (!string.IsNullOrEmpty(filename)) Filename = filename;
            CreateDocument(docType);
            OnChanged += Document_OnChanged;
            OnElementAdded += Document_OnElementAdded;
            OnElementRemoved += Document_OnElementRemoved;
            LoadEngines();
        }
        /// <summary>
        /// List of loaded document engines
        /// </summary>
        public Dictionary<DocumentEngineInfo, DocumentEngine> DocumentEngines { get; private set; } = new Dictionary<DocumentEngineInfo, DocumentEngine>();
        void LoadEngines()
        {
            var ret = new Dictionary<DocumentEngineInfo, DocumentEngine>();
            DocumentEngines = ret;
            foreach (var engineInfo in SchemaSet.DocumentEngines)
            {
                var engine = engineInfo.Create(this);
                ret.Add(engineInfo, engine);
            }
        }
        private void Document_OnChanged(IEnumerable<BaseElement> elements)
        {
            var element = elements.First();
            Console.WriteLine($"DOC: Document_OnChanged: {elements.Count()} {element.Name} {element.Path}");
            foreach (var el in elements)
            {
                if (el is MasterElement masterElement)
                {
                    var changed = masterElement.UpdateCRC();
                    if (changed)
                    {
                        // we modified an element, the OnChanged event will fire again
                        // the CRC check will be continued when it is fired next
                        break;
                    }
                }
            }
        }
        private void Document_OnElementAdded(MasterElement masterElement, BaseElement element)
        {
            Console.WriteLine($"DOC: Document_OnElementAdded: {element.Name} {element.Path}");
        }
        private void Document_OnElementRemoved(MasterElement masterElement, BaseElement element)
        {
            Console.WriteLine($"DOC: Document_OnElementRemoved: {element.Depth} {masterElement.Path}\\{element.Name}");
        }
        /// <summary>
        /// This initializes a very minimal EBML document based on the current DocType
        /// </summary>
        void CreateDocument(string docType)
        {
            // - create EBML header based on DocType
            var ebmlHeader = AddContainer("EBML")!;
            var strEl = ebmlHeader.AddString("DocType", docType);
            if (SchemaSet.Schemas.TryGetValue(docType, out var schema))
            {
                var version = uint.TryParse(schema.Version, out var ver) ? ver : 1;
                ebmlHeader.AddUint("DocTypeVersion", version);
                // Adds any required root level containers
                AddMissingContainers();
            }
        }
        /// <summary>
        /// Release resources
        /// </summary>
        public void Dispose()
        {
            var engines = DocumentEngines;
            DocumentEngines.Clear();
            foreach (var engine in engines.Values)
            {
                if (engine is IDisposable disposable) disposable.Dispose();
            }
        }
    }
}
