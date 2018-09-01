using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
    /// <summary>
    /// Handles high-level basic operations on the given audio file, calling Metadata readers when needed
    /// </summary>
    public class AudioDataManager
    {
        // Settings to use when opening any FileStream
        // NB : These settings are optimal according to performance tests on the dev environment
        public static int bufferSize = 2048;
        public static FileOptions fileOptions = FileOptions.RandomAccess;

        public static void SetFileOptions(FileOptions options)
        {
            fileOptions = options;
        }

        public static void SetBufferSize(int bufSize)
        {
            bufferSize = bufSize;
        }


        public class SizeInfo
        {
            public long FileSize = 0;
            public IDictionary<int, long> TagSizes = new Dictionary<int, long>();

            public void ResetData() { FileSize = 0; TagSizes.Clear(); }

            public long ID3v1Size { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_ID3V1) ? TagSizes[MetaDataIOFactory.TAG_ID3V1] : 0; } }
            public long ID3v2Size { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_ID3V2) ? TagSizes[MetaDataIOFactory.TAG_ID3V2] : 0; } }
            public long APESize { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_APE) ? TagSizes[MetaDataIOFactory.TAG_APE] : 0; } }
            public long NativeSize { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_NATIVE) ? TagSizes[MetaDataIOFactory.TAG_NATIVE] : 0; } }
            public long TotalTagSize { get { return ID3v1Size + ID3v2Size + APESize + NativeSize; } }
        }

        private IMetaDataIO iD3v1 = new ID3v1();
        private IMetaDataIO iD3v2 = new ID3v2();
        private IMetaDataIO aPEtag = new APEtag();
        private IMetaDataIO nativeTag;

        private readonly IAudioDataIO audioDataIO;
        private readonly Stream stream;

        private SizeInfo sizeInfo = new SizeInfo();


        private string fileName
        {
            get { return audioDataIO.FileName; }
        }
        public IMetaDataIO ID3v1 // ID3v1 tag data
        {
            get { return this.iD3v1; }
        }
        public IMetaDataIO ID3v2 // ID3v2 tag data
        {
            get { return this.iD3v2; }
        }
        public IMetaDataIO APEtag // APE tag data
        {
            get { return this.aPEtag; }
        }
        public IMetaDataIO NativeTag // Native tag data
        {
            get { return this.nativeTag; }
        }



        public AudioDataManager(IAudioDataIO audioDataReader)
        {
            this.audioDataIO = audioDataReader;
            this.stream = null;
        }

        public AudioDataManager(IAudioDataIO audioDataReader, Stream stream)
        {
            this.audioDataIO = audioDataReader;
            this.stream = stream;
        }


        // ====================== METHODS =========================

        private void resetData()
        {
            sizeInfo.ResetData();
        }

        public bool hasMeta(int tagType)
        {
            if (tagType.Equals(MetaDataIOFactory.TAG_ID3V1))
            {
                return ((iD3v1 != null) && (iD3v1.Exists));
            } else if (tagType.Equals(MetaDataIOFactory.TAG_ID3V2))
            {
                return ((iD3v2 != null) && (iD3v2.Exists));
            } else if (tagType.Equals(MetaDataIOFactory.TAG_APE))
            {
                return ((aPEtag != null) && (aPEtag.Exists));
            } else if (tagType.Equals(MetaDataIOFactory.TAG_NATIVE))
            {
                return ((nativeTag != null) && (nativeTag.Exists));
            } else return false;
        }

        public IList<int> getAvailableMetas()
        {
            IList<int> result = new List<int>();

            if (hasMeta(MetaDataIOFactory.TAG_ID3V1)) result.Add(MetaDataIOFactory.TAG_ID3V1);
            if (hasMeta(MetaDataIOFactory.TAG_ID3V2)) result.Add(MetaDataIOFactory.TAG_ID3V2);
            if (hasMeta(MetaDataIOFactory.TAG_APE)) result.Add(MetaDataIOFactory.TAG_APE);
            if (hasMeta(MetaDataIOFactory.TAG_NATIVE)) result.Add(MetaDataIOFactory.TAG_NATIVE);

            return result;
        }

        public IMetaDataIO getMeta(int tagType)
        {
            if (tagType.Equals(MetaDataIOFactory.TAG_ID3V1))
            {
                return iD3v1;
            }
            else if (tagType.Equals(MetaDataIOFactory.TAG_ID3V2))
            {
                return iD3v2;
            }
            else if (tagType.Equals(MetaDataIOFactory.TAG_APE))
            {
                return aPEtag;
            }
            else if (tagType.Equals(MetaDataIOFactory.TAG_NATIVE) && nativeTag != null)
            {
                return nativeTag;
            }
            else return new DummyTag();
        }

        [Obsolete]
        public bool ReadFromFile(TagData.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames = false)
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
                    result = read(source, pictureStreamHandler, readAllMetaFrames);
                }
                finally
                {
                    if (null == stream) source.Close();
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
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
                result = false;
            }

            return result;
        }

        public bool UpdateTagInFile(TagData theTag, int tagType)
        {
            bool result = true;
            IMetaDataIO theMetaIO;
            LogDelegator.GetLocateDelegate()(fileName);

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

                        result = theMetaIO.Write(r, w, theTag);
                    } finally
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
                    System.Console.WriteLine(e.Message);
                    System.Console.WriteLine(e.StackTrace);
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
                    result = false;
                }
            } else
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Tag type " + tagType + " not supported");
            }

            return result;
        }

        public bool RemoveTagFromFile(int tagType)
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
                    result = read(reader,false,false,true);

                    IMetaDataIO metaIO = getMeta(tagType);
                    if (metaIO.Exists)
                    {
                        writer = new BinaryWriter(s);
                        metaIO.Remove(writer);
                    }
                } finally
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

        [Obsolete]
        private bool read(BinaryReader source, TagData.PictureStreamHandlerDelegate pictureStreamHandler = null, bool readAllMetaFrames = false, bool prepareForWriting = false)
        {
            sizeInfo.ResetData();

            sizeInfo.FileSize = source.BaseStream.Length;
            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(pictureStreamHandler, readAllMetaFrames);
            readTagParams.PrepareForWriting = prepareForWriting;

            return read(source, readTagParams);
        }

        private bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;

            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_ID3V1))
            {
                if (iD3v1.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_ID3V1, iD3v1.Size);
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_ID3V2))
            {
                if (!(audioDataIO is IMetaDataEmbedder)) // No embedded ID3v2 tag => supported tag is the standard version of ID3v2
                {
                    if (iD3v2.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_ID3V2, iD3v2.Size);
                }
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_APE))
            {
                if (aPEtag.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_APE, aPEtag.Size);
            }

            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_NATIVE) && audioDataIO is IMetaDataIO)
            {
                IMetaDataIO nativeTag = (IMetaDataIO)audioDataIO;
                this.nativeTag = nativeTag;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);

                if (result) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_NATIVE, nativeTag.Size);
            } else
            {
                readTagParams.ReadTag = false;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);
            }

            if (audioDataIO is IMetaDataEmbedder) // Embedded ID3v2 tag detected while reading
            {
                if (((IMetaDataEmbedder)audioDataIO).HasEmbeddedID3v2 > 0)
                {
                    readTagParams.offset = ((IMetaDataEmbedder)audioDataIO).HasEmbeddedID3v2;
                    if (iD3v2.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_ID3V2, iD3v2.Size);
                } else
                {
                    iD3v2.Clear();
                }
            }

            return result;
        }

        public bool HasNativeMeta()
        {
            return audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_NATIVE);
        }
    }
}
