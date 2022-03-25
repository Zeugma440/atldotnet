using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.ChannelsArrangements;
using System.Linq;

namespace ATL.AudioData
{
    /// <summary>
	/// This class is the one which is _really_ called when encountering a file.
	/// It calls AudioReaderFactory and queries AudioDataReader/MetaDataReader to provide physical 
	/// _and_ meta information about the given file.
	/// </summary>
	internal class AudioFileIO : IMetaDataIO, IAudioDataIO
    {
        private readonly IAudioDataIO audioData;                     // Audio data reader used for this file
        private readonly IMetaDataIO metaData;                       // Metadata reader used for this file
        private readonly AudioDataManager audioManager;
        private readonly IProgress<float> writeProgress;

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

            audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate);
            audioManager = new AudioDataManager(audioData, writeProgress);
            this.writeProgress = writeProgress;
            found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetFromPath(path, alternate);
                audioManager = new AudioDataManager(audioData, writeProgress);
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

            audioManager = new AudioDataManager(audioData, stream, writeProgress);
            this.writeProgress = writeProgress;
            found = audioManager.ReadFromFile(readEmbeddedPictures, readAllMetaFrames);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetFromMimeType(mimeType, "In-memory", alternate);
                audioManager = new AudioDataManager(audioData, stream, writeProgress);
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

            float written = 0;
            if (writeProgress != null) writeProgress.Report(written++ / availableMetas.Count);
            foreach (MetaDataIOFactory.TagType meta in availableMetas)
            {
                result &= audioManager.UpdateTagInFile(data, meta);
                if (writeProgress != null) writeProgress.Report(written++ / availableMetas.Count);
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

            float written = 0;
            if (writeProgress != null) writeProgress.Report(written++ / metasToRemove.Count);
            foreach (MetaDataIOFactory.TagType meta in metasToRemove)
            {
                result &= audioManager.RemoveTagFromFile(meta);
                if (writeProgress != null) writeProgress.Report(written++ / metasToRemove.Count);
            }
            return result;
        }

        // ============ FIELD ACCESSORS

        private string processString(string value)
        {
            return value.Replace(Settings.InternalValueSeparator, Settings.DisplayValueSeparator);
        }

        /// <inheritdoc/>
        public string FileName
        {
            get { return audioData.FileName; }
        }
        /// <inheritdoc/>
        public string Title
        {
            get { return processString(metaData.Title); }
        }
        /// <inheritdoc/>
        public string Artist
        {
            get { return processString(metaData.Artist); }
        }
        /// <inheritdoc/>
        public string Composer
        {
            get { return processString(metaData.Composer); }
        }
        /// <inheritdoc/>
        public string Publisher
        {
            get { return processString(metaData.Publisher); }
        }
        /// <inheritdoc/>
        public DateTime PublishingDate
        {
            get { return metaData.PublishingDate; }
        }
        /// <inheritdoc/>
        public string Conductor
        {
            get { return processString(metaData.Conductor); }
        }
        /// <inheritdoc/>
        public string ProductId
        {
            get { return processString(metaData.ProductId); }
        }
        /// <inheritdoc/>
        public string AlbumArtist
        {
            get { return processString(metaData.AlbumArtist); }
        }
        /// <inheritdoc/>
        public string GeneralDescription
        {
            get { return processString(metaData.GeneralDescription); }
        }
        /// <inheritdoc/>
        public string Copyright
        {
            get { return processString(metaData.Copyright); }
        }
        /// <inheritdoc/>
        public string OriginalArtist
        {
            get { return processString(metaData.OriginalArtist); }
        }
        /// <inheritdoc/>
        public string OriginalAlbum
        {
            get { return processString(metaData.OriginalAlbum); }
        }
        /// <inheritdoc/>
        public long PaddingSize
        {
            get { return metaData.PaddingSize; }
        }

        /// <inheritdoc/>
        public string Comment
        {
            get { return processString(metaData.Comment); }
        }
        /// <inheritdoc/>
        public IList<PictureInfo> PictureTokens
        {
            get { return metaData.PictureTokens; }
        }
        /// <inheritdoc/>
        public string Genre
        {
            get { return processString(metaData.Genre); }
        }
        /// <inheritdoc/>
        public ushort Track
        {
            get { return metaData.Track; }
        }
        /// <inheritdoc/>
        public ushort TrackTotal
        {
            get { return metaData.TrackTotal; }
        }
        /// <inheritdoc/>
        public ushort Disc
        {
            get { return metaData.Disc; }
        }
        /// <inheritdoc/>
        public ushort DiscTotal
        {
            get { return metaData.DiscTotal; }
        }
        /// <inheritdoc/>
        public string Album
        {
            get { return processString(metaData.Album); }
        }
        /// <summary>
        /// Track duration (seconds), rounded
        /// </summary>
        public int IntDuration
        {
            get { return (int)Math.Round(audioData.Duration); }
        }
        /// <summary>
        /// Track bitrate (KBit/s), rounded
        /// </summary>
        public int IntBitRate
        {
            get { return (int)Math.Round(audioData.BitRate); }
        }
        /// <inheritdoc/>
        public float? Popularity
        {
            get { return metaData.Popularity; }
        }
        /// <inheritdoc/>
        public Format AudioFormat
        {
            get { return audioData.AudioFormat; }
        }
        /// <inheritdoc/>
        public int CodecFamily
        {
            get { return audioData.CodecFamily; }
        }
        /// <inheritdoc/>
        public bool IsVBR
        {
            get { return audioData.IsVBR; }
        }
        /// <inheritdoc/>
        public bool Exists
        {
            get { return metaData.Exists; }
        }
        /// <inheritdoc/>
        public IList<Format> MetadataFormats
        {
            get { return metaData.MetadataFormats; }
        }
        /// <inheritdoc/>
        public DateTime Date
        {
            get { return metaData.Date; }
        }
        /// <inheritdoc/>
        public double BitRate
        {
            get { return audioData.BitRate; }
        }
        /// <inheritdoc/>
        public int SampleRate
        {
            get { return audioData.SampleRate; }
        }
        /// <inheritdoc/>
        public double Duration
        {
            get { return audioData.Duration; }
        }
        /// <inheritdoc/>
        public long Size
        {
            get { return metaData.Size; }
        }
        /// <inheritdoc/>
        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                IDictionary<string, string> result = new Dictionary<string, string>();
                foreach (string key in metaData.AdditionalFields.Keys)
                {
                    result.Add(key, processString(metaData.AdditionalFields[key]));
                }
                return result;
            }
        }
        /// <inheritdoc/>
        public IList<ChapterInfo> Chapters
        {
            get
            {
                return metaData.Chapters;
            }
        }
        /// <inheritdoc/>
        public LyricsInfo Lyrics
        {
            get
            {
                return metaData.Lyrics;
            }
        }
        /// <inheritdoc/>
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                return metaData.EmbeddedPictures;
            }
        }
        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement
        {
            get
            {
                return audioData.ChannelsArrangement;
            }
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
        public string ChaptersTableDescription
        {
            get
            {
                return metaData.ChaptersTableDescription;
            }
        }
        /// <inheritdoc/>
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return audioData.IsMetaSupported(metaDataType);
        }
        /// <inheritdoc/>
        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return metaData.Read(source, readTagParams);
        }
        /// <inheritdoc/>
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return audioData.Read(source, sizeInfo, readTagParams);
        }
        /// <inheritdoc/>
        public bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            return metaData.Write(r, w, tag, writeProgress);
        }
        /// <inheritdoc/>
        public bool Remove(BinaryWriter w)
        {
            return metaData.Remove(w);
        }
        /// <inheritdoc/>
        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            metaData.SetEmbedder(embedder);
        }
        /// <inheritdoc/>
        public void Clear()
        {
            metaData.Clear();
        }
    }

}
