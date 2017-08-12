using Commons;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData
{
    public abstract class MetaDataIO : IMetaDataIO
    {
        // General properties
        protected static bool ID3v2_useExtendedHeaderRestrictions = false;
        protected static bool ASF_keepNonWMFieldsWhenRemovingTag = false;

        // Default tag offset
        protected const int TO_EOF = 0;     // End Of File
        protected const int TO_BOF = 1;     // Beginning Of File
        protected const int TO_BUILTIN = 2; // Built-in location (e.g. MP4)


        public class ReadTagParams
        {
            public TagData.PictureStreamHandlerDelegate PictureStreamHandler = null;
            public bool ReadAllMetaFrames = false;

            public bool ReadTag = true;
            public bool PrepareForWriting = false;

            public ReadTagParams(TagData.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames)
            {
                PictureStreamHandler = pictureStreamHandler; ReadAllMetaFrames = readAllMetaFrames;
            }
        }


        protected bool tagExists;
        protected Encoding tagEncoding; // TODO check if needs to be there + check if a tag-wide encoding does have any sense 
        protected int tagVersion;

        protected TagData tagData;

        private IDictionary<TagData.PictureInfo, int> picturePositions;
        protected IList<TagData.PictureInfo> pictureTokens;

        protected FileStructureHelper structureHelper;


        public static void SetID3v2ExtendedHeaderRestrictionsUsage(bool b) { ID3v2_useExtendedHeaderRestrictions = b; }
        public static void SetASFKeepNonWMFieldWhenRemoving(bool b) { ASF_keepNonWMFieldsWhenRemovingTag = b; }

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
            get {
                int result = 0;

                foreach (Zone zone in Zones) result += zone.Size;

                return result;
            }
        }
        public ICollection<Zone> Zones
        {
            get
            {
                return structureHelper.Zones;
            }
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
        /// NB : when querying multi-stream files (e.g. MP4, ASF), this attribute will only return stream-independent properties of the whole file, in the first language available
        /// For detailed, stream-by-stream and language-by-language properties, use GetAdditionalFields
        /// </summary>
        public IDictionary<string, string> AdditionalFields
        {
            get {
                IDictionary<string, string> result = new Dictionary<string, string>();

                IList<TagData.MetaFieldInfo> additionalFields = GetAdditionalFields(0);
                foreach (TagData.MetaFieldInfo fieldInfo in additionalFields)
                {
                    if (!result.ContainsKey(fieldInfo.NativeFieldCode)) result.Add(fieldInfo.NativeFieldCode, fieldInfo.Value);
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

        public virtual byte FieldCodeFixedLength
        {
            get { return 0; }
        }

        protected virtual bool IsLittleEndian
        {
            get { return true; }
        }


        // ------ PICTURE HELPER METHODS -----------------------------------------------------

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

        // ------ ABSTRACT METHODS -----------------------------------------------------

        abstract protected int getDefaultTagOffset();

        abstract protected int getImplementedTagType();

        abstract public bool Read(BinaryReader Source, ReadTagParams readTagParams);

        abstract protected bool write(TagData tag, BinaryWriter w, string zone);

        abstract protected void resetSpecificData();

        // ------ COMMON METHODS -----------------------------------------------------

        public void ResetData()
        {
            tagExists = false;
            tagVersion = 0;

            tagData = new TagData();
            pictureTokens = new List<TagData.PictureInfo>();
            picturePositions = new Dictionary<TagData.PictureInfo, int>();
            structureHelper = new FileStructureHelper(IsLittleEndian);

            resetSpecificData();
        }

        public IList<TagData.MetaFieldInfo> GetAdditionalFields(int streamNumber = -1, string language = "")
        {
            IList<TagData.MetaFieldInfo> result = new List<TagData.MetaFieldInfo>();

            foreach (TagData.MetaFieldInfo fieldInfo in tagData.AdditionalFields)
            {
                if (
                    getImplementedTagType().Equals(fieldInfo.TagType)
                    && (-1 == streamNumber) || (streamNumber == fieldInfo.StreamNumber)
                    && ("".Equals(language) || language.Equals(fieldInfo.Language))
                    )
                {
                    result.Add(fieldInfo);
                }
            }

            return result;
        }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            long oldTagSize;
            long newTagSize;
            long cumulativeDelta = 0;
            bool result = true;

            // Contraint-check on non-supported values
            if (FieldCodeFixedLength > 0)
            {
                foreach (TagData.PictureInfo picInfo in tag.Pictures)
                {
                    if (TagData.PIC_TYPE.Unsupported.Equals(picInfo.PicType) && (picInfo.TagType.Equals(getImplementedTagType())))
                    {
                        if (Utils.ProtectValue(picInfo.NativePicCodeStr).Length != FieldCodeFixedLength)
                        {
                            throw new NotSupportedException("Field code fixed length is " + FieldCodeFixedLength + "; detected field '" + Utils.ProtectValue(picInfo.NativePicCodeStr) + "' is " + Utils.ProtectValue(picInfo.NativePicCodeStr).Length + " characters long and cannot be written");
                        }
                    }
                }
                foreach (TagData.MetaFieldInfo fieldInfo in tag.AdditionalFields)
                {
                    if (fieldInfo.TagType.Equals(getImplementedTagType()))
                    {
                        if (Utils.ProtectValue(fieldInfo.NativeFieldCode).Length != FieldCodeFixedLength)
                        {
                            throw new NotSupportedException("Field code fixed length is " + FieldCodeFixedLength + "; detected field '" + Utils.ProtectValue(fieldInfo.NativeFieldCode) + "' is " + Utils.ProtectValue(fieldInfo.NativeFieldCode).Length + " characters long and cannot be written");
                        }
                    }
                }
            }

            structureHelper.Clear();
            tagData.Pictures.Clear();

            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true);
            readTagParams.PrepareForWriting = true;
            this.Read(r, readTagParams);

            if (!tagExists && 0 == Zones.Count)
            {
                structureHelper.AddZone(0, 0);
            }

            foreach (Zone zone in Zones)
            {
                oldTagSize = zone.Size;

                TagData dataToWrite;
                tagEncoding = Encoding.UTF8; // TODO make default UTF-8 encoding customizable

                dataToWrite = tagData;
                dataToWrite.IntegrateValues(tag); // Write existing information + new tag information

                // Write new tag to a MemoryStream
                using (MemoryStream s = new MemoryStream(zone.Size))
                using (BinaryWriter msw = new BinaryWriter(s, tagEncoding))
                {
                    if (write(dataToWrite, msw, zone.Name))
                    {
                        newTagSize = s.Length;
                    } else {
                        newTagSize = zone.CoreSignature.Length;
                    }

                    // -- Adjust tag slot to new size in file --
                    long tagEndOffset;
                    long tagBeginOffset;

                    if (tagExists && zone.Size > zone.CoreSignature.Length) // An existing tag has been reprocessed
                    {
                        tagBeginOffset = zone.Offset + cumulativeDelta;
                        tagEndOffset = tagBeginOffset + zone.Size;
                    }
                    else // A brand new tag has been added to the file
                    {
                        switch (getDefaultTagOffset())
                        {
                            case TO_EOF: tagBeginOffset = r.BaseStream.Length; break;
                            case TO_BOF: tagBeginOffset = 0; break;
                            case TO_BUILTIN: tagBeginOffset = zone.Offset + cumulativeDelta; break;
                            default: tagBeginOffset = -1; break;
                        }
                        tagEndOffset = tagBeginOffset + zone.Size;
                    }

                    // Need to build a larger file
                    if (newTagSize > zone.Size)
                    {
                        StreamUtils.LengthenStream(w.BaseStream, tagEndOffset, (uint)(newTagSize - zone.Size));
                    }
                    else if (newTagSize < zone.Size) // Need to reduce file size
                    {
                        StreamUtils.ShortenStream(w.BaseStream, tagEndOffset, (uint)(zone.Size - newTagSize));
                    }

                    // Copy tag contents to the new slot
                    r.BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                    s.Seek(0, SeekOrigin.Begin);

                    if (newTagSize > zone.CoreSignature.Length)
                    {
                        StreamUtils.CopyStream(s, w.BaseStream, s.Length);
                    } else
                    {
                        if (zone.CoreSignature.Length > 0) msw.Write(zone.CoreSignature);
                    }

                    tagData = dataToWrite;

                    int delta = (int)(newTagSize - oldTagSize);
                    cumulativeDelta += delta;

                    // Edit wrapping size markers and frame counters if needed
                    if (delta != 0 && MetaDataIOFactory.TAG_NATIVE == getImplementedTagType())
                    {
                        int action;

                        if (oldTagSize == zone.CoreSignature.Length && delta > 0) action = ACTION_ADD;
                        else if (newTagSize == zone.CoreSignature.Length && delta < 0) action = ACTION_DELETE;
                        else action = ACTION_EDIT;

                        result = structureHelper.RewriteMarkers(ref w, delta, action, zone.Name);
                    }
                }
            }

            return result;
        }

        public virtual bool Remove(BinaryWriter w)
        {
            bool result = true;
            long cumulativeDelta = 0;

            foreach (Zone zone in Zones)
            {
                if (zone.Offset > -1 && zone.Size > zone.CoreSignature.Length)
                {
                    StreamUtils.ShortenStream(w.BaseStream, zone.Offset + zone.Size - cumulativeDelta, (uint)(zone.Size - zone.CoreSignature.Length));

                    if (zone.CoreSignature.Length > 0)
                    {
                        w.BaseStream.Position = zone.Offset - cumulativeDelta;
                        w.Write(zone.CoreSignature);
                    }
                    if (MetaDataIOFactory.TAG_NATIVE == getImplementedTagType()) result = result && structureHelper.RewriteMarkers(ref w, -zone.Size + zone.CoreSignature.Length, FileStructureHelper.ACTION_DELETE, zone.Name);

                    cumulativeDelta += zone.Size - zone.CoreSignature.Length;
                }
            }

            return result;
        }

        private void readPictureData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            TagData.PictureInfo picInfo = new TagData.PictureInfo(imgFormat, picType, originalTag, picCode, position);
            picInfo.PictureData = StreamUtils.ReadBinaryStream(s);

            tagData.Pictures.Add(picInfo);
        }
    }
}
