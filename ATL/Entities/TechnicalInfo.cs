namespace ATL
{
    /// <summary>
    /// Information describing low-level / technical informations about the audio file
    /// </summary>
    public class TechnicalInfo
    {
        /// <summary>
        /// Offset of the audio data chunk (bytes)
        /// </summary>
        public long AudioDataOffset { get; }
        /// <summary>
        /// Size of the audio data chunk (bytes)
        /// </summary>
        public long AudioDataSize { get; }


        /// <summary>
        /// Constructor
        /// </summary>
        public TechnicalInfo(long audioDataOffset, long audioDataSize)
        {
            AudioDataOffset = audioDataOffset;
            AudioDataSize = audioDataSize;
        }
    }
}
