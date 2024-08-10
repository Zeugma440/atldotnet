using System;
using System.Drawing;

namespace SpawnDev.EBML
{
    public class ByteSegment : SegmentSource<byte[]>
    {
        #region Constructors
        public ByteSegment(byte[] source, long offset, long size, bool ownsSource = false) : base(source, offset, size, ownsSource)
        {
        }
        public ByteSegment(byte[] source, long size, bool ownsSource = false) : base(source, 0, size, ownsSource)
        {
        }
        public ByteSegment(byte[] source, bool ownsSource = false) : base(source, 0, source.Length, ownsSource)
        {
        }
        #endregion
        public override int Read(byte[] buffer, int offset, int count)
        {
            long bytesLeftInSegment;
            bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            Array.Copy(Source, SourcePosition, buffer, offset, count);
            SourcePosition += count;
            return count;
        }
    }
}
