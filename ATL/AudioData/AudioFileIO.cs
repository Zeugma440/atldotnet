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
	public class AudioFileIO : IMetaDataIO, IAudioDataIO
    {
        private readonly string thePath;                             // Path of this file
        private readonly IAudioDataIO audioData;                     // Audio data reader used for this file
        private readonly IMetaDataIO metaData;                       // Metadata reader used for this file
        private readonly AudioDataManager audioManager;

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Path of the file to be parsed</param>
        public AudioFileIO(string path, TagData.PictureStreamHandlerDelegate pictureStreamHandler)
        {
            byte alternate = 0;
            bool found = false;
            thePath = path;

            audioData = AudioDataIOFactory.GetInstance().GetDataReader(path, alternate);
            audioManager = new AudioDataManager(audioData);
            found = audioManager.ReadFromFile(pictureStreamHandler);

            while (!found && alternate < AudioDataIOFactory.MAX_ALTERNATES)
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetDataReader(path, alternate);
                audioManager = new AudioDataManager(audioData);
                found = audioManager.ReadFromFile(pictureStreamHandler);
            }

            metaData = MetaDataIOFactory.GetInstance().GetMetaReader(audioManager);

            if (audioData.AllowsParsableMetadata && metaData is DummyTag) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata");
        }

        public void Save(TagData data)
        {
            foreach (int meta in audioManager.getAvailableMetas())
            {
                audioManager.UpdateTagInFile(data, meta);
            }
        }

        // ============ FIELD ACCESSORS

        private string processString(string value)
        {
            return value.Replace('\t', ',').Replace("\r", "").Replace("\n", ",").Replace(Settings.InternalLineSeparator, Settings.DisplayLineSeparator).Replace("\0", "").Replace(Settings.InternalValueSeparator, Settings.DisplayValueSeparator);
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
        public IList<TagData.PictureInfo> PictureTokens
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
        public ushort Rating
        {
            get { return metaData.Rating; }
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
        /// Indicates whether the audio format allows parsable metadata to exist
        /// </summary>
        public bool AllowsParsableMetadata
        {
            get { return audioData.AllowsParsableMetadata; }
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
        /// Track duration (seconds)
        /// </summary>
        public double Duration
        {
            get { return audioData.Duration; }
        }

        public bool HasNativeMeta()
        {
            return audioData.HasNativeMeta();
        }

        public bool IsMetaSupported(int metaDataType)
        {
            return audioData.IsMetaSupported(metaDataType);
        }


        // AudioFileReader aims at simplifying standard interfaces
        // => the below methods are not implemented
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

        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return metaData.Read(source, readTagParams);
        }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return metaData.Write(r, w, tag);
        }


        // TODO - make interfaces more modular to clean the mess below
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            throw new NotImplementedException();
        }

        public bool Remove(BinaryWriter w)
        {
            throw new NotImplementedException();
        }

        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }
    }

}
