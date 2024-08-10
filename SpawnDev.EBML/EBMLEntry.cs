namespace SpawnDev.EBML
{
    public class EBMLEntry<T> where T : struct
    {
        public long HeadOffset { get; set; }
        public T Id { get; set; }
        public long DataOffset { get; set; }
        public ulong DataSize { get; set; }
        public EBMLEntry(long headOffset, T id, long dataOffset, ulong dataSize)
        {
            HeadOffset = headOffset;
            Id = id;
            DataOffset = dataOffset;
            DataSize = dataSize;
        }
    }
    public class EBMLEntry 
    {
        public long HeadOffset { get; set; }
        public ulong Id { get; set; }
        public long DataOffset { get; set; }
        public ulong DataSize { get; set; }
        public EBMLEntry(long headOffset, ulong id, long dataOffset, ulong dataSize)
        {
            HeadOffset = headOffset;
            Id = id;
            DataOffset = dataOffset;
            DataSize = dataSize;
        }
    }
}
