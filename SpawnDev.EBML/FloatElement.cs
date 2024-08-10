using System;

namespace SpawnDev.EBML
{
    public class FloatElement : BaseElement<double>
    { 
        public static explicit operator double(FloatElement? element) => element == null ? 0 : element.Data;
        public static explicit operator double?(FloatElement? element) => element?.Data;
        int DataSize = 4;
        public FloatElement(Enum id) : base(id) { }
        public FloatElement(Enum id, float value) : base(id)
        {
            DataSize = 4;
            Data = value;
        }
        public FloatElement(Enum id, double value) : base(id)
        {
            DataSize = 8;
            Data = value;
        }
        public override void UpdateBySource()
        {
            DataSize = (int)Stream!.Length;
            _DataValue = new Lazy<double>(() =>
            {
                DataSize = (int)Stream!.Length;
                Stream!.Position = 0;
                return Stream!.ReadEBMLFloat(DataSize);
            });
        }
        public override void UpdateByData()
        {
            _DataStream = new Lazy<SegmentSource?>(() =>
            {
                if (DataSize == 4)
                {
                    var bytes = BigEndian.GetBytes((float)Data);
                    return new ByteSegment(bytes);
                }
                else if (DataSize == 8)
                {
                    var bytes = BigEndian.GetBytes(Data);
                    return new ByteSegment(bytes);
                }
                return new ByteSegment(new byte[0]);
            });
        }
    }
}
