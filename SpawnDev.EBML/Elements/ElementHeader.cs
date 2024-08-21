using System.IO;
using System.Threading.Tasks;
using SpawnDev.EBML.Extensions;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML.Elements
{
    public class ElementHeader
    {
        public ulong Id { get; set; }
        public ulong? Size
        {
            get => _Size;
            set
            {
                _Size = value;
                _SegmentSource = CreateSegmentSource();
            }
        }
        public int IdMinOctets { get; set; }
        public int SizeMinOctets { get; set; }
        public long HeaderSize => SegmentSource.Length;
        protected ulong? _Size { get; set; }
        protected SegmentSource? _SegmentSource { get; set; }
        public SegmentSource SegmentSource
        {
            get
            {
                if (_SegmentSource == null)
                {
                    _SegmentSource = CreateSegmentSource();
                }
                return _SegmentSource;
            }
        }
        SegmentSource CreateSegmentSource()
        {
            var stream = new MemoryStream();
            stream.WriteEBMLElementIdRaw(Id, IdMinOctets);
            stream.WriteEBMLElementSize(Size, SizeMinOctets);
            stream.Position = 0;
            var ret = new StreamSegment(stream, true);
            return ret;
        }
        public ElementHeader(ulong id, ulong? size, int idMinOctets = 0, int sizeMinOctets = 0)
        {
            IdMinOctets = idMinOctets;
            SizeMinOctets = sizeMinOctets;
            Id = id;
            _Size = size;
        }
        public ElementHeader(ulong id, ulong? size, SegmentSource source)
        {
            Id = id;
            _Size = size;
            _SegmentSource = source;
        }
        public static ElementHeader Read(Stream stream)
        {
            var position = stream.Position;
            var id = stream.ReadEBMLElementIdRaw();
            var size = stream.ReadEBMLElementSizeN();
            var headerSize = stream.Position - position;
            var ret = new ElementHeader(id, size, new StreamSegment(stream, position, headerSize));
            return ret;
        }
        public void CopyTo(Stream stream)
        {
            SegmentSource.Position = 0;
            SegmentSource.CopyTo(stream);
        }
        public Task CopyToAsync(Stream stream)
        {
            SegmentSource.Position = 0;
            return SegmentSource.CopyToAsync(stream);
        }
    }
}
