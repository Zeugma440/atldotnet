
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpawnDev.EBML.Segments
{
    public class MultiStreamSegment : SegmentSource<Stream[]>
    {
        #region Constructors
        public MultiStreamSegment(IEnumerable<byte[]> source, bool ownsSource = false) : base(source.Select(o => new MemoryStream(o)).ToArray(), 0, source.Sum(o => o.Length), ownsSource) { }
        public MultiStreamSegment(IEnumerable<Stream> source, bool ownsSource = false) : base(source.ToArray(), 0, source.Sum(o => o.Length), ownsSource) { }
        #endregion
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            var sourceIndex = 0;
            var source = Source[sourceIndex];
            var currentOffset = Position;
            while (source.Length < currentOffset)
            {
                if (sourceIndex >= source.Length - 1) return 0;
                sourceIndex++;
                currentOffset = currentOffset - source.Length;
                source = Source[sourceIndex];
            }
            int bytesRead = 0;
            int bytesLeft = count;
            var bytesReadTotal = 0;
            var positions = Source.Select(o => o.Position).ToArray();
            source.Position = currentOffset;
            while (sourceIndex < Source.Length && bytesLeft > 0)
            {
                var sourceBytesLeft = source.Length - source.Position;
                while (sourceBytesLeft <= 0)
                {
                    if (sourceIndex >= Source.Length - 1) goto LoopEnd;
                    sourceIndex++;
                    source = Source[sourceIndex];
                    source.Position = 0;
                    sourceBytesLeft = source.Length;
                }
                var readByteCount = (int)Math.Min(bytesLeft, sourceBytesLeft);
                bytesRead = await source.ReadAsync(buffer, bytesReadTotal + offset, readByteCount, cancellationToken);
                bytesReadTotal += (int)bytesRead;
                bytesLeft -= bytesRead;
                if (bytesRead <= 0 || bytesLeft <= 0) break;
            }
        LoopEnd:
            SourcePosition += bytesReadTotal;
            // restore stream positions
            for (var i = 0; i < Source.Length; i++)
            {
                Source[i].Position = positions[i];
            }
            return bytesReadTotal;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            var sourceIndex = 0;
            var source = Source[sourceIndex];
            var currentOffset = Position;
            while (source.Length < currentOffset)
            {
                if (sourceIndex >= source.Length - 1) return 0;
                sourceIndex++;
                currentOffset = currentOffset - source.Length;
                source = Source[sourceIndex];
            }
            int bytesRead = 0;
            int bytesLeft = count;
            var bytesReadTotal = 0;
            var positions = Source.Select(o => o.Position).ToArray();
            source.Position = currentOffset;
            while (sourceIndex < Source.Length && bytesLeft > 0)
            {
                var sourceBytesLeft = source.Length - source.Position;
                while (sourceBytesLeft <= 0)
                {
                    if (sourceIndex >= Source.Length - 1) goto LoopEnd;
                    sourceIndex++;
                    source = Source[sourceIndex];
                    source.Position = 0;
                    sourceBytesLeft = source.Length;
                }
                var readByteCount = (int)Math.Min(bytesLeft, sourceBytesLeft);
                bytesRead = source.Read(buffer, bytesReadTotal + offset, readByteCount);
                bytesReadTotal += (int)bytesRead;
                bytesLeft -= bytesRead;
                if (bytesRead <= 0 || bytesLeft <= 0) break;
            }
            LoopEnd:
            SourcePosition += bytesReadTotal;
            // restore stream positions
            for (var i = 0; i < Source.Length; i++)
            {
                Source[i].Position = positions[i];
            }
            return bytesReadTotal;
        }
    }
}
