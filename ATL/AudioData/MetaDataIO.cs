using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.FileStructureHelper;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Superclass that "consolidates" all metadata I/O algorithms to ease development of new classes and minimize their code
    /// </summary>
    public abstract class MetaDataIO : MetadataHolder, IMetaDataIO
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
            /// File offset to start reading metadata from (bytes)
            /// </summary>
            public long Offset { get; set; }

            /// <summary>
            /// Create a new ReadTagParams
            /// </summary>
            /// <param name="readPictures">true if pictures have to be read</param>
            /// <param name="readAllMetaFrames">true if all meta frames have t be read</param>
            public ReadTagParams(bool readPictures, bool readAllMetaFrames)
            {
                ReadPictures = readPictures;
                ReadAllMetaFrames = readAllMetaFrames;
                ReadTag = true;
                PrepareForWriting = false;
                Offset = 0;
            }
        }


        // ------ PROPERTIES -----------------------------------------------------

        /// <summary>
        /// True if the tag exists
        /// </summary>
        protected bool tagExists;
        /// <summary>
        /// Version of the tag
        /// </summary>
        protected int tagVersion;
        /// <summary>
        /// Picture tokens (i.e. presence of pictures, without its binary data)
        /// </summary>
        [Obsolete("Use PictureInfo instead", false)]
        protected IList<PictureInfo> pictureTokens;
        /// <summary>
        /// Tag embedder (3rd party tagging system within the tag)
        /// </summary>
        protected IMetaDataEmbedder embedder;

        private IList<KeyValuePair<string, int>> picturePositions;

        internal FileStructureHelper structureHelper;


        // ------ READ-ONLY "PHYSICAL" TAG INFO FIELDS ACCESSORS -----------------------------------------------------

        /// <inheritdoc/>
        public bool Exists
        {
            get { return this.tagExists; }
        }
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
        public ICollection<Zone> Zones
        {
            get
            {
                return structureHelper.Zones;
            }
        }


        // ------ TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <inheritdoc/>
        public long PaddingSize
        {
            get { return tagData.PaddingSize; }
        }

        /// <inheritdoc/>
        [Obsolete("Use PictureInfo instead", false)]
        public IList<PictureInfo> PictureTokens
        {
            get { return pictureTokens; }
        }
        /// <summary>
        /// Rating convention to use to format Popularity for the current file
        /// </summary>
        protected virtual byte ratingConvention
        {
            get { return MetaDataIO.RC_ID3v2; }
        }

        // ------ NON-TAGDATA FIELDS ACCESSORS -----------------------------------------------------

        /// <summary>
        /// Indicate whether the metadata field code must have a fixed length or not
        /// Default : 0 (no fixed length)
        /// </summary>
        public virtual byte FieldCodeFixedLength
        {
            get { return 0; }
        }

        /// <summary>
        /// Indicate whether metadata should be read with little endian convention
        /// true : little endian; false : big endian
        /// </summary>
        protected virtual bool isLittleEndian
        {
            get { return true; }
        }

        // ------ PICTURE HELPER METHODS -----------------------------------------------------

        /// <summary>
        /// Add a picture token of the given type
        /// </summary>
        /// <param name="picType">Type of the picture token to add</param>
        protected void addPictureToken(PictureInfo.PIC_TYPE picType)
        {
            pictureTokens.Add(new PictureInfo(picType));
        }

        /// <summary>
        /// Add a picture token for the given tag type, with the given code as a byte
        /// </summary>
        /// <param name="tagType">Tag type to use</param>
        /// <param name="nativePicCode">Native code to use</param>
        protected void addPictureToken(MetaDataIOFactory.TagType tagType, byte nativePicCode)
        {
            pictureTokens.Add(new PictureInfo(tagType, nativePicCode));
        }

        /// <summary>
        /// Add a picture token for the given tag type, with the given code as a string
        /// </summary>
        /// <param name="tagType">Tag type to use</param>
        /// <param name="nativePicCode">Native code to use</param>
        protected void addPictureToken(MetaDataIOFactory.TagType tagType, string nativePicCode)
        {
            pictureTokens.Add(new PictureInfo(tagType, nativePicCode));
        }

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
        /// Get the frame code (per <see cref="TagData"/> standards for the given field ID in the given zone and the given tag version
        /// </summary>
        /// <param name="zone">Code of the zone of the given field</param>
        /// <param name="ID">ID of the field to get the mapping for</param>
        /// <param name="tagVersion">Version the tagging system (e.g. 3 for ID3v2.3)</param>
        /// <returns></returns>
        abstract protected Field getFrameMapping(string zone, string ID, byte tagVersion);


        // ------ COMMON METHODS -----------------------------------------------------

        /// <summary>
        /// Set the given embedded
        /// </summary>
        /// <param name="embedder">Embedder to set</param>
        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            this.embedder = embedder;
        }

        /// <summary>
        /// Reset all data
        /// </summary>
        protected void ResetData()
        {
            tagExists = false;
            tagVersion = 0;

            if (null == tagData) tagData = new TagData(); else tagData.Clear();
            if (null == pictureTokens) pictureTokens = new List<PictureInfo>(); else pictureTokens.Clear();
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
            if (Field.TRACK_NUMBER == ID && dataIn.Length > 1 && dataIn.StartsWith("0")) tagData.TrackDigitsForLeadingZeroes = dataIn.Length;
            else if (Field.TRACK_NUMBER_TOTAL == ID)
            {
                if (dataIn.Contains("/"))
                {
                    string[] parts = dataIn.Split('/');
                    if (parts[0].Length > 1 && parts[0].StartsWith("0")) tagData.TrackDigitsForLeadingZeroes = parts[0].Length;
                }
            }
            else if (Field.DISC_NUMBER == ID && dataIn.Length > 1 && dataIn.StartsWith("0")) tagData.DiscDigitsForLeadingZeroes = dataIn.Length;
            else if (Field.DISC_NUMBER_TOTAL == ID && dataIn.Contains("/"))
            {
                string[] parts = dataIn.Split('/');
                if (parts[0].Length > 1 && parts[0].StartsWith("0")) tagData.DiscDigitsForLeadingZeroes = parts[0].Length;
            }

            // Use the appropriate convention if needed
            if (Field.RATING == ID)
            {
                dataOut = TrackUtils.DecodePopularity(dataIn, ratingConvention).ToString();
            }

            tagData.IntegrateValue(ID, dataOut);
        }

        protected string formatBeforeWriting(Field frameType, TagData tag, IDictionary<Field, string> map)
        {
            string value;
            string total;
            switch (frameType)
            {
                case Field.RATING: return TrackUtils.EncodePopularity(Utils.ParseDouble(map[frameType]) * 5, ratingConvention).ToString();
                case Field.TRACK_NUMBER:
                    value = map[frameType];
                    map.TryGetValue(Field.TRACK_TOTAL, out total);
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.DISC_NUMBER:
                    value = map[frameType];
                    map.TryGetValue(Field.DISC_TOTAL, out total);
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.TRACK_NUMBER_TOTAL:
                case Field.TRACK_TOTAL:
                    value = map[frameType];
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.TrackDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                case Field.DISC_NUMBER_TOTAL:
                case Field.DISC_TOTAL:
                    value = map[frameType];
                    total = value;
                    return TrackUtils.FormatWithLeadingZeroes(value, Settings.OverrideExistingLeadingZeroesFormat, tag.DiscDigitsForLeadingZeroes, Settings.UseLeadingZeroes, total);
                default: return map[frameType];
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ResetData();
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
                    if (fieldInfo.TagType.Equals(getImplementedTagType()) || MetaDataIOFactory.TagType.ANY == fieldInfo.TagType)
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
                readTagParams.Offset = embedder.HasEmbeddedID3v2;
            }

            read(r, readTagParams);

            if (embedder != null && getImplementedTagType() == MetaDataIOFactory.TagType.ID3V2)
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

        /// <inheritdoc/>
        public virtual bool Remove(BinaryWriter w)
        {
            bool result = true;
            long cumulativeDelta = 0;

            if (embedder != null && getImplementedTagType() == MetaDataIOFactory.TagType.ID3V2)
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
                    if (MetaDataIOFactory.TagType.NATIVE == getImplementedTagType() || (embedder != null && getImplementedTagType() == MetaDataIOFactory.TagType.ID3V2))
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
