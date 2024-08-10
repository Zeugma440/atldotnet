namespace SpawnDev.EBML
{
    public enum ElementId : ulong
    {
        EBMLSource = ulong.MaxValue,
        EBML = 0xa45dfa3,
        EBMLVersion = 0x286,
        EBMLReadVersion = 0x2f7,
        EBMLMaxIDLength = 0x2f2,
        EBMLMaxSizeLength = 0x2f3,
        DocType = 0x282,
        DocTypeVersion = 0x287,
        DocTypeReadVersion = 0x285,
        DocTypeExtension = 0x281,
        DocTypeExtensionName = 0x283,
        DocTypeExtensionVersion = 0x284,
        Void = 0x6c,
        CRC32 = 0x3f,
    }
}
