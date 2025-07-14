using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.FileStructureHelper;
using System.Linq;
using static ATL.TagData;
using System.Threading.Tasks;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Vorbis tags (VorbisComment) manipulation
    /// 
    /// TODO - Rewrite as "pure" helper, with Ogg and FLAC inheriting MetaDataIO
    /// 
    /// </summary>
    partial class VorbisTag : MetaDataIO
    {
        private const string PICTURE_METADATA_ID_NEW = "METADATA_BLOCK_PICTURE";
        private const string PICTURE_METADATA_ID_OLD = "COVERART";
        public const string VENDOR_METADATA_ID = "VORBIS-VENDOR";

        // empty vendor with zero fields, plus final framing bit
        private static readonly byte[] OGG_CORE_SIGNATURE = { 0, 0, 0, 0, 0, 0, 0, 0, 1 };


        // Reference : https://xiph.org/flac/format.html#metadata_block_picture
        public class VorbisMetaDataBlockPicture
        {
            public PictureInfo.PIC_TYPE picType;
            public int nativePicCode;
            public string mimeType;
            public string description;
            public int width;
            public int height;
            public int colorDepth;
            public int colorNum;
            public int picDataLength;

            public int picDataOffset;
        }

        // Mapping between Vorbis field IDs and ATL fields
        private static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
                { "DESCRIPTION", Field.GENERAL_DESCRIPTION },
                { "ARTIST", Field.ARTIST },
                { "TITLE", Field.TITLE },
                { "ALBUM", Field.ALBUM },
                { "DATE", Field.RECORDING_DATE_OR_YEAR },
                { "GENRE", Field.GENRE },
                { "COMPOSER", Field.COMPOSER },
                { "TRACKNUMBER", Field.TRACK_NUMBER },
                { "TRACKTOTAL", Field.TRACK_TOTAL },
                { "TOTALTRACKS", Field.TRACK_TOTAL },
                { "DISCNUMBER", Field.DISC_NUMBER },
                { "DISCTOTAL", Field.DISC_TOTAL },
                { "TOTALDISCS", Field.DISC_TOTAL },
                { "COMMENT", Field.COMMENT },
                { "ALBUMARTIST", Field.ALBUM_ARTIST },
                { "CONDUCTOR", Field.CONDUCTOR },
                { "RATING", Field.RATING },
                { "COPYRIGHT", Field.COPYRIGHT },
                { "PUBLISHER", Field.PUBLISHER },
                { "ORIGINALDATE", Field.PUBLISHING_DATE },
                { "PRODUCTNUMBER", Field.PRODUCT_ID },
                { "LYRICS", Field.LYRICS_UNSYNCH },
                { "BPM", Field.BPM },
                { "ENCODED-BY", Field.ENCODED_BY },
                { "ORIGINALDATE ", Field.ORIG_RELEASE_DATE },
                { "ENCODER", Field.ENCODER },
                { "LANGUAGE", Field.LANGUAGE },
                { "ISRC", Field.ISRC },
                { "CATALOGNUMBER", Field.CATALOG_NUMBER },
                { "LABELNO", Field.CATALOG_NUMBER },
                { "LYRICIST", Field.LYRICIST },
                { "ALBUMARTISTSORT", Field.SORT_ALBUM_ARTIST },
                { "ARTISTSORT", Field.SORT_ARTIST }
        };

        // Tweak to prevent/allow pictures to be written within the rest of metadata (OGG vs. FLAC behaviour)
        private bool writePicturesWithMetadata;
        // Tweak to prevent/allow framing bit to be written at the end of the metadata block (OGG vs. FLAC behaviour)
        private bool writeMetadataFramingBit;
        // Tweak to enable/disable core signature (OGG vs. FLAC behaviour)
        private bool hasCoreSignature;
        // Tweak to enable/disable padding management at VorbisComment level (OGG vs. FLAC behaviour)
        private bool managePadding;

        // Initial offset of the entire Vorbis tag
        private long initialTagOffset;

        // Initial offset of the padding block; used to handle padding the smart way when rewriting data
        private long initialPaddingOffset, initialPaddingSize;

        // ---------- CONSTRUCTORS & INITIALIZERS

        public VorbisTag(bool writePicturesWithMetadata, bool writeMetadataFramingBit, bool hasCoreSignature, bool managePadding, TagData tagData) : base(tagData)
        {
            this.writePicturesWithMetadata = writePicturesWithMetadata;
            this.writeMetadataFramingBit = writeMetadataFramingBit;
            this.hasCoreSignature = hasCoreSignature;
            this.managePadding = managePadding;

            ResetData();
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        protected override int getDefaultTagOffset() => TO_BUILTIN;

        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;

        protected override byte ratingConvention => RC_APE;

        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        /// <inheritdoc/>
        protected override bool supportsPictures => true;

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;
            ID = ID.ToUpper();

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }


        // ---------- SPECIFIC MEMBERS

        private void switchBehaviour(bool writePicturesWithMetadata, bool writeMetadataFramingBit, bool hasCoreSignature, bool managePadding)
        {
            this.writePicturesWithMetadata = writePicturesWithMetadata;
            this.writeMetadataFramingBit = writeMetadataFramingBit;
            this.hasCoreSignature = hasCoreSignature;
            this.managePadding = managePadding;
        }

        public void switchFlacBehaviour()
        {
            switchBehaviour(false, false, false, false);
        }

        public void switchOggBehaviour()
        {
            switchBehaviour(true, true, true, true);
        }

        public static VorbisMetaDataBlockPicture ReadMetadataBlockPicture(Stream s)
        {
            VorbisMetaDataBlockPicture result = new VorbisMetaDataBlockPicture();

            BinaryReader r = new BinaryReader(s);
            result.nativePicCode = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.picType = ID3v2.DecodeID3v2PictureType(result.nativePicCode);
            var stringLen = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.mimeType = Utils.Latin1Encoding.GetString(r.ReadBytes(stringLen));
            stringLen = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.description = Encoding.UTF8.GetString(r.ReadBytes(stringLen));
            result.width = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.height = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.colorDepth = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.colorNum = StreamUtils.DecodeBEInt32(r.ReadBytes(4));
            result.picDataLength = StreamUtils.DecodeBEInt32(r.ReadBytes(4));

            result.picDataOffset = 4 + 4 + result.mimeType.Length + 4 + result.description.Length + 4 + 4 + 4 + 4 + 4;

            return result;
        }

        private void setChapterData(string fieldName, string fieldValue)
        {
            tagData.Chapters ??= new List<ChapterInfo>();

            // Capture numeric sequence within field name
            // NB : Handled this way to retrieve badly formatted chapter indexes (e.g. CHAPTER2, CHAPTER02NAME...)
            int i = 7;
            while (i < fieldName.Length && char.IsDigit(fieldName[i])) i++;
            uint chapterId = uint.Parse(fieldName.Substring(7, i - 7));

            ChapterInfo info = tagData.Chapters.FirstOrDefault(c => c.UniqueNumericID == chapterId);
            if (null == info)
            {
                info = new ChapterInfo
                {
                    UniqueID = chapterId.ToString(),
                    UniqueNumericID = chapterId
                };
                tagData.Chapters.Add(info);
            }

            if (fieldName.EndsWith("NAME", StringComparison.OrdinalIgnoreCase)) // Chapter name
            {
                info.Title = fieldValue;
            }
            else if (fieldName.EndsWith("URL", StringComparison.OrdinalIgnoreCase)) // Chapter url
            {
                info.Url = new ChapterInfo.UrlInfo("", fieldValue);
            }
            else // Chapter start time
            {
                int result = Utils.DecodeTimecodeToMs(fieldValue);
                if (-1 == result)
                {
                    Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_WARNING, "Invalid timecode for chapter " + info.UniqueNumericID + " : " + fieldValue);
                }
                else
                {
                    info.StartTime = (uint)result;
                }
            }
        }

        // Reads large data chunks by streaming
        private void SetPictureItem(Stream source, string tagId, int size, ReadTagParams readTagParams)
        {
            if (tagId.Equals(PICTURE_METADATA_ID_NEW))
            {
                size = size - 1 - PICTURE_METADATA_ID_NEW.Length;
                // Make sure total size is a multiple of 4
                size = size - size % 4;

                // Read the whole base64-encoded picture header _and_ binary data
                using MemoryStream outStream = new MemoryStream(size);
                StreamUtils.CopyStream(source, outStream, size);
                byte[] encodedData = outStream.ToArray();

                // Gets rid of unwanted zeroes
                // 0x3D ('=' char) is the padding neutral character that has to replace zero, which is not part of base64 range
                for (int i = 0; i < encodedData.Length; i++) if (0 == encodedData[i]) encodedData[i] = 0x3D;

                using MemoryStream mem = new MemoryStream(Utils.DecodeFrom64(encodedData));
                mem.Seek(0, SeekOrigin.Begin);
                ReadPicture(mem, readTagParams);
            }
            else if (tagId.Equals(PICTURE_METADATA_ID_OLD)) // Deprecated picture info
            {
                const PictureInfo.PIC_TYPE picType = PictureInfo.PIC_TYPE.Generic;
                int picturePosition = takePicturePosition(picType);

                if (!readTagParams.ReadPictures) return;

                size = size - 1 - PICTURE_METADATA_ID_OLD.Length;
                // Make sure total size is a multiple of 4
                size = size - size % 4;

                using MemoryStream outStream = new MemoryStream(size);
                StreamUtils.CopyStream(source, outStream, size);
                byte[] encodedData = outStream.ToArray();

                PictureInfo picInfo = PictureInfo.fromBinaryData(Utils.DecodeFrom64(encodedData), picType, getImplementedTagType(), 0, picturePosition);
                tagData.Pictures.Add(picInfo);
            }
        }

        public void ReadPicture(Stream s, ReadTagParams readTagParams)
        {
            long initPosition = s.Position;
            VorbisMetaDataBlockPicture block = ReadMetadataBlockPicture(s);

            var picturePosition = block.picType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? takePicturePosition(getImplementedTagType(), (byte)block.nativePicCode) : takePicturePosition(block.picType);

            if (!readTagParams.ReadPictures) return;

            s.Seek(initPosition + block.picDataOffset, SeekOrigin.Begin);
            PictureInfo picInfo = PictureInfo.fromBinaryData(s, block.picDataLength, block.picType, getImplementedTagType(), block.nativePicCode, picturePosition);
            picInfo.Description = block.description;
            tagData.Pictures.Add(picInfo);
        }

        // FLAC-specific override to handle multiple tags being authorized (e.g. representing multiple artists with multiple ARTIST tags)
        public new void SetMetaField(string ID, string data, bool readAllMetaFrames, string zone = DEFAULT_ZONE_NAME, byte tagVersion = 0, ushort streamNumber = 0, string language = "")
        {
            // Finds the ATL field identifier
            Field supportedMetaID = getFrameMapping(zone, ID, tagVersion);

            // If ID has been mapped with an 'classic' ATL field, store it in the dedicated place...
            if (supportedMetaID != Field.NO_FIELD)
            {
                string targetData = data;
                if (tagData.hasKey(supportedMetaID)) // If the value already exists, concatenate it with the new one
                {
                    targetData = tagData[supportedMetaID] + Settings.InternalValueSeparator + data;
                }
                base.SetMetaField(ID, targetData, readAllMetaFrames, zone, tagVersion, streamNumber, language);
            }
            else if (readAllMetaFrames && ID.Length > 0) // ...else store it in the additional fields Dictionary
            {
                MetaFieldInfo fieldInfo = new MetaFieldInfo(getImplementedTagType(), ID, data, streamNumber, language, zone);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Prevent duplicates
                {
                    // If the value already exists, concatenate it with the new one
                    foreach (var info in tagData.AdditionalFields.Where(info => info.Equals(fieldInfo)))
                    {
                        fieldInfo.Value = info.Value + Settings.InternalValueSeparator + fieldInfo.Value;
                    }

                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                tagData.AdditionalFields.Add(fieldInfo);
            }
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            int nbFields = 0;
            int index = 0;

            // ResetData(); <-- no; that calls resets image data when VorbisTag is managed by FLAC. ResetData has to be called manually by using Clear
            // Resets stuff anyway
            initialPaddingOffset = -1;
            initialPaddingSize = 0;

            // TODO - check if still useful
            if (readTagParams.PrepareForWriting && !readTagParams.ReadPictures)
            {
                readTagParams.ReadPictures = true;
            }

            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            initialTagOffset = reader.Position;
            do
            {
                var size = reader.ReadInt32();
                var position = reader.Position;

                string strData;
                if (0 == index) // Mandatory : first metadata has to be the Vorbis vendor string
                {
                    strData = Encoding.UTF8.GetString(reader.ReadBytes(size)).Trim();
                    if (strData.Length > 0) SetMetaField(VENDOR_METADATA_ID, strData, readTagParams.ReadAllMetaFrames);
                }
                else
                {
                    const int KEY_BUFFER = 20;
                    StringBuilder tagIdBuilder = new StringBuilder();
                    byte[] stringData = new byte[KEY_BUFFER];
                    int equalsIndex = -1;
                    int nbBuffered = -1;

                    while (-1 == equalsIndex)
                    {
                        int nbRead = reader.Read(stringData, 0, KEY_BUFFER);
                        nbBuffered++;

                        for (int i = 0; i < nbRead; i++)
                        {
                            if (stringData[i] != 0x3D) continue; // '=' character
                            equalsIndex = i;
                            break;
                        }

                        tagIdBuilder.Append(Utils.Latin1Encoding.GetString(stringData, 0, (-1 == equalsIndex) ? nbRead : equalsIndex));
                    }
                    equalsIndex += KEY_BUFFER * nbBuffered;
                    reader.Seek(position + equalsIndex + 1, SeekOrigin.Begin);

                    string tagId = tagIdBuilder.ToString().ToUpper();

                    if (tagId.Equals(PICTURE_METADATA_ID_NEW) || tagId.Equals(PICTURE_METADATA_ID_OLD))
                    {
                        SetPictureItem(reader, tagId, size, readTagParams);
                    }
                    else
                    {
                        strData = Encoding.UTF8.GetString(reader.ReadBytes(size - equalsIndex - 1)).Trim();

                        if (tagId.StartsWith("CHAPTER", StringComparison.OrdinalIgnoreCase)) // Chapter description
                        {
                            setChapterData(tagId, strData);
                        }
                        else // Standard textual field
                        {
                            SetMetaField(tagId, strData, readTagParams.ReadAllMetaFrames);
                        }
                    }
                }
                reader.Seek(position + size, SeekOrigin.Begin);

                if (0 == index) nbFields = reader.ReadInt32();

                index++;
            } while (index <= nbFields);

            structureHelper.AddZone(initialTagOffset, (int)(reader.Position - initialTagOffset), hasCoreSignature ? OGG_CORE_SIGNATURE : Array.Empty<byte>());

            if (!readTagParams.PrepareForWriting) return true;

            // Skip framing bit
            if (reader.PeekChar()) reader.Seek(1, SeekOrigin.Current);

            long streamPos = reader.Position;

            // Prod to see if there's padding after the last field
            if (streamPos + 4 >= reader.Length || 0 != reader.ReadInt32()) return true;

            initialPaddingOffset = streamPos;
            initialPaddingSize = StreamUtils.TraversePadding(reader) - initialPaddingOffset;
            tagData.PaddingSize = initialPaddingSize;

            return true;
        }

        /// <summary>
        /// Add the specified information to current tag information (async variant)
        ///   - Any existing field is overwritten
        ///   - Any non-specified field is kept as is
        /// NB : That method needs to have that signature to prevent Ogg.WriteAsync from calling MetaDataIO.WriteAsync instead
        /// </summary>
        /// <param name="s">Stream for the resource to edit</param>
        /// <param name="tag">Tag information to be added</param>
        /// <param name="args">Writing parameters</param>
        /// <param name="writeProgress">Progress to be updated during write operations</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public new Task<int> WriteAsync(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null)
        {
            TagData dataToWrite = tagData;
            dataToWrite.IntegrateValues(tag, writePicturesWithMetadata); // Write existing information + new tag information
            dataToWrite.Cleanup();

            // Write new tag to a MemoryStream
            var result = write(s, dataToWrite);
            if (result > -1) tagData.IntegrateValues(dataToWrite); // TODO - Isn't that a bit too soon ?
            return Task.FromResult(result);
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            return write(s, tag);
        }

        private int write(Stream w, TagData tag)
        {
            long initialWriteOffset = w.Position;

            // Even when no existing field, vendor field is mandatory in OGG structure
            // => a file with no vendor is a FLAC file
            string vendor = AdditionalFields.ContainsKey(VENDOR_METADATA_ID) ? AdditionalFields[VENDOR_METADATA_ID] : "";

            w.Write(StreamUtils.EncodeUInt32((uint)vendor.Length));
            w.Write(Encoding.UTF8.GetBytes(vendor));

            var counterPos = w.Position;
            w.Write(StreamUtils.EncodeUInt32(0)); // Tag counter placeholder to be rewritten in a few lines

            var counter = writeFrames(w, tag);

            if (writeMetadataFramingBit) w.WriteByte(1); // Framing bit (mandatory for OGG container)

            // PADDING MANAGEMENT
            // Write the remaining padding bytes, if any detected during initial reading
            if (managePadding)
            {
                long paddingSizeToWrite;
                if (tag.PaddingSize > -1) paddingSizeToWrite = tag.PaddingSize;
                else paddingSizeToWrite = TrackUtils.ComputePaddingSize(initialPaddingOffset, initialPaddingSize, initialPaddingOffset - initialTagOffset, w.Position - initialWriteOffset);
                if (paddingSizeToWrite > 0)
                    for (int i = 0; i < paddingSizeToWrite; i++) w.WriteByte(0);
            }

            long finalPos = w.Position;
            w.Seek(counterPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeUInt32(counter));
            w.Seek(finalPos, SeekOrigin.Begin);

            return (int)counter;
        }

        private uint writeFrames(Stream w, TagData tag)
        {
            uint nbFrames = 0;

            IDictionary<Field, string> map = tag.ToMap();
            // Keep these in memory to prevent setting them twice using AdditionalFields
            var writtenFieldCodes = new HashSet<string>();

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    if (frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // Don't write frames with empty values
                        {
                            string value = formatBeforeWriting(frameType, tag, map);
                            if (value.Contains(Settings.DisplayValueSeparator + ""))
                            {
                                // Write multiple fields when there are multiple values (specific to VorbisTag)
                                string[] valueParts = value.Split(Settings.DisplayValueSeparator);
                                foreach (string valuePart in valueParts) writeTextFrame(w, s, valuePart);
                                nbFrames += (uint)valueParts.Length;
                            }
                            else
                            {
                                writeTextFrame(w, s, value);
                                nbFrames++;
                            }
                            writtenFieldCodes.Add(s.ToUpper());
                        }
                        break;
                    }
                }
            }

            // Chapters
            if (Chapters.Count > 0) writeChapters(w, Chapters);

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if (fieldInfo.NativeFieldCode.Equals(VENDOR_METADATA_ID)
                    || writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToUpper())) continue;

                string value = FormatBeforeWriting(fieldInfo.Value);
                if (value.Contains(Settings.DisplayValueSeparator + ""))
                {
                    // Write multiple fields when there are multiple values (specific to VorbisTag)
                    string[] valueParts = value.Split(Settings.DisplayValueSeparator);
                    foreach (string valuePart in valueParts) writeTextFrame(w, fieldInfo.NativeFieldCode, valuePart);
                    nbFrames += (uint)valueParts.Length;
                }
                else
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, value);
                    nbFrames++;
                }
            }

            // Picture fields
            if (!writePicturesWithMetadata) return nbFrames;

            foreach (PictureInfo picInfo in tag.Pictures.Where(isPictureWritable))
            {
                writePictureFrame(w, picInfo.PictureData, picInfo.MimeType, picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? picInfo.NativePicCode : ID3v2.EncodeID3v2PictureType(picInfo.PicType), picInfo.Description);
                nbFrames++;
            }

            return nbFrames;
        }

        private static void writeChapters(Stream w, IList<ChapterInfo> chapters)
        {
            int masterChapterIndex = 0;
            foreach (ChapterInfo chapterInfo in chapters)
            {
                // Take the valued index if existing; take the current numerical index if not
                var chapterIndex = 0 == chapterInfo.UniqueNumericID
                    ? masterChapterIndex++
                    : (int)chapterInfo.UniqueNumericID;
                // Specs says chapter index if formatted over 3 chars
                var formattedIndex = Utils.BuildStrictLengthString(chapterIndex, 3, '0', false);
                writeTextFrame(w, "CHAPTER" + formattedIndex, Utils.EncodeTimecode_ms(chapterInfo.StartTime));
                if (chapterInfo.Title.Length > 0) writeTextFrame(w, "CHAPTER" + formattedIndex + "NAME", chapterInfo.Title);
                if (chapterInfo.Url != null && chapterInfo.Url.Url.Length > 0)
                    writeTextFrame(w, "CHAPTER" + formattedIndex + "URL", chapterInfo.Url.Url);
            }
        }

        private static void writeTextFrame(Stream w, string frameCode, string text)
        {
            var frameSizePos = w.Position;
            w.Write(StreamUtils.EncodeUInt32(0)); // Frame size placeholder to be rewritten in a few lines

            // TODO : handle multi-line comments : comment[0], comment[1]...
            w.Write(Utils.Latin1Encoding.GetBytes(frameCode + "="));
            w.Write(Encoding.UTF8.GetBytes(text));

            // Go back to frame size location to write its actual size 
            var finalFramePos = w.Position;
            w.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeUInt32((uint)(finalFramePos - frameSizePos - 4)));
            w.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private static void writePictureFrame(Stream w, byte[] pictureData, string mimeType, int pictureTypeCode, string picDescription)
        {
            var frameSizePos = w.Position;
            w.Write(StreamUtils.EncodeInt32(0)); // Frame size placeholder to be rewritten in a few lines

            w.Write(Utils.Latin1Encoding.GetBytes(PICTURE_METADATA_ID_NEW + "="));

            using (MemoryStream picStream = new MemoryStream(pictureData.Length + 60))
            {
                WritePicture(picStream, pictureData, mimeType, pictureTypeCode, picDescription);
                w.Write(Utils.EncodeTo64(picStream.ToArray()));
            }

            // Go back to frame size location to write its actual size 
            var finalFramePos = w.Position;
            w.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeUInt32((uint)(finalFramePos - frameSizePos - 4)));
            w.Seek(finalFramePos, SeekOrigin.Begin);
        }

        public static void WritePicture(Stream w, byte[] pictureData, string mimeType, int pictureTypeCode, string picDescription)
        {
            w.Write(StreamUtils.EncodeBEInt32(pictureTypeCode));
            w.Write(StreamUtils.EncodeBEInt32(mimeType.Length));
            w.Write(Utils.Latin1Encoding.GetBytes(mimeType));
            w.Write(StreamUtils.EncodeBEInt32(picDescription.Length));
            w.Write(Encoding.UTF8.GetBytes(picDescription));

            ImageProperties props = ImageUtils.GetImageProperties(pictureData);

            w.Write(StreamUtils.EncodeBEInt32(props.Width));
            w.Write(StreamUtils.EncodeBEInt32(props.Height));
            w.Write(StreamUtils.EncodeBEInt32(props.ColorDepth));
            w.Write(props.Format.Equals(ImageFormat.Gif)
                ? StreamUtils.EncodeBEInt32(props.NumColorsInPalette)
                : StreamUtils.EncodeBEInt32(0)); // Color num

            w.Write(StreamUtils.EncodeBEInt32(pictureData.Length));
            w.Write(pictureData);
        }

        public TagData GetDeletionTagData()
        {
            TagData tag = new TagData();

            foreach (Field b in frameMapping.Values)
            {
                tag.IntegrateValue(b, "");
            }

            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo)
                {
                    MarkedForDeletion = true
                };
                tag.AdditionalFields.Add(emptyFieldInfo);
            }

            return tag;
        }
    }
}
