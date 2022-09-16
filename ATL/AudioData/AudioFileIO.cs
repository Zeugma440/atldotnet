using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.ChannelsArrangements;
using System.Linq;
using System.Threading.Tasks;

namespace ATL.AudioData
{
    /// <summary>
	/// This class is the one which is _really_ called when encountering a file.
	/// It calls AudioReaderFactory and queries AudioDataReader/MetaDataReader to provide physical 
	/// _and_ meta information about the given file.
	/// </summary>
	internal class AudioFileIO : IAudioDataIO
    {
        private readonly IAudioDataIO audioData;                     // Audio data reader used for this file
        private readonly IMetaDataIO metaData;                       // Metadata reader used for this file
        private readonly AudioDataManager audioManager;
        private readonly ProgressManager writeProgressManager;

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Path of the file to be parsed</param>
        /// <param name="readEmbeddedPictures">Embedded pictures will be read if true; ignored if false</param>
        /// <param name="readAllMetaFrames">All metadata frames (including unmapped ones) will be read if true; ignored if false</param>
        /// <param name="writeProgress">Object to use to signal writing progress (optional)</param>
        public AudioFileIO(string path, bool readEmbeddedPictures, bool readAllMetaFrames = false, IProgress<float> writeProgress = null)
        {
            byte alternate = 0;
            bool found = false;

            if (writeProgress != null) writeProgressManager = new ProgressManager(writeProgress, "AudioFileIO");
            audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate);
            audioManager = new AudioDataManager(audioData, writeProgressManager);
            found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate);
                audioManager = new AudioDataManager(audioData, writeProgressManager);
                found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
            }

            metaData = MetaDataIOFactory.GetInstance().GetMetaReader(audioManager);

            if (metaData is DummyTag && (0 == audioManager.getAvailableMetas().Count)) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="stream">Stream to access in-memory data to be parsed</param>
        /// <param name="mimeType">Mime-type of the stream to process</param>
        /// <param name="readEmbeddedPictures">Embedded pictures will be read if true; ignored if false</param>
        /// <param name="readAllMetaFrames">All metadata frames (including unmapped ones) will be read if true; ignored if false</param>
        /// <param name="writeProgress">Object to use to signal writing progress (optional)</param>
        public AudioFileIO(Stream stream, String mimeType, bool readEmbeddedPictures, bool readAllMetaFrames = false, IProgress<float> writeProgress = null)
        {
            byte alternate = 0;
            bool found = false;

            audioData = AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, "In-memory", alternate);

            if (writeProgress != null) writeProgressManager = new ProgressManager(writeProgress, "AudioFileIO");
            audioManager = new AudioDataManager(audioData, stream, writeProgressManager);
            found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, "In-memory", alternate);
                audioManager = new AudioDataManager(audioData, stream, writeProgressManager);
                found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
            }

            metaData = MetaDataIOFactory.GetInstance().GetMetaReader(audioManager);

            if (metaData is DummyTag && (0 == audioManager.getAvailableMetas().Count)) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        public bool Save(TagData data)
        {
            bool result = true;
            IList<MetaDataIOFactory.TagType> availableMetas = audioManager.getAvailableMetas();
            IList<MetaDataIOFactory.TagType> supportedMetas = audioManager.getSupportedMetas();

            bool hasNothing = (0 == availableMetas.Count);
            if (Settings.EnrichID3v1 && 1 == availableMetas.Count && availableMetas[0] == MetaDataIOFactory.TagType.ID3V1) hasNothing = true;

            // File has no existing metadata
            // => Try writing with one of the metas set in the Settings
            if (hasNothing)
            {
                foreach (var i in Settings.DefaultTagsWhenNoMetadata.Where(i => supportedMetas.Contains(i)))
                {
                    availableMetas.Add(i);
                }

                // File does not support any of the metas we want to write
                // => Use the first supported meta available
                if (0 == availableMetas.Count && supportedMetas.Count > 0) availableMetas.Add(supportedMetas[0]);
            }

            if (writeProgressManager != null) writeProgressManager.MaxSections = availableMetas.Count;
            foreach (MetaDataIOFactory.TagType meta in availableMetas)
            {
                result &= audioManager.UpdateTagInFile(data, meta);
                if (writeProgressManager != null) writeProgressManager.CurrentSection++;
            }
            return result;
        }

        public async Task<bool> SaveAsync(TagData data)
        {
            bool result = true;
            IList<MetaDataIOFactory.TagType> availableMetas = audioManager.getAvailableMetas();
            IList<MetaDataIOFactory.TagType> supportedMetas = audioManager.getSupportedMetas();

            bool hasNothing = (0 == availableMetas.Count);
            if (Settings.EnrichID3v1 && 1 == availableMetas.Count && availableMetas[0] == MetaDataIOFactory.TagType.ID3V1) hasNothing = true;

            // File has no existing metadata
            // => Try writing with one of the metas set in the Settings
            if (hasNothing)
            {
                foreach (var i in Settings.DefaultTagsWhenNoMetadata.Where(i => supportedMetas.Contains(i)))
                {
                    availableMetas.Add(i);
                }

                // File does not support any of the metas we want to write
                // => Use the first supported meta available
                if (0 == availableMetas.Count && supportedMetas.Count > 0) availableMetas.Add(supportedMetas[0]);
            }

            if (writeProgressManager != null) writeProgressManager.MaxSections = availableMetas.Count;
            foreach (MetaDataIOFactory.TagType meta in availableMetas)
            {
                result &= await audioManager.UpdateTagInFileAsync(data, meta);
                if (writeProgressManager != null) writeProgressManager.CurrentSection++;
            }
            return result;
        }

        public bool Remove(MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY)
        {
            bool result = true;
            IList<MetaDataIOFactory.TagType> metasToRemove;

            if (MetaDataIOFactory.TagType.ANY == tagType)
            {
                metasToRemove = audioManager.getAvailableMetas();
            }
            else
            {
                metasToRemove = new List<MetaDataIOFactory.TagType>() { tagType };
            }

            if (writeProgressManager != null) writeProgressManager.MaxSections = metasToRemove.Count;
            foreach (MetaDataIOFactory.TagType meta in metasToRemove)
            {
                result &= audioManager.RemoveTagFromFile(meta);
                if (writeProgressManager != null) writeProgressManager.CurrentSection++;
            }
            return result;
        }

        // ============ FIELD ACCESSORS

        /// <summary>
        /// Metadata fields container
        /// </summary>
        public IMetaDataIO Metadata
        {
            get => metaData;
        }
        /// <inheritdoc/>
        public string FileName
        {
            get => audioData.FileName;
        }
        /// <summary>
        /// Track duration (seconds), rounded
        /// </summary>
        public int IntDuration
        {
            get => (int)Math.Round(audioData.Duration);
        }
        /// <summary>
        /// Track bitrate (KBit/s), rounded
        /// </summary>
        public int IntBitRate
        {
            get => (int)Math.Round(audioData.BitRate);
        }
        /// <inheritdoc/>
        public Format AudioFormat
        {
            get => audioData.AudioFormat;
        }
        /// <inheritdoc/>
        public int CodecFamily
        {
            get => audioData.CodecFamily;
        }
        /// <inheritdoc/>
        public bool IsVBR
        {
            get => audioData.IsVBR;
        }
        /// <inheritdoc/>
        public double BitRate
        {
            get => audioData.BitRate;
        }
        /// <inheritdoc/>
        public int SampleRate
        {
            get => audioData.SampleRate;
        }
        /// <inheritdoc/>
        public double Duration
        {
            get => audioData.Duration;
        }
        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement
        {
            get => audioData.ChannelsArrangement;
        }
        /// <inheritdoc/>
        public long AudioDataOffset
        {
            get => audioData.AudioDataOffset;
        }
        /// <inheritdoc/>
        public long AudioDataSize
        {
            get => audioData.AudioDataSize;
        }
        /// <inheritdoc/>
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return audioData.IsMetaSupported(metaDataType);
        }
        /// <inheritdoc/>
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return audioData.Read(source, sizeInfo, readTagParams);
        }
    }

}
