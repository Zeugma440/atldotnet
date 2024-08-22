using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;
using System;
using System.Linq;

namespace SpawnDev.EBML.Elements
{
    public class SimpleBlockElement : BlockElement
    {
        public const string TypeName = "mkvSimpleBlock";

        public SimpleBlockElement(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public SimpleBlockElement(SchemaElement schemaElement, byte[] value) : base(schemaElement, value) { }
        public SimpleBlockElement(SchemaElement schemaElement) : base(schemaElement, Array.Empty<byte>()) { }

        public bool Keyframe => (Flags & 128) != 0;
        public bool Discardable => (Flags & 1) != 0;
    }
}
