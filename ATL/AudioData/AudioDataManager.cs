using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ATL.AudioData
{
    /// <summary>
    /// Handles high-level basic operations on the given audio file, calling Metadata readers when needed
    /// </summary>
    public class AudioDataManager
    {
        // Settings to use when opening any FileStream
        // NB : These settings are optimal according to performance tests on the dev environment
        private static int bufferSize = 2048;
        private static FileOptions fileOptions = FileOptions.RandomAccess;

        /// <summary>
        /// Set file options to use when opening any FileStream
        /// </summary>
        /// <param name="options">FileOptions to use when opening any FileStream</param>
        public static void SetFileOptions(FileOptions options)
        {
            fileOptions = options;
        }

        /// <summary>
        /// Set I/O buffer size to use when opening any FileStream
        /// </summary>
        /// <param name="bufSize">I/O buffer size to use when opening any FileStream</param>
        public static void SetBufferSize(int bufSize)
        {
            bufferSize = bufSize;
        }

        /// <summary>
        /// Contains various useful information about the size of an audio file and its components
        /// </summary>
        public class SizeInfo
        {
            private readonly IDictionary<MetaDataIOFactory.TagType, long> TagSizes = new Dictionary<MetaDataIOFactory.TagType, long>();
            private long audioDataSize = -1;

            /// <summary>
            /// Reset all data
            /// </summary>
            public void ResetData() { FileSize = 0; TagSizes.Clear(); }

            /// <summary>
            /// Set the size for the given TagType, in bytes
            /// </summary>
            /// <param name="type">Tag type to set the size for</param>
            /// <param name="size">Size to set (bytes)</param>
            public void SetSize(MetaDataIOFactory.TagType type, long size)
            {
                TagSizes[type] = size;
            }

            /// <summary>
            /// Size of the ID3v1 tag (bytes)
            /// </summary>
            public long ID3v1Size { get { return TagSizes.ContainsKey(MetaDataIOFactory.TagType.ID3V1) ? TagSizes[MetaDataIOFactory.TagType.ID3V1] : 0; } }
            /// <summary>
            /// Size of the ID3v2 tag (bytes)
            /// </summary>
            public long ID3v2Size { get { return TagSizes.ContainsKey(MetaDataIOFactory.TagType.ID3V2) ? TagSizes[MetaDataIOFactory.TagType.ID3V2] : 0; } }
            /// <summary>
            /// Size of the APE tag (bytes)
            /// </summary>
            public long APESize { get { return TagSizes.ContainsKey(MetaDataIOFactory.TagType.APE) ? TagSizes[MetaDataIOFactory.TagType.APE] : 0; } }
            /// <summary>
            /// Size of the native tag (bytes)
            /// </summary>
            public long NativeSize { get { return TagSizes.ContainsKey(MetaDataIOFactory.TagType.NATIVE) ? TagSizes[MetaDataIOFactory.TagType.NATIVE] : 0; } }
            /// <summary>
            /// Total size of all tags (bytes)
            /// </summary>
            public long TotalTagSize { get { return ID3v1Size + ID3v2Size + APESize + NativeSize; } }
            /// <summary>
            /// Size of the entire file (bytes)
            /// </summary>
            public long FileSize { get; set; } = 0;
            /// <summary>
            /// Offset of the audio data (bytes)
            /// </summary>
            public long AudioDataOffset { get; set; } = -1;
            /// <summary>
            /// Size of the audio data (bytes)
            /// </summary>
            public long AudioDataSize
            {
                get
                {
                    if (audioDataSize <= 0) return FileSize - TotalTagSize;
                    else return audioDataSize;
                }
                set => audioDataSize = value;
            }
        }

        private IMetaDataIO iD3v1 = new ID3v1();
        private IMetaDataIO iD3v2 = new ID3v2();
        private IMetaDataIO aPEtag = new APEtag();
        private IMetaDataIO nativeTag;

        private readonly IAudioDataIO audioDataIO;
        private readonly Stream stream;

        private readonly SizeInfo sizeInfo = new SizeInfo();
        private readonly IProgress<float> writeProgress;


        private string fileName
        {
            get { return audioDataIO.FileName; }
        }
        /// <summary>
        /// ID3v1 tag data
        /// </summary>
        public IMetaDataIO ID3v1
        {
            get { return iD3v1; }
        }
        /// <summary>
        /// ID3v2 tag data
        /// </summary>
        public IMetaDataIO ID3v2
        {
            get { return iD3v2; }
        }
        /// <summary>
        /// APE tag data
        /// </summary>
        public IMetaDataIO APEtag
        {
            get { return aPEtag; }
        }
        /// <summary>
        /// Native tag data
        /// </summary>
        public IMetaDataIO NativeTag
        {
            get { return nativeTag; }
        }
        /// <summary>
        /// Offset of audio data (bytes)
        /// </summary>
        public long AudioDataOffset { get => sizeInfo.AudioDataOffset; }
        /// <summary>
        /// Size of audio data (bytes)
        /// </summary>
        public long AudioDataSize { get => sizeInfo.AudioDataSize; }

        /// <summary>
        /// Create a new instance using the given IAudioDataIO and the given IProgress
        /// </summary>
        /// <param name="audioDataReader">Audio data reader to use</param>
        /// <param name="writeProgress">IProgress to report with (optional)</param>
        public AudioDataManager(IAudioDataIO audioDataReader, IProgress<float> writeProgress = null)
        {
            this.audioDataIO = audioDataReader;
            this.stream = null;
            this.writeProgress = writeProgress;
        }

        /// <summary>
        /// Create a new instance using the given IAudioDataIO, the given data Stream and the given IProgress
        /// </summary>
        /// <param name="audioDataReader">Audio data reader to use</param>
        /// <param name="stream">Data stream to use</param>
        /// <param name="writeProgress">IProgress to report with (optional)</param>
        public AudioDataManager(IAudioDataIO audioDataReader, Stream stream, IProgress<float> writeProgress = null)
        {
            this.audioDataIO = audioDataReader;
            this.stream = stream;
            this.writeProgress = writeProgress;
        }


        // ====================== METHODS =========================

        private void resetData()
        {
            sizeInfo.ResetData();
        }

        /// <summary>
        /// Indicate whether the current audio file contains a tag from the given type
        /// </summary>
        /// <param name="type">Tag type whose presence to check</param>
        /// <returns>True if the current audio file contains a tag of the given type; false if not</returns>
        public bool hasMeta(MetaDataIOFactory.TagType type)
        {
            if (type.Equals(MetaDataIOFactory.TagType.ID3V1))
            {
                return (iD3v1 != null) && iD3v1.Exists;
            }
            else if (type.Equals(MetaDataIOFactory.TagType.ID3V2))
            {
                return (iD3v2 != null) && iD3v2.Exists;
            }
            else if (type.Equals(MetaDataIOFactory.TagType.APE))
            {
                return (aPEtag != null) && aPEtag.Exists;
            }
            else if (type.Equals(MetaDataIOFactory.TagType.NATIVE))
            {
                return (nativeTag != null) && nativeTag.Exists;
            }
            else return false;
        }

        /// <summary>
        /// Indicate whether the current file supports native tagging
        /// </summary>
        /// <returns>True if the current file supports native tagging; false if it doesn't</returns>
        public bool HasNativeMeta()
        {
            return audioDataIO.IsMetaSupported(MetaDataIOFactory.TagType.NATIVE);
        }

        /// <summary>
        /// List the available tag types of the current file
        /// </summary>
        /// <returns>List of tag types available in the current file</returns>
        public IList<MetaDataIOFactory.TagType> getAvailableMetas()
        {
            IList<MetaDataIOFactory.TagType> result = new List<MetaDataIOFactory.TagType>();
            foreach (var tagType in from MetaDataIOFactory.TagType tagType in Enum.GetValues(typeof(MetaDataIOFactory.TagType))
                                    where hasMeta(tagType)
                                    select tagType)
            {
                result.Add(tagType);
            }

            return result;
        }

        /// <summary>
        /// List the tag types supported by the format of the current file
        /// </summary>
        /// <returns>Tag types supported by the format of the current file</returns>
        public IList<MetaDataIOFactory.TagType> getSupportedMetas()
        {
            IList<MetaDataIOFactory.TagType> result = new List<MetaDataIOFactory.TagType>();
            foreach (var tagType in from MetaDataIOFactory.TagType tagType in Enum.GetValues(typeof(MetaDataIOFactory.TagType))
                                    where audioDataIO.IsMetaSupported(tagType)
                                    select tagType)
            {
                result.Add(tagType);
            }

            return result;
        }

        /// <summary>
        /// Return metadata from the given tag type from the current file
        /// </summary>
        /// <param name="type">Tag type to retrieve metadata from</param>
        /// <returns>Metadata I/O for the given tag type</returns>
        public IMetaDataIO getMeta(MetaDataIOFactory.TagType type)
        {
            if (type.Equals(MetaDataIOFactory.TagType.ID3V1))
            {
                return iD3v1;
            }
            else if (type.Equals(MetaDataIOFactory.TagType.ID3V2))
            {
                return iD3v2;
            }
            else if (type.Equals(MetaDataIOFactory.TagType.APE))
            {
                return aPEtag;
            }
            else if (type.Equals(MetaDataIOFactory.TagType.NATIVE) && nativeTag != null)
            {
                return nativeTag;
            }
            else return new DummyTag();
        }

        /// <summary>
        /// Set the given metadata to the current file
        /// NB : Operates on RAM; doesn't save the file on disk. To do so, use UpdateTagInFile
        /// </summary>
        /// <param name="meta">Metadata to set</param>
        public void setMeta(IMetaDataIO meta)
        {
            if (meta is ID3v1)
            {
                iD3v1 = meta;
                sizeInfo.SetSize(MetaDataIOFactory.TagType.ID3V1, iD3v1.Size);
            }
            else if (meta is ID3v2)
            {
                iD3v2 = meta;
                sizeInfo.SetSize(MetaDataIOFactory.TagType.ID3V2, iD3v2.Size);
            }
            else if (meta is APEtag)
            {
                aPEtag = meta;
                sizeInfo.SetSize(MetaDataIOFactory.TagType.APE, aPEtag.Size);
            }
            else
            {
                nativeTag = meta;
                sizeInfo.SetSize(MetaDataIOFactory.TagType.NATIVE, nativeTag.Size);
            }
        }

        /// <summary>
        /// Read all metadata from the current file
        /// </summary>
        /// <param name="readEmbeddedPictures">True if embedded pictures should be read; false if not (faster, less memory)</param>
        /// <param name="readAllMetaFrames">True if all frames, including "Additional fields" should be read; false if only fields published in IMetaDataIO should be read</param>
        /// <returns>True if the operation succeeds; false if an issue happened (in that case, the problem is logged on screen + in a Log)</returns>
        public bool ReadFromFile(bool readEmbeddedPictures = false, bool readAllMetaFrames = false)
        {
            bool result = false;
            LogDelegator.GetLocateDelegate()(fileName);

            resetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                Stream s = (null == stream) ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOptions) : stream;
                BinaryReader source = new BinaryReader(s);
                try
                {
                    result = read(source, readEmbeddedPictures, readAllMetaFrames);
                }
                finally
                {
                    if (null == stream) source.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Update metadata of current file and save it to disk
        /// </summary>
        /// <param name="theTag">Metadata to save</param>
        /// <param name="tagType">TagType to save the given metadata with</param>
        /// <returns>True if the operation succeeds; false if an issue happened (in that case, the problem is logged on screen + in a Log)</returns>
        public bool UpdateTagInFile(TagData theTag, MetaDataIOFactory.TagType tagType)
        {
            bool result = true;
            IMetaDataIO theMetaIO;
            LogDelegator.GetLocateDelegate()(fileName);
            theTag.DurationMs = audioDataIO.Duration;

            if (audioDataIO.IsMetaSupported(tagType))
            {
                try
                {
                    theMetaIO = getMeta(tagType);

                    Stream s = (null == stream) ? new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions) : stream;
                    BinaryReader r = new BinaryReader(s);
                    BinaryWriter w = new BinaryWriter(s);
                    try
                    {
                        // If current file can embed metadata, do a 1st pass to detect embedded metadata position
                        if (audioDataIO is IMetaDataEmbedder)
                        {
                            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(false, false);
                            readTagParams.PrepareForWriting = true;

                            audioDataIO.Read(r, sizeInfo, readTagParams);
                            theMetaIO.SetEmbedder((IMetaDataEmbedder)audioDataIO);
                        }

                        result = theMetaIO.Write(r, w, theTag, writeProgress);
                        if (result) setMeta(theMetaIO);
                    }
                    finally
                    {
                        if (null == stream)
                        {
                            r.Close();
                            w.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
                    result = false;
                }
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Tag type " + tagType + " not supported");
            }

            return result;
        }

        /// <summary>
        /// Remove the tagging from the given type (i.e. the whole technical structure, not only values) from the current file
        /// </summary>
        /// <param name="tagType">Type of the tagging to be removed</param>
        /// <returns>True if the operation succeeds; false if an issue happened (in that case, the problem is logged on screen + in a Log)</returns>
        public bool RemoveTagFromFile(MetaDataIOFactory.TagType tagType)
        {
            bool result = false;
            LogDelegator.GetLocateDelegate()(fileName);

            try
            {
                Stream s = (null == stream) ? new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions) : stream;
                BinaryReader reader = new BinaryReader(s);
                BinaryWriter writer = null;
                try
                {
                    result = read(reader, false, false, true);

                    IMetaDataIO metaIO = getMeta(tagType);
                    if (metaIO.Exists)
                    {
                        writer = new BinaryWriter(s);
                        metaIO.Remove(writer);
                    }
                }
                finally
                {
                    if (null == stream)
                    {
                        reader.Close();
                        if (writer != null) writer.Close();
                    }
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
                result = false;
            }

            return result;
        }

        private bool read(BinaryReader source, bool readEmbeddedPictures = false, bool readAllMetaFrames = false, bool prepareForWriting = false)
        {
            sizeInfo.ResetData();

            sizeInfo.FileSize = source.BaseStream.Length;
            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(readEmbeddedPictures, readAllMetaFrames);
            readTagParams.PrepareForWriting = prepareForWriting;

            return read(source, readTagParams);
        }

        private bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TagType.ID3V1))
            {
                if (iD3v1.Read(source, readTagParams)) sizeInfo.SetSize(MetaDataIOFactory.TagType.ID3V1, iD3v1.Size);
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TagType.ID3V2))
            {
                if (!(audioDataIO is IMetaDataEmbedder)) // No embedded ID3v2 tag => supported tag is the standard version of ID3v2
                {
                    if (iD3v2.Read(source, readTagParams)) sizeInfo.SetSize(MetaDataIOFactory.TagType.ID3V2, iD3v2.Size);
                }
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TagType.APE))
            {
                if (aPEtag.Read(source, readTagParams)) sizeInfo.SetSize(MetaDataIOFactory.TagType.APE, aPEtag.Size);
            }

            bool result;
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TagType.NATIVE) && audioDataIO is IMetaDataIO)
            {
                nativeTag = (IMetaDataIO)audioDataIO;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);

                if (result) sizeInfo.SetSize(MetaDataIOFactory.TagType.NATIVE, nativeTag.Size);
            }
            else
            {
                readTagParams.ReadTag = false;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);
            }

            if (audioDataIO is IMetaDataEmbedder embedder) // Embedded ID3v2 tag detected while reading
            {
                if (embedder.HasEmbeddedID3v2 > 0)
                {
                    readTagParams.Offset = embedder.HasEmbeddedID3v2;
                    if (iD3v2.Read(source, readTagParams)) sizeInfo.SetSize(MetaDataIOFactory.TagType.ID3V2, iD3v2.Size);
                }
                else
                {
                    iD3v2.Clear();
                }
            }

            sizeInfo.AudioDataOffset = audioDataIO.AudioDataOffset;
            if (audioDataIO.AudioDataSize > 0) sizeInfo.AudioDataSize = audioDataIO.AudioDataSize;

            return result;
        }
    }
}
