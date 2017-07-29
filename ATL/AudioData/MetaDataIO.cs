using Commons;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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

        protected bool tagExists;
        protected Encoding tagEncoding; // TODO check if needs to be there after all...
        protected int tagVersion;
        protected long tagOffset;
        protected int tagSize;

        protected TagData tagData;

        private IDictionary<TagData.PictureInfo, int> picturePositions;

        protected IList<TagData.PictureInfo> pictureTokens;


        public static void SetID3v2ExtendedHeaderRestrictionsUsage(bool b) { useID3v2ExtendedHeaderRestrictions = b; }


        // ------ READ-ONLY "PHYSICAL" TAG INFO FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// True if tag has been found in media file
        /// </summary>
        public bool Exists
        {
            get { return this.tagExists; }
        }
        /// <summary>
        /// Tag version
        /// </summary>
        public int Version
        {
            get { return this.tagVersion; }
        }
        /// <summary>
        /// Total size of tag (in bytes)
        /// </summary>
        public int Size
        {
            get { return this.tagSize; }
        }
        /// <summary>
        /// Tag offset in media file
        /// </summary>
        public long Offset
        {
            get { return this.tagOffset; }
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
        /// <summary>
        /// Conductor
        /// </summary>
        public String Conductor
        {
            get { return Utils.ProtectValue(tagData.Conductor); }
            set { tagData.Conductor = value; }
        }


        // ------ NON-TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// Collection of fields that are not supported by ATL (i.e. not implemented by a getter/setter of MetaDataIO class; e.g. custom fields such as "MOOD")
        /// </summary>
        public IDictionary<string, string> AdditionalFields
        {
            get {
                IDictionary<string, string> result = new Dictionary<string, string>();

                foreach (TagData.MetaFieldInfo fieldInfo in tagData.AdditionalFields)
                {
                    if (fieldInfo.TagType.Equals(getImplementedTagType())) result.Add(fieldInfo.NativeFieldCode, fieldInfo.Value);
                }

                return result;
            }
        }

        /// <summary>
        /// Each positioned flag indicates the presence of an embedded picture
        /// </summary>
        public IList<TagData.PictureInfo> PictureTokens
        {
            get { return this.pictureTokens; }
        }

        protected void addPictureToken(TagData.PIC_TYPE picType)
        {
            pictureTokens.Add( new TagData.PictureInfo(null, picType) );
        }

        protected void addPictureToken(int tagType, byte nativePicCode)
        {
            pictureTokens.Add(new TagData.PictureInfo(null, tagType, nativePicCode) );
        }

        protected int takePicturePosition(TagData.PIC_TYPE picType)
        {
            return takePicturePosition(new TagData.PictureInfo(null, picType));
        }

        protected int takePicturePosition(int tagType, byte nativePicCode)
        {
            return takePicturePosition(new TagData.PictureInfo(null, tagType, nativePicCode));
        }

        protected int takePicturePosition(int tagType, string nativePicCode)
        {
            return takePicturePosition(new TagData.PictureInfo(null, tagType, nativePicCode));
        }

        protected int takePicturePosition(TagData.PictureInfo picInfo)
        {
            if (picturePositions.ContainsKey(picInfo))
            {
                picturePositions[picInfo] = picturePositions[picInfo] + 1;
            }
            else
            {
                picturePositions.Add(picInfo, 1);
            }
            return picturePositions[picInfo]; ;
        }

        public virtual void ResetData()
        {
            tagExists = false;
            tagVersion = 0;
            tagSize = 0;
            tagOffset = 0;

            tagData = new TagData();
            pictureTokens = new List<TagData.PictureInfo>();
            picturePositions = new Dictionary<TagData.PictureInfo, int>();
        }

        abstract protected int getDefaultTagOffset();

        abstract protected int getImplementedTagType();

        abstract public bool Read(BinaryReader Source, TagData.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames);


        abstract protected bool write(TagData tag, BinaryWriter w);

        public long Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            long newTagSize = -1;
            tagData.Pictures.Clear();

            // Read all the fields in the existing tag (including unsupported fields)
            this.Read(r, new TagData.PictureStreamHandlerDelegate(this.readPictureData), true);

            TagData dataToWrite;
            tagEncoding = Encoding.UTF8; // TODO make default UTF-8 encoding customizable

            if (!tagExists) // If tag not found (e.g. empty file)
            {
                dataToWrite = tag; // Write new tag information
            }
            else
            {
                dataToWrite = tagData;
                dataToWrite.IntegrateValues(tag); // Write existing information + new tag information
            }

            // Write new tag to a MemoryStream
            using (MemoryStream s = new MemoryStream(Size))
            using (BinaryWriter msw = new BinaryWriter(s, tagEncoding))
            {
                if (write(dataToWrite, msw))
                {
                    newTagSize = s.Length;

                    // -- Adjust tag slot to new size in file --
                    long audioDataOffset;
                    long tagOffset;

                    if (tagExists) // An existing tag has been reprocessed
                    {
                        tagOffset = this.tagOffset;
                        audioDataOffset = this.tagOffset + tagSize;
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
                    if (newTagSize > tagSize)
                    {
                        StreamUtils.LengthenStream(w.BaseStream, audioDataOffset, (uint)(newTagSize - tagSize));
                    }
                    else if (newTagSize < tagSize) // Need to reduce file size
                    {
                        StreamUtils.ShortenStream(w.BaseStream, audioDataOffset, (uint)(tagSize - newTagSize));
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

        private void readPictureData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            TagData.PictureInfo picInfo = new TagData.PictureInfo(imgFormat, picType, originalTag, picCode, position);
            picInfo.PictureData = StreamUtils.ReadBinaryStream(s);

            tagData.Pictures.Add(picInfo);
        }

    }
}
