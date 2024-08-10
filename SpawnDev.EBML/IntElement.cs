using System;

namespace SpawnDev.EBML
{
    public class IntElement : BaseElement<long>
    {
        public static explicit operator long(IntElement? element) => element == null ? 0 : element.Data;
        public static explicit operator long?(IntElement? element) => element?.Data;
        int DataSize = 4;
        public IntElement(Enum id) : base(id) { }
        public IntElement(Enum id, long value) : base(id)
        {
            Data = value;
        }
        public override void UpdateBySource()
        {
            _DataValue = new Lazy<long>(() =>
            {
                Stream!.Position = 0;
                return Stream!.ReadEBMLInt((int)Stream.Length);
            });
        }
        public override void UpdateByData()
        {
            _DataStream = new Lazy<SegmentSource?>(() =>
            {
                //// switch endianness and remove preceding 0 bytes
                //var bytes = BitConverter.GetBytes(Data).Reverse().ToList();
                //while (bytes.Count > 1 && bytes[0] == 0) bytes.RemoveAt(0);
                //return new ByteSegment(bytes.ToArray());
                var bytes = EBMLConverter.ToIntBytes(Data);
                return new ByteSegment(bytes);
            });
        }
    }
}
