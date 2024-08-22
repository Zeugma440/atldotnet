using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;
using System.Text;

namespace SpawnDev.EBML.Elements
{
    public class UTF8Element : BaseElement<string>
    {
        public const string TypeName  = "utf-8";
        public override string DataString
        {
            get => Data?.ToString() ?? "";
            set => Data = value ?? "";
        }
        public UTF8Element(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public UTF8Element(SchemaElement schemaElement, string value) : base(schemaElement, value) { }
        public UTF8Element(SchemaElement schemaElement) : base(schemaElement, string.Empty) { }
        protected override void DataFromSegmentSource(ref string data) => data = Encoding.UTF8.GetString(SegmentSource.ReadBytes(0, SegmentSource.Length, true));
        protected override void DataToSegmentSource(ref SegmentSource source) => source = new ByteSegment(Encoding.UTF8.GetBytes(Data));
    }
}
