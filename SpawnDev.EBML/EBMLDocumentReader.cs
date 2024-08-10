using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace SpawnDev.EBML
{
    public class DocTypeChangedEventArgs
    {
        /// <summary>
        /// The current EBMLElement
        /// </summary>
        public EBMLElement EBML { get; set; }
        /// <summary>
        /// Get or set the EBMLSchema that should be used for the given EBML document
        /// </summary>
        public EBMLSchema? Schema { get; set; }

    }
    /// <summary>
    /// A .Net EBML library for reading and writing EBML streams<br />
    /// https://github.com/ietf-wg-cellar/ebml-specification/blob/master/specification.markdown<br />
    /// </summary>
    public class EBMLDocumentReader : MasterElement
    {
        private Dictionary<string, EBMLSchema> _Schemas { get; } = new Dictionary<string, EBMLSchema>();
        public ReadOnlyCollection<EBMLSchema> Schemas => _Schemas.Values.ToList().AsReadOnly();

        public string DocType => EBML == null ? "" : EBML.DocType ?? "";
        /// <summary>
        /// Add an EBML schema for use when decoding EBML
        /// </summary>
        /// <param name="schema"></param>
        public void AddSchema(EBMLSchema schema) => _Schemas[schema.DocType] = schema;
        public void AddSchemas(List<EBMLSchema> schemas) => schemas.ForEach(AddSchema);
        public bool RemoveSchema(string docType) => _Schemas.Remove(docType);

        /// <summary>
        /// Called when an EBML element is loaded from the document<br />
        /// The EBMLSchema property Schema can be set based on the EBML element data
        /// </summary>
        public event EventHandler<DocTypeChangedEventArgs> OnEBMLFound;

        protected override void EBMLElementFound(EBMLElement ebml)
        {
            var args = new DocTypeChangedEventArgs
            {
                EBML = ebml,
                Schema = !string.IsNullOrEmpty(ebml.DocType) && _Schemas.TryGetValue(ebml.DocType, out var knownSchema) ? knownSchema : null,
            };
            OnEBMLFound?.Invoke(this, args);
            if (args.Schema != null)
            {
                _ActiveSchema = args.Schema;
            }
        }

        public EBMLElement? EBML => GetElement<EBMLElement>(ElementId.EBML);

        public EBMLDocumentReader(Stream? stream = null, List<EBMLSchema>? schemas = null) : base(ElementId.EBMLSource)
        {
            if (schemas != null)
            {
                AddSchemas(schemas);
            }
            if (stream != null)
            {
                if (stream is SegmentSource segmentSource)
                {
                    Stream = segmentSource;
                }
                else
                {
                    Stream = new StreamSegment(stream);
                }
            }
        }
    }
}
