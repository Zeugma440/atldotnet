using System;

namespace SpawnDev.EBML
{
    public class UintElement : BaseElement<ulong>
    {
        public static explicit operator ulong(UintElement? element) => element == null ? 0 : element.Data;
        public static explicit operator ulong?(UintElement? element) => element == null ? default : element.Data;
        public UintElement(Enum id) : base(id) { }
        public UintElement(Enum id, ulong value) : base(id)
        {
            Data = value;
        }
        public override void UpdateBySource()
        {
            _DataValue = new Lazy<ulong>(() =>
            {
                Stream!.Position = 0;
                return Stream!.ReadEBMLUInt((int)Stream.Length);
            });
        }
        public override void UpdateByData()
        {
            _DataStream = new Lazy<SegmentSource?>(() =>
            {
                //var bytes = BigEndian.GetBytes(Data).ToList();
                //while (bytes.Count > 1 && bytes[0] == 0) bytes.RemoveAt(0);
                //return new ByteSegment(bytes.ToArray());
                var bytes = EBMLConverter.ToUIntBytes(Data);
                return new ByteSegment(bytes);
            });
        }
    }
}
