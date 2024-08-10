using System;

namespace SpawnDev.EBML.Matroska
{
    public enum TrackType : byte
    {
        Video = 1,
        Audio = 2,
        Complex = 3,
        Logo = 0x10,
        Subtitle = 0x11,
        Buttons = 0x12,
        Control = 0x20,
    }

    public class TrackEntryElement : MasterElement
    {
        public TrackEntryElement(Enum id) : base(id)
        {
        }
        public ulong TrackNumber
        {
            get => (ulong)GetElement<UintElement>(MatroskaId.TrackNumber);
            set => GetElement<UintElement>(MatroskaId.TrackNumber)!.Data = value;
        }
        public ulong TrackUID
        {
            get => (ulong)GetElement<UintElement>(MatroskaId.TrackUID);
        }
        public TrackType TrackType
        {
            get => (TrackType)(byte)(ulong)GetElement<UintElement>(MatroskaId.TrackType);
        }
        public bool FlagEnabled
        {
            get => 1 == (GetElement<UintElement>(MatroskaId.FlagEnabled)?.Data ?? 1);
        }
        public bool FlagDefault
        {
            get => 1 == (GetElement<UintElement>(MatroskaId.FlagDefault)?.Data ?? 1);
        }
        public string CodecID
        {
            get => (string)GetElement<StringElement>(MatroskaId.CodecID)!;
        }
        public string Language
        {
            get => (string)GetElement<StringElement>(MatroskaId.Language)!;
        }
        public ulong DefaultDuration
        {
            get => (ulong)GetElement<UintElement>(MatroskaId.DefaultDuration);
        }
    }
}
