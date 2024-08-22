using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;
using System.Text;

namespace SpawnDev.EBML.Elements
{
    public class StringElement : BaseElement<string>
    {
        public const string TypeName = "string";
        public override string DataString
        {
            get => Data?.ToString() ?? "";
            set => Data = value ?? "";
        }
        public StringElement(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public StringElement(SchemaElement schemaElement, string value) : base(schemaElement, value) { }
        public StringElement(SchemaElement schemaElement) : base(schemaElement, string.Empty) { }
        protected override void DataFromSegmentSource(ref string data) => data = Encoding.UTF8.GetString(SegmentSource.ReadBytes(0, SegmentSource.Length, true));
        protected override void DataToSegmentSource(ref SegmentSource source) => source = new ByteSegment(Encoding.ASCII.GetBytes(Data));
    }
}
