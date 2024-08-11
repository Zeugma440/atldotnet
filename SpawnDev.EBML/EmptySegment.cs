namespace SpawnDev.EBML
{
    public class EmptySegment : ByteSegment
    {
        /// <summary>
        /// Creates a new empty ByteSegment
        /// </summary>
        public EmptySegment() : base(new byte[0], 0, 0, true) { }
    }
}
