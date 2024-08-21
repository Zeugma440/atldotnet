using System;

namespace SpawnDev.EBML
{
    public class EBMLDocumentEngineInfo
    {
        public Type EngineType { get; private set; }
        private Func<EBMLDocument, EBMLDocumentEngine>? Factory { get; set; }
        public EBMLDocumentEngineInfo(Type type, Func<EBMLDocument, EBMLDocumentEngine>? factory = null)
        {
            EngineType = type;
            Factory = factory;
        }
        public EBMLDocumentEngine Create(EBMLDocument doc)
        {
            return Factory != null ? Factory(doc) : (EBMLDocumentEngine)Activator.CreateInstance(EngineType, new object?[] { doc })!;
        }
    }
}
