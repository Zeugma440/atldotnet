using System;

namespace SpawnDev.EBML
{
    public class NullSegment : SegmentSource<object>
    {
        #region Constructors
        public NullSegment(long size) : base(null, 0, size, true)
        {
        }
        public NullSegment() : base(null, 0, 0, true)
        {
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            Position += count;
            
        }
        public override bool CanWrite => true;
        #endregion
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            SourcePosition += count;
            return count;
        }
    }
}
