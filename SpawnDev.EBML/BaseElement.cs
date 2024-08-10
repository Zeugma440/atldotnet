using System;
using System.Collections.Generic;
using System.IO;

namespace SpawnDev.EBML
{
    /// <summary>
    /// Base element class
    /// </summary>
    public abstract class BaseElement
    {
        public BaseElement? GetPreviousSibling()
        {
            if (Parent == null) return null;
            var index = Parent.Data.IndexOf(this);
            if (index < 1) return null;
            return Parent.Data[index - 1];
        }
        public BaseElement? GetNextSibling()
        {
            if (Parent == null) return null;
            var index = Parent.Data.IndexOf(this);
            if (index == -1) return null;
            if (index >= Parent.Data.Count - 1) return null;
            return Parent.Data[index + 1];
        }
        //public long BaseStreamOffset
        //{
        //    get
        //    {
        //        if (Parent == null) return 0;
        //        var prev = GetPreviousSibling();
        //        if (prev == null) return 0;
        //        return Parent.BaseStreamOffset + prev.BaseStreamOffset + prev.Length;
        //    }
        //}
        public int DocumentDepth => IdChain.Length == 0 ? 0 : IdChain.Length - 1;
        /// <summary>
        /// The 0 based index of this item in the parent container, or 0 if not in a container
        /// </summary>
        public int Index => Parent == null ? 0 : Parent.Data.IndexOf(this);
        /// <summary>
        /// Returns the parent element this element belongs to, or null if it has no parent
        /// </summary>
        public MasterElement? Parent { get; private set; }
        /// <summary>
        /// Returns true of this element or any descendant has been modified
        /// </summary>
        public bool DataChanged { get; protected set; }
        /// <summary>
        /// Returns the ElementId of this element
        /// </summary>
        public Enum Id { get; set; }
        /// <summary>
        /// An array of ElementIds ending with this elements id, preceded by this element's parent's id, and so on
        /// </summary>
        public Enum[] IdChain { get; protected set; }
        protected Lazy<SegmentSource?> _DataStream = new Lazy<SegmentSource?>();
        /// <summary>
        /// The segment source of this element
        /// </summary>
        public SegmentSource? Stream
        {
            get => _DataStream.Value;
            set
            {
                _DataStream = new Lazy<SegmentSource?>(value);
                UpdateBySource();
            }
        }
        /// <summary>
        /// Returns the size in bytes of this element
        /// </summary>
        public virtual long Length => Stream != null ? Stream.Length : 0;
        /// <summary>
        /// Constructs source less instance with the given element id
        /// </summary>
        /// <param name="id"></param>
        public BaseElement(Enum id)
        {
            Id = id;
            IdChain = Id.Equals(ElementId.EBMLSource) ? Array.Empty<Enum>() : new Enum[] { Id };
        }
        /// <summary>
        /// Remove this element from its parent
        /// </summary>
        /// <returns>Returns true if element has a parent and was successfully removed</returns>
        public bool Remove() => Parent == null ? false : Parent.Remove(this);
        internal void SetParent(MasterElement? parent = null)
        {
            if (Parent == parent) return;
            if (Parent != null) Parent.Remove(this);
            Parent = parent;
            if (parent != null)
            {
                var idChain = new List<Enum>(parent.IdChain);
                if (!Id.Equals(ElementId.EBMLSource)) idChain.Add(Id);
                IdChain = idChain.ToArray();
            }
            else
            {
                IdChain = Id.Equals(ElementId.EBMLSource) ? Array.Empty<Enum>() : new Enum[] { Id };
            }
        }
        /// <summary>
        /// Copies the element to a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public virtual long CopyTo(Stream stream, int? bufferSize = null)
        {
            if (Stream == null)
            {
                return 0;
            }
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
            if (bytesWritten != Length || bytesWritten == 0)
            {
                var nmt = true;
            }
            return Length;
        }
        /// <summary>
        /// Should be overridden and internally update WebMBase.Data when called.<br />
        /// </summary>
        public virtual void UpdateBySource()
        {
            //
        }
        /// <summary>
        /// Returns a string that gives information about this element
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Index} {Id} - IdChain: [ {IdChain.ToString(", ")} ] Type: {this.GetType().Name} Length: {Length} bytes";
        /// <summary>
        /// Called when an elements Data is changed
        /// </summary>
        public event Action<BaseElement> OnDataChanged;
        /// <summary>
        /// Should be called when Data is changed
        /// </summary>
        protected void DataChangedInvoke()
        {
            DataChanged = true;
            OnDataChanged?.Invoke(this);
        }//}
        //public static int GetElementIdSize(ElementId x) => GetVINTSize((long)x);
        //public static int GetVINTSize(long x)
        //{
        //    int bytes;
        //    int flag;
        //    for (bytes = 1, flag = 0x80; x >= flag && bytes < 8; bytes++, flag *= 0x80) { }
        //    return bytes;
        //}
        //public static byte[] GetElementIdBytes(ElementId x) => GetVINTBytes((long)x);
        //public static byte[] GetVINTBytes(long x)
        //{
        //    int bytes;
        //    int flag;
        //    for (bytes = 1, flag = 0x80; x >= flag && bytes < 8; bytes++, flag *= 0x80) { }
        //    var ret = new byte[bytes];
        //    var value = flag + x;
        //    for (var i = bytes - 1; i >= 0; i--)
        //    {
        //        var c = value % 256;
        //        ret[i] = (byte)c;
        //        value = (value - c) / 256;
        //    }
        //    return ret;
        //}
    }
    /// <summary>
    /// A typed BaseElement
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BaseElement<T> : BaseElement
    {
        protected Lazy<T> _DataValue = new Lazy<T>();
        //private T _Data = default(T);
        /// <summary>
        /// The data contained in this element
        /// </summary>
        public virtual T Data
        {
            get => _DataValue.Value;
            set
            {
                //var isEqual = EqualityComparer<T>.Default.Equals(_Data, value);
                //if (isEqual) return;
                _DataValue = new Lazy<T>(value);
                //_Data = value;
                UpdateByData();
                DataChangedInvoke();
            }
        }
        public BaseElement(Enum id) : base(id) { }
        /// <summary>
        /// Should be overridden and internally update WebMBase.Source when called
        /// </summary>
        public virtual void UpdateByData()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Should be overridden and internally update WebMBase.Data when called
        /// </summary>
        public override void UpdateBySource()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Provides information specific to this instance
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Index} {Id} - IdChain: [ {IdChain.ToString(", ")} ] Type: {this.GetType().Name} Length: {Length} bytes Value: {Data}";
    }
}
