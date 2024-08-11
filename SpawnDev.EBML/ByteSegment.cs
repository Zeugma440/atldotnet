using System;

namespace SpawnDev.EBML
{
    public class ByteSegment : SegmentSource<byte[]>
    {
        #region Constructors
        /// <summary>
        /// Creates a new ByteSegment
        /// </summary>
        public ByteSegment(byte[] source, long offset, long size, bool ownsSource = false) : base(source, offset, size, ownsSource) { }
        /// <summary>
        /// Creates a new ByteSegment
        /// </summary>
        public ByteSegment(byte[] source, long size, bool ownsSource = false) : base(source, 0, size, ownsSource) { }
        /// <summary>
        /// Creates a new ByteSegment
        /// </summary>
        public ByteSegment(byte[] source, bool ownsSource = false) : base(source, 0, source.Length, ownsSource) { }
        #endregion
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            Array.Copy(Source, SourcePosition, buffer, offset, count);
            SourcePosition += count;
            return count;
        }
    }
}