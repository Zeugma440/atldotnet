using System;
using System.Text;

namespace SpawnDev.EBML
{
    public class StringElement : BaseElement<string>
    {
        public static explicit operator string?(StringElement? element) => element == null ? null : element.Data;
        public StringElement(Enum id) : base(id) { }
        public virtual Encoding Encoding { get; } = Encoding.ASCII;
        public StringElement(Enum id, string value) : base(id)
        {
            Data = value;
        }
        public override void UpdateBySource()
        {
            _DataValue = new Lazy<string>(() =>
            {
                Stream!.Position = 0;
                return Stream!.ReadEBMLString((int)Stream.Length, Encoding);
            });
        }
        public override void UpdateByData()
        {
            _DataStream = new Lazy<SegmentSource?>(() =>
            {;
                return new ByteSegment(Encoding.GetBytes(Data));
            });
        }
    }
}
