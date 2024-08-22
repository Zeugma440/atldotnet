using System;
using System.ComponentModel.Design;
using System.Dynamic;
using System.Linq;
using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML.Elements
{
    public class DateElement : BaseElement<DateTime>
    {
        public const string TypeName = "date";
        public override string DataString
        {
            get => Data.ToString();
            set
            {
                if (DateTime.TryParse(value, out var v))
                {
                    Data = v;
                }
            }
        }
        readonly double TimeScale = 1000000;
        public static readonly DateTime DateTimeReferencePoint = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static explicit operator DateTime(DateElement? element) => element == null ? DateTimeReferencePoint : element.Data;
        public static explicit operator DateTime?(DateElement? element)
        {
            if (element == null) return null;
            return element.Data;
        } 
        public DateElement(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public DateElement(SchemaElement schemaElement, DateTime value) : base(schemaElement, value) { }
        public DateElement(SchemaElement schemaElement) : base(schemaElement, default) { }
        protected override void DataFromSegmentSource(ref DateTime data)
        {
            SegmentSource!.Position = 0;
            data = SegmentSource!.ReadEBMLDate((int)SegmentSource!.Length);
        }
        protected override void DataToSegmentSource(ref SegmentSource source)
        {
            var timeOffset = (long)((Data - DateTimeReferencePoint).TotalMilliseconds * TimeScale);
            var bytes = BitConverter.GetBytes(timeOffset).Reverse().ToList();
            while (bytes.Count > 1 && bytes[0] == 0) bytes.RemoveAt(0);
            source = new ByteSegment(bytes.ToArray());
        }
    }
}
