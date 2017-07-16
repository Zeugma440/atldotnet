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

        protected TagData tagData;

        protected IList<MetaDataIOFactory.PIC_TYPE> FPictureTokens;

        protected IDictionary<string, string> otherTagFields;
        protected IDictionary<int, Image> unsupportedPictures;


        public static void SetID3v2ExtendedHeaderRestrictionsUsage(bool b) { useID3v2ExtendedHeaderRestrictions = b; }


        // ------ READ-ONLY "PHYSICAL" TAG INFO FIELDS ACCESSORS -----------------------------------------------------

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


        // ------ TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// Song/piece title
        /// </summary>
        public String Title
        {
            get { return Utils.ProtectValue(tagData.Title); }
            set { tagData.Title = value; }
        }
        /// <summary>
        /// Artist (Performer)
        /// </summary>
        public String Artist
        {
            get
            {
                string result = Utils.ProtectValue(tagData.Artist);
                if (0 == result.Length) result = AlbumArtist;
                return result;
            }
            set { tagData.Artist = value; }
        }
        /// <summary>
        /// Album Artist
        /// </summary>
        public String AlbumArtist
        {
            get { return Utils.ProtectValue(tagData.AlbumArtist); }
            set { tagData.AlbumArtist = value; }
        }
        /// <summary>
        /// Composer
        /// </summary>
        public String Composer
        {
            get { return Utils.ProtectValue(tagData.Composer); }
            set { tagData.Composer = value; }
        }
        /// <summary>
        /// Album title
        /// </summary>
        public String Album
        {
            get { return Utils.ProtectValue(tagData.Album); }
            set { tagData.Album = value; }
        }
        /// <summary>
        /// Track number
        /// </summary>
        public ushort Track
        {
            get { return TrackUtils.ExtractTrackNumber(tagData.TrackNumber); }
            set { tagData.TrackNumber = value.ToString(); }
        }
        /// <summary>
        /// Disc number
        /// </summary>
        public ushort Disc
        {
            get { return TrackUtils.ExtractTrackNumber(tagData.DiscNumber); }
            set { tagData.DiscNumber = value.ToString(); }
        }
        /// <summary>
        /// Rating, from 0 to 5
        /// </summary>
        public ushort Rating
        {
            get { return TrackUtils.ExtractIntRating(tagData.Rating); }
            set { tagData.Rating = value.ToString(); }
        }
        /// <summary>
        /// Release year
        /// </summary>
        public String Year
        {
            get
            {
                String result;
                result = TrackUtils.ExtractStrYear(tagData.RecordingYear);
                if (0 == result.Length) result = TrackUtils.ExtractStrYear(tagData.RecordingDate);
                return result;
            }
            set { tagData.RecordingYear = value; }
        }
        /// <summary>
        /// Release date (DateTime.MinValue if field does not exist)
        /// </summary>
        public DateTime Date
        {
            get // TODO - TEST EXTENSIVELY
            {
                DateTime result;
                if (!DateTime.TryParse(tagData.RecordingDate, out result)) // First try with a proper Recording date field
                {
                    bool success = false;
                    string dayMonth = Utils.ProtectValue(tagData.RecordingDayMonth); // If not, try to assemble year and dateMonth (e.g. ID3v2)
                    if (4 == dayMonth.Length && 4 == Year.Length)
                    {
                        success = DateTime.TryParse(dayMonth.Substring(0, 2) + "/" + dayMonth.Substring(3, 2) + "/" + Year, out result);
                    }
                }
                return result;
            }
            set { tagData.RecordingDate = value.ToShortDateString(); }
        }
        /// <summary>
        /// Genre name
        /// </summary>
        public String Genre
        {
            get { return Utils.ProtectValue(tagData.Genre); }
            set { tagData.Genre = value; }
        }
        /// <summary>
        /// Commment
        /// </summary>
        public String Comment
        {
            get { return Utils.ProtectValue(tagData.Comment); }
            set { tagData.Comment = value; }
        }
        /// <summary>
        /// Copyright
        /// </summary>
        public String Copyright
        {
            get { return Utils.ProtectValue(tagData.Copyright); }
            set { tagData.Copyright = value; }
        }
        /// <summary>
        /// Original artist
        /// </summary>
        public String OriginalArtist
        {
            get { return Utils.ProtectValue(tagData.OriginalArtist); }
            set { tagData.OriginalArtist = value; }
        }
        /// <summary>
        /// Original album
        /// </summary>
        public String OriginalAlbum
        {
            get { return Utils.ProtectValue(tagData.OriginalAlbum); }
            set { tagData.OriginalAlbum = value; }
        }
        /// <summary>
        /// General Description
        /// </summary>
        public String GeneralDescription
        {
            get { return Utils.ProtectValue(tagData.GeneralDescription); }
            set { tagData.GeneralDescription = value; }
        }
        /// <summary>
        /// Publisher
        /// </summary>
        public String Publisher
        {
            get { return Utils.ProtectValue(tagData.Publisher); }
            set { tagData.Publisher = value; }
        }



        // ------ NON-TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// Collection of fields that are not supported by ATL (i.e. not implemented by a getter/setter; e.g. custom fields such as "MOOD")
        /// </summary>
        public IDictionary<string, string> OtherFields
        {
            get { return otherTagFields!=null? otherTagFields : new Dictionary<string,String>(); }
        }

        /// <summary>
        /// Each positioned flag indicates the presence of an embedded picture
        /// </summary>
        public IList<MetaDataIOFactory.PIC_TYPE> PictureTokens
        {
            get { return this.FPictureTokens; }
        }

/*
        public IList<IDictionary<MetaDataIOFactory.PIC_TYPE,Image>> EmbeddedPictures
        {
            get
            {
                Read()
            }
        }
*/

        // TODO 
        //   getPictures
        //   access to unsupported pictures
        //   access to unsupported fields
        //   review storage of unsupported data

        public virtual void ResetData()
        {
            FExists = false;
            FVersion = 0;
            FSize = 0;
            FOffset = 0;

            tagData = new TagData();
            FPictureTokens = new List<MetaDataIOFactory.PIC_TYPE>();
            otherTagFields = new Dictionary<string, string>();
            unsupportedPictures = new Dictionary<int, Image>();
        }

        abstract public bool Read(BinaryReader Source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler, bool storeOtherMetaFields);

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
            Read(r, null, true);

            TagData dataToWrite;
            if (!FExists) // If tag not found (e.g. empty file)
            {
                FEncoding = Encoding.UTF8; // TODO make default UTF-8 encoding customizable
                dataToWrite = tag; // Write new tag information
            }
            else
            {
                dataToWrite = tagData;
                dataToWrite.IntegrateValues(tag); // Write existing information + new tag information
            }

            // Write new tag to a MemoryStream
            using (MemoryStream s = new MemoryStream(Size))
            using (BinaryWriter msw = new BinaryWriter(s, FEncoding))
            {
                if (Write(dataToWrite, msw))
                {
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
                    }
                    else if (newTagSize < FSize) // Need to reduce file size
                    {
                        StreamUtils.ShortenStream(w.BaseStream, audioDataOffset, (uint)(FSize - newTagSize));
                    }

                    // Copy tag contents to the new slot
                    r.BaseStream.Seek(tagOffset, SeekOrigin.Begin);
                    s.Seek(0, SeekOrigin.Begin);
                    StreamUtils.CopyStream(s, w.BaseStream, s.Length);

                    tagData = dataToWrite;
                }
            }

            return newTagSize;
        }
    }
}
