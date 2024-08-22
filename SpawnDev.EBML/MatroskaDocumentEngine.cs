using System;
using System.Collections.Generic;
using System.Linq;
using SpawnDev.EBML.Elements;
using SpawnDev.EBML.Extensions;

namespace SpawnDev.EBML
{
    /// <summary>
    /// Matroska EBML document engine
    /// </summary>
    public class MatroskaDocumentEngine : DocumentEngine
    {
        /// <summary>
        /// DocTypes this engine supports
        /// </summary>
        public override string[] DocTypes { get; } = new string[] { "matroska", "webm" };
        /// <summary>
        /// This constructor can take an existing EBMLDocument and parse it<br/>
        /// This is used by the generic Parser
        /// </summary>
        public MatroskaDocumentEngine(Document document) : base(document)
        {
            Document.OnElementAdded += Document_OnElementAdded;
            Document.OnElementRemoved += Document_OnElementRemoved;
            Document.OnChanged += Document_OnChanged;
        }
        public bool GetSeeks(out MasterElement? segmentElement, out ulong segmentStart, out MasterElement? seekHeadElement, out List<Seek>? seeks)
        {
            segmentElement = Document.GetContainer("Segment");
            if (segmentElement == null)
            {
                seekHeadElement = null;
                seeks = null;
                segmentStart = 0;
                return false;
            }
            segmentStart = segmentElement.Offset + segmentElement.HeaderSize;
            seeks = new List<Seek>();
            seekHeadElement = Document.GetContainer(@"\Segment\SeekHead");
            if (seekHeadElement == null) return false;
            var seekElements = seekHeadElement.GetContainers("Seek");
            foreach (var seek in seekElements)
            {
                seeks.Add(new Seek(seek, segmentStart));
            }
            return true;
        }
        public class Seek
        {
            public MasterElement? SeekElement { get; set; }
            public BinaryElement? SeekIdElement { get; set; }
            public UintElement? SeekPositionElement { get; set; }
            public ulong TargetId { get; set; }
            public ulong SeekPosition { get; set; }
            public Seek(MasterElement seekElement, ulong segmentDataStartPosition)
            {
                SeekElement = seekElement;
                SeekIdElement = SeekElement.GetElement<BinaryElement>("SeekID");
                SeekPositionElement = SeekElement.GetElement<UintElement>("SeekPosition");
                TargetId = SeekIdElement == null ? 0 : EBMLConverter.ReadEBMLUInt(SeekIdElement.Data);
                SeekPosition = SeekPositionElement == null ? 0 : SeekPositionElement.Data + segmentDataStartPosition;
            }
        }
        /// <summary>
        /// Fires when any document element changes<br/>
        /// When an element changes, this event will fire for the element that changes and for every one of its parent elements up the chain
        /// </summary>
        /// <param name="elements">The element that changed</param>
        private void Document_OnChanged(IEnumerable<BaseElement> elements)
        {
            var element = elements.First();
            //Console.WriteLine($"MKVE: Document_OnChanged: {elements.Count()} {element.Depth} {element.Name} {element.Path}");
            // Verify SeekPosition element values if any
            UpdateSeekHead();
        }
        public bool AutoPopulateSeekHead { get; set; } = true;
        /// <summary>
        /// If AutoPopulateSeekHead == true these Top level elements will have Seeks created for them if they do not already exist
        /// </summary>
        public List<string> DefaultSeekHeadTargets = new List<string> { "Info", "Tracks", "Chapters", "Cues", "Attachments" };
        public bool UpdateSeekHeadOnChange { get; set; } = true;
        public bool VerifySeekHeadOnChange { get; set; } = true;
        bool UpdatingSeekHead = false;
        void UpdateSeekHead()
        {
            if (!DocTypeSupported) return;
            if (!VerifySeekHeadOnChange && !UpdateSeekHeadOnChange)
            {
                return;
            }
            if (UpdatingSeekHead)
            {
                Console.WriteLine("Not UpdateSeekHead due to already in progress");
                //return;
            }
            Console.WriteLine(">> UpdateSeekHead");
            UpdatingSeekHead = true;
            try
            {
                if (GetSeeks(out var segmentElement, out var segmentStart, out var seekHeadElement, out var seeks))
                {
                    var requiredTargets = DefaultSeekHeadTargets.ToList();
                    var seekSchema = Document.SchemaSet.GetElement("Seek", Document.DocType);
                    if (seekSchema == null) return;
                    var seekIDSchema = Document.SchemaSet.GetElement("SeekID", Document.DocType);
                    var seekPositionSchema = Document.SchemaSet.GetElement("SeekPosition", Document.DocType);
                    if (seekIDSchema == null || seekPositionSchema == null)
                    {
                        return;
                    }
                    foreach (var requiredTarget in DefaultSeekHeadTargets)
                    {
                        var targetElement = segmentElement!.GetContainer(requiredTarget);
                        if (targetElement == null)
                        {
                            // target does not exist, we can skip it
                            requiredTargets.Remove(requiredTarget);
                            continue;
                        }
                        var targetElementPosition = targetElement!.Offset;
                        var targetSeekPosition = targetElementPosition - segmentStart;
                        var targetSeek = seeks!.FirstOrDefault(o => o.TargetId == targetElement.Id);
                        if (targetSeek == null)
                        {
                            // seek does not exist
                            if (AutoPopulateSeekHead)
                            {
                                // create seek
                                var idUint = EBMLConverter.ToUIntBytes(targetElement.Id);
                                var newSeekEl = new MasterElement(Document.SchemaSet, seekSchema);
                                var newSeekIdEl = new BinaryElement(seekIDSchema, idUint);
                                var newSeekPositionEl = new UintElement(seekPositionSchema, targetSeekPosition);
                                newSeekEl.AddElement(newSeekIdEl);
                                newSeekEl.AddElement(newSeekPositionEl);
                                // attach to document
                                seekHeadElement!.AddElement(newSeekEl);
                                break;
                            }
                            else
                            {
                                // do not create
                            }
                        }
                        else
                        {
                            // seekID el has already been verified to exist
                            // make sure position el exists and the value is 
                            if (targetSeek.SeekPositionElement == null)
                            {
                                var newSeekPositionEl = new UintElement(seekPositionSchema, targetSeekPosition);
                                seekHeadElement!.AddElement(newSeekPositionEl);
                                Console.WriteLine("Added seek position");
                                break;
                            }
                            else if (targetSeek.SeekPositionElement.Data != targetSeekPosition)
                            {
                                targetSeek.SeekPositionElement.Data = targetSeekPosition;
                                Console.WriteLine("Updated seek position");
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                Console.WriteLine("<< UpdateSeekHead");
                UpdatingSeekHead = false;
            }
        }
        private void Document_OnElementAdded(MasterElement masterElement, BaseElement element)
        {

        }
        private void Document_OnElementRemoved(MasterElement masterElement, BaseElement element)
        {
            //Console.WriteLine($"MKVE: Document_OnElementRemoved: {element.Depth} {masterElement.Path}\\{element.Name}");
        }
    }
}

