using System;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace ATL
{
    /// <summary>
    /// Represents the format of an audio file, composed of :
    ///   - Container (e.g. MKA, OGG)
    ///   - Audio data (e.g. MPEG, WAV)
    /// </summary>
    public class AudioFormat : Format
    {
        // Audio data format / Codec, if different from the container
        private Format? _dataFormat = null;

        /// <summary>
        /// Container ID
        /// Reference : AudioDataIOFactory.CID_ constants
        /// </summary>
        public short ContainerId { get; set; }

        /// <summary>
        /// Name of the container, if the format has one (e.g. "Matroska")
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;


        /// <summary>
        /// Name of the container, if the format has one (e.g. "MKV")
        /// </summary>
        public string ContainerShortName { get; set; } = string.Empty;

        /// <summary>
        /// Audio data format / Codec
        /// </summary>
        public Format DataFormat
        {
            get
            {
                if (null == _dataFormat) return this;
                else return _dataFormat;
            }
            set { _dataFormat = value; }
        }


        /// <summary>
        /// Constructor for a simple format where audio data and container are the same structure (e.g. MP3)
        /// </summary>
        /// <param name="id">ID of the data format / codec</param>
        /// <param name="name">Name of the data format / codec</param>
        /// <param name="shortName">Short name of the data format / codec</param>
        /// <param name="writable">Indicate if ATL implements writing for this Format</param>
        public AudioFormat(short id, string name, string shortName = "", bool writable = true)
        {
            ContainerId = id;
            ContainerName = name;
            ContainerShortName = shortName;
            init(id, name, shortName, writable);
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
        }

        /// <summary>
        /// Integrate data from the given Format object
        /// </summary>
        /// <param name="f">Format to copy data from</param>
        protected void copyFrom(AudioFormat f)
        {
            ContainerId = f.ContainerId;
            ContainerName = f.ContainerName;
            ContainerShortName = f.ContainerShortName;
            if (f._dataFormat != null) _dataFormat = new Format(f._dataFormat);
            base.copyFrom(f);
        }

        /// <summary>
        /// Turns into a container format, using the given Audio data / codec name and ID
        /// </summary>
        public void SpecifyCodec(string codecName, string codecShortName = "", short codecId = 0)
        {
            DataFormat = new Format(codecId, codecName, codecShortName);
        }

        /// <summary>
        /// Turns into a container format, using the given Format as Audio data / codec information
        /// </summary>
        public void SpecifyCodec(Format format)
        {
            DataFormat = new Format(format);
        }


        /// <summary>
        /// Name of the format (e.g. "Free Lossless Audio Codec"), including the Audio data format / Codec if the current format is a container (e.g. "Matroska / Free Lossless Audio Codec")
        /// </summary>
        override public string Name
        {
            get
            {
                if (_dataFormat != null) return $"{ContainerName} / {_dataFormat.Name}";
                else return ContainerName;
            }
        }

        /// <summary>
        /// Short name of the format (e.g. "FLAC"), including the Audio data format / Codec if the current format is a container (e.g. "MKV / FLAC")
        /// </summary>
        override public string ShortName
        {
            get
            {
                if (ContainerShortName.Length > 0 && ContainerId != DataFormat.MainID) return $"{ContainerShortName} / {DataFormat.ShortName}";
                else return ContainerShortName;
            }
        }

        /// <inheritdoc/>
        protected override int UniqueID
        {
            get
            {
                return (ContainerId << 16) + DataFormat.MainID;
            }
        }
    }
}
