using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.ChannelsArrangements;
using System.Linq;
using System.Threading.Tasks;
using static ATL.AudioData.MetaDataIOFactory;

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

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Path of the file to be parsed</param>
        /// <param name="readEmbeddedPictures">Embedded pictures will be read if true; ignored if false</param>
        /// <param name="readAllMetaFrames">All metadata frames (including unmapped ones) will be read if true; ignored if false</param>
        public AudioFileIO(string path, bool readEmbeddedPictures, bool readAllMetaFrames = false)
        {
            byte alternate = 0;
            bool found = false;

            audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate);
            audioManager = new AudioDataManager(audioData);
            found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate);
                audioManager = new AudioDataManager(audioData);
                found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
            }

            metaData = GetInstance().GetMetaReader(audioManager);

            if (metaData is DummyTag && (0 == audioManager.getAvailableMetas().Count)) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="stream">Stream to access in-memory data to be parsed</param>
        /// <param name="mimeType">Mime-type of the stream to process</param>
        /// <param name="readEmbeddedPictures">Embedded pictures will be read if true; ignored if false</param>
        /// <param name="readAllMetaFrames">All metadata frames (including unmapped ones) will be read if true; ignored if false</param>
        public AudioFileIO(Stream stream, string mimeType, bool readEmbeddedPictures, bool readAllMetaFrames = false)
        {
            byte alternate = 0;
            bool found = false;

            audioData = AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, "In-memory", alternate);

            audioManager = new AudioDataManager(audioData, stream);
            found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, "In-memory", alternate);
                audioManager = new AudioDataManager(audioData, stream);
                found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
            }

            metaData = GetInstance().GetMetaReader(audioManager);

            if (metaData is DummyTag && (0 == audioManager.getAvailableMetas().Count)) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        private IList<TagType> detectAvailableMetas()
        {
            IList<TagType> result = audioManager.getAvailableMetas();
            IList<TagType> supportedMetas = audioManager.getSupportedMetas();

            bool hasNothing = (0 == result.Count);
            if (Settings.EnrichID3v1 && 1 == result.Count && result[0] == TagType.ID3V1) hasNothing = true;

            // File has no existing metadata
            // => Try writing with one of the metas set in the Settings
            if (hasNothing)
            {
                foreach (var i in Settings.DefaultTagsWhenNoMetadata.Where(i => supportedMetas.Contains(i)))
                {
                    result.Add(i);
                }

                // File does not support any of the metas we want to write
                // => Use the first supported meta available
                if (0 == result.Count && supportedMetas.Count > 0) result.Add(supportedMetas[0]);
            }
            return result;
        }

        public bool Save(TagData data, Action<float> writeProgress = null)
        {
            IList<TagType> availableMetas = detectAvailableMetas();

            bool result = true;
            ProgressManager progressManager = null;
            if (writeProgress != null)
            {
                progressManager = new ProgressManager(writeProgress, "AudioFileIO");
                progressManager.MaxSections = availableMetas.Count;
            }
            foreach (TagType meta in availableMetas)
            {
                result &= audioManager.UpdateTagInFile(data, meta, progressManager);
                if (progressManager != null) progressManager.CurrentSection++;
            }
            return result;
        }

        public async Task<bool> SaveAsync(TagData data, IProgress<float> writeProgress = null)
        {
            IList<TagType> availableMetas = detectAvailableMetas();

            bool result = true;
            ProgressManager progressManager = null;
            if (writeProgress != null)
            {
                progressManager = new ProgressManager(writeProgress, "AudioFileIO");
                progressManager.MaxSections = availableMetas.Count;
            }
            foreach (TagType meta in availableMetas)
            {
                result &= await audioManager.UpdateTagInFileAsync(data, meta, progressManager);
                if (progressManager != null) progressManager.CurrentSection++;
            }
            return result;
        }

        public bool Remove(TagType tagType = TagType.ANY, Action<float> writeProgress = null)
        {
            bool result = true;
            IList<TagType> metasToRemove = getMetasToRemove(tagType);

            ProgressManager progressManager = null;
            if (writeProgress != null)
            {
                progressManager = new ProgressManager(writeProgress, "AudioFileIO");
                progressManager.MaxSections = metasToRemove.Count;
            }
            foreach (TagType meta in metasToRemove)
            {
                result &= audioManager.RemoveTagFromFile(meta, progressManager);
                if (progressManager != null) progressManager.CurrentSection++;
            }
            return result;
        }

        public async Task<bool> RemoveAsync(TagType tagType = TagType.ANY, IProgress<float> writeProgress = null)
        {
            bool result = true;
            IList<TagType> metasToRemove = getMetasToRemove(tagType);

            ProgressManager progressManager = null;
            if (writeProgress != null)
            {
                progressManager = new ProgressManager(writeProgress, "AudioFileIO");
                progressManager.MaxSections = metasToRemove.Count;
            }
            foreach (TagType meta in metasToRemove)
            {
                result &= await audioManager.RemoveTagFromFileAsync(meta, progressManager);
                if (progressManager != null) progressManager.CurrentSection++;
            }
            return result;
        }

        private IList<TagType> getMetasToRemove(TagType tagType)
        {
            if (TagType.ANY == tagType) return audioManager.getAvailableMetas();
            else return new List<TagType>() { tagType };
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
        public int BitDepth
        {
            get => audioData.BitDepth;
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
        public bool IsMetaSupported(TagType metaDataType)
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
