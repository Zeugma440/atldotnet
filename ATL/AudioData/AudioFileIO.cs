using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.ChannelsArrangements;
using System.Linq;
using System.Threading.Tasks;
using static ATL.AudioData.MetaDataIOFactory;
using static ATL.LyricsInfo;

namespace ATL.AudioData
{
    /// <summary>
	/// This class is the one which is _really_ called when encountering a file.
	/// It calls AudioReaderFactory and queries AudioDataReader/MetaDataReader to provide physical 
	/// _and_ meta information about the given file.
	/// </summary>
	internal partial class AudioFileIO : IAudioDataIO
    {
        private readonly IAudioDataIO audioData;                     // Audio data reader used for this file
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
            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate++);
                if (!(audioData is DummyReader))
                {
                    audioManager = new AudioDataManager(audioData);
                    found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
                }
            }
            // Try auto-detecting if nothing worked
            if (!found)
            {
                if (File.Exists(path))
                {
                    using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileBufferSize, FileOptions.RandomAccess);
                    audioData = AudioDataIOFactory.GetInstance().GetFromStream(fs);
                    audioManager = new AudioDataManager(audioData, fs);
                    audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
                }
                else // Invalid path
                {
                    audioData = new DummyReader(path);
                    audioManager = new AudioDataManager(audioData);
                }
            }
            Metadata = getAndCheckMetadata();
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
            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                audioData = mimeType.Length > 0 ? AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, "In-memory", alternate++) : AudioDataIOFactory.GetInstance().GetFromStream(stream);
                audioManager = new AudioDataManager(audioData, stream);
                found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
            }
            Metadata = getAndCheckMetadata();
        }

        private IMetaDataIO getAndCheckMetadata()
        {
            IMetaDataIO result = GetInstance().GetMetaReader(audioManager);

            if (result is DummyTag && (0 == audioManager.getAvailableMetas().Count))
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");

            // Consistency checks
            if (result.TrackTotal > 0 && result.TrackNumber > result.TrackTotal)
                LogDelegator.GetLogDelegate()(Log.LV_INFO, "Track number (" + result.TrackNumber + ") is > total tracks (" + result.TrackTotal + ")");

            if (result.DiscTotal > 0 && result.DiscNumber > result.DiscTotal)
                LogDelegator.GetLogDelegate()(Log.LV_INFO, "Disc number (" + result.DiscNumber + ") is > total discs (" + result.DiscTotal + ")");

            if (result.Chapters != null && result.Chapters.Count > 0)
            {
                foreach (ChapterInfo chapter in result.Chapters)
                {
                    if (chapter.StartTime > audioData.Duration)
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Chapter " + chapter.Title + " : start timestamp goes beyond file duration !");
                    if (chapter.EndTime > audioData.Duration)
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Chapter " + chapter.Title + " : end timestamp goes beyond file duration !");
                }
            }

            if (result.Lyrics != null && result.Lyrics.SynchronizedLyrics.Count > 0)
            {
                foreach (LyricsPhrase phrase in result.Lyrics.SynchronizedLyrics)
                {
                    if (phrase.TimestampMs > audioData.Duration)
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Lyrics phrase " + phrase.Text + " : start timestamp goes beyond file duration !");
                }
            }

            return result;
        }

        private IList<TagType> detectAvailableMetas()
        {
            ISet<TagType> result = audioManager.getAvailableMetas();
            ISet<TagType> supportedMetas = audioManager.getSupportedMetas();

            bool hasNothing = 0 == result.Count;
            if (Settings.EnrichID3v1 && 1 == result.Count && result.First() == TagType.ID3V1) hasNothing = true;

            if (!hasNothing) return result.ToList();

            // File has no existing metadata
            // => Try writing with one of the metas set in the Settings
            foreach (var i in Settings.DefaultTagsWhenNoMetadata.Where(i => supportedMetas.Contains(i)))
            {
                result.Add(i);
            }

            // File does not support any of the metas we want to write
            // => Use the first supported meta available
            if (0 == result.Count && supportedMetas.Count > 0) result.Add(supportedMetas.First());
            return result.ToList();
        }

        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> SaveAsync(TagData data, TagType? tagType, ProgressToken<float> writeProgress = null)
        {
            IList<TagType> metasToWrite = new List<TagType>();
            ISet<TagType> supportedMetas = audioManager.getSupportedMetas();
            Lazy<IList<TagType>> detectedMetas = new Lazy<IList<TagType>>(detectAvailableMetas);

            if (null == tagType || TagType.ANY == tagType) metasToWrite = detectedMetas.Value;
            else
            {
                foreach (var att in detectedMetas.Value) metasToWrite.Add(att);
                if (supportedMetas.Contains(tagType.Value)) metasToWrite.Add(tagType.Value);
                else LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Cannot create " + tagType + " tag type inside a " + AudioFormat.ShortName + " file, as it is not supported");
            }

            bool result = true;
            ProgressManager progressManager = null;
            if (writeProgress != null)
            {
                progressManager = new ProgressManager(writeProgress, "AudioFileIO");
                progressManager.MaxSections = metasToWrite.Count;
            }
            foreach (var meta in metasToWrite)
            {
                result &= await audioManager.UpdateTagInFileAsync(data, meta, progressManager);
                if (progressManager != null) progressManager.CurrentSection++;
            }
            return result;
        }

        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> RemoveAsync(TagType tagType = TagType.ANY, ProgressToken<float> writeProgress = null)
        {
            bool result = true;
            ISet<TagType> metasToRemove = getMetasToRemove(tagType);

            ProgressManager progressManager = null;
            if (writeProgress != null)
            {
                progressManager = new ProgressManager(writeProgress, "AudioFileIO");
                progressManager.MaxSections = metasToRemove.Count;
            }
            foreach (var meta in metasToRemove)
            {
                result &= await audioManager.RemoveTagFromFileAsync(meta, progressManager);
                if (progressManager != null) progressManager.CurrentSection++;
            }
            return result;
        }

        private ISet<TagType> getMetasToRemove(TagType tagType)
        {
            if (TagType.ANY == tagType) return audioManager.getAvailableMetas();
            return new HashSet<TagType>() { tagType };
        }

        // ============ FIELD ACCESSORS

        /// <summary>
        /// Metadata fields container
        /// </summary>
        public IMetaDataIO Metadata { get; }

        /// <inheritdoc/>
        public string FileName => audioData.FileName;
        /// <summary>
        /// Track duration (seconds), rounded
        /// </summary>
        public int IntDuration => (int)Math.Round(audioData.Duration);
        /// <summary>
        /// Track bitrate (KBit/s), rounded
        /// </summary>
        public int IntBitRate => (int)Math.Round(audioData.BitRate);
        /// <inheritdoc/>
        public Format AudioFormat => audioData.AudioFormat;
        /// <inheritdoc/>
        public int CodecFamily => audioData.CodecFamily;
        /// <inheritdoc/>
        public bool IsVBR => audioData.IsVBR;
        /// <inheritdoc/>
        public double BitRate => audioData.BitRate;
        /// <inheritdoc/>
        public int BitDepth => audioData.BitDepth;
        /// <inheritdoc/>
        public int SampleRate => audioData.SampleRate;
        /// <inheritdoc/>
        public double Duration => audioData.Duration;
        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement => audioData.ChannelsArrangement;
        /// <inheritdoc/>
        public long AudioDataOffset => audioData.AudioDataOffset;
        /// <inheritdoc/>
        public long AudioDataSize => audioData.AudioDataSize;
        /// <inheritdoc/>
        public bool IsMetaSupported(TagType metaDataType)
        {
            return audioData.IsMetaSupported(metaDataType);
        }
        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return audioData.Read(source, sizeInfo, readTagParams);
        }
    }

}
