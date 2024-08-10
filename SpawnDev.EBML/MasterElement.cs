using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace SpawnDev.EBML
{
    public class MasterElement : BaseElement<ReadOnlyCollection<BaseElement>>
    {
        public static readonly EBMLSchemaDefault DefaultEBMLSchema = new EBMLSchemaDefault();

        private Lazy<List<BaseElement>> ___Data = new Lazy<List<BaseElement>>(new List<BaseElement>());
        private List<BaseElement> __Data => ___Data.Value;
        public override ReadOnlyCollection<BaseElement> Data
        {
            get => __Data.AsReadOnly();
            set => throw new NotImplementedException($"{nameof(MasterElement)}.Data must be manipulated using {nameof(MasterElement)} instance methods.");
        }

        public override string ToString() => $"{Index} {Id} - IdChain: [ {IdChain.ToString(", ")} ] Type: {this.GetType().Name} Length: {Length} bytes";
        public MasterElement(Enum id) : base(id) { }
        public MasterElement? GetContainer(params Enum[] ids) => GetElement<MasterElement>(ids);
        public List<MasterElement> GetContainers(params Enum[] ids) => GetElements<MasterElement>(ids);
        public bool ElementExists(params Enum[] idChain) => GetElement<BaseElement>(idChain) != null;
        public BaseElement? GetElement(params Enum[] idChain) => GetElement<BaseElement>(idChain);
        public List<BaseElement> GetElements(params Enum[] idChain) => GetElements<BaseElement>(idChain);
        public T? GetElement<T>(params Enum[] idChain) where T : BaseElement
        {
            if (idChain.Length == 0) throw new Exception("Invalid idChain");
            if (idChain.Length == 1)
            {
                return (T?)Data.FirstOrDefault(o => o.Id.Equals(idChain[0]));
            }
            else
            {
                var subChain = idChain.Skip(1).ToArray();
                var masters = Data.Where(o => o.Id.Equals(idChain[0])).Cast<MasterElement>();
                foreach (var master in masters)
                {
                    var match = master.GetElement(subChain);
                    if (match != null) return (T)match;
                }
            }
            return null;
        }
        long CalculatedLength = 0;
        /// <summary>
        /// Returns all elements with an IdChain that ends with the provided idChain<br />
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="idChain">idChain is an array ending in the desired Enum types to be returned, with optional preceding parent ElementIds</param>
        /// <returns></returns>
        public List<T> GetElements<T>(params Enum[] idChain) where T : BaseElement
        {
            if (idChain.Length == 0) throw new Exception("Invalid idChain");
            if (idChain.Length == 1)
            {
                return Data.Where(o => o.Id.Equals(idChain[0])).Cast<T>().ToList();
            }
            else
            {
                var subChain = idChain.Skip(1).ToArray();
                var tmp = Data.Where(o => o.Id.Equals(idChain[0])).ToList();
                return Data.Where(o => o.Id.Equals(idChain[0])).Cast<MasterElement>().SelectMany(o => o.GetElements<T>(subChain)).ToList();
            }
        }

        //long _Length = 0;
        /// <summary>
        /// Returns the byte length of this container
        /// </summary>
        public override long Length => !DataChanged && Stream != null ? Stream.Length : CalculateLength();
        private long CalculateLength()
        {
            long ret = 0;
            foreach (var element in Data)
            {
                var len = element.Length;
                if (len == 0)
                {
                    var nmt1 = true;
                }
                ret += len;
                var idSize = EBMLConverter.ToVINTByteSize(element.Id.ToUInt64());
                var lenSize = EBMLConverter.ToVINTByteSize((ulong)len);
                if (lenSize == 0)
                {
                    var nmt = true;
                }
                ret += idSize;
                ret += lenSize;
            }
            return ret;
        }
        /// <summary>
        /// Copies the container to the specified stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public override long CopyTo(Stream stream, int? bufferSize = null)
        {
            if (!DataChanged && Stream != null)
            {
                var pos = stream.Position;
                Stream.Position = 0;
                if (bufferSize != null)
                {
                    Stream.CopyTo(stream, bufferSize.Value);
                }
                else
                {
                    Stream.CopyTo(stream);
                }
                var bytesWritten = stream.Position - pos;
                if (bytesWritten != Length)
                {
                    var nmt = true;
                }
                return Length;
            }
            else
            {
                var pos = stream.Position;
                if (Stream != null) Stream.Position = 0;
                foreach (var element in Data)
                {
                    var len = element.Length;
                    if (len == 127)
                    {
                        var nmt = true;
                    }
                    var idBytes = EBMLConverter.ToVINTBytes(element.Id.ToUInt64());
                    var lenBytes = EBMLConverter.ToVINTBytes((ulong)len);
                    stream.Write(idBytes);
                    stream.Write(lenBytes);
                    element.CopyTo(stream, bufferSize);
                }
                var bytesWritten = stream.Position - pos;
                if (bytesWritten != Length)
                {
                    var nmt = true;
                }
                return Length;
            }
        }
        public override void UpdateByData()
        {
            //_Length = CalculateLength();
            DataChangedInvoke();
        }

        void ReleaseData()
        {
            if (___Data.IsValueCreated)
            {
                foreach (var el in __Data)
                {
                    el.OnDataChanged -= Element_DataChanged;
                    el.SetParent();
                }
                __Data.Clear();
                LastGoodPosition = 0;
            }
        }
        public override void UpdateBySource()
        {
            ReleaseData();
            ParseError = null;
            //_Length = Stream!.Length;
            ___Data = new Lazy<List<BaseElement>>(() => GetChildrenFromStream());
        }
        public virtual EBMLSchema ActiveSchema
        {
            get => Parent == null ? (_ActiveSchema ?? DefaultEBMLSchema) : Parent.ActiveSchema;
            set => _ActiveSchema = value;
        }

        protected EBMLElement? _ActiveEBML = null;

        protected EBMLSchema? _ActiveSchema = null;

        protected virtual void EBMLElementFound(EBMLElement element)
        {
            // Load schema
        }

        protected virtual void ElementFound(BaseElement element)
        {

        }

        public virtual EBMLElement? ActiveEBML => Parent == null ? _ActiveEBML : Parent.ActiveEBML;

        public long LastGoodPosition { get; private set; } = 0;

        private List<BaseElement> GetChildrenFromStream()
        {
            var elements = new List<BaseElement>();
            if (Stream == null) throw new NullReferenceException($"{nameof(Stream)} cannot be null.");
            var thisIsRoot = Id.Equals(ElementId.EBMLSource);
            var thisIsEbmlHead = Id.Equals(ElementId.EBML);
            ParseError = null;
            try
            {
                Stream.Position = 0;
                LastGoodPosition = 0;

                //var bytess = Stream.ReadBytes(16);
                //var idd = EBMLConverter.ReadEBMLVINT(bytess, out var ssize);
                //var lenn = EBMLConverter.ReadEBMLVINT(bytess, out var ssize2, ssize);
                //var iddn = EBMLConverter.ToVINTBytes(idd);
                //var lennn = EBMLConverter.ToVINTBytes(lenn);
                //Stream.Position = 0;

                while (Stream.Position < Stream.Length)
                {
                    var schema = thisIsEbmlHead ? DefaultEBMLSchema : ActiveSchema;
                    var headPosition = Stream.Position;
                    var id = Stream.ReadEBMLElementId().ToEnum(schema.ElementIdEnumType);
                    var isEbmlElement = id.Equals(ElementId.EBML);
                    if (Id.Equals(ElementId.EBMLSource))
                    {
                        // The first element should be an EBML element
                        if (elements.Count == 0 && !isEbmlElement && schema == DefaultEBMLSchema)
                        {
                            throw new Exception("Invalid source. EBML element must be the first element.");
                        }
                    }
                    else if (isEbmlElement)
                    {
                        // invalid. EBML element must be a root element.
                        throw new Exception("Invalid source. EBML element found in non-root location.");
                    }
                    var len = Stream.ReadEBMLElementSize(out var isUnknownSize);
                    var dataPosition = Stream.Position;
                    if (isUnknownSize)
                    {
                        var childIdChain = IdChain.Concat(new Enum[] { id }).ToArray();
                        len = Stream.DetermineEBMLElementSize(childIdChain, schema.ValidChildCheck);
                    }
                    ulong bytesLeft = (ulong)(Stream.Length - Stream.Position);
                    if (len > bytesLeft)
                    {
                        // invalid section length
                        break;
                    }
                    //var headSegment = new StreamSegment(Stream, headPosition, dataPosition - headPosition);
                    var dataSegment = Stream.Slice((long)len);
                    var elementType = schema.GetElementType(id);
                    if (elementType == null)
                    {
                        id = id.ToEnum(DefaultEBMLSchema.ElementIdEnumType);
                        if (schema == DefaultEBMLSchema)
                        {
                            elementType = typeof(UnknownElement);
                        }
                        else
                        {
                            elementType = DefaultEBMLSchema.GetElementType(id) ?? typeof(UnknownElement);
                        }
                    }
                    var element = (BaseElement)Activator.CreateInstance(elementType, id)!;
                    elements.Add(element);
                    // if something goes wrong after this point, remove the element and then reset to the lastGoodPos
                    element.SetParent(this);
                    element.Stream = dataSegment;
                    element.OnDataChanged += Element_DataChanged;
                    ElementFound(element);
                    if (element is EBMLElement ebmlElement)
                    {
                        _ActiveEBML = ebmlElement;
                        EBMLElementFound(ebmlElement);
                    }
                    Stream.Position = (long)(dataPosition + (long)len);
                    LastGoodPosition = Stream.Position;
                }
            }
            catch (Exception ex)
            {
                ParseError = ex;
            }
            return elements;
        }
        public Exception? ParseError { get; private set; }
        /// <summary>
        /// A flat list of all the elements contained by this element and their children<br />
        /// WARNING: By default, all MasterElement.Data is lazy loaded to improve performance and responsiveness, especially when only small portions of data are needed.<br />
        /// Calling GetDescendants() will cause all descendant elements to be loaded which can take a bit with larger sources.
        /// </summary>
        public ReadOnlyCollection<BaseElement> GetDescendants()
        {
            var ret = new List<BaseElement>();
            foreach (var el in Data)
            {
                ret.Add(el);
                if (el is MasterElement container)
                {
                    ret.AddRange(container.GetDescendants());
                }
            }
            return ret.AsReadOnly();
        }
        /// <summary>
        /// Adds a FloatElement to this container with the given value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Add(Enum id, double value) => Add(new FloatElement(id, value));
        /// <summary>
        /// Adds a FloatElement to this container with the given value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Add(Enum id, float value) => Add(new FloatElement(id, value));
        /// <summary>
        /// Adds a StringElement to this container with the given value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Add(Enum id, string value, bool utf8 = true) => utf8 ? Add(new UTF8StringElement(id, value)) : Add(new StringElement(id, value));
        /// <summary>
        /// Adds a UintElement to this container with the given value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Add(Enum id, ulong value) => Add(new UintElement(id, value));
        /// <summary>
        /// Adds an IntElement to this container with the given value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Add(Enum id, long value) => Add(new IntElement(id, value));
        /// <summary>
        /// Adds a WebMElement to this container with the given value
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public bool Add(BaseElement element)
        {
            if (__Data.Contains(element)) return false;
            __Data.Add(element);
            element.SetParent(this);
            element.OnDataChanged += Element_DataChanged;
            UpdateByData();
            return true;
        }
        private void Element_DataChanged(BaseElement obj)
        {
            UpdateByData();
        }
        /// <summary>
        /// Removes a WebElement from this container
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public bool Remove(BaseElement element)
        {
            var succ = __Data.Remove(element);
            if (!succ) return false;
            element.SetParent();
            element.OnDataChanged -= Element_DataChanged;
            UpdateByData();
            return succ;
        }
        //public static List<Enum> ClusterChildIds = new List<Enum>
        //{
        //    Enum.Timecode,
        //    Enum.Position,
        //    Enum.PrevSize,
        //    Enum.SimpleBlock,
        //    Enum.BlockGroup,
        //    Enum.Void,
        //};
        ///// <summary>
        ///// The element ids that a segment can contain. Used when detecting the end of a segment.
        ///// </summary>
        //public static List<Enum> SegmentChildIds = new List<Enum>
        //{
        //    Enum.SeekHead,
        //    Enum.Info,
        //    Enum.Tracks,
        //    Enum.Chapters,
        //    Enum.Cluster,
        //    Enum.Cues,
        //    Enum.Attachments,
        //    Enum.Tags,
        //    Enum.Void,
        //};
        //bool ValidChildCheck(Enum elementId, Enum childElementId)
        //{
        //    if (childElementId == Enum.Void) return true;
        //    switch (elementId)
        //    {
        //        case Enum.Segment: return SegmentChildIds.Contains(childElementId);
        //        case Enum.Cluster: return ClusterChildIds.Contains(childElementId);
        //    }
        //    return true;
        //}
        //static long FindSegmentLength(Stream stream)
        //{
        //    long startOffset = stream.Position;
        //    long pos = stream.Position;
        //    while (true)
        //    {
        //        pos = stream.Position;
        //        if (stream.Position >= stream.Length) break;
        //        var id = stream.ReadEBMLElementId<Enum>();
        //        var len = stream.ReadEBMLElementSize(out var isUnknownSize);
        //        if (isUnknownSize && id == Enum.Cluster)
        //        {
        //            len = (ulong)FindClusterLength(stream);
        //        }
        //        if (!SegmentChildIds.Contains(id))
        //        {
        //            break;
        //        }
        //        stream.Seek((long)len, SeekOrigin.Current);
        //    }
        //    stream.Position = startOffset;
        //    return pos - startOffset;
        //}
        //static long FindClusterLength(Stream stream)
        //{
        //    long startOffset = stream.Position;
        //    long pos = stream.Position;
        //    while (true)
        //    {
        //        pos = stream.Position;
        //        if (stream.Position >= stream.Length) break;
        //        var id = stream.ReadEBMLElementId<Enum>();
        //        var len = stream.ReadEBMLElementSize(out var isUnknownSize);
        //        if (!ClusterChildIds.Contains(id))
        //        {
        //            break;
        //        }
        //        stream.Seek((long)len, SeekOrigin.Current);
        //    }
        //    stream.Position = startOffset;
        //    return pos - startOffset;
        //}
    }
}
