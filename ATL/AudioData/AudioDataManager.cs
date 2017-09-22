using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
    public class AudioDataManager
    {
        // Optimal settings according to performance test
        public static int bufferSize = 2048;
        public static FileOptions fileOptions = FileOptions.RandomAccess;

        public static void ChangeFileOptions(FileOptions options)
        {
            fileOptions = options;
        }

        public static void ChangeBufferSize(int bufSize)
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

        // TODO - instanciate only when needed !
        private IMetaDataIO iD3v1 = new ID3v1();
        private IMetaDataIO iD3v2 = new ID3v2();
        private IMetaDataIO aPEtag = new APEtag();
        private IMetaDataIO nativeTag;

        private readonly IAudioDataIO audioDataIO;

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

        public bool ReadFromFile(TagData.PictureStreamHandlerDelegate pictureStreamHandler = null, bool readAllMetaFrames = false)
        {
            bool result = false;
            LogDelegator.GetLocateDelegate()(fileName);

            resetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOptions))
                using (BinaryReader source = new BinaryReader(fs))
                {
                    result = read(source, pictureStreamHandler, readAllMetaFrames);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + fileName + ")");
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

                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions))
                    using (BinaryReader r = new BinaryReader(fs))
                    using (BinaryWriter w = new BinaryWriter(fs))
                    {
                        if (audioDataIO is IMetaDataEmbedder)
                        {
                            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(null, false);
                            readTagParams.PrepareForWriting = true;

                            audioDataIO.Read(r, sizeInfo, readTagParams);
                            theMetaIO.SetEmbedder((IMetaDataEmbedder)audioDataIO);
                        }

                        result = theMetaIO.Write(r, w, theTag);
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.Message);
                    System.Console.WriteLine(e.StackTrace);
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + fileName + ")");
                    result = false;
                }
            } else
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Tag type " + tagType + " not supported in " + fileName);
            }

            return result;
        }

        public bool RemoveTagFromFile(int tagType)
        {
            bool result = false;
            LogDelegator.GetLocateDelegate()(fileName);

            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    result = read(reader,null,false,true);

                    IMetaDataIO metaIO = getMeta(tagType);
                    if (metaIO.Exists)
                    {
                        using (BinaryWriter writer = new BinaryWriter(fs))
                        {
                            metaIO.Remove(writer);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + fileName + ")");
                result = false;
            }

            return result;
        }

        private bool read(BinaryReader source, TagData.PictureStreamHandlerDelegate pictureStreamHandler = null, bool readAllMetaFrames = false, bool prepareForWriting = false)
        {
            bool result = false;
            sizeInfo.ResetData();

            sizeInfo.FileSize = source.BaseStream.Length;
            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(pictureStreamHandler, readAllMetaFrames);
            readTagParams.PrepareForWriting = prepareForWriting;

            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_ID3V1))
            {
                if (iD3v1.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_ID3V1, iD3v1.Size);
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_ID3V2))
            {
                if (!(audioDataIO is IMetaDataEmbedder)) // No embedded ID3v2 tag => supported tag is the standard version
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
                IMetaDataIO nativeTag = (IMetaDataIO)audioDataIO; // TODO : This is dirty as ****; there must be a better way !
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
