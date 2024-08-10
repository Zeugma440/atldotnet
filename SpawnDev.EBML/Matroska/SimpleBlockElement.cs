
using System;
using System.IO;

namespace SpawnDev.EBML.Matroska
{
    public enum SimpleBlockLacing : byte
    {
        None = 0,
        Xiph,
        Fixed,
        EBML,
    }
    public class SimpleBlockElement : BinaryElement
    {
        SegmentSource? SimpleBlockData = null;
        public SimpleBlockElement(Enum id) : base(id)
        {

        }
        private ulong _TrackId { get; set; } = 0;
        public ulong TrackId
        {
            get => _TrackId;
            set
            {
                _TrackId = value;
                UpdateByData();
            }
        }
        private short _Timecode { get; set; } = 0;
        public short Timecode
        {
            get => _Timecode;
            set
            {
                _Timecode = value;
                UpdateByData();
            }
        }
        private byte _SimpleBlocksFlags { get; set; }
        public byte SimpleBlocksFlags
        {
            get => _SimpleBlocksFlags;
            set
            {
                _SimpleBlocksFlags = value;
                UpdateByData();
            }
        }
        public bool Keyframe
        {
            get => (SimpleBlocksFlags & 128) != 0;
        }
        public bool Invisible
        {
            get => (SimpleBlocksFlags & 8) != 0;
        }
        public bool Discardable
        {
            get => (SimpleBlocksFlags & 1) != 0;
        }
        public SimpleBlockLacing Lacing
        {
            get => (SimpleBlockLacing)((SimpleBlocksFlags >> 1) & 3);
        }
        public override long Length
        {
            get
            {
                using var n = new NullSegment();
                CopyTo(n);
                return n.Position;
            }
        }
        void UpdateByData()
        {
            //_DataStream = new Lazy<SegmentSource?>(() => {
            //    var stream = new MemoryStream();
            //    SimpleBlockData!.Position = 0;
            //    stream.Write(EBMLConverter.ToVINTBytes(TrackId));
            //    stream.Write(BigEndian.GetBytes(Timecode));
            //    stream.WriteByte(SimpleBlocksFlags);
            //    SimpleBlockData!.CopyTo(stream);
            //    stream.Position = 0;
            //    return new StreamSegment(stream);
            //});
            DataChangedInvoke();
        }
        public override long CopyTo(Stream stream, int? bufferSize = null)
        {
            SimpleBlockData!.Position = 0;
            var pos = stream.Position;
            stream.Write(EBMLConverter.ToVINTBytes(TrackId));
            stream.Write(BigEndian.GetBytes(Timecode));
            stream.WriteByte(SimpleBlocksFlags);
            if (bufferSize == null)
            {
                SimpleBlockData!.CopyTo(stream);
            }
            else
            {
                SimpleBlockData!.CopyTo(stream, bufferSize.Value);
            }
            var bytesWritten = stream.Position - pos;
            return bytesWritten;
        }
        public override void UpdateBySource()
        {
            Stream!.Position = 0;
            _TrackId = Stream!.ReadEBMLVINT();
            _Timecode = BigEndian.ToInt16(Stream!.ReadBytes(2));
            _SimpleBlocksFlags = Stream.ReadByteOrThrow();
            SimpleBlockData = Stream.Slice(Stream.Length - Stream!.Position);
        }
        public override string ToString() => $"{Index} {Id} - IdChain: [ {IdChain.ToString(", ")} ] Type: {GetType().Name} Length: {Length} bytes TrackId: {TrackId} Timecode: {Timecode} Keyframe: {Keyframe}";
    }
}
