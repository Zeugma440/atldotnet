using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SpawnDev.EBML.Segments;
using SpawnDev.EBML.Extensions;

namespace SpawnDev.EBML.Elements
{
    /// <summary>
    /// base EBML element
    /// </summary>
    public abstract class BaseElement
    {
        /// <summary>
        /// The byte offset from the document root
        /// </summary>
        public ulong Offset
        {
            get
            {
                var index = Index;
                if (index < 0) return 0;
                ulong offset = 0;
                BaseElement prev = index == 0 ? Parent! : Parent!.Data.ElementAt(index - 1);
                if (index == 0)
                {
                    offset = prev.Offset + Parent!.HeaderSize;
                }
                else
                {
                    offset = prev.Offset + prev.TotalSize;
                }
                return offset;
            }
        }
        /// <summary>
        /// Returns the source type location of the current Data
        /// </summary>
        public ElementDataSource Source { get; protected set; }
        /// <summary>
        /// Element index in its container
        /// </summary>
        public int Index => Parent == null ? -1 : Parent.GetChildIndex(this);
        /// <summary>
        /// Schema element type:<br/>
        /// - uinteger<br/>
        /// - integer<br/>
        /// - master<br/>
        /// - float<br/>
        /// - string<br/>
        /// - utf-8<br/>
        /// - binary<br/>
        /// - date<br/>
        /// </summary>
        public virtual string Type => SchemaElement?.Type ?? "";
        /// <summary>
        /// Element data as a string
        /// </summary>
        public virtual string DataString { get; set; } = "";
        /// <summary>
        /// Element DocType
        /// </summary>
        public virtual string? DocType => SchemaElement?.DocType ?? Parent?.DocType ?? EBMLParser.EBML;
        /// <summary>
        /// Element parent
        /// </summary>
        public virtual MasterElement? Parent { get; private set; }
        /// <summary>
        /// Adds this element to the parent element
        /// </summary>
        /// <param name="parent"></param>
        public virtual void SetParent(MasterElement? parent)
        {
            if (Parent == parent) return;
            if (Parent != null) Remove();
            Parent = parent;
            if (parent != null)
            {
                parent.AddElement(this);
            }
        }
        /// <summary>
        /// Remove the element from its parent
        /// </summary>
        public void Remove()
        {
            if (Parent == null) return;
            var parent = Parent;
            Parent = null;
            parent.RemoveElement(this);
        }
        /// <summary>
        /// Returns true if this element requires a parent and does not have one
        /// </summary>
        public bool IsOrphan => !IsDocument && Parent == null;
        /// <summary>
        /// Returns true if the Depth == 0 and not an orphan
        /// </summary>
        public bool PathIsRoot => !IsOrphan && Depth == -1;
        /// <summary>
        /// Returns true if the Depth == 1 and not an orphan
        /// </summary>
        public bool PathIsTopLevel => !IsOrphan && Depth == 0;
        /// <summary>
        /// Element depth<br/>
        /// - Document or orphan = -1<br/>
        /// - Root elements = 0<br/>
        /// - Top level = 1<br/>
        /// </summary>
        public int Depth => Path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length - 1;
        /// <summary>
        /// Element name
        /// </summary>
        public string? Name => SchemaElement?.Name ?? (Id == 0 ? "" : $"{Id}");
        /// <summary>
        /// Element header size + element data size
        /// </summary>
        public ulong TotalSize => DataSize + HeaderSize;
        /// <summary>
        /// Header size
        /// </summary>
        public ulong HeaderSize => ElementHeader == null ? 0 : (ulong)ElementHeader.HeaderSize;
        /// <summary>
        /// Data size
        /// </summary>
        public virtual ulong DataSize => (ulong)SegmentSource.Length;
        /// <summary>
        /// Returns true if this element is an EBMLDocument
        /// </summary>
        public virtual bool IsDocument { get; } = false;
        /// <summary>
        /// Element Id
        /// </summary>
        public ulong Id { get; protected set; }

        /// <summary>
        /// Element Id as hex string
        /// Disabled as .NET Standard 2.1 doesn't support Convert.ToHexString
        /// </summary>
        public string IdHex => ""; //$"0x{Convert.ToHexString(EBMLConverter.ToUIntBytes(Id))}";
        protected ElementHeader? _ElementHeader { get; set; }
        public ElementHeader? ElementHeader
        {
            get
            {
                if (_ElementHeader == null && !IsDocument) _ElementHeader = new ElementHeader(Id, DataSize);
                return _ElementHeader;
            }
            set => _ElementHeader = !IsDocument ? value : null;
        }
        /// <summary>
        /// The schema element info for this element
        /// </summary>
        public SchemaElement? SchemaElement { get; set; }
        /// <summary>
        /// Returns true if the element has been modified
        /// </summary>
        public bool Modified { get; set; }
        /// <summary>
        /// Element path
        /// </summary>
        public virtual string Path => Parent == null ? $@"\{Name}" : $@"{Parent.Path.TrimEnd('\\')}\{Name}";
        /// <summary>
        /// protected SegmentSource
        /// </summary>
        protected SegmentSource? _SegmentSource = null;
        /// <summary>
        /// SegmentSource
        /// </summary>
        public virtual SegmentSource SegmentSource
        {
            get
            {
                if (_SegmentSource == null)
                {
                    DataToSegmentSource(ref _SegmentSource);
                }
                return _SegmentSource!;
            }
            set
            {
                _SegmentSource = value;
                if (_SegmentSource == null) return;
                StreamChanged();
            }
        }
        private MasterElement? GetRootLevelElement()
        {
            MasterElement? ret = Parent;
            while (ret?.Parent != null)
            {
                ret = ret.Parent;
            }
            return ret;
        }
        /// <summary>
        /// Returns the EBMLDocument this element belongs to or null
        /// </summary>
        /// <returns></returns>
        public Document? GetDocumentElement() => GetRootLevelElement() as Document;
        /// <summary>
        /// Fired when the stream data has been set
        /// </summary>
        protected virtual void StreamChanged(IEnumerable<BaseElement>? changedElement = null) => throw new NotImplementedException();
        /// <summary>
        /// Constructor used by MasterElements when reading elements from its SegmentSource
        /// </summary>
        public BaseElement(ulong id, SchemaElement? schemaElement, SegmentSource source, ElementHeader? header)
        {
            Id = id;
            SchemaElement = schemaElement;
            _SegmentSource = source;
            if (_SegmentSource != null) Source = ElementDataSource.SegmentSource;
            ElementHeader = header;
        }
        /// <summary>
        /// Constructor used by typed BaseElements
        /// </summary>
        /// <param name="id"></param>
        /// <param name="schemaElement"></param>
        public BaseElement(ulong id, SchemaElement? schemaElement)
        {
            SchemaElement = schemaElement;
            Id = id;
        }
        /// <summary>
        /// Copy to stream
        /// </summary>
        /// <param name="stream"></param>
        public virtual void CopyTo(Stream stream)
        {
            SegmentSource.Seek(0, SeekOrigin.Begin);
            if (ElementHeader != null)
            {
                ElementHeader.CopyTo(stream);
            }
            SegmentSource.Position = 0;
            SegmentSource.CopyTo(stream);
        }
        /// <summary>
        /// Copy to stream
        /// </summary>
        public virtual async Task CopyToAsync(Stream stream)
        {
            SegmentSource.Seek(0, SeekOrigin.Begin);
            if (ElementHeader != null)
            {
                await ElementHeader.CopyToAsync(stream);
            }
            SegmentSource.Position = 0;
            await SegmentSource.CopyToAsync(stream);
        }
        /// <summary>
        /// Creates a new SegmentSource from the current Data
        /// </summary>
        protected virtual void DataToSegmentSource(ref SegmentSource? segmentSource) => throw new NotImplementedException();
        /// <summary>
        /// Firs the OnChanged event
        /// </summary>
        internal void Changed(IEnumerable<BaseElement>? changedElement)
        {
            changedElement ??= new List<BaseElement>();
            var changedElements = (List<BaseElement>)changedElement;
            changedElements.Add(this);
            OnChanged?.Invoke(changedElements);
        }
        /// <summary>
        /// Fired when the element data has changed
        /// </summary>
        public event Action<IEnumerable<BaseElement>> OnChanged;
        /// <summary>
        /// Returns the element as a stream
        /// </summary>
        /// <returns></returns>
        public Stream ToStream()
        {
            return ElementHeader == null ? new MultiStreamSegment(new Stream[] { SegmentSource }) : new MultiStreamSegment(new Stream[] { ElementHeader.SegmentSource, SegmentSource });
        }
        /// <summary>
        /// Returns the element as a btye array
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            using var stream = ToStream();
            byte[] output = new byte[stream.Length];
            int bytesRead = stream.Read(output, 0, output.Length);
            return output;
        }
    }
    /// <summary>
    /// base EBML element
    /// </summary>
    public abstract class BaseElement<T> : BaseElement
    {
        /// <summary>
        /// Returns true if Data is being created from SegmentSource
        /// </summary>
        public bool ProcessingSegmentSource { get; set; }
        /// <summary>
        /// Returns the data as a string
        /// </summary>
        public override string DataString
        {
            get => Data?.ToString() ?? "";
        }
        private T _Data = default(T)!;
        /// <summary>
        /// Returns true if the _Data has been set
        /// </summary>
        protected bool _DataIsValueCreated { get; set; }
        /// <summary>
        /// Returns true is the new value equals the old value
        /// </summary>
        protected virtual bool EqualCheck(T obj1, T obj2) => object.Equals(obj1, obj2);
        /// <summary>
        /// Fired after Data is read from SegmentSource
        /// </summary>
        public event Action<BaseElement> OnSegmentSourceProcessed;
        /// <summary>
        /// The data this element holds
        /// </summary>
        public virtual T Data
        {
            get
            {
                if (!_DataIsValueCreated)
                {
                    _DataIsValueCreated = true;
                    ProcessingSegmentSource = true;
                    try
                    {
                        DataFromSegmentSource(ref _Data);
                        OnSegmentSourceProcessed?.Invoke(this);
                    }
                    finally
                    {
                        ProcessingSegmentSource = false;
                    }
                }
                return _Data;
            }
            set
            {
                if (_DataIsValueCreated && EqualCheck(value, _Data)) return;
                _Data = value;
                DataChanged();
            }
        }
        /// <summary>
        /// Creates new instance with data, and unknown EBMLSchemaElement
        /// </summary>
        public BaseElement(SchemaElement schemaElement, T data) : base(schemaElement.Id, schemaElement)
        {
            Source = ElementDataSource.Data;
            _Data = data;
            _DataIsValueCreated = true;
        }
        /// <summary>
        /// Creates new instance with data, and unknown EBMLSchemaElement
        /// </summary>
        public BaseElement(ulong id, T data) : base(id, null)
        {
            Source = ElementDataSource.Data;
            _Data = data;
            _DataIsValueCreated = true;
        }
        /// <summary>
        /// Creates new instance from SegmentSource
        /// </summary>
        public BaseElement(SchemaElement? schemaElement, SegmentSource source, ElementHeader? header) : base(schemaElement?.Id ?? header?.Id ?? 0, schemaElement, source, header)
        {
            // from SegmentSource
        }
        /// <summary>
        /// This method must must convert SegmentSource and to type T<br/>
        /// Starts at 0 and is the full size of the SegmentSource unless unknown size
        /// </summary>
        protected abstract void DataFromSegmentSource(ref T data);
        /// <summary>
        /// Fired when Data has been set
        /// </summary>
        protected virtual void DataChanged(IEnumerable<BaseElement>? changedElement = null)
        {
            _DataIsValueCreated = true;
            Source = ElementDataSource.Data;
            Modified = true;
            ElementHeader = null;
            _SegmentSource = null;
            Changed(changedElement);
        }
        /// <summary>
        /// Fired when SegmentSource has been set
        /// </summary>
        protected override void StreamChanged(IEnumerable<BaseElement>? changedElement = null)
        {
            _DataIsValueCreated = false;
            Source = ElementDataSource.SegmentSource;
            Modified = true;
            ElementHeader = null;
            _Data = default(T)!;
            Changed(changedElement);
        }
    }
}
