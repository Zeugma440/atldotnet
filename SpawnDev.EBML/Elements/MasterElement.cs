using System;
using System.Collections.Generic;
using System.Linq;
using SpawnDev.EBML.Crc32;
using SpawnDev.EBML.Segments;

namespace SpawnDev.EBML.Elements
{
    public class MasterElement : BaseElement<IEnumerable<BaseElement>>
    {
        static Crc32Algorithm CRC = new Crc32Algorithm(false);
        public EBMLSchemaSet SchemaSet { get; }
        public const string TypeName = "master";
        public override string DataString => $"";
        public event Action<MasterElement, BaseElement> OnElementAdded;
        public event Action<MasterElement, BaseElement> OnElementRemoved;
        public MasterElement(EBMLSchemaSet schemas, EBMLSchemaElement schemaElement, SegmentSource source, ElementHeader? header = null) : base(schemaElement, source, header)
        {
            SchemaSet = schemas;
        }
        public MasterElement(EBMLSchemaSet schemas, SegmentSource source, ElementHeader? header = null) : base(null, source, header)
        {
            SchemaSet = schemas;
        }
        public MasterElement(EBMLSchemaSet schemas, EBMLSchemaElement schemaElement) : base(schemaElement, new List<BaseElement>())
        {
            SchemaSet = schemas;
        }
        public MasterElement(EBMLSchemaSet schemas, ulong id) : base(id, new List<BaseElement>())
        {
            SchemaSet = schemas;
        }
        public MasterElement(EBMLSchemaSet schemas) : base(0, new List<BaseElement>())
        {
            SchemaSet = schemas;
        }
        /// <summary>
        /// Returns a list of EBMLSchemaElement that can be added to this MasterElement
        /// </summary>
        /// <param name="includeMaxCountItems"></param>
        /// <returns></returns>
        public IEnumerable<EBMLSchemaElement> GetAddableElementSchemas(bool includeMaxCountItems = false)
        {
            var ret = new List<EBMLSchemaElement>();
            var allSchemaElements = SchemaSet.GetElements(DocType);
            foreach (var addable in allSchemaElements.Values)
            {
                var parentAllowed = SchemaSet.CheckParent(this, addable);
                if (!parentAllowed) continue;
                if (!includeMaxCountItems)
                {
                    var atMaxCount = false;
                    if (addable.MaxOccurs > 0 || addable.MinOccurs > 0)
                    {
                        var count = Data.Count(o => o.Id == addable.Id);
                        atMaxCount = addable.MaxOccurs > 0 && count >= addable.MaxOccurs;
                    }
                    if (atMaxCount) continue;
                }
                ret.Add(addable);
            }
            return ret;
        }
        public int GetChildIndex(BaseElement element)
        {
            var data = (List<BaseElement>)Data;
            return data.IndexOf(element);
        }
        /// <summary>
        /// Returns a list of EBMLSchemaElement for elements that do not occur in this MasterElement as many times as there EBML minOccurs value states it should
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EBMLSchemaElement> GetMissingElementSchemas()
        {
            var ret = new List<EBMLSchemaElement>();
            var allSchemaElements = SchemaSet.GetElements(DocType);
            foreach (var addable in allSchemaElements.Values)
            {
                var parentAllowed = SchemaSet.CheckParent(this, addable);
                if (!parentAllowed) continue;
                var requiresAdd = false;
                if (addable.MinOccurs > 0)
                {
                    var count = Data.Count(o => o.Id == addable.Id);
                    requiresAdd = count < addable.MinOccurs;
                    if (requiresAdd) ret.Add(addable);
                }
            }
            return ret;
        }
        public IEnumerable<MasterElement> AddMissingContainers()
        {
            var missing = GetMissingElementSchemas();
            var masterEls = missing.Where(o => o.Type == MasterElement.TypeName).Select(o => AddElement(o)).Cast<MasterElement>().ToList();
            return masterEls;
        }
        public MasterElement? AddContainer(string name)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != MasterElement.TypeName) throw new Exception("Invalid element type");
            var masterEl = new MasterElement(SchemaSet, schemaElement);
            AddElement(masterEl);
            return masterEl;
        }
        public UTF8Element? AddUTF8(string name, string data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != UTF8Element.TypeName) throw new Exception("Invalid element type");
            var element = new UTF8Element(schemaElement, data);
            AddElement(element);
            return element;
        }
        public StringElement? AddString(string name, string data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != StringElement.TypeName) throw new Exception("Invalid element type");
            var element = new StringElement(schemaElement, data);
            AddElement(element);
            return element;
        }
        public UintElement? AddUint(string name, ulong data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != UintElement.TypeName) throw new Exception("Invalid element type");
            var element = new UintElement(schemaElement, data);
            AddElement(element);
            return element;
        }
        public IntElement? AddInt(string name, long data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != IntElement.TypeName) throw new Exception("Invalid element type");
            var element = new IntElement(schemaElement, data);
            AddElement(element);
            return element;
        }
        public FloatElement? AddFloat(string name, double data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != FloatElement.TypeName) throw new Exception("Invalid element type");
            var element = new FloatElement(schemaElement, data);
            AddElement(element);
            return element;
        }
        public DateElement? AddDate(string name, DateTime data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != DateElement.TypeName) throw new Exception("Invalid element type");
            var element = new DateElement(schemaElement, data);
            AddElement(element);
            return element;
        }
        public BinaryElement? AddBinary(string name, byte[] data)
        {
            var schemaElement = SchemaSet.GetEBMLSchemaElement(name, DocType);
            if (schemaElement == null || schemaElement.Type != BinaryElement.TypeName) throw new Exception("Invalid element type");
            var element = new BinaryElement(schemaElement, data);
            AddElement(element);
            return element;
        }
        public TElement AddElement<TElement>(EBMLSchemaElement elementSchema) where TElement : BaseElement
        {
            var element = Create<TElement>(elementSchema);
            if (element == null) return null;
            return AddElement(element);
        }
        public BaseElement? AddElement(EBMLSchemaElement elementSchema)
        {
            var element = Create(elementSchema);
            if (element == null) return null;
            return AddElement(element);
        }
        /// <summary>
        /// Updates the container's CRC-32 element if it has one
        /// </summary>
        /// <returns>Returns true if the CRC was updated</returns>
        public bool UpdateCRC()
        {
            var crcEl = Data.FirstOrDefault(o => o is BinaryElement && o.Name == "CRC-32");
            if (crcEl is BinaryElement binaryElement)
            {
                Console.WriteLine($"Verifying CRC in: {Path}");
                var crc = CalculateCRC();
                if (crc != null && !binaryElement.Data.SequenceEqual(crc))
                {
                    Console.WriteLine($"CRC about to update: {Path}");
                    binaryElement.Data = crc;
                    Console.WriteLine($"CRC updated: {Path}");
                    return true;
                }
            }
            return false;
        }
        public byte[]? CalculateCRC()
        {
            var crcSchema = SchemaSet.GetEBMLSchemaElement("CRC-32");
            if (crcSchema == null) return null;
            var dataToCRC = Data.Where(o => o.Id != crcSchema.Id).Select(o => o.ToStream());
            using var stream = new MultiStreamSegment(dataToCRC);
            var hash = CRC.ComputeHash(stream);
            return hash;
        }
        public TElement RemoveElement<TElement>(TElement element) where TElement : BaseElement
        {
            var data = (List<BaseElement>)Data;
            if (!data.Contains(element)) return element;
            data.Remove(element);
            element.OnChanged -= Child_OnChanged;
            if (element is MasterElement masterElement)
            {
                masterElement.OnElementAdded -= Child_OnElementAdded;
                masterElement.OnElementRemoved -= Child_ElementRemoved;
            }
            if (element.Parent == this) element.Remove();
            OnElementRemoved?.Invoke(this, element);
            DataChanged();
            return element;
        }
        public TElement AddElement<TElement>(TElement element) where TElement : BaseElement
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            if (element.SchemaElement == null)
            {
                throw new ArgumentNullException(nameof(element.SchemaElement));
            }
            var data = (List<BaseElement>)Data;
            if (data.Contains(element))
            {
                return element;
            }
            var added = false;
            if (element.SchemaElement.Position != null)
            {
                if (element.SchemaElement.Position.Value == 0)
                {
                    // first
                    var index = 0;
                    for (var i = 0; i < data.Count; i++)
                    {
                        var item = data[i];
                        if (item.SchemaElement == null) break;
                        if (item.SchemaElement.Position == null) break;
                        var itemPos = item.SchemaElement.Position.Value;
                        if (itemPos > 0) break;
                        if (item.SchemaElement.PositionWeight < element.SchemaElement.PositionWeight) break;
                        index = i;
                    }
                    added = true;
                    data.Insert(index, element);
                }
                else if (element.SchemaElement.Position.Value == -1)
                {
                    // last
                    // TODO
                }
            }
            if (!added) data.Add(element);
            if (element.Parent != this) element.SetParent(this);
            element.OnChanged += Child_OnChanged;
            if (element is MasterElement masterElement)
            {
                masterElement.OnElementAdded += Child_OnElementAdded;
                masterElement.OnElementRemoved += Child_ElementRemoved;
            }
            OnElementAdded?.Invoke(this, element);
            DataChanged(new List<BaseElement> { element });
            return element;
        }
        private void Child_ElementRemoved(MasterElement masterElement, BaseElement element)
        {
            OnElementRemoved?.Invoke(masterElement, element);
        }
        private void Child_OnElementAdded(MasterElement masterElement, BaseElement element)
        {
            OnElementAdded?.Invoke(masterElement, element);
        }
        public TElement InsertElement<TElement>(TElement element, int index = 0) where TElement : BaseElement
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            var data = (List<BaseElement>)Data;
            if (data.Contains(element)) return element;
            data.Insert(0, element);
            if (element.Parent != this) element.SetParent(this);
            element.OnChanged += Child_OnChanged;
            if (element is MasterElement masterElement)
            {
                masterElement.OnElementAdded += Child_OnElementAdded;
                masterElement.OnElementRemoved += Child_ElementRemoved;
            }
            OnElementAdded?.Invoke(this, element);
            DataChanged(new List<BaseElement> { element });
            return element;
        }
        private void Child_OnChanged(IEnumerable<BaseElement> elements)
        {
            DataChanged(elements);
        }
        public string? ReadUTF8(string path)
        {
            var els = GetElements<UTF8Element>(path).FirstOrDefault();
            return els?.Data;
        }
        public string? ReadASCII(string path)
        {
            var els = GetElements<StringElement>(path).FirstOrDefault();
            return els?.Data;
        }
        public string? ReadString(string path)
        {
            var stringElement = GetElements<BaseElement>(path).FirstOrDefault();
            if (stringElement == null) return null;
            if (stringElement is UTF8Element stringUTF8) return stringUTF8.Data;
            if (stringElement is StringElement stringASCII) return stringASCII.Data;
            throw new Exception("Unknown type");
        }
        //public ulong CalculatedSize
        //{
        //    get
        //    {
        //        ulong ret = 0;
        //        var children = Data;
        //        foreach (var child in children)
        //        {
        //            if (child is MasterElement masterElement)
        //            {
        //                ret += masterElement.CalculatedSize;
        //            }
        //            else
        //            {
        //                ret += child.TotalSize;
        //            }
        //        }
        //        return ret;
        //    }
        //}
        /// <summary>
        /// Returns all children recursively
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BaseElement> GetDescendants()
        {
            var ret = new List<BaseElement>();
            var children = Data;
            ret.AddRange(children);
            foreach (var child in children)
            {
                if (child is MasterElement masterElement)
                {
                    ret.AddRange(masterElement.GetDescendants());
                }
            }
            return ret;
        }
        public BaseElement? GetContainer(ulong id) => GetElement<MasterElement>(id);
        public IEnumerable<MasterElement> GetContainers(ulong id) => GetElements<MasterElement>(id);
        public TElement? GetElement<TElement>(ulong id) where TElement : BaseElement => Data.FirstOrDefault(o => o.Id == id && o is TElement) as TElement;
        public IEnumerable<TElement> GetElements<TElement>(ulong id) where TElement : BaseElement => Data.Where(o => o.Id == id && o is TElement).Cast<TElement>().ToList();
        public BaseElement? GetElement(ulong id) => Data.FirstOrDefault(o => o.Id == id);
        public IEnumerable<BaseElement> GetElements(ulong id) => Data.Where(o => o.Id == id).ToList();
        public BaseElement? GetElement(string path) => GetElements(path).FirstOrDefault();
        public TElement? GetElement<TElement>(string path) where TElement : BaseElement => GetElements<TElement>(path).FirstOrDefault();
        public IEnumerable<TElement> GetElements<TElement>(string path) where TElement : BaseElement
        {
            var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            if (parts.Length == 0)
            {
                return this is TElement ? new TElement[] { (this as TElement)! } : Enumerable.Empty<TElement>();
            }
            var masterEls = new List<MasterElement> { this };
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var elementName = parts[i];
                masterEls = masterEls.SelectMany(o => o.Data.Where(o => o.Name == elementName && o is MasterElement).Cast<MasterElement>()).ToList();
                if (masterEls.Count == 0) return Enumerable.Empty<TElement>();
            }
            var elementNameFinal = parts.Last();
            var results = masterEls.SelectMany(o => o.Data.Where(o => o.Name == elementNameFinal && o is TElement)!).Cast<TElement>().ToList();
            return results;
        }
        public IEnumerable<BaseElement> GetElements(string path) => GetElements<BaseElement>(path);
        public IEnumerable<MasterElement> GetContainers(string path) => GetElements<MasterElement>(path);
        public MasterElement? GetContainer(string path) => GetElements<MasterElement>(path).FirstOrDefault();
        protected override void DataFromSegmentSource(ref IEnumerable<BaseElement> _data)
        {
            SegmentSource.Position = 0;
            var source = SegmentSource;
            var data = new List<BaseElement>();
            _data = data;
            var isUnknownSize = _ElementHeader != null && _ElementHeader.Size == null;
            var parsingDocType = DocType ?? EBMLSchemaSet.EBML;
            while (true)
            {
                BaseElement? element = null;
                var elementHeaderOffset = source.Position;
                if (source.Position == source.Length)
                {
                    break;
                }
                ElementHeader elementHeader;
                try
                {
                    elementHeader = ElementHeader.Read(source);
                }
                catch (Exception ex)
                {
                    break;
                }
                var elementDataOffset = source.Position;
                var id = elementHeader.Id;
                var schemaElement = SchemaSet.GetEBMLSchemaElement(id, parsingDocType);
                if (schemaElement == null)
                {
                    var nmttt = true;
                }
                var elementDataSize = elementHeader.Size;
                var elementName = schemaElement?.Name ?? $"{id}";
                // The end of an Unknown - Sized Element is determined by whichever comes first:
                // - Any EBML Element that is a valid Parent Element of the Unknown - Sized Element according to the EBML Schema, Global Elements excluded.
                // - Any valid EBML Element according to the EBML Schema, Global Elements excluded, that is not a Descendant Element of the Unknown-Sized Element but shares a common direct parent, such as a Top - Level Element.
                // - Any EBML Element that is a valid Root Element according to the EBML Schema, Global Elements excluded.
                // - The end of the Parent Element with a known size has been reached.
                // - The end of the EBML Document, either when reaching the end of the file or because a new EBML Header started.
                var elementDataSizeMax = elementDataSize ?? (ulong)(SegmentSource.Length - elementDataOffset);
                var elementSegmentSource = SegmentSource.Slice(elementDataOffset, (long)elementDataSizeMax);
                var canParent = SchemaSet.CheckParent(this, schemaElement);
                if (!canParent)
                {
                    source.Position = elementDataOffset;
                    break;
                }
                else
                {
                    element = Create(schemaElement, elementSegmentSource, elementHeader);
                    if (element == null)
                    {
                        element = new BinaryElement(schemaElement, source, elementHeader);
                        //ret = new BaseElement(id, schemaElement, elementSegmentSource, elementHeader);
                    }
                }
                if (element == null)
                {
                    break;
                }
                data.Add(element);
                element.SetParent(this);
                element.OnChanged += Child_OnChanged;
                if (element is MasterElement masterElement)
                {
                    masterElement.OnElementAdded += Child_OnElementAdded;
                    masterElement.OnElementRemoved += Child_ElementRemoved;
                    if (element.Path == @"\EBML")
                    {
                        var newDocType = masterElement.ReadString("DocType");
                        if (!string.IsNullOrEmpty(newDocType))
                        {
                            parsingDocType = newDocType;
                        }
                    }
                }
                SegmentSource.Position = (long)elementDataSizeMax + elementDataOffset;
            }
            if (isUnknownSize)
            {
                // - create a new header with the actual size of the master element
                // - re-slice the SegmentSource so it is only as big as the element
                var last = data.LastOrDefault();
                var measuredSize = last == null ? 0 : (long)last.SegmentSource.Offset + (long)last.DataSize - (long)SegmentSource.Offset;
                if (last != null)
                {
                    if (ElementHeader != null) ElementHeader.Size = (ulong)measuredSize;
                    _SegmentSource = SegmentSource.Slice(0, measuredSize);
                }
            }
        }
        private BaseElement? Create(EBMLSchemaElement? schemaElement, SegmentSource source, ElementHeader? header = null)
        {
            if (schemaElement == null) return null;
            var type = SchemaSet.GetElementType(schemaElement.Type);
            if (type == null) return null;
            BaseElement? ret = schemaElement.Type switch
            {
                MasterElement.TypeName => new MasterElement(SchemaSet, schemaElement, source, header),
                UintElement.TypeName => new UintElement(schemaElement, source, header),
                IntElement.TypeName => new IntElement(schemaElement, source, header),
                FloatElement.TypeName => new FloatElement(schemaElement, source, header),
                StringElement.TypeName => new StringElement(schemaElement, source, header),
                UTF8Element.TypeName => new UTF8Element(schemaElement, source, header),
                BinaryElement.TypeName => new BinaryElement(schemaElement, source, header),
                DateElement.TypeName => new DateElement(schemaElement, source, header),
                _ => null
            };
            return ret;
        }
        TElement? Create<TElement>(EBMLSchemaElement? schemaElement) where TElement : BaseElement
        {
            if (schemaElement == null) return null;
            var type = SchemaSet.GetElementType(schemaElement.Type);
            if (type == null) return null;
            if (!typeof(TElement).IsAssignableFrom(type)) throw new Exception("Create type mismatch");
            BaseElement? ret = schemaElement.Type switch
            {
                MasterElement.TypeName => new MasterElement(SchemaSet, schemaElement),
                UintElement.TypeName => new UintElement(schemaElement),
                IntElement.TypeName => new IntElement(schemaElement),
                FloatElement.TypeName => new FloatElement(schemaElement),
                StringElement.TypeName => new StringElement(schemaElement),
                UTF8Element.TypeName => new UTF8Element(schemaElement),
                BinaryElement.TypeName => new BinaryElement(schemaElement),
                DateElement.TypeName => new DateElement(schemaElement),
                _ => null
            };
            return (TElement?)ret;
        }
        BaseElement? Create(EBMLSchemaElement? schemaElement)
        {
            if (schemaElement == null) return null;
            var type = SchemaSet.GetElementType(schemaElement.Type);
            if (type == null) return null;
            BaseElement? ret = schemaElement.Type switch
            {
                MasterElement.TypeName => new MasterElement(SchemaSet, schemaElement),
                UintElement.TypeName => new UintElement(schemaElement),
                IntElement.TypeName => new IntElement(schemaElement),
                FloatElement.TypeName => new FloatElement(schemaElement),
                StringElement.TypeName => new StringElement(schemaElement),
                UTF8Element.TypeName => new UTF8Element(schemaElement),
                BinaryElement.TypeName => new BinaryElement(schemaElement),
                DateElement.TypeName => new DateElement(schemaElement),
                _ => null
            };
            return ret;
        }
        protected override void DataToSegmentSource(ref SegmentSource source)
        {
            source = new MultiStreamSegment(Data.Select(o => o.ToStream()).ToArray());
        }
    }
}
