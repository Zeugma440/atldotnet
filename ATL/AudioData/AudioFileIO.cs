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
using Commons;

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
                    // First detect format using stream
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, Settings.FileBufferSize, FileOptions.RandomAccess))
                    {
                        audioData = AudioDataIOFactory.GetInstance().GetFromStream(fs);
                    }

                    // Use detected format to create proper instances
                    audioData = AudioDataIOFactory.GetFromFormat(path, audioData.AudioFormat);
                    audioManager = new AudioDataManager(audioData);
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
                audioData = mimeType.Length > 0 ? AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, AudioDataIOFactory.IN_MEMORY, alternate) : AudioDataIOFactory.GetInstance().GetFromStream(stream);
                audioManager = new AudioDataManager(audioData, stream);
                found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);
                alternate++;
            }
            Metadata = getAndCheckMetadata();
        }

        private IMetaDataIO getAndCheckMetadata()
        {
            IMetaDataIO result = GetInstance().GetMetaReader(audioManager);

            if (result is DummyTag && (0 == audioManager.getAvailableMetas().Count))
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");

            // Consistency checks
            if (result.TrackTotal > 0 && Utils.IsNumeric(result.TrackNumber) && Utils.ParseFirstIntegerPart(result.TrackNumber) > result.TrackTotal)
                LogDelegator.GetLogDelegate()(Log.LV_INFO, "Track number (" + result.TrackNumber + ") is > total tracks (" + result.TrackTotal + ")");

            if (result.DiscTotal > 0 && result.DiscNumber > result.DiscTotal)
                LogDelegator.GetLogDelegate()(Log.LV_INFO, "Disc number (" + result.DiscNumber + ") is > total discs (" + result.DiscTotal + ")");

            if (result.Chapters is { Count: > 0 })
            {
                foreach (ChapterInfo chapter in result.Chapters)
                {
                    if (chapter.StartTime > audioData.Duration)
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Chapter " + chapter.Title + " : start timestamp goes beyond file duration !");
                    if (chapter.EndTime > audioData.Duration)
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Chapter " + chapter.Title + " : end timestamp goes beyond file duration !");
                }
            }

            if (result.Lyrics != null)
            {
                foreach (LyricsInfo lyrics in result.Lyrics)
                {
                    foreach (LyricsPhrase phrase in lyrics.SynchronizedLyrics)
                    {
                        if (phrase.TimestampStart > audioData.Duration)
                            LogDelegator.GetLogDelegate()(Log.LV_INFO, "Lyrics phrase " + phrase.Text + " : start timestamp goes beyond file duration !");

                        if (phrase.TimestampEnd > audioData.Duration)
                            LogDelegator.GetLogDelegate()(Log.LV_INFO, "Lyrics phrase " + phrase.Text + " : end timestamp goes beyond file duration !");

                        if (phrase.Beats != null)
                            foreach (LyricsPhrase beat in phrase.Beats)
                            {
                                if (beat.TimestampStart < phrase.TimestampStart && phrase.TimestampEnd > -1 && beat.TimestampStart > phrase.TimestampEnd)
                                    LogDelegator.GetLogDelegate()(Log.LV_INFO, "Lyrics beat " + beat.Text + " : start timestamp is out of phrase boundaries !");

                                if (beat.TimestampEnd > -1 && beat.TimestampEnd < phrase.TimestampStart && phrase.TimestampEnd > -1 && beat.TimestampEnd > phrase.TimestampEnd)
                                    LogDelegator.GetLogDelegate()(Log.LV_INFO, "Lyrics beat " + beat.Text + " : end timestamp is out of phrase boundaries !");
                            }
                    }
                }
            }

            return result;
        }

        private IList<TagType> detectAvailableMetas()
        {
            ISet<TagType> result = audioManager.getAvailableMetas();
            ISet<TagType> supportedMetas = audioManager.getSupportedMetas();
            ISet<TagType> recommendedMetas = audioManager.getRecommendedMetas();

            bool hasNothing = 0 == result.Count || Settings.EnrichID3v1 && 1 == result.Count && result.First() == TagType.ID3V1;

            if (!hasNothing) return result.ToList();

            // File has no existing metadata
            // => Try writing with one of the metas set in the Settings
            foreach (var i in Settings.DefaultTagsWhenNoMetadata)
            {
                if (i == TagType.RECOMMENDED) foreach (var reco in recommendedMetas) result.Add(reco);
                else if (supportedMetas.Contains(i)) result.Add(i);
            }

            // File does not support any of the metas we want to write
            // => Use the first supported meta available
            if (0 == result.Count && supportedMetas.Count > 0) result.Add(supportedMetas.First());
            return result.ToList();
        }

        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> SaveAsync(
            TagData data,
            TagType? tagType,
            string targetPath = null,
            Stream targetStream = null,
            ProgressToken<float> writeProgress = null)
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
                progressManager = new ProgressManager(writeProgress, "AudioFileIO")
                {
                    MaxSections = metasToWrite.Count
                };
            }
            foreach (var meta in metasToWrite)
            {
                result &= await audioManager.UpdateTagInFileAsync(data, meta, targetPath, targetStream, progressManager);
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
                progressManager = new ProgressManager(writeProgress, "AudioFileIO")
                {
                    MaxSections = metasToRemove.Count
                };
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
            return TagType.ANY == tagType ? audioManager.getAvailableMetas() : new HashSet<TagType> { tagType };
        }

        // ============ FIELD ACCESSORS

        /// <summary>
        /// Metadata fields container
        /// </summary>
        public IMetaDataIO Metadata { get; }

        /// <inheritdoc/>
        public string FileName => audioData.FileName;

        /// <summary>
        /// Track bitrate (KBit/s), rounded
        /// </summary>
        public int IntBitRate => (int)Math.Round(audioData.BitRate);
        /// <inheritdoc/>
        public AudioFormat AudioFormat => audioData.AudioFormat;
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
        public List<TagType> GetSupportedMetas()
        {
            return audioData.GetSupportedMetas();
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => audioData.IsNativeMetadataRich;
        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return audioData.Read(source, sizeNfo, readTagParams);
        }
    }

}
