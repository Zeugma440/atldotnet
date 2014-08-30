using ATL.AudioReaders.BinaryLogic;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders
{
    abstract class AudioDataReader : IAudioDataReader
    {
        // Audio data
        protected double FBitrate;
        protected double FDuration;

        // File data
        protected long FFileSize;
        protected String FFileName;
        protected bool FValid;

        protected TID3v1 FID3v1 = new TID3v1();
        protected TID3v2 FID3v2 = new TID3v2();
        protected TAPEtag FAPEtag = new TAPEtag();



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

        public TID3v1 ID3v1 // ID3v1 tag data
        {
            get { return this.FID3v1; }
        }
        public TID3v2 ID3v2 // ID3v2 tag data
        {
            get { return this.FID3v2; }
        }
        public TAPEtag APEtag // APE tag data
        {
            get { return this.FAPEtag; }
        }

        // ====================== METHODS =========================

        abstract protected void resetSpecificData();

        protected void resetData()
        {
            FFileName = "";
            FFileSize = 0;
            FValid = false;

            FBitrate = 0;
            FDuration = 0;

            if (FID3v1 != null) FID3v1.ResetData();
            if (FID3v2 != null) FID3v2.ResetData();
            if (FAPEtag != null) FAPEtag.ResetData();

            resetSpecificData();
        }


        abstract public bool Read(BinaryReader Source, StreamUtils.StreamHandlerDelegate pictureStreamHandler);

        public bool ReadFromFile(String FileName, StreamUtils.StreamHandlerDelegate pictureStreamHandler = null)
        {
            bool result = false;
            resetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
                using (BinaryReader SourceFile = new BinaryReader(fs))
                {
                    FFileSize = fs.Length;
                    FFileName = FileName;

                    result = Read(SourceFile, pictureStreamHandler);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FileName + ")");
                result = false;
            }

            return result;
        }
    }
}
