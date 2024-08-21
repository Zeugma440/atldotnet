using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SpawnDev.EBML.Segments
{
    public abstract class SegmentSource : Stream
    {
        /// <summary>
        /// The underlying source of the segment
        /// </summary>
        public virtual object SourceObject { get; protected set; }
        /// <summary>
        /// Segment start position in Source
        /// </summary>
        public virtual long Offset { get; set; }
        /// <summary>
        /// Whether this SegmentSource owns the underlying source object
        /// </summary>
        public virtual bool OwnsSource { get; set; }
        /// <summary>
        /// Segment size in bytes.
        /// </summary>
        protected virtual long Size { get; set; }
        protected virtual long SourcePosition { get; set; }
        // Stream
        public override long Length => Size;
        public override bool CanRead => SourceObject != null;
        public override bool CanSeek => SourceObject != null;
        public override bool CanWrite => false;
        public override bool CanTimeout => false;
        public override long Position { get => SourcePosition - Offset; set => SourcePosition = value + Offset; }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public SegmentSource(long offset, long size, bool ownsSource)
        {
            Offset = offset;
            Size = size;
            OwnsSource = ownsSource;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
                case SeekOrigin.Current:
                    Position = Position + offset;
                    break;
            }
            return Position;
        }
        /// <summary>
        /// Returns a new instance of this type with the same source, representing a segment of this instance
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public virtual SegmentSource Slice(long offset, long size)
        {
            var slice =  (SegmentSource)Activator.CreateInstance(GetType(), SourceObject, Offset + offset, size, OwnsSource)!;
            if (slice.Position != 0) slice.Position = 0;
            return slice;
        }
        public SegmentSource Slice(long size) => Slice(Position, size);
        public override void CopyTo(Stream destination, int bufferSize)
        {
            base.CopyTo(destination, bufferSize);
        }
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
    public abstract class SegmentSource<T> : SegmentSource
    {
        public T Source { get; private set; }
        public SegmentSource(T source, long offset, long size, bool ownsSource) : base(offset, size, ownsSource)
        {
            Source = source;
            SourceObject = source!;
        }
    }
}
