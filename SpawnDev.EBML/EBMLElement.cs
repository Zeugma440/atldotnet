using System;

namespace SpawnDev.EBML
{
    public class EBMLElement : MasterElement
    {
        public EBMLElement(Enum id) : base(id) { }
        public uint? EBMLVersion => (uint?)GetElement<UintElement>(ElementId.EBMLVersion);
        public uint? EBMLReadVersion => (uint?)GetElement<UintElement>(ElementId.EBMLReadVersion);
        public uint? EBMLMaxIDLength => (uint?)GetElement<UintElement>(ElementId.EBMLMaxIDLength);
        public uint? EBMLMaxSizeLength => (uint?)GetElement<UintElement>(ElementId.EBMLMaxSizeLength);
        public string? DocType => (string?)GetElement<StringElement>(ElementId.DocType);
        public uint? DocTypeVersion => (uint?)GetElement<UintElement>(ElementId.DocTypeVersion);
        public uint? DocTypeReadVersion => (uint?)GetElement<UintElement>(ElementId.DocTypeReadVersion);
    }
}
