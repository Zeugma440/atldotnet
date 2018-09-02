using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

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

            metaData = MetaDataIOFactory.GetInstance().GetMetaReader(audioManager);

            if (metaData is DummyTag && (0 == audioManager.getAvailableMetas().Count)) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="stream">Stream to access in-memory data to be parsed</param>
        /// <param name="readEmbeddedPictures">Embedded pictures will be read if true; ignored if false</param>
        /// <param name="readAllMetaFrames">All metadata frames (including unmapped ones) will be read if true; ignored if false</param>
        public AudioFileIO(Stream stream, String mimeType, bool readEmbeddedPictures, bool readAllMetaFrames = false)
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

            metaData = MetaDataIOFactory.GetInstance().GetMetaReader(audioManager);

            if (metaData is DummyTag && (0 == audioManager.getAvailableMetas().Count)) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        public void Save(TagData data)
        {
            IList<int> availableMetas = audioManager.getAvailableMetas();

            if (0 == availableMetas.Count)
            {
                foreach (int i in Settings.DefaultTagsWhenNoMetadata)
                {
                    availableMetas.Add(i);
                }
            }

            foreach (int meta in availableMetas)
            {
                audioManager.UpdateTagInFile(data, meta);
            }
        }

        public void Remove(int tagType = MetaDataIOFactory.TAG_ANY)
        {
            IList<int> metasToRemove;

            if (MetaDataIOFactory.TAG_ANY == tagType)
            {
                metasToRemove = audioManager.getAvailableMetas();
            } else
            {
                metasToRemove = new List<int>() { tagType };
            }

            foreach(int meta in metasToRemove)
            {
                audioManager.RemoveTagFromFile(meta);
            }
        }

        // ============ FIELD ACCESSORS

        private string processString(string value)
        {
            return value.Replace(Settings.InternalValueSeparator, Settings.DisplayValueSeparator);
        }

        /// <summary>
        /// Audio file name
        /// </summary>
        public string FileName
        {
            get { return audioData.FileName; }
        }
        /// <summary>
        /// Title of the track
        /// </summary>
        public string Title
        {
            get { return processString(metaData.Title); }
        }
        /// <summary>
        /// Artist
        /// </summary>
        public string Artist
        {
            get { return processString(metaData.Artist); }
        }
        /// <summary>
        /// Composer
        /// </summary>
        public string Composer
        {
            get { return processString(metaData.Composer); }
        }
        /// <summary>
        /// Publisher
        /// </summary>
        public string Publisher
        {
            get { return processString(metaData.Publisher); }
        }
        /// <summary>
        /// Conductor
        /// </summary>
        public string Conductor
        {
            get { return processString(metaData.Conductor); }
        }
        /// <summary>
        /// Album Artist
        /// </summary>
        public string AlbumArtist
        {
            get { return processString(metaData.AlbumArtist); }
        }
        /// <summary>
        /// General description
        /// </summary>
        public string GeneralDescription
        {
            get { return processString(metaData.GeneralDescription); }
        }
        /// <summary>
        /// Copyright
        /// </summary>
        public string Copyright
        {
            get { return processString(metaData.Copyright); }
        }
        /// <summary>
        /// Original artist
        /// </summary>
        public string OriginalArtist
        {
            get { return processString(metaData.OriginalArtist); }
        }
        /// <summary>
        /// Original album
        /// </summary>
        public string OriginalAlbum
        {
            get { return processString(metaData.OriginalAlbum); }
        }

        /// <summary>
        /// Comments
        /// </summary>
        public string Comment
        {
            get { return processString(metaData.Comment); }
        }
        /// <summary>
        /// Flag indicating the presence of embedded pictures
        /// </summary>
        public IList<PictureInfo> PictureTokens
        {
            get { return metaData.PictureTokens; }
        }
        /// <summary>
        /// Genre
        /// </summary>
        public string Genre
        {
            get { return processString(metaData.Genre); }
        }
        /// <summary>
        /// Track number
        /// </summary>
        public ushort Track
        {
            get { return metaData.Track; }
        }
        /// <summary>
        /// Disc number
        /// </summary>
        public ushort Disc
        {
            get { return metaData.Disc; }
        }
        /// <summary>
        /// Year, converted to int
        /// </summary>
        public int IntYear
        {
            get { return TrackUtils.ExtractIntYear(metaData.Year); }
        }
        /// <summary>
        /// Album title
        /// </summary>
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
        /// <summary>
        /// Track rating
        /// </summary>
        [Obsolete("Use popularity")]
        public ushort Rating
        {
            get { return metaData.Rating; }
        }
        /// <summary>
        /// Track rating
        /// </summary>
        public float Popularity
        {
            get { return metaData.Popularity; }
        }
        /// <summary>
        /// Codec family
        /// </summary>
        public int CodecFamily
        {
            get { return audioData.CodecFamily; }
        }
        /// <summary>
        /// Indicates whether the audio stream is in VBR
        /// </summary>
        public bool IsVBR
        {
            get { return audioData.IsVBR; }
        }
        /// <summary>
        /// Does the tag exist ?
        /// </summary>
        public bool Exists
        {
            get { return metaData.Exists; }
        }
        /// <summary>
        /// Year, in its original form
        /// </summary>
        public string Year
        {
            get { return metaData.Year; }
        }
        /// <summary>
        /// Track bitrate (Kbit/s)
        /// </summary>
        public double BitRate
        {
            get { return audioData.BitRate; }
        }
        /// <summary>
        /// Sample rate (Hz)
        /// </summary>
        public int SampleRate
        {
            get { return audioData.SampleRate; }
        }
        /// <summary>
        /// Track duration (milliseconds)
        /// </summary>
        public double Duration
        {
            get { return audioData.Duration; }
        }
        /// <summary>
        /// Metadata size (bytes)
        /// </summary>
        public int Size
        {
            get { return metaData.Size; }
        }

        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                return metaData.AdditionalFields;
            }
        }

        public IList<ChapterInfo> Chapters
        {
            get
            {
                return metaData.Chapters;
            }
        }

        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                return metaData.EmbeddedPictures;
            }
        }

        public bool IsMetaSupported(int metaDataType)
        {
            return audioData.IsMetaSupported(metaDataType);
        }

        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return metaData.Read(source, readTagParams);
        }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return metaData.Write(r, w, tag);
        }

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return audioData.Read(source, sizeInfo, readTagParams);
        }

        public bool Remove(BinaryWriter w)
        {
            return metaData.Remove(w);
        }

        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            metaData.SetEmbedder(embedder);
        }

        public void Clear()
        {
            metaData.Clear();
        }
    }

}
