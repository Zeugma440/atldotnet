using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Superclass that "consolidates" all metadata I/O algorithms to ease development of new classes and minimize their code
    /// </summary>
    public abstract class MetaDataIO : IMetaDataIO
    {
        // ------ CONSTS -----------------------------------------------------

        // Default tag offset
        public const int TO_EOF = 0;     // Tag offset is at End Of File
        public const int TO_BOF = 1;     // Tag offset is at Beginning Of File
        public const int TO_BUILTIN = 2; // Tag offset is at a Built-in location (e.g. MP4)

        // Rating conventions
        public const int RC_ID3v2 = 0;       // ID3v2 convention (0..255 scale with various tweaks)
        public const int RC_ASF = 1;         // ASF convention (0..100 scale with 1 being encoded as 1)
        public const int RC_APE = 2;         // APE convention (proper 0..100 scale)


        // ------ INNER CLASSES -----------------------------------------------------

        /// <summary>
        /// Container class describing tag reading parameters
        /// </summary>
        public class ReadTagParams
        {
            /// <summary>
            /// True : read metadata; False : do not read metadata (only "physical" audio data)
            /// </summary>
            public bool ReadTag = true;

            /// <summary>
            /// True : read all metadata frames; False : only read metadata frames that match IMetaDataIO public properties (="supported" metadata)
            /// </summary>
            public bool ReadAllMetaFrames = false;

            /// <summary>
            /// True : read embedded pictures; False : skip embedded pictures
            /// </summary>
            public bool ReadPictures = false;

            /// <summary>
            /// True : read all data that will be useful for writing; False : only read metadata values
            /// </summary>
            public bool PrepareForWriting = false;

            /// <summary>
            /// File offset to start reading metadata from (bytes)
            /// </summary>
            public long offset = 0;

            public ReadTagParams(bool readPictures, bool readAllMetaFrames)
            {
                ReadPictures = readPictures; ReadAllMetaFrames = readAllMetaFrames;
            }
        }


        // ------ PROPERTIES -----------------------------------------------------

        protected bool tagExists;
        protected int tagVersion;
        protected TagData tagData;
        protected IList<PictureInfo> pictureTokens;

        private IList<KeyValuePair<string, int>> picturePositions;

        internal FileStructureHelper structureHelper;

        protected IMetaDataEmbedder embedder;


        // ------ READ-ONLY "PHYSICAL" TAG INFO FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// True if tag has been found in media file
        /// </summary>
        public bool Exists
        {
            get { return this.tagExists; }
        }
        /// <inheritdoc/>
        public virtual IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0]);
                if (this is IAudioDataIO iO)
                {
                    nativeFormat.Name = nativeFormat.Name + " / " + iO.AudioFormat.ShortName;
                    nativeFormat.ID += iO.AudioFormat.ID;
                }
                return new List<Format>(new Format[1] { nativeFormat });
            }
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
        public long Size
        {
            get
            {
                long result = 0;

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
        }
        /// <summary>
        /// Album Artist
        /// </summary>
        public String AlbumArtist
        {
            get { return Utils.ProtectValue(tagData.AlbumArtist); }
        }
        /// <summary>
        /// Composer
        /// </summary>
        public String Composer
        {
            get { return Utils.ProtectValue(tagData.Composer); }
        }
        /// <summary>
        /// Album title
        /// </summary>
        public String Album
        {
            get { return Utils.ProtectValue(tagData.Album); }
        }
        /// <summary>
        /// Track number
        /// </summary>
        public ushort Track
        {
            get
            {
                if (tagData.TrackNumberTotal != null)
                    return TrackUtils.ExtractTrackNumber(tagData.TrackNumberTotal);
                else return TrackUtils.ExtractTrackNumber(tagData.TrackNumber);
            }
        }
        /// <summary>
        /// Total track number
        /// </summary>
        public ushort TrackTotal
        {
            get
            {
                if (tagData.TrackNumberTotal != null)
                    return TrackUtils.ExtractTrackTotal(tagData.TrackNumberTotal);
                else if (Utils.IsNumeric(tagData.TrackTotal))
                    return ushort.Parse(tagData.TrackTotal);
                else return TrackUtils.ExtractTrackTotal(tagData.TrackNumber);
            }
        }
        /// <summary>
        /// Disc number
        /// </summary>
        public ushort Disc
        {
            get
            {
                if (tagData.DiscNumberTotal != null)
                    return TrackUtils.ExtractTrackNumber(tagData.DiscNumberTotal);
                else return TrackUtils.ExtractTrackNumber(tagData.DiscNumber);
            }
        }
        /// <summary>
        /// Total disc number
        /// </summary>
        public ushort DiscTotal
        {
            get
            {
                if (tagData.DiscNumberTotal != null)
                    return TrackUtils.ExtractTrackTotal(tagData.DiscNumberTotal);
                else if (Utils.IsNumeric(tagData.DiscTotal))
                    return ushort.Parse(tagData.DiscTotal);
                else return TrackUtils.ExtractTrackTotal(tagData.DiscNumber);
            }
        }
        /// <summary>
        /// Rating, from 0 to 100%
        /// </summary>
        public float Popularity
        {
            get { return (float)TrackUtils.DecodePopularity(tagData.Rating, ratingConvention); }
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
        }
        /// <summary>
        /// Recording date (DateTime.MinValue if field does not exist)
        /// </summary>
        public DateTime Date
        {
            get
            {
                DateTime result;
                if (!DateTime.TryParse(tagData.RecordingDate, out result)) // First try with a proper Recording date field
                {
                    bool success = false;
                    string dayMonth = Utils.ProtectValue(tagData.RecordingDayMonth); // If not, try to assemble year and dateMonth (e.g. ID3v2)
                    if (4 == dayMonth.Length && 4 == Year.Length)
                    {
                        StringBuilder dateTimeBuilder = new StringBuilder();
                        dateTimeBuilder.Append(Year).Append("-");
                        dateTimeBuilder.Append(dayMonth.Substring(2, 2)).Append("-");
                        dateTimeBuilder.Append(dayMonth.Substring(0, 2));
                        string time = Utils.ProtectValue(tagData.RecordingTime); // Try to add time if available
                        if (time.Length >= 4)
                        {
                            dateTimeBuilder.Append("T");
                            dateTimeBuilder.Append(time.Substring(0, 2)).Append(":");
                            dateTimeBuilder.Append(time.Substring(2, 2)).Append(":");
                            dateTimeBuilder.Append((6 == time.Length) ? time.Substring(4, 2) : "00");
                        }
                        success = DateTime.TryParse(dateTimeBuilder.ToString(), out result);
                    }
                    if (!success) result = DateTime.MinValue;
                }
                return result;
            }
        }
        /// <summary>
        /// Publishing date (DateTime.MinValue if field does not exist)
        /// </summary>
        public DateTime PublishingDate
        {
            get
            {
                DateTime result;
                if (!DateTime.TryParse(tagData.PublishingDate, out result))
                    result = DateTime.MinValue;
                return result;
            }
        }
        /// <summary>
        /// Genre name
        /// </summary>
        public String Genre
        {
            get { return Utils.ProtectValue(tagData.Genre); }
        }
        /// <summary>
        /// Commment
        /// </summary>
        public String Comment
        {
            get { return Utils.ProtectValue(tagData.Comment); }
        }
        /// <summary>
        /// Copyright
        /// </summary>
        public String Copyright
        {
            get { return Utils.ProtectValue(tagData.Copyright); }
        }
        /// <summary>
        /// Original artist
        /// </summary>
        public String OriginalArtist
        {
            get { return Utils.ProtectValue(tagData.OriginalArtist); }
        }
        /// <summary>
        /// Original album
        /// </summary>
        public String OriginalAlbum
        {
            get { return Utils.ProtectValue(tagData.OriginalAlbum); }
        }
        /// <summary>
        /// General Description
        /// </summary>
        public String GeneralDescription
        {
            get { return Utils.ProtectValue(tagData.GeneralDescription); }
        }
        /// <summary>
        /// Publisher
        /// </summary>
        public String Publisher
        {
            get { return Utils.ProtectValue(tagData.Publisher); }
        }
        /// <summary>
        /// Conductor
        /// </summary>
        public String Conductor
        {
            get { return Utils.ProtectValue(tagData.Conductor); }
        }
        /// <summary>
        /// Size of padding zone, if any
        /// </summary>
        public long PaddingSize
        {
            get { return tagData.PaddingSize; }
        }


        /// <summary>
        /// Collection of fields that are not supported by ATL (i.e. not implemented by a getter/setter of MetaDataIO class; e.g. custom fields such as "MOOD")
        /// NB : when querying multi-stream files (e.g. MP4, ASF), this attribute will only return stream-independent properties of the whole file, in the first language available
        /// For detailed, stream-by-stream and language-by-language properties, use GetAdditionalFields
        /// </summary>
        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                IDictionary<string, string> result = new Dictionary<string, string>();

                IList<MetaFieldInfo> additionalFields = GetAdditionalFields(0);
                foreach (MetaFieldInfo fieldInfo in additionalFields)
                {
                    if (!result.ContainsKey(fieldInfo.NativeFieldCode)) result.Add(fieldInfo.NativeFieldCode, fieldInfo.Value);
                }

                return result;
            }
        }

        public IList<MetaFieldInfo> GetAdditionalFields(int streamNumber = -1, string language = "")
        {
            IList<MetaFieldInfo> result = new List<MetaFieldInfo>();

            foreach (MetaFieldInfo fieldInfo in tagData.AdditionalFields)
            {
                if (
                    getImplementedTagType().Equals(fieldInfo.TagType)
                    && (-1 == streamNumber) || (streamNumber == fieldInfo.StreamNumber)
                    && ((0 == language.Length) || language.Equals(fieldInfo.Language))
                    )
                {
                    result.Add(fieldInfo);
                }
            }

            return result;
        }

        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                IList<PictureInfo> result = new List<PictureInfo>();

                foreach (PictureInfo picInfo in tagData.Pictures)
                {
                    if (!picInfo.MarkedForDeletion && (!picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) || (picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && picInfo.TagType.Equals(getImplementedTagType())))) result.Add(picInfo);
                }

                return result;
            }
        }

        /// <summary>
        /// Each positioned flag indicates the presence of an embedded picture
        /// </summary>
        public IList<PictureInfo> PictureTokens
        {
            get { return this.pictureTokens; }
        }

        public IList<ChapterInfo> Chapters
        {
            get
            {
                IList<ChapterInfo> result = new List<ChapterInfo>();

                if (tagData.Chapters != null)
                {
                    foreach (ChapterInfo chapter in tagData.Chapters)
                    {
                        result.Add(new ChapterInfo(chapter));
                    }
                }

                return result;
            }
        }

        public LyricsInfo Lyrics
        {
            get
            {
                if (tagData.Lyrics != null)
                {
                    return new LyricsInfo(tagData.Lyrics);
                }
                else
                {
                    return new LyricsInfo();
                }
            }
        }

        public string ChaptersTableDescription
        {
            get { return Utils.ProtectValue(tagData.ChaptersTableDescription); }
        }

        // ------ NON-TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        public virtual byte FieldCodeFixedLength
        {
            get { return 0; }
        }

        protected virtual bool isLittleEndian
        {
            get { return true; }
        }

        protected virtual byte ratingConvention
        {
            get { return RC_ID3v2; }
        }

        // ------ PICTURE HELPER METHODS -----------------------------------------------------

        protected void addPictureToken(PictureInfo.PIC_TYPE picType)
        {
            pictureTokens.Add(new PictureInfo(picType));
        }

        protected void addPictureToken(int tagType, byte nativePicCode)
        {
            pictureTokens.Add(new PictureInfo(tagType, nativePicCode));
        }

        protected void addPictureToken(int tagType, string nativePicCode)
        {
            pictureTokens.Add(new PictureInfo(tagType, nativePicCode));
        }

        protected int takePicturePosition(PictureInfo.PIC_TYPE picType)
        {
            return takePicturePosition(new PictureInfo(picType));
        }

        protected int takePicturePosition(int tagType, byte nativePicCode)
        {
            return takePicturePosition(new PictureInfo(tagType, nativePicCode));
        }

        protected int takePicturePosition(int tagType, string nativePicCode)
        {
            return takePicturePosition(new PictureInfo(tagType, nativePicCode));
        }

        protected int takePicturePosition(PictureInfo picInfo)
        {
            string picId = picInfo.ToString();
            bool found = false;
            int picPosition = 1;

            for (int i = 0; i < picturePositions.Count; i++)
            {
                if (picturePositions[i].Key.Equals(picId))
                {
                    picPosition = picturePositions[i].Value + 1;
                    picturePositions[i] = new KeyValuePair<string, int>(picId, picPosition);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                picturePositions.Add(new KeyValuePair<string, int>(picId, 1));
            }

            return picPosition;
        }


        // ------ ABSTRACT METHODS -----------------------------------------------------

        /// <summary>
        /// Read metadata from the given source, using the given parameters
        /// </summary>
        /// <param name="source">Source to read metadata from</param>
        /// <param name="readTagParams">Read parameters</param>
        /// <returns>True if read has been successful, false if it failed</returns>
        abstract protected bool read(BinaryReader source, ReadTagParams readTagParams);

        /// <summary>
        /// Write the given zone's metadata using the given writer
        /// </summary>
        /// <param name="tag">Metadata to write</param>
        /// <param name="w">Writer to use</param>
        /// <param name="zone">Code of the zone to write</param>
        /// <returns>Number of written fields; 0 if no field has been added not edited</returns>
        abstract protected int write(TagData tag, BinaryWriter w, string zone);

        /// <summary>
        /// Return the default offset of the metadata block
        /// </summary>
        /// <returns></returns>
        abstract protected int getDefaultTagOffset();

        /// <summary>
        /// Return the implemented tag type (see <see cref="MetaDataIOFactory"/> constants)
        /// TODO make it return a <see cref="MetaDataIOFactory.TagType"/>
        /// </summary>
        /// <returns></returns>
        abstract protected int getImplementedTagType();

        /// <summary>
        /// Get the frame code (per <see cref="TagData"/> standards for the given field ID in the given zone and the given tag version
        /// </summary>
        /// <param name="zone">Code of the zone of the given field</param>
        /// <param name="ID">ID of the field to get the mapping for</param>
        /// <param name="tagVersion">Version the tagging system (e.g. 3 for ID3v2.3)</param>
        /// <returns></returns>
        abstract protected byte getFrameMapping(string zone, string ID, byte tagVersion);


        // ------ COMMON METHODS -----------------------------------------------------

        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            this.embedder = embedder;
        }

        protected void ResetData()
        {
            tagExists = false;
            tagVersion = 0;

            if (null == tagData) tagData = new TagData(); else tagData.Clear();
            if (null == pictureTokens) pictureTokens = new List<PictureInfo>(); else pictureTokens.Clear();
            if (null == picturePositions) picturePositions = new List<KeyValuePair<string, int>>(); else picturePositions.Clear();
            if (null == structureHelper) structureHelper = new FileStructureHelper(isLittleEndian); else structureHelper.Clear();
        }

        public void SetMetaField(string ID, string data, bool readAllMetaFrames, string zone = DEFAULT_ZONE_NAME, byte tagVersion = 0, ushort streamNumber = 0, string language = "")
        {
            // Finds the ATL field identifier
            byte supportedMetaID = getFrameMapping(zone, ID, tagVersion);

            // If ID has been mapped with an 'classic' ATL field, store it in the dedicated place...
            if (supportedMetaID < 255)
            {
                if (TagData.TAG_FIELD_TRACK_NUMBER == supportedMetaID && data.Length > 1 && data.StartsWith("0")) tagData.TrackDigitsForLeadingZeroes = data.Length;
                else if (TagData.TAG_FIELD_TRACK_NUMBER_TOTAL == supportedMetaID)
                {
                    if (data.Contains("/"))
                    {
                        string[] parts = data.Split('/');
                        if (parts[0].Length > 1 && parts[0].StartsWith("0")) tagData.TrackDigitsForLeadingZeroes = parts[0].Length;
                    }
                }
                else if (TagData.TAG_FIELD_DISC_NUMBER == supportedMetaID && data.Length > 1 && data.StartsWith("0")) tagData.DiscDigitsForLeadingZeroes = data.Length;
                else if (TagData.TAG_FIELD_DISC_NUMBER_TOTAL == supportedMetaID && data.Contains("/"))
                {
                    string[] parts = data.Split('/');
                    if (parts[0].Length > 1 && parts[0].StartsWith("0")) tagData.DiscDigitsForLeadingZeroes = parts[0].Length;
                }

                setMetaField(supportedMetaID, data);
            }
            else if (readAllMetaFrames && ID.Length > 0) // ...else store it in the additional fields Dictionary
            {
                MetaFieldInfo fieldInfo = new MetaFieldInfo(getImplementedTagType(), ID, data, streamNumber, language, zone);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Prevent duplicates
                {
                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                tagData.AdditionalFields.Add(fieldInfo);
            }
        }

        protected void setMetaField(byte ID, string data)
        {
            tagData.IntegrateValue(ID, data);
        }

        protected string formatBeforeWriting(byte frameType, TagData tag, IDictionary<byte, string> map)
        {
            string value;
            string total;
            switch (frameType)
            {
                case TagData.TAG_FIELD_RATING: return TrackUtils.EncodePopularity(map[frameType], ratingConvention).ToString();
                case TagData.TAG_FIELD_TRACK_NUMBER:
                    value = map[TagData.TAG_FIELD_TRACK_NUMBER];
                    map.TryGetValue(TagData.TAG_FIELD_TRACK_TOTAL, out total);
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case TagData.TAG_FIELD_TRACK_TOTAL:
                    value = map[TagData.TAG_FIELD_TRACK_TOTAL];
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case TagData.TAG_FIELD_TRACK_NUMBER_TOTAL:
                    value = map[TagData.TAG_FIELD_TRACK_NUMBER_TOTAL];
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case TagData.TAG_FIELD_DISC_NUMBER:
                    value = map[TagData.TAG_FIELD_DISC_NUMBER];
                    map.TryGetValue(TagData.TAG_FIELD_DISC_TOTAL, out total);
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case TagData.TAG_FIELD_DISC_TOTAL:
                    value = map[TagData.TAG_FIELD_DISC_TOTAL];
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case TagData.TAG_FIELD_DISC_NUMBER_TOTAL:
                    value = map[TagData.TAG_FIELD_DISC_NUMBER_TOTAL];
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                default: return map[frameType];
            }
        }

        public void Clear()
        {
            ResetData();
        }

        public bool Read(BinaryReader source, ReadTagParams readTagParams)
        {
            if (readTagParams.PrepareForWriting) structureHelper.Clear();

            return read(source, readTagParams);
        }

        private FileSurgeon.WriteResult writeAdapter(BinaryWriter w, TagData tag, Zone zone)
        {
            int result = write(tag, w, zone.Name);
            FileSurgeon.WriteMode writeMode = (result > -1) ? FileSurgeon.WriteMode.REPLACE : FileSurgeon.WriteMode.OVERWRITE;
            return new FileSurgeon.WriteResult(writeMode, result);
        }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            bool result = true;

            // Constraint-check on non-supported values
            if (FieldCodeFixedLength > 0)
            {
                if (tag.Pictures != null)
                {
                    foreach (PictureInfo picInfo in tag.Pictures)
                    {
                        if (PictureInfo.PIC_TYPE.Unsupported.Equals(picInfo.PicType) && (picInfo.TagType.Equals(getImplementedTagType())))
                        {
                            if ((-1 == picInfo.NativePicCode) && (Utils.ProtectValue(picInfo.NativePicCodeStr).Length != FieldCodeFixedLength))
                            {
                                throw new NotSupportedException("Field code fixed length is " + FieldCodeFixedLength + "; detected field '" + Utils.ProtectValue(picInfo.NativePicCodeStr) + "' is " + Utils.ProtectValue(picInfo.NativePicCodeStr).Length + " characters long and cannot be written");
                            }
                        }
                    }
                }
                foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
                {
                    if (fieldInfo.TagType.Equals(getImplementedTagType()) || MetaDataIOFactory.TAG_ANY == fieldInfo.TagType)
                    {
                        string fieldCode = Utils.ProtectValue(fieldInfo.NativeFieldCode);
                        if (fieldCode.Length != FieldCodeFixedLength && !fieldCode.Contains("----")) // "----" = exception for MP4 extended fields (e.g. ----:com.apple.iTunes:CONDUCTOR)
                        {
                            throw new NotSupportedException("Field code fixed length is " + FieldCodeFixedLength + "; detected field '" + fieldCode + "' is " + fieldCode.Length + " characters long and cannot be written");
                        }
                    }
                }
            }

            structureHelper.Clear();
            tagData.Pictures.Clear();

            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(true, true);
            readTagParams.PrepareForWriting = true;

            if (embedder != null && embedder.HasEmbeddedID3v2 > 0)
            {
                readTagParams.offset = embedder.HasEmbeddedID3v2;
            }

            this.read(r, readTagParams);

            if (embedder != null && getImplementedTagType() == MetaDataIOFactory.TAG_ID3V2)
            {
                structureHelper.Clear();
                structureHelper.AddZone(embedder.Id3v2Zone);
            }

            // Give engine something to work with if the tag is really empty
            if (!tagExists && 0 == Zones.Count)
            {
                structureHelper.AddZone(0, 0);
            }

            TagData dataToWrite;
            dataToWrite = tagData;
            dataToWrite.IntegrateValues(tag); // Merge existing information + new tag information
            dataToWrite.Cleanup();

            FileSurgeon surgeon = new FileSurgeon(structureHelper, embedder, getImplementedTagType(), getDefaultTagOffset(), writeProgress);
            result = surgeon.RewriteZones(w, new FileSurgeon.WriteDelegate(writeAdapter), Zones, dataToWrite, tagExists);

            // Update tag information without calling Read
            /* TODO - this implementation is too risky : 
             *   - if one of the writing operations fails, data is updated as if everything went right
             *   - any picture slot with a markForDeletion flag is recorded as-is in the tag
             */
            tagData = dataToWrite;

            return result;
        }

        public virtual bool Remove(BinaryWriter w)
        {
            bool result = true;
            long cumulativeDelta = 0;

            if (embedder != null && getImplementedTagType() == MetaDataIOFactory.TAG_ID3V2)
            {
                structureHelper.Clear();
                structureHelper.AddZone(embedder.Id3v2Zone);
            }

            foreach (Zone zone in Zones)
            {
                if (zone.Offset > -1 && !zone.Name.Equals(PADDING_ZONE_NAME))
                {
                    if (zone.IsDeletable)
                    {
                        if (zone.Size > zone.CoreSignature.Length) StreamUtils.ShortenStream(w.BaseStream, zone.Offset + zone.Size - cumulativeDelta, (uint)(zone.Size - zone.CoreSignature.Length));

                        if (zone.CoreSignature.Length > 0)
                        {
                            w.BaseStream.Position = zone.Offset - cumulativeDelta;
                            w.Write(zone.CoreSignature);
                        }
                    }
                    if (MetaDataIOFactory.TAG_NATIVE == getImplementedTagType() || (embedder != null && getImplementedTagType() == MetaDataIOFactory.TAG_ID3V2))
                    {
                        if (zone.IsDeletable)
                            result = result && structureHelper.RewriteHeaders(w, null, -zone.Size + zone.CoreSignature.Length, ACTION.Delete, zone.Name);
                        else
                            result = result && structureHelper.RewriteHeaders(w, null, 0, ACTION.Edit, zone.Name);
                    }

                    if (zone.IsDeletable) cumulativeDelta += zone.Size - zone.CoreSignature.Length;
                }
            }

            return result;
        }
    }
}
