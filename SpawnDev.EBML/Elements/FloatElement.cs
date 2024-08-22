using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML.Elements
{
    public class FloatElement : BaseElement<double>
    {
        public const string TypeName  = "float";
        public override string DataString
        {
            get => Data.ToString();
            set
            {
                if (double.TryParse(value, out var v))
                {
                    Data = v;
                }
            }
        }
        public FloatElement(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public FloatElement(SchemaElement schemaElement, double value) : base(schemaElement, value) { }
        public FloatElement(SchemaElement schemaElement) : base(schemaElement, default) { }
        protected override void DataFromSegmentSource(ref double data) => data = EBMLConverter.ReadEBMLFloat(SegmentSource.ReadBytes(0, SegmentSource.Length, true));
        protected override void DataToSegmentSource(ref SegmentSource source) => source = new ByteSegment(EBMLConverter.ToFloatBytes(Data));
    }
}
