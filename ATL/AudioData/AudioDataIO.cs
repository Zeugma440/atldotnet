using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    public abstract class AudioDataIO : IOBase, IAudioDataIO
    {
        // Audio data
        protected double FBitrate;
        protected double FDuration;

        // File data
        protected long FFileSize;
        protected String FFileName;
        protected bool FValid;

        protected ID3v1 FID3v1 = new ID3v1();
        protected ID3v2 FID3v2 = new ID3v2();
        protected APEtag FAPEtag = new APEtag();
        protected IMetaDataIO FNativeTag;


        public double BitRate // Bitrate (KBit/s)
        {
            get { return Math.Round(FBitrate/1000.00); }
        }
        public double Duration // Duration (s)
        {
            get { return FDuration; }
        }

        // To be overriden by children classes
        public abstract bool IsVBR
        {
            get;
        }
        public abstract int CodecFamily
        {
            get;
        }
        public abstract bool AllowsParsableMetadata
        {
            get;
        }

        public ID3v1 ID3v1 // ID3v1 tag data
        {
            get { return this.FID3v1; }
        }
        public ID3v2 ID3v2 // ID3v2 tag data
        {
            get { return this.FID3v2; }
        }
        public APEtag APEtag // APE tag data
        {
            get { return this.FAPEtag; }
        }
        public IMetaDataIO NativeTag // Native tag data
        {
            get { return this.FNativeTag; }
        }

        // ====================== METHODS =========================

        abstract protected void resetSpecificData();

        protected void resetData()
        {
            FFileSize = 0;
            FValid = false;

            FBitrate = 0;
            FDuration = 0;

            FID3v1.ResetData();
            FID3v2.ResetData();
            FAPEtag.ResetData();

            resetSpecificData();
        }


        abstract public bool IsMetaSupported(int metaType);

        abstract protected bool Read(BinaryReader Source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler);

        abstract protected bool RewriteFileSizeInHeader(BinaryWriter w, long newFileSize);


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

        public bool ReadFromFile(MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler = null)
        {
            bool result = false;
            resetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                using (FileStream fs = new FileStream(FFileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOptions))
                using (BinaryReader source = new BinaryReader(fs))
                {
                    FFileSize = fs.Length;

                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "begin");
                    if (IsMetaSupported(MetaDataIOFactory.TAG_ID3V1)) FID3v1.Read(source);
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "id3v1");
                    if (IsMetaSupported(MetaDataIOFactory.TAG_ID3V2)) FID3v2.Read(source, pictureStreamHandler);
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "id3v2");
                    if (IsMetaSupported(MetaDataIOFactory.TAG_APE)) FAPEtag.Read(source, pictureStreamHandler);
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "ape");

                    result = Read(source, pictureStreamHandler);
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "read");

                    if (result && IsMetaSupported(MetaDataIOFactory.TAG_NATIVE)) FNativeTag = (IMetaDataIO)this; // TODO : This is dirty as ****; there must be a better way !
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FFileName + ")");
                result = false;
            }

            return result;
        }

        public bool AddTagToFile(TagData theTag, int tagType)
        {
            bool result = true;
            IMetaDataIO theMetaIO = null;

            if (IsMetaSupported(tagType))
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
                        using (FileStream fs = new FileStream(FFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions))
                        using (BinaryReader r = new BinaryReader(fs))
                        {
                            theMetaIO.Write(r, theTag);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.Message);
                    System.Console.WriteLine(e.StackTrace);
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FFileName + ")");
                    result = false;
                }
            } else
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Tag type " + tagType + " not supported in " + FFileName);
            }

            return result;
        }

        public bool RemoveTagFromFile(int tagType)
        {
            bool result = false;

            try
            {
                using (FileStream fs = new FileStream(FFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize, fileOptions))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    result = Read(reader, null);

                    long tagOffset = -1;
                    int tagSize = 0;

                    if (tagType.Equals(MetaDataIOFactory.TAG_ID3V1) && (hasMeta(MetaDataIOFactory.TAG_ID3V1)))
                    {
                        tagOffset = ID3v1.Offset;
                        tagSize = ID3v1.Size;
                    }
                    else if (tagType.Equals(MetaDataIOFactory.TAG_ID3V2) && (hasMeta(MetaDataIOFactory.TAG_ID3V2)))
                    {
                        tagOffset = ID3v2.Offset;
                        tagSize = ID3v2.Size;
                    }
                    else if (tagType.Equals(MetaDataIOFactory.TAG_APE) && (hasMeta(MetaDataIOFactory.TAG_APE)))
                    {
                        tagOffset = APEtag.Offset;
                        tagSize = APEtag.Size;
                    }
                    else if (tagType.Equals(MetaDataIOFactory.TAG_NATIVE) && (hasMeta(MetaDataIOFactory.TAG_NATIVE)))
                    {
                        // TODO : handle native tags scattered amond various, not necessarily contiguous chunks (e.g. AIFF)
                        tagOffset = NativeTag.Offset;
                        tagSize = NativeTag.Size;
                    }

                    if ((tagOffset > -1) && (tagSize > 0))
                    {
                        StreamUtils.ShortenStream(fs, tagOffset+tagSize, (uint)tagSize);
                        using (BinaryWriter writer = new BinaryWriter(fs))
                        {
                            result = RewriteFileSizeInHeader(writer, fs.Length);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FFileName + ")");
                result = false;
            }

            return result;
        }

        public bool HasNativeMeta()
        {
            return IsMetaSupported(MetaDataIOFactory.TAG_NATIVE);
        }
    }
}
