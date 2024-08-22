using System;

namespace SpawnDev.EBML
{
    /// <summary>
    /// Information about a document engine
    /// </summary>
    public class DocumentEngineInfo
    {
        /// <summary>
        /// Engine Type
        /// </summary>
        public Type EngineType { get; private set; }
        /// <summary>
        /// Engine factory that will be called when the engine is being attached to a new Document
        /// </summary>
        private Func<Document, DocumentEngine>? Factory { get; set; }
        /// <summary>
        /// Creates a new engine info
        /// </summary>
        /// <param name="type"></param>
        /// <param name="factory"></param>
        public DocumentEngineInfo(Type type, Func<Document, DocumentEngine>? factory = null)
        {
            EngineType = type;
            Factory = factory;
        }
        /// <summary>
        /// Creates a new instance of the document engine
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public DocumentEngine Create(Document doc)
        {
            return Factory != null ? Factory(doc) : (DocumentEngine)Activator.CreateInstance(EngineType, new object?[] { doc })!;
        }
    }
}
