using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML.Elements
{
    public class IntElement : BaseElement<long>
    {
        public const string TypeName = "integer";
        public override string DataString
        {
            get => Data.ToString();
            set
            {
                if (long.TryParse(value, out var v))
                {
                    Data = v;
                }
            }
        }
        public IntElement(SchemaElement schemaElement , SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public IntElement(SchemaElement schemaElement, long value) : base(schemaElement, value) { }
        public IntElement(SchemaElement schemaElement) : base(schemaElement, default) { }
        protected override void DataFromSegmentSource(ref long data) => data = EBMLConverter.ReadEBMLInt(SegmentSource.ReadBytes(0, SegmentSource.Length, true));
        protected override void DataToSegmentSource(ref SegmentSource source) => source = new ByteSegment(EBMLConverter.ToIntBytes(Data));
    }
}
