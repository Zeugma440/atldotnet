using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders
{
    public abstract class MetaDataReader : IMetaDataReader
    {
        protected bool FExists;
        protected int FVersion;
        protected long FSize;
        protected String FTitle;
        protected String FArtist;
        protected String FComposer;
        protected String FAlbum;
        protected ushort FTrack;
        protected ushort FDisc;
        protected ushort FRating;
        protected String FRatingStr;
        protected String FYear;
        protected String FGenre;
        protected String FComment;
        protected String FCopyright;
        protected IList<MetaReaderFactory.PIC_CODE> FPictures;


        public bool Exists // True if tag found
        {
            get { return this.FExists; }
        }
        public int Version // Tag version
        {
            get { return this.FVersion; }
        }
        public long Size // Total tag size
        {
            get { return this.FSize; }
        }
        public String Title // Song title
        {
            get { return this.FTitle; }
            set { FTitle = value; }
        }
        public String Artist // Artist name
        {
            get { return this.FArtist; }
            set { FArtist = value; }
        }
        public String Composer // Composer name
        {
            get { return this.FComposer; }
            set { FComposer = value; }
        }
        public String Album // Album title
        {
            get { return this.FAlbum; }
            set { FAlbum = value; }
        }
        public ushort Track // Track number
        {
            get { return this.FTrack; }
            set { FTrack = value; }
        }
        public ushort Disc // Disc number
        {
            get { return (ushort)this.FDisc; }
            set { FDisc = ((byte)value); }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
            set { FRating = value; }
        }
        public String Year // Release year
        {
            get { return this.FYear; }
            set { FYear = value; }
        }
        public String Genre // Genre name
        {
            get { return this.FGenre; }
            set { FGenre = value; }
        }
        public String Comment // Comment
        {
            get { return this.FComment; }
            set { FComment = value; }
        }
        public String Copyright // (c)
        {
            get { return this.FCopyright; }
            set { FCopyright = value; }
        }
        public IList<MetaReaderFactory.PIC_CODE> Pictures // (Embedded pictures flags)
        {
            get { return this.FPictures; }
        }

        public virtual void ResetData()
        {
            FExists = false;
            FVersion = 0;
            FSize = 0;
            FTitle = "";
            FArtist = "";
            FComposer = "";
            FAlbum = "";
            FTrack = 0;
            FDisc = 0;
            FRating = 0;
            FYear = "";
            FGenre = "";
            FComment = "";
            FCopyright = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
        }

        abstract public bool Read(BinaryReader Source, StreamUtils.StreamHandlerDelegate pictureStreamHandler);

        public bool ReadFromFile(String FileName, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            bool result = false;
            ResetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
                using (BinaryReader source = new BinaryReader(fs))
                {
                    result = Read(source, pictureStreamHandler);
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
