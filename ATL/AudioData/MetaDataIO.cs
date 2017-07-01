using Commons;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    public abstract class MetaDataIO : IOBase, IMetaDataIO
    {
        // General properties
        protected static bool useID3v2ExtendedHeaderRestrictions = false;

        // Default tag offset
        protected const int TO_EOF = 0; // End Of File
        protected const int TO_BOF = 1; // Beginning Of File

        protected bool FExists;
        protected Encoding FEncoding; // TODO check if needs to be there after all...
        protected int FVersion;
        protected long FOffset;
        protected int FSize;

        // TODO ultimately replace all these fields by TagData ?
        protected String FGeneralDesc;
        protected String FTitle;
        protected String FArtist;
        protected String FOriginalArtist;
        protected String FComposer;
        protected String FAlbum;
        protected String FOriginalAlbum;
        protected ushort FTrack;
        protected ushort FDisc;
        protected ushort FRating;
        protected String FRatingStr;
        protected String FReleaseYear;
        protected DateTime FReleaseDate;
        protected String FGenre;
        protected String FComment;
        protected String FCopyright;
        protected IList<MetaDataIOFactory.PIC_TYPE> FPictures;

        protected IDictionary<String, String> unsupportedTagFields;
        protected IDictionary<int, Image> unsupportedPictures;


        public static void SetID3v2ExtendedHeaderRestrictionsUsage(bool b) { useID3v2ExtendedHeaderRestrictions = b; }

        /// <summary>
        /// True if tag has been found in media file
        /// </summary>
        public bool Exists
        {
            get { return this.FExists; }
        }
        /// <summary>
        /// Tag version
        /// </summary>
        public int Version
        {
            get { return this.FVersion; }
        }
        /// <summary>
        /// Total size of tag (in bytes)
        /// </summary>
        public int Size
        {
            get { return this.FSize; }
        }
        /// <summary>
        /// Tag offset in media file
        /// </summary>
        public long Offset
        {
            get { return this.FOffset; }
        }
        /// <summary>
        /// Song/piece title
        /// </summary>
        public String Title
        {
            get { return this.FTitle; }
            set { FTitle = value; }
        }
        /// <summary>
        /// Artist (Performer)
        /// </summary>
        public String Artist
        {
            get { return this.FArtist; }
            set { FArtist = value; }
        }
        /// <summary>
        /// Composer
        /// </summary>
        public String Composer
        {
            get { return this.FComposer; }
            set { FComposer = value; }
        }
        /// <summary>
        /// Album title
        /// </summary>
        public String Album
        {
            get { return this.FAlbum; }
            set { FAlbum = value; }
        }
        /// <summary>
        /// Track number
        /// </summary>
        public ushort Track
        {
            get { return this.FTrack; }
            set { FTrack = value; }
        }
        /// <summary>
        /// Disc number
        /// </summary>
        public ushort Disc
        {
            get { return (ushort)this.FDisc; }
            set { FDisc = ((byte)value); }
        }
        /// <summary>
        /// Rating, from 0 to 5
        /// </summary>
        public ushort Rating
        {
            get { return this.FRating; }
            set { FRating = value; }
        }
        /// <summary>
        /// Release year
        /// </summary>
        public String Year
        {
            get { return this.FReleaseYear; }
            set { FReleaseYear = value; }
        }
        /// <summary>
        /// Genre name
        /// </summary>
        public String Genre
        {
            get { return this.FGenre; }
            set { FGenre = value; }
        }
        /// <summary>
        /// Commment
        /// </summary>
        public String Comment
        {
            get { return this.FComment; }
            set { FComment = value; }
        }
        /// <summary>
        /// Copyright
        /// </summary>
        public String Copyright
        {
            get { return this.FCopyright; }
            set { FCopyright = value; }
        }
        /// <summary>
        /// Each positioned flag indicates the presence of an embedded picture
        /// </summary>
        public IList<MetaDataIOFactory.PIC_TYPE> Pictures
        {
            get { return this.FPictures; }
        }

        public virtual void ResetData()
        {
            FExists = false;
            FVersion = 0;
            FSize = 0;
            FOffset = 0;

            FGeneralDesc = "";
            FTitle = "";
            FArtist = "";
            FOriginalArtist = "";
            FComposer = "";
            FAlbum = "";
            FOriginalAlbum = "";
            FTrack = 0;
            FDisc = 0;
            FRating = 0;
            FReleaseYear = "";
            FReleaseDate = new DateTime();
            FGenre = "";
            FComment = "";
            FCopyright = "";
            FPictures = new List<MetaDataIOFactory.PIC_TYPE>();
        }

        protected void fromTagData(TagData info)
        {
            FAlbum = Utils.ProtectValue(info.Album);
            FOriginalAlbum = Utils.ProtectValue(info.OriginalAlbum);
            if (0 == FAlbum.Length) FAlbum = FOriginalAlbum;
            FArtist = Utils.ProtectValue(info.Artist);
            FOriginalArtist = Utils.ProtectValue(info.OriginalArtist);
            if (0 == FArtist.Length) FArtist = FOriginalArtist;
            FComment = info.Comment;
            FComposer = info.Composer;
            FReleaseYear = TrackUtils.ExtractStrYear(info.ReleaseYear);
            DateTime.TryParse(info.ReleaseDate, out FReleaseDate); // TODO - TEST EXTENSIVELY
            if (0 == FReleaseYear.Length) FReleaseYear = TrackUtils.ExtractStrYear(info.ReleaseDate);
            FDisc = TrackUtils.ExtractTrackNumber(info.DiscNumber);
            FTrack = TrackUtils.ExtractTrackNumber(info.TrackNumber);
            FGenre = info.Genre;
            FRating = TrackUtils.ExtractIntRating(info.Rating);
            FTitle = Utils.ProtectValue(info.Title);
            FGeneralDesc = Utils.ProtectValue(info.GeneralDescription);
            FCopyright = Utils.ProtectValue(info.Copyright);
            if (0 == FTitle.Length) FTitle = FGeneralDesc;
        }

        abstract public bool Read(BinaryReader Source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler, bool storeUnsupportedMetaFields);

        public bool Read(BinaryReader Source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler)
        {
            return Read(Source, pictureStreamHandler, false);
        }

        abstract public bool Write(TagData tag, BinaryWriter w);

        abstract protected int getDefaultTagOffset();

        public long Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            long newTagSize = -1;

            // Read all the fields in the existing tag (including unsupported fields)
            Read(r, null, true); // TODO use output wisely

            // TODO make default UTF-8 encoding customizable
            if (!FExists) // If tag not found (e.g. empty file)
            {
                FEncoding = Encoding.UTF8;
                unsupportedTagFields = new Dictionary<string, string>();
            }

            // Write new tag to a MemoryStream
            using (MemoryStream s = new MemoryStream(Size))
            using (BinaryWriter msw = new BinaryWriter(s, FEncoding))
            {
                Write(tag, msw);  // TODO get a better use of the return value

                newTagSize = s.Length;

                // -- Adjust tag slot to new size in file --
                long audioDataOffset;
                long tagOffset;

                if (FExists) // An existing tag has been reprocessed
                {
                    tagOffset = FOffset;
                    audioDataOffset = FOffset + FSize;
                }
                else // A brand new tag has been added to the file
                {
                    switch (getDefaultTagOffset())
                    {
                        case TO_EOF: tagOffset = r.BaseStream.Length; break;
                        case TO_BOF: tagOffset = 0; break;
                        default: tagOffset = -1; break;
                    }
                    audioDataOffset = tagOffset;
                }

                // Need to build a larger file
                if (newTagSize > FSize)
                {
                    StreamUtils.LengthenStream(w.BaseStream, audioDataOffset, (uint)(newTagSize - FSize));
                } else if (newTagSize < FSize) // Need to reduce file size
                {
                    StreamUtils.ShortenStream(w.BaseStream, audioDataOffset, (uint)(FSize - newTagSize));
                }

                // Copy tag contents to the new slot
                r.BaseStream.Seek(tagOffset, SeekOrigin.Begin);
                s.Seek(0, SeekOrigin.Begin);
                StreamUtils.CopyStream(s, w.BaseStream, s.Length);
            }

            fromTagData(tag);

            return newTagSize;
        }
    }
}
