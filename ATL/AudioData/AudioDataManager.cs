using ATL.AudioData.IO;
using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ATL.AudioData.IO.MetaDataIO;
using static ATL.AudioData.MetaDataIOFactory;

namespace ATL.AudioData
{
    /// <summary>
    /// Handles high-level basic operations on the given audio file, calling Metadata readers when needed
    /// </summary>
    public partial class AudioDataManager
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
            private readonly IDictionary<TagType, long> TagSizes = new Dictionary<TagType, long>();
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
            public void SetSize(TagType type, long size)
            {
                TagSizes[type] = size;
            }

            /// <summary>
            /// Size of the ID3v1 tag (bytes)
            /// </summary>
            public long ID3v1Size => TagSizes.ContainsKey(TagType.ID3V1) ? TagSizes[TagType.ID3V1] : 0;

            /// <summary>
            /// Size of the ID3v2 tag (bytes)
            /// </summary>
            public long ID3v2Size => TagSizes.ContainsKey(TagType.ID3V2) ? TagSizes[TagType.ID3V2] : 0;
            /// <summary>
            /// Size of the APE tag (bytes)
            /// </summary>
            public long APESize => TagSizes.ContainsKey(TagType.APE) ? TagSizes[TagType.APE] : 0;

            /// <summary>
            /// Size of the native tag (bytes)
            /// </summary>
            public long NativeSize => TagSizes.ContainsKey(TagType.NATIVE) ? TagSizes[TagType.NATIVE] : 0;
            /// <summary>
            /// Total size of all tags (bytes)
            /// </summary>
            public long TotalTagSize => ID3v1Size + ID3v2Size + APESize + NativeSize;

            /// <summary>
            /// Size of the entire file (bytes)
            /// </summary>
            public long FileSize { get; set; }
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


        private string fileName => audioDataIO.FileName;

        /// <summary>
        /// ID3v1 tag data
        /// </summary>
        public IMetaDataIO ID3v1 => iD3v1;

        /// <summary>
        /// ID3v2 tag data
        /// </summary>
        public IMetaDataIO ID3v2 => iD3v2;

        /// <summary>
        /// APE tag data
        /// </summary>
        public IMetaDataIO APEtag => aPEtag;

        /// <summary>
        /// Native tag data
        /// </summary>
        public IMetaDataIO NativeTag => nativeTag;

        /// <summary>
        /// Offset of audio data (bytes)
        /// </summary>
        public long AudioDataOffset => sizeInfo.AudioDataOffset;

        /// <summary>
        /// Size of audio data (bytes)
        /// </summary>
        public long AudioDataSize => sizeInfo.AudioDataSize;

        /// <summary>
        /// Create a new instance using the given IAudioDataIO and the given IProgress
        /// </summary>
        /// <param name="audioDataReader">Audio data reader to use</param>
        internal AudioDataManager(IAudioDataIO audioDataReader)
        {
            this.audioDataIO = audioDataReader;
            this.stream = null;
        }

        /// <summary>
        /// Create a new instance using the given IAudioDataIO, the given data Stream and the given IProgress
        /// </summary>
        /// <param name="audioDataReader">Audio data reader to use</param>
        /// <param name="stream">Data stream to use</param>
        internal AudioDataManager(IAudioDataIO audioDataReader, Stream stream)
        {
            this.audioDataIO = audioDataReader;
            this.stream = stream;
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
        public bool hasMeta(TagType type)
        {
            return type switch
            {
                TagType.ID3V1 => iD3v1 is { Exists: true },
                TagType.ID3V2 => iD3v2 is { Exists: true },
                TagType.APE => aPEtag is { Exists: true },
                TagType.NATIVE => nativeTag is { Exists: true },
                _ => false
            };
        }

        /// <summary>
        /// Indicate whether the current file supports native tagging
        /// </summary>
        /// <returns>True if the current file supports native tagging; false if it doesn't</returns>
        public bool HasNativeMeta()
        {
            return isMetaSupported(TagType.NATIVE);
        }

        private bool isMetaSupported(TagType meta)
        {
            return audioDataIO.GetSupportedMetas().Contains(meta);
        }

        /// <summary>
        /// List the available tag types of the current file
        /// </summary>
        /// <returns>List of tag types available in the current file</returns>
        public ISet<TagType> getAvailableMetas()
        {
            ISet<TagType> result = new HashSet<TagType>();
            foreach (var tagType in from TagType tagType in Enum.GetValues(typeof(TagType))
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
        public ISet<TagType> getSupportedMetas()
        {
            ISet<TagType> result = new HashSet<TagType>();
            foreach (var tagType in from TagType tagType in Enum.GetValues(typeof(TagType))
                                    where isMetaSupported(tagType)
                                    select tagType)
            {
                result.Add(tagType);
            }

            return result;
        }

        /// <summary>
        /// List the tag types recommended for the format of the current file
        /// </summary>
        /// <returns>Tag types recommended for the format of the current file</returns>
        public ISet<TagType> getRecommendedMetas()
        {
            ISet<TagType> result = new HashSet<TagType>();
            var supportedMetas = audioDataIO.GetSupportedMetas();
            if (supportedMetas.Count <= 0) return result;

            if (1 == supportedMetas.Count) result.Add(supportedMetas[0]);
            else
            {
                // TODO this is ugly (see #249)
                if (audioDataIO is OptimFrog) result.Add(TagType.APE);
                else if (audioDataIO is WAV)
                {
                    result.Add(TagType.ID3V2);
                    result.Add(TagType.NATIVE);
                }
                else
                {
                    var id3v2Exists = supportedMetas.Contains(TagType.ID3V2);
                    bool isNativeRich = audioDataIO.IsNativeMetadataRich && supportedMetas.Exists(meta => meta == TagType.NATIVE);
                    foreach (var meta in supportedMetas.Where(meta => meta != TagType.ID3V1))
                    {
                        if (meta == TagType.NATIVE && isNativeRich) result.Add(meta);
                        if (meta == TagType.ID3V2 && !isNativeRich) result.Add(meta); // If poor native metadata
                        if (meta == TagType.APE && !id3v2Exists && !isNativeRich) result.Add(meta); // If no ID3v2 support and poor native metadata
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Return metadata from the given tag type from the current file
        /// </summary>
        /// <param name="type">Tag type to retrieve metadata from</param>
        /// <returns>Metadata I/O for the given tag type</returns>
        public IMetaDataIO getMeta(TagType type)
        {
            if (type.Equals(TagType.ID3V1)) return iD3v1;
            if (type.Equals(TagType.ID3V2)) return iD3v2;
            if (type.Equals(TagType.APE)) return aPEtag;
            if (type.Equals(TagType.NATIVE) && nativeTag != null) return nativeTag;
            return new DummyTag();
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
                sizeInfo.SetSize(TagType.ID3V1, iD3v1.Size);
            }
            else if (meta is ID3v2)
            {
                iD3v2 = meta;
                sizeInfo.SetSize(TagType.ID3V2, iD3v2.Size);
            }
            else if (meta is APEtag)
            {
                aPEtag = meta;
                sizeInfo.SetSize(TagType.APE, aPEtag.Size);
            }
            else
            {
                nativeTag = meta;
                sizeInfo.SetSize(TagType.NATIVE, nativeTag.Size);
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
            bool result;
            LogDelegator.GetLocateDelegate()(fileName);

            resetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                Stream s = stream ?? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOptions);
                try
                {
                    result = read(s, readEmbeddedPictures, readAllMetaFrames);
                }
                finally
                {
                    if (null == stream) s.Close();
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Update metadata of current file and save it to disk
        /// Pre-requisite : ReadFromFile must have been called before
        /// </summary>
        /// <param name="theTag">Metadata to save</param>
        /// <param name="tagType">TagType to save the given metadata with</param>
        /// <param name="targetPath">Target path to save the data to (default : null = use current file)</param>
        /// <param name="targetStream">Target Stream to save the data to (default : null = use current Stream)</param>
        /// <param name="writeProgress">ProgressManager to report with (optional)</param>
        /// <returns>True if the operation succeeds; false if an issue happened (in that case, the problem is logged on screen + in a Log)</returns>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> UpdateTagInFileAsync(
            TagData theTag,
            TagType tagType,
            string targetPath = null,
            Stream targetStream = null,
            ProgressManager writeProgress = null)
        {
            bool result = true;
            if (null == targetPath && null == targetStream)
            {
                targetPath = fileName;
                targetStream = stream;
            }

            LogDelegator.GetLocateDelegate()(targetPath);
            theTag.DurationMs = audioDataIO.Duration;

            if (isMetaSupported(tagType) || hasMeta(tagType)) // Update supported _and_ present tagging systems
            {
                try
                {
                    var theMetaIO = getMeta(tagType);

                    var s = targetStream ?? new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize, fileOptions | FileOptions.Asynchronous);
                    try
                    {
                        // If current file can embed metadata, do a 1st pass to detect embedded metadata position
                        handleEmbedder(s, theMetaIO);

                        ProgressToken<float> progress = writeProgress?.CreateProgressToken();
                        var args = new WriteTagParams() {
                            ExtraID3v2PaddingDetection = isMetaSupported(TagType.ID3V2)
                        };
                        result = await theMetaIO.WriteAsync(s, theTag, args, progress);
                        if (result) setMeta(theMetaIO);
                    }
                    finally
                    {
                        if (null == targetStream) s.Close();
                    }
                }
                catch (Exception e)
                {
                    Utils.TraceException(e);
                    result = false;
                }
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Tag type " + tagType + " not supported");
            }

            return result;
        }

        private void handleEmbedder(Stream r, IMetaDataIO theMetaIO)
        {
            if (audioDataIO is IMetaDataEmbedder embedder)
            {
                MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams()
                {
                    PrepareForWriting = true
                };

                audioDataIO.Read(r, sizeInfo, readTagParams);
                theMetaIO.SetEmbedder(embedder);
            }
        }

        /// <summary>
        /// Remove the tagging from the given type (i.e. the whole technical structure, not only values) from the current file
        /// </summary>
        /// <param name="tagType">Type of the tagging to be removed</param>
        /// <param name="progressManager">ProgressManager to report with (optional)</param>
        /// <returns>True if the operation succeeds; false if an issue happened (in that case, the problem is logged on screen + in a Log)</returns>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> RemoveTagFromFileAsync(TagType tagType, ProgressManager progressManager = null)
        {
            bool result;
            LogDelegator.GetLocateDelegate()(fileName);

            try
            {
                var s = stream ?? new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize, fileOptions | FileOptions.Asynchronous);
                try
                {
                    result = read(s, false, false, true);

                    IMetaDataIO metaIO = getMeta(tagType);
                    var args = new WriteTagParams()
                    {
                        ExtraID3v2PaddingDetection = isMetaSupported(TagType.ID3V2)
                    };
                    if (metaIO.Exists) await metaIO.RemoveAsync(s, args);
                }
                finally
                {
                    if (null == stream) s.Close();
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
                result = false;
            }

            return result;
        }

        private bool read(Stream source, bool readEmbeddedPictures = false, bool readAllMetaFrames = false, bool prepareForWriting = false)
        {
            sizeInfo.ResetData();

            sizeInfo.FileSize = source.Length;
            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(readEmbeddedPictures, readAllMetaFrames);
            readTagParams.PrepareForWriting = prepareForWriting;

            return read(source, readTagParams);
        }

        private bool read(Stream source, MetaDataIO.ReadTagParams readTagParams)
        {
            if (isMetaSupported(TagType.ID3V1) && iD3v1.Read(source, readTagParams))
            {
                sizeInfo.SetSize(TagType.ID3V1, iD3v1.Size);
            }
            // No embedded ID3v2 tag => supported tag is the standard version of ID3v2
            if (!(audioDataIO is IMetaDataEmbedder))
            {
                // Reset data from ID3v2 tag structure
                iD3v2.Clear();
                // Test for ID3v2 regardless of it being supported, to properly handle files with illegal ID3v2 tags
                source.Position = 0;
                byte[] data = new byte[32];
                if (32 == source.Read(data, 0, 32) && IO.ID3v2.IsValidHeader(data))
                {
                    source.Position = 0;
                    readTagParams.ExtraID3v2PaddingDetection = isMetaSupported(TagType.ID3V2);
                    if (iD3v2.Read(source, readTagParams)) sizeInfo.SetSize(TagType.ID3V2, iD3v2.Size);
                }
                source.Position = 0;
            }
            if (isMetaSupported(TagType.APE) && aPEtag.Read(source, readTagParams))
            {
                sizeInfo.SetSize(TagType.APE, aPEtag.Size);
            }

            bool result;
            if (isMetaSupported(TagType.NATIVE) && audioDataIO is IMetaDataIO)
            {
                nativeTag = (IMetaDataIO)audioDataIO;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);

                if (result) sizeInfo.SetSize(TagType.NATIVE, nativeTag.Size);
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
                    if (iD3v2.Read(source, readTagParams)) sizeInfo.SetSize(TagType.ID3V2, iD3v2.Size);
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
