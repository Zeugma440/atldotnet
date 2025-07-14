using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static ATL.AudioData.FileStructureHelper;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Superclass that "consolidates" all metadata I/O algorithms to ease development of new classes and minimize their code
    /// </summary>
    public abstract partial class MetaDataIO : MetaDataHolder, IMetaDataIO
    {
        // ------ CONSTS -----------------------------------------------------

        // Default tag offset
        /// <summary>
        /// Tag offset is at End Of File
        /// </summary>
        public const int TO_EOF = 0;
        /// <summary>
        /// Tag offset is at Beginning Of File
        /// </summary>
        public const int TO_BOF = 1;
        /// <summary>
        /// Tag offset is at a Built-in location (e.g. MP4)
        /// </summary>
        public const int TO_BUILTIN = 2;

        // Rating conventions
        /// <summary>
        /// ID3v2 convention (0..255 scale with various tweaks)
        /// </summary>
        public const int RC_ID3v2 = 0;
        /// <summary>
        /// ASF convention (0..100 scale with 1 being encoded as 1)
        /// </summary>
        public const int RC_ASF = 1;
        /// <summary>
        /// APE convention (proper 0..100 scale)
        /// </summary>
        public const int RC_APE = 2;

        /// <summary>
        /// Default constructor
        /// </summary>
        protected MetaDataIO() { }

        /// <summary>
        /// Instanciate a new TagHolder populated with the given TagData
        /// </summary>
        /// <param name="tagData">Data to use to populate the new instance</param>
        protected MetaDataIO(TagData tagData) : base(tagData) { }


        // ------ INNER CLASSES -----------------------------------------------------

        /// <summary>
        /// Container class describing tag reading parameters
        /// </summary>
        public class ReadTagParams
        {
            /// <summary>
            /// True : read metadata; False : do not read metadata (only "physical" audio data)
            /// </summary>
            public bool ReadTag { get; set; }

            /// <summary>
            /// True : read all metadata frames; False : only read metadata frames that match IMetaDataIO public properties (="supported" metadata)
            /// </summary>
            public bool ReadAllMetaFrames { get; set; }

            /// <summary>
            /// True : read embedded pictures; False : skip embedded pictures (faster, less memory taken)
            /// </summary>
            public bool ReadPictures { get; set; }

            /// <summary>
            /// True : read all data that will be useful for writing; False : only read metadata values
            /// </summary>
            public bool PrepareForWriting { get; set; }

            /// <summary>
            /// True : read extra padding bytes at the end of ID3v2 block
            /// </summary>
            public bool ExtraID3v2PaddingDetection { get; set; }

            /// <summary>
            /// File offset to start reading metadata from (bytes)
            /// </summary>
            public long Offset { get; set; }

            /// <summary>
            /// Create a new ReadTagParams
            /// </summary>
            /// <param name="readPictures">true if pictures have to be read</param>
            /// <param name="readAllMetaFrames">true if all meta frames have t be read</param>
            public ReadTagParams(bool readPictures = false, bool readAllMetaFrames = false)
            {
                ReadPictures = readPictures;
                ReadAllMetaFrames = readAllMetaFrames;
                ReadTag = true;
                PrepareForWriting = false;
                ExtraID3v2PaddingDetection = true;
                Offset = 0;
            }
        }

        /// <summary>
        /// Container class describing tag writing parameters
        /// </summary>
        public class WriteTagParams
        {
            /// <summary>
            /// True : read extra padding bytes at the end of ID3v2 block
            /// </summary>
            public bool ExtraID3v2PaddingDetection { get; set; }

            /// <summary>
            /// Create a new ReadTagParams
            /// </summary>
            public WriteTagParams()
            {
                ExtraID3v2PaddingDetection = true;
            }
        }


        // ------ PROPERTIES -----------------------------------------------------

        /// <summary>
        /// Version of the tag
        /// </summary>
        protected int m_tagVersion;
        /// <summary>
        /// Tag embedder (3rd party tagging system within the tag)
        /// </summary>
        protected IMetaDataEmbedder m_embedder;

        private IList<KeyValuePair<string, int>> picturePositions;

        internal FileStructureHelper structureHelper;


        // ------ READ-ONLY "PHYSICAL" TAG INFO FIELDS ACCESSORS -----------------------------------------------------

        /// <inheritdoc/>
        public bool Exists => tagData.Exists();

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0]);
#pragma warning disable S3060 // "is" should not be used with "this"
                if (this is IAudioDataIO iO)
#pragma warning restore S3060 // "is" should not be used with "this"
                {
                    nativeFormat.Name = nativeFormat.Name + " / " + iO.AudioFormat.ShortName;
                    nativeFormat.ID += iO.AudioFormat.ID;
                }
                return new List<Format>(new[] { nativeFormat });
            }
        }
        /// <summary>
        /// Tag version
        /// </summary>
        public int Version => m_tagVersion;

        /// <inheritdoc/>
        public long Size
        {
            get
            {
                long result = 0;

                foreach (Zone zone in Zones) result += zone.Size;

                return result;
            }
        }
        /// <summary>
        /// Zones of the file
        /// </summary>
        public ICollection<Zone> Zones => structureHelper.Zones;


        // ------ TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <inheritdoc/>
        public long PaddingSize => tagData.PaddingSize;

        /// <summary>
        /// Rating convention to use to format Popularity for the current tagging format
        /// </summary>
        protected virtual byte ratingConvention => RC_ID3v2;

        /// <summary>
        /// Indicate if the tagging system supports embedded pictures
        /// </summary>
        protected virtual bool supportsPictures => false;

        /// <summary>
        /// Indicate if the tagging system supports additional fields
        /// NB : Usually true except when the fields are a fixed set
        /// </summary>
        protected virtual bool supportsAdditionalFields => false;

        /// <summary>
        /// Indicate if the tagging system supports synchronized lyrics
        /// </summary>
        protected virtual bool supportsSynchronizedLyrics => false;

        /// <summary>
        /// Encode the given DateTime for the current tagging format
        /// </summary>
        public virtual string EncodeDate(DateTime date)
        {
            return TrackUtils.FormatISOTimestamp(date);
        }

        // ------ NON-TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// Indicate whether the metadata field code must have a fixed length or not
        /// Default : 0 (no fixed length)
        /// </summary>
        public virtual byte FieldCodeFixedLength => 0;

        /// <summary>
        /// Indicate whether metadata should be read with little endian convention
        /// true : little endian; false : big endian
        /// </summary>
        protected virtual bool isLittleEndian => true;

        // ------ PICTURE HELPER METHODS -----------------------------------------------------

        protected int takePicturePosition(PictureInfo.PIC_TYPE picType)
        {
            return takePicturePosition(new PictureInfo(picType));
        }

        protected int takePicturePosition(MetaDataIOFactory.TagType tagType, byte nativePicCode)
        {
            return takePicturePosition(new PictureInfo(tagType, nativePicCode));
        }

        protected int takePicturePosition(MetaDataIOFactory.TagType tagType, string nativePicCode)
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
        protected abstract bool read(Stream source, ReadTagParams readTagParams);

        /// <summary>
        /// Write the given zone's metadata using the given writer
        /// </summary>
        /// <param name="tag">Metadata to write</param>
        /// <param name="s">Writer to use</param>
        /// <param name="zone">Code of the zone to write</param>
        /// <returns>Number of written fields; 0 if no field has been added not edited</returns>
        protected abstract int write(TagData tag, Stream s, string zone);

        /// <summary>
        /// Return the default offset of the metadata block
        /// </summary>
        /// <returns></returns>
        protected abstract int getDefaultTagOffset();

        /// <summary>
        /// Get the frame code (per <see cref="TagData"/> standards for the given field ID in the given zone and the given tag version
        /// </summary>
        /// <param name="zone">Code of the zone of the given field</param>
        /// <param name="ID">ID of the field to get the mapping for</param>
        /// <param name="tagVersion">Version the tagging system (e.g. 3 for ID3v2.3)</param>
        /// <returns></returns>
        protected abstract Field getFrameMapping(string zone, string ID, byte tagVersion);


        // ------ COMMON METHODS -----------------------------------------------------

        /// <summary>
        /// Set the given embedded
        /// </summary>
        /// <param name="embedder">Embedder to set</param>
        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            m_embedder = embedder;
        }

        /// <summary>
        /// Reset all data
        /// </summary>
        protected void ResetData()
        {
            m_tagVersion = 0;

            tagData.Clear();
            if (null == picturePositions) picturePositions = new List<KeyValuePair<string, int>>(); else picturePositions.Clear();
            if (null == structureHelper) structureHelper = new FileStructureHelper(isLittleEndian); else structureHelper.Clear();
        }

        /// <summary>
        /// Set a new metadata field with the given information
        /// </summary>
        /// <param name="ID">ID of the metadata field</param>
        /// <param name="data">Metadata</param>
        /// <param name="readAllMetaFrames">True if can be stored in AdditionalData</param>
        /// <param name="zone">Zone where this metadata appears</param>
        /// <param name="tagVersion">Version of the tagging system</param>
        /// <param name="streamNumber">Number of the corresponding stream</param>
        /// <param name="language">Language</param>
        public void SetMetaField(string ID, string data, bool readAllMetaFrames, string zone = DEFAULT_ZONE_NAME, byte tagVersion = 0, ushort streamNumber = 0, string language = "")
        {
            // Finds the ATL field identifier
            Field supportedMetaID = getFrameMapping(zone, ID, tagVersion);

            // If ID has been mapped with an 'classic' ATL field, store it in the dedicated place...
            if (supportedMetaID != Field.NO_FIELD)
            {
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

        /// <summary>
        /// Set a new metadata field with the given information 
        /// </summary>
        /// <param name="ID">ID of the metadata field</param>
        /// <param name="dataIn">Metadata</param>
        protected void setMetaField(Field ID, string dataIn)
        {
            string dataOut = dataIn;
            switch (ID)
            {
                case Field.TRACK_NUMBER:
                case Field.TRACK_NUMBER_TOTAL:
                    {
                        string trackNumberStr = TrackUtils.ExtractTrackNumberStr(dataIn);
                        if (trackNumberStr.Length > 1 && trackNumberStr.StartsWith('0')) tagData.TrackDigitsForLeadingZeroes = trackNumberStr.Length;
                        break;
                    }
                case Field.DISC_NUMBER_TOTAL:
                case Field.DISC_NUMBER:
                    {
                        string discNumberStr = TrackUtils.ExtractTrackNumberStr(dataIn);
                        if (discNumberStr.Length > 1 && discNumberStr.StartsWith('0')) tagData.DiscDigitsForLeadingZeroes = discNumberStr.Length;
                        break;
                    }
                // Use the appropriate convention if needed
                case Field.RATING:
                    dataOut = TrackUtils.DecodePopularity(dataIn, ratingConvention).ToString();
                    break;
            }

            tagData.IntegrateValue(ID, dataOut);
        }

        /// <summary>
        /// Indicate whether the current MetaIO can handle the given non-standard field
        /// See https://github.com/Zeugma440/atldotnet/wiki/Focus-on-non-standard-fields
        /// </summary>
        /// <param name="code">Code of the non-standard field</param>
        /// <param name="value">Value of the non-standard field</param>
        /// <returns></returns>
        protected virtual bool canHandleNonStandardField(string code, string value)
        {
            return false;
        }

        /// <summary>
        /// Overridable function called when writing the file, just before looping the zones
        /// </summary>
        /// <param name="dataToWrite">Metadata to write</param>
        protected virtual void preprocessWrite(TagData dataToWrite)
        {
            // Nothing here; the point is to override when needed
        }

        /// <summary>
        /// Overridable function called after writing the file
        /// </summary>
        /// <param name="s">Stream to the file</param>
        protected virtual void postprocessWrite(Stream s)
        {
            // Nothing here; the point is to override when needed
        }

        /// <summary>
        /// Format fields that require a specific format (e.g. dates, numbers)
        /// </summary>
        /// <param name="frameType">Frame type</param>
        /// <param name="tag">TagData carrying formatting parameters</param>
        /// <param name="map">Metadata values</param>
        /// <returns></returns>
        protected string formatBeforeWriting(Field frameType, TagData tag, IDictionary<Field, string> map)
        {
            string total;
            DateTime dateTime;
            string value = map[frameType];
            switch (frameType)
            {
                case Field.RATING:
                    var valueD = Utils.ParseDouble(map[frameType]);
                    if (double.IsNaN(valueD)) valueD = 0;
                    return TrackUtils.EncodePopularity(valueD * 5, ratingConvention).ToString();
                case Field.RECORDING_DATE:
                case Field.PUBLISHING_DATE:
                    return DateTime.TryParse(value, out dateTime) ? EncodeDate(dateTime) : value;
                case Field.RECORDING_DATE_OR_YEAR:
                    if (value.Length > 4 && DateTime.TryParse(value, out dateTime)) return EncodeDate(dateTime);
                    return value;
                case Field.TRACK_NUMBER:
                    map.TryGetValue(Field.TRACK_TOTAL, out total);
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.DISC_NUMBER:
                    map.TryGetValue(Field.DISC_TOTAL, out total);
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.TRACK_NUMBER_TOTAL:
                case Field.TRACK_TOTAL:
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.DISC_NUMBER_TOTAL:
                case Field.DISC_TOTAL:
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.NO_FIELD:
                case Field.GENERAL_DESCRIPTION:
                case Field.TITLE:
                case Field.ARTIST:
                case Field.COMPOSER:
                case Field.COMMENT:
                case Field.GENRE:
                case Field.ALBUM:
                case Field.RECORDING_YEAR:
                case Field.RECORDING_DAYMONTH:
                case Field.RECORDING_TIME:
                case Field.ORIGINAL_ARTIST:
                case Field.ORIGINAL_ALBUM:
                case Field.COPYRIGHT:
                case Field.ALBUM_ARTIST:
                case Field.PUBLISHER:
                case Field.CONDUCTOR:
                case Field.CHAPTERS_TOC_DESCRIPTION:
                case Field.LYRICS_UNSYNCH:
                case Field.PRODUCT_ID:
                case Field.SORT_ALBUM:
                case Field.SORT_ALBUM_ARTIST:
                case Field.SORT_ARTIST:
                case Field.SORT_TITLE:
                case Field.GROUP:
                case Field.SERIES_TITLE:
                case Field.SERIES_PART:
                case Field.LONG_DESCRIPTION:
                case Field.BPM:
                case Field.ENCODED_BY:
                case Field.ORIG_RELEASE_YEAR:
                case Field.ORIG_RELEASE_DATE:
                case Field.ENCODER:
                case Field.LANGUAGE:
                case Field.ISRC:
                case Field.CATALOG_NUMBER:
                case Field.AUDIO_SOURCE_URL:
                case Field.LYRICIST:
                case Field.INVOLVED_PEOPLE:
                default: return map[frameType];
            }
        }

        internal string FormatBeforeWriting(string value)
        {
            if (Settings.AutoFormatAdditionalDates && value.StartsWith(DATETIME_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                return EncodeDate(DateTime.FromFileTime(long.Parse(value[DATETIME_PREFIX.Length..])));
            }
            return value;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ResetData();
        }

        /// <inheritdoc/>
        public bool Read(Stream source, ReadTagParams readTagParams)
        {
            if (readTagParams.PrepareForWriting) structureHelper.Clear();

            return read(source, readTagParams);
        }

        private FileSurgeon.WriteResult writeAdapter(Stream w, TagData tag, Zone zone)
        {
            int result = write(tag, w, zone.Name);
            FileSurgeon.WriteMode writeMode = result > -1 ? FileSurgeon.WriteMode.REPLACE : FileSurgeon.WriteMode.OVERWRITE;
            return new FileSurgeon.WriteResult(writeMode, result);
        }

        /// <inheritdoc/>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> WriteAsync(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null)
        {
            TagData dataToWrite = prepareWrite(s, args, tag);

            FileSurgeon surgeon = new FileSurgeon(structureHelper, m_embedder, getImplementedTagType(), getDefaultTagOffset(), writeProgress);
            bool result = await surgeon.RewriteZonesAsync(s, writeAdapter, Zones, dataToWrite, Exists);

            // Update tag information without calling Read
            if (result) tagData.IntegrateValues(dataToWrite, supportsPictures, supportsAdditionalFields);

            postprocessWrite(s);

            return result;
        }

        private TagData prepareWrite(Stream r, WriteTagParams args, TagData tag)
        {
            structureHelper.Clear();
            tagData.Pictures.Clear();

            // Constraint-check on non-supported values
            if (FieldCodeFixedLength > 0)
            {
                ISet<MetaFieldInfo> infoToRemove = new HashSet<MetaFieldInfo>();
                foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
                {
                    if (fieldInfo.TagType.Equals(getImplementedTagType()) || MetaDataIOFactory.TagType.ANY == fieldInfo.TagType)
                    {
                        string fieldCode = Utils.ProtectValue(fieldInfo.NativeFieldCode);
                        if (fieldCode.Length != FieldCodeFixedLength && !canHandleNonStandardField(fieldCode, Utils.ProtectValue(fieldInfo.Value)))
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Field code fixed length is " + FieldCodeFixedLength + "; detected field '" + fieldCode + "' is " + fieldCode.Length + " characters long and will be ignored");
                            infoToRemove.Add(fieldInfo);
                        }
                    }
                }
                foreach (MetaFieldInfo info in infoToRemove) tag.AdditionalFields.Remove(info);
            }

            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(true, true)
            {
                PrepareForWriting = true,
                ExtraID3v2PaddingDetection = args.ExtraID3v2PaddingDetection
            };

            if (m_embedder is { HasEmbeddedID3v2: > 0 })
            {
                readTagParams.Offset = m_embedder.HasEmbeddedID3v2;
            }

            read(r, readTagParams);

            if (m_embedder != null && getImplementedTagType() == MetaDataIOFactory.TagType.ID3V2)
            {
                structureHelper.Clear();
                structureHelper.AddZone(m_embedder.Id3v2Zone);
            }

            // Give engine something to work with if the tag is really empty
            if (!Exists && 0 == Zones.Count) structureHelper.AddZone(0, 0);

            // Merge existing information + new tag information
            var dataToWrite = new TagData(tagData, supportsSynchronizedLyrics);
            dataToWrite.IntegrateValues(tag, supportsPictures, supportsAdditionalFields, supportsSynchronizedLyrics);
            dataToWrite.Cleanup();

            preprocessWrite(dataToWrite);
            return dataToWrite;
        }

        /// <inheritdoc/>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public virtual async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            handleEmbedder();

            bool result = true;
            long cumulativeDelta = 0;
            foreach (var zone in Zones)
            {
                if (zone.Offset <= -1 || zone.Name.Equals(PADDING_ZONE_NAME)) continue;
                if (zone.IsDeletable)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "Deleting " + zone.Name + " (deletable) @ " + zone.Offset + " [" + zone.Size + "]");

                    if (zone.Size > zone.CoreSignature.Length) await StreamUtils.ShortenStreamAsync(s, zone.Offset + zone.Size - cumulativeDelta, (uint)(zone.Size - zone.CoreSignature.Length));

                    if (zone.CoreSignature.Length > 0)
                    {
                        s.Position = zone.Offset - cumulativeDelta;
                        await StreamUtils.WriteBytesAsync(s, zone.CoreSignature);
                    }
                }

                result = result && rewriteHeaders(s, zone);

                if (!zone.IsDeletable) continue;

                cumulativeDelta += zone.Size - zone.CoreSignature.Length;
                zone.IsDeleted = true;
            }
            postprocessWrite(s);

            return result;
        }

        private void handleEmbedder()
        {
            if (m_embedder == null || getImplementedTagType() != MetaDataIOFactory.TagType.ID3V2) return;
            structureHelper.Clear();
            structureHelper.AddZone(m_embedder.Id3v2Zone);
        }

        private bool rewriteHeaders(Stream s, Zone zone)
        {
            if (MetaDataIOFactory.TagType.NATIVE != getImplementedTagType() &&
                (m_embedder == null || getImplementedTagType() != MetaDataIOFactory.TagType.ID3V2)) return true;

            return zone.IsDeletable ? structureHelper.RewriteHeaders(s, null, -zone.Size + zone.CoreSignature.Length, ACTION.Delete, zone.Name)
                : structureHelper.RewriteHeaders(s, null, 0, ACTION.Edit, zone.Name);
        }

        /// <summary>
        /// Indicate whether the given field can be written to the current file, according to their properties
        /// </summary>
        /// <param name="fieldInfo">Field to test</param>
        /// <returns>True if the given field can be written; false if not</returns>
        protected bool isMetaFieldWritable(MetaFieldInfo fieldInfo)
        {
            return (fieldInfo.TagType.Equals(MetaDataIOFactory.TagType.ANY) ||
                    fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion;
        }

        /// <summary>
        /// Indicate whether the given picture can be written to the current file, according to their properties
        /// </summary>
        /// <param name="picInfo">Picture to test</param>
        /// <returns>True if the given picture can be written; false if not</returns>
        protected bool isPictureWritable(PictureInfo picInfo)
        {
            // Picture has either to be supported, or to come from the right tag standard
            var result = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
            if (!result) result = getImplementedTagType() == picInfo.TagType;
            // It also has not to be marked for deletion
            return result && !picInfo.MarkedForDeletion;
        }
    }
}
