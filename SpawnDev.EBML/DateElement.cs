using System;
using System.Linq;

namespace SpawnDev.EBML
{
    public class DateElement : BaseElement<DateTime>
    {
        readonly double TimeScale = 1000000;
        public static readonly DateTime DateTimeReferencePoint = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static explicit operator DateTime(DateElement? element) => element?.Data ?? DateTimeReferencePoint;
        public static explicit operator DateTime?(DateElement? element) => element?.Data;
        public DateElement(Enum id) : base(id)
        {
            Data = DateTimeReferencePoint;
        }
        public DateElement(Enum id, DateTime value) : base(id)
        {
            Data = value;
        }
        public override void UpdateBySource()
        {
            _DataValue = new Lazy<DateTime>(() =>
            {
                Stream!.Position = 0;
                return Stream!.ReadEBMLDate((int)Stream!.Length);
            });
        }
        public override void UpdateByData()
        {
            _DataStream = new Lazy<SegmentSource?>(() =>
            {
                // switch endianness and remove preceding 0 bytes
                var timeOffset = (long)((Data - DateTimeReferencePoint).TotalMilliseconds * TimeScale);
                var bytes = BitConverter.GetBytes(timeOffset).Reverse().ToList();
                while (bytes.Count > 1 && bytes[0] == 0) bytes.RemoveAt(0);
                return new ByteSegment(bytes.ToArray());
            });
        }
    }
}
