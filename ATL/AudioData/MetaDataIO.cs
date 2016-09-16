using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    public abstract class MetaDataIO : IMetaDataIO
    {
        // Default tag offset
        protected const int TO_EOF = 0; // End Of File
        protected const int TO_BOF = 1; // Beginning Of File

        protected bool FExists;
        protected Encoding FEncoding; // TODO check if needs to be there after all...
        protected int FVersion;
        protected long FOffset;
        protected int FSize;

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
        protected IList<MetaDataIOFactory.PIC_CODE> FPictures;

        protected IDictionary<String, String> unsupportedTagFields;


        public bool Exists // True if tag found
        {
            get { return this.FExists; }
        }
        public int Version // Tag version
        {
            get { return this.FVersion; }
        }
        public int Size // Total tag size
        {
            get { return this.FSize; }
        }
        public long Offset // Tag offset
        {
            get { return this.FOffset; }
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
        public IList<MetaDataIOFactory.PIC_CODE> Pictures // (Embedded pictures flags)
        {
            get { return this.FPictures; }
        }

        public virtual void ResetData()
        {
            FExists = false;
            FVersion = 0;
            FSize = 0;
            FOffset = 0;

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
            FPictures = new List<MetaDataIOFactory.PIC_CODE>();
        }

        abstract public bool Read(BinaryReader Source, StreamUtils.StreamHandlerDelegate pictureStreamHandler, bool storeUnsupportedMetaFields);

        public bool Read(BinaryReader Source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            return Read(Source, pictureStreamHandler, false);
        }

        abstract public bool Write(TagData tag, BinaryWriter w);

        abstract protected int getDefaultTagOffset();


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

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            bool result = false;
            long newTagSize = -1;

            // Read all the fields in the existing tag
            result = Read(r, null, true);

            // Write new tag to a MemoryStream
            using (MemoryStream s = new MemoryStream(Size))
            using (BinaryWriter msw = new BinaryWriter(s, FEncoding))
            {
                Write(tag, msw);
                newTagSize = s.Length;


                long audioDataOffset;
                long tagOffset;

                // Adjust tag slot to new size in file
                if (Exists)
                {
                    tagOffset = FOffset;
                    audioDataOffset = FOffset + FSize;
                } else
                {
                    switch (getDefaultTagOffset())
                    {
                        case TO_EOF: tagOffset = r.BaseStream.Length; break;
                        case TO_BOF: tagOffset = 0; break;
                        default: tagOffset = -1; break;
                    }
                    audioDataOffset = tagOffset;
                }

                // Needs to build a larger file
                if (newTagSize > FSize)
                {
                    StreamUtils.LengthenStream(w.BaseStream, audioDataOffset, (uint)(newTagSize - FSize));
                } else if (newTagSize < FSize) // Need to reduce file size
                {
                    StreamUtils.ShortenStream(w.BaseStream, audioDataOffset, (uint)(FSize - newTagSize));
                }

                // Copy memoryStream contents to the new slot
                w.BaseStream.Seek(tagOffset, SeekOrigin.Begin);
                s.Seek(0, SeekOrigin.Begin);
                StreamUtils.CopyStreamFrom(w.BaseStream, s, s.Length);
            }

            return true;
        }
    }
}
