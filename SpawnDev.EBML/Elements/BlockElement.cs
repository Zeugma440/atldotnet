using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;
using System;
using System.Linq;

namespace SpawnDev.EBML.Elements
{
    public enum BlockLacing : byte
    {
        None = 0,
        Xiph,
        Fixed,
        EBML,
    }

    public class BlockElement : BaseElement<byte[]>
    {
        public const string TypeName = "mkvBlock";

        protected override bool EqualCheck(byte[] obj1, byte[] obj2) => obj1 == obj2 || obj1.SequenceEqual(obj2);

        public BlockElement(SchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header) { }
        public BlockElement(SchemaElement schemaElement, byte[] value) : base(schemaElement, value) { }
        public BlockElement(SchemaElement schemaElement) : base(schemaElement, Array.Empty<byte>()) { }


        // Binary audio/video data
        private SegmentSource? BlockData = null;

        private ulong _TrackId { get; set; }
        public ulong TrackId
        {
            get => _TrackId;
            set => _TrackId = value;
        }
        private short _Timecode { get; set; }
        public short Timecode
        {
            get => _Timecode;
            set => _Timecode = value;
        }
        private byte _Flags { get; set; }
        public byte Flags
        {
            get => _Flags;
            set => _Flags = value;
        }

        public bool Invisible => (Flags & 8) != 0;
        public BlockLacing Lacing => (BlockLacing)((Flags >> 1) & 3);

        protected override void DataFromSegmentSource(ref byte[] data)
        {
            SegmentSource.Position = 0;
            _TrackId = SegmentSource.ReadEBMLVINT();
            _Timecode = BigEndian.ToInt16(SegmentSource.ReadBytes(2));
            _Flags = SegmentSource.ReadByteOrThrow();
            BlockData = SegmentSource.Slice(SegmentSource.Length - SegmentSource.Position);
        }

        protected override void DataToSegmentSource(ref SegmentSource source)
        {
            BlockData!.Position = 0;
            source.Write(EBMLConverter.ToVINTBytes(TrackId));
            source.Write(BigEndian.GetBytes(Timecode));
            source.WriteByte(Flags);
            BlockData!.CopyTo(source, 100 * 1024);
        }
    }
}
