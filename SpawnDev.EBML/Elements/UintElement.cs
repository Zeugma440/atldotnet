using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML.Elements
{
    public class UintElement : BaseElement<ulong>
    {
        public const string TypeName = "uinteger";

        public override string DataString
        {
            get => Data.ToString();
            set
            {
                if (ulong.TryParse(value, out var v))
                {
                    Data = v;
                }
            }
        }
        public UintElement(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public UintElement(SchemaElement schemaElement, ulong value) : base(schemaElement, value) { }
        public UintElement(SchemaElement schemaElement) : base(schemaElement, default) { }
        protected override void DataFromSegmentSource(ref ulong data) => data = EBMLConverter.ReadEBMLUInt(SegmentSource.ReadBytes(0, SegmentSource.Length, true));
        protected override void DataToSegmentSource(ref SegmentSource source) => source = new ByteSegment(EBMLConverter.ToUIntBytes(Data));
    }
}
