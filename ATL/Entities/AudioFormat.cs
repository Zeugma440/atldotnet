using System;

namespace ATL
{
    /// <summary>
    /// Represents the format of an audio file, composed of :
    ///   - Container (e.g. MKA, OGG)
    ///   - Audio data (e.g. MPEG, WAV)
    /// </summary>
    public class AudioFormat : Format
    {
        /// <summary>
        /// Container ID
        /// Reference : AudioDataIOFactory.CID_ constants
        /// </summary>
        public int ContainerId { get; set; }

        /// <summary>
        /// Audio format
        /// </summary>
        public Format DataFormat { get; set; }

        /// <summary>
        /// Standard Constructor
        /// </summary>
        /// <param name="containerId">ID of the container format</param>
        /// <param name="dataFormat">Audio data format</param>
        /// <param name="name">Name</param>
        /// <param name="shortName">Short name</param>
        /// <param name="writable">Indicate if ATL implements writing for this Format</param>
        public AudioFormat(short containerId, Format dataFormat, string name, string shortName = "", bool writable = true)
        {
            ContainerId = containerId;
            DataFormat = new Format(dataFormat);
            init(combineIds(containerId, dataFormat.ID), name, 0 == shortName.Length ? name : shortName, writable);
        }

        /// <summary>
        /// Standard Constructor
        /// </summary>
        /// <param name="dataFormatId">ID of the data format</param>
        /// <param name="name">Name</param>
        /// <param name="shortName">Short name</param>
        /// <param name="writable">Indicate if ATL implements writing for this Format</param>
        public AudioFormat(short dataFormatId, string name, string shortName = "", bool writable = true)
        {
            ContainerId = dataFormatId;
            DataFormat = new Format(dataFormatId, name, 0 == shortName.Length ? name : shortName, writable);
            init(dataFormatId, name, shortName, writable);
        }

        /// <summary>
        /// Construct an AudioFormat by copying data from the given AudioFormat object
        /// </summary>
        /// <param name="f">AudioFormat to copy data from</param>
        public AudioFormat(AudioFormat f)
        {
            copyFrom(f);
        }

        /// <summary>
        /// Construct an AudioFormat by copying data from the given Format object
        /// </summary>
        /// <param name="f">Format to copy data from</param>
        public AudioFormat(Format f)
        {
            copyFrom(f);
            DataFormat = f;
            ComputeId();
        }

        /// <summary>
        /// Integrate data from the given Format object
        /// </summary>
        /// <param name="f">Format to copy data from</param>
        protected void copyFrom(AudioFormat f)
        {
            ContainerId = f.ContainerId;
            DataFormat = new Format(f.DataFormat);
            base.copyFrom(f);
        }

        /// <summary>
        /// Compute the Format ID using Container format ID and Audio format ID
        /// </summary>
        public void ComputeId()
        {
            ID = combineIds((short)Math.Min(short.MaxValue, ContainerId), (short)Math.Min(short.MaxValue, DataFormat.ID));
        }

        private static int combineIds(int id1, int id2)
        {
            return (id1 << 16) + id2;
        }
    }
}
