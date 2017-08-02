using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
    public class AudioDataIO : IOBase
    {
        public class SizeInfo
        {
            public long FileSize = 0;
            public IDictionary<int, long> TagSizes = new Dictionary<int, long>();

            public void ResetData() { FileSize = 0; TagSizes.Clear(); }

            public long ID3v1Size { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_ID3V1) ? TagSizes[MetaDataIOFactory.TAG_ID3V1] : 0; } }
            public long ID3v2Size { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_ID3V2) ? TagSizes[MetaDataIOFactory.TAG_ID3V2] : 0; } }
            public long APESize { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_APE) ? TagSizes[MetaDataIOFactory.TAG_APE] : 0; } }
            public long NativeSize { get { return TagSizes.ContainsKey(MetaDataIOFactory.TAG_NATIVE) ? TagSizes[MetaDataIOFactory.TAG_NATIVE] : 0; } }
        }

        private IMetaDataIO FID3v1 = new ID3v1();
        private IMetaDataIO FID3v2 = new ID3v2();
        private IMetaDataIO FAPEtag = new APEtag();
        private IMetaDataIO FNativeTag;

        private readonly IAudioDataIO audioDataIO;

        private SizeInfo sizeInfo = new SizeInfo();


        private string fileName
        {
            get { return audioDataIO.FileName; }
        }
        public IMetaDataIO ID3v1 // ID3v1 tag data
        {
            get { return this.FID3v1; }
        }
        public IMetaDataIO ID3v2 // ID3v2 tag data
        {
            get { return this.FID3v2; }
        }
        public IMetaDataIO APEtag // APE tag data
        {
            get { return this.FAPEtag; }
        }
        public IMetaDataIO NativeTag // Native tag data
        {
            get { return this.FNativeTag; }
        }

        // ====================== METHODS =========================
        public AudioDataIO(IAudioDataIO audioDataReader)
        {
            this.audioDataIO = audioDataReader;
        }

        private void resetData()
        {
            sizeInfo.ResetData();
        }

        public bool hasMeta(int tagType)
        {
            if (tagType.Equals(MetaDataIOFactory.TAG_ID3V1))
            {
                return ((FID3v1 != null) && (FID3v1.Exists));
            } else if (tagType.Equals(MetaDataIOFactory.TAG_ID3V2))
            {
                return ((FID3v2 != null) && (FID3v2.Exists));
            } else if (tagType.Equals(MetaDataIOFactory.TAG_APE))
            {
                return ((FAPEtag != null) && (FAPEtag.Exists));
            } else if (tagType.Equals(MetaDataIOFactory.TAG_NATIVE))
            {
                return ((FNativeTag != null) && (FNativeTag.Exists));
            } else return false;
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
            IMetaDataIO theMetaIO = null;
            LogDelegator.GetLocateDelegate()(fileName);

            if (audioDataIO.IsMetaSupported(tagType))
            {
                try
                {
                    switch (tagType)
                    {
                        case MetaDataIOFactory.TAG_ID3V1:
                            theMetaIO = ID3v1;
                            break;
                        case MetaDataIOFactory.TAG_ID3V2:
                            theMetaIO = ID3v2;
                            break;
                        case MetaDataIOFactory.TAG_APE:
                            theMetaIO = APEtag;
                            break;
                        case MetaDataIOFactory.TAG_NATIVE:
                            theMetaIO = NativeTag;
                            break;
                        default:
                            theMetaIO = null;
                            break;
                    }

                    if (theMetaIO != null)
                    {
                        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions))
                        using (BinaryReader r = new BinaryReader(fs))
                        using (BinaryWriter w = new BinaryWriter(fs))
                        {
                            long deltaTagSize = theMetaIO.Write(r, w, theTag);
                            if (deltaTagSize < long.MaxValue)
                            {
                                if (deltaTagSize != 0)
                                {
                                    result = audioDataIO.RewriteFileSizeInHeader(w, (int)deltaTagSize);
                                }
                            }
                            else
                            {
                                result = false;
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
                    result = read(reader);

                    long tagOffset = -1;
                    int tagSize = 0;
                    byte[] coreSignature = new byte[0];

                    if (tagType.Equals(MetaDataIOFactory.TAG_ID3V1) && (hasMeta(MetaDataIOFactory.TAG_ID3V1)))
                    {
                        tagOffset = ID3v1.Offset;
                        tagSize = ID3v1.Size;
                        coreSignature = ID3v1.CoreSignature;
                    }
                    else if (tagType.Equals(MetaDataIOFactory.TAG_ID3V2) && (hasMeta(MetaDataIOFactory.TAG_ID3V2)))
                    {
                        tagOffset = ID3v2.Offset;
                        tagSize = ID3v2.Size;
                        coreSignature = ID3v2.CoreSignature;
                    }
                    else if (tagType.Equals(MetaDataIOFactory.TAG_APE) && (hasMeta(MetaDataIOFactory.TAG_APE)))
                    {
                        tagOffset = APEtag.Offset;
                        tagSize = APEtag.Size;
                        coreSignature = APEtag.CoreSignature;
                    }
                    else if (tagType.Equals(MetaDataIOFactory.TAG_NATIVE) && (hasMeta(MetaDataIOFactory.TAG_NATIVE)))
                    {
                        // TODO : handle native tags scattered amond various, not necessarily contiguous chunks (e.g. AIFF)
                        tagOffset = NativeTag.Offset;
                        tagSize = NativeTag.Size;
                        coreSignature = NativeTag.CoreSignature;
                    }

                    if ((tagOffset > -1) && (tagSize > 0))
                    {
                        StreamUtils.ShortenStream(fs, tagOffset+tagSize, (uint)(tagSize-coreSignature.Length));
                        using (BinaryWriter writer = new BinaryWriter(fs))
                        {
                            if (coreSignature.Length > 0)
                            {
                                fs.Position = tagOffset;
                                writer.Write(coreSignature);
                            }
                            result = audioDataIO.RewriteFileSizeInHeader(writer, -tagSize+coreSignature.Length);
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

        private bool read(BinaryReader source, TagData.PictureStreamHandlerDelegate pictureStreamHandler = null, bool readAllMetaFrames = false)
        {
            bool result = false;
            sizeInfo.ResetData();

            sizeInfo.FileSize = source.BaseStream.Length;
            MetaDataIO.ReadTagParams readTagParams = new MetaDataIO.ReadTagParams(pictureStreamHandler, readAllMetaFrames);

            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "read begin");
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_ID3V1))
            {
                if (FID3v1.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_ID3V1, FID3v1.Size);
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "read id3v1 end");
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_ID3V2))
            {
                if (FID3v2.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_ID3V2, FID3v2.Size);
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "read id3v2 end");
            }
            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_APE))
            {
                if (FAPEtag.Read(source, readTagParams)) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_APE, FAPEtag.Size);
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "read ape end");
            }

            if (audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_NATIVE) && audioDataIO is IMetaDataIO)
            {
                IMetaDataIO nativeTag = (IMetaDataIO)audioDataIO; // TODO : This is dirty as ****; there must be a better way !
                FNativeTag = nativeTag;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);

                if (result) sizeInfo.TagSizes.Add(MetaDataIOFactory.TAG_NATIVE, nativeTag.Size);
            } else
            {
                readTagParams.ReadTag = false;
                result = audioDataIO.Read(source, sizeInfo, readTagParams);
            }
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "read end");

            return result;
        }

        public bool HasNativeMeta()
        {
            return audioDataIO.IsMetaSupported(MetaDataIOFactory.TAG_NATIVE);
        }
    }
}
