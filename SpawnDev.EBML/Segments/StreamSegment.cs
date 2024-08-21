using System;
using System.IO;

namespace SpawnDev.EBML.Segments
{
    public class StreamSegment : SegmentSource<Stream>
    {
        #region Constructors
        public StreamSegment(Stream source, long offset, long size, bool ownsSource = false) : base(source, offset, size, ownsSource)
        {
        }
        public StreamSegment(Stream source, long size, bool ownsSource = false) : base(source, source.Position, size, ownsSource)
        {
        }
        public StreamSegment(Stream source, bool ownsSource = false) : base(source, source.Position, source.Length - source.Position, ownsSource)
        {
        }
        #endregion
        protected override long SourcePosition { get => Source.Position; set => Source.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            return Source.Read(buffer, offset, count);
        }
    }
}
