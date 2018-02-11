using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Vorbis tags manipulation
    /// 
    /// TODO - Rewrite as "pure" helper, with Ogg and FLAC inheriting MetaDataIO
    /// </summary>
    class VorbisTag : MetaDataIO
    {
        private const string PICTURE_METADATA_ID_NEW = "METADATA_BLOCK_PICTURE";
        private const string PICTURE_METADATA_ID_OLD = "COVERART";
        private const string VENDOR_METADATA_ID = "VENDOR";

        private const string VENDOR_DEFAULT_FLAC = "reference libFLAC 1.2.1 20070917";

        // "Xiph.Org libVorbis I 20150105" vendor with zero fields
        private static readonly byte[] CORE_SIGNATURE = new byte[43] { 34, 0, 0, 0, 88, 105, 112, 104, 46, 79, 114, 103, 32, 108, 105, 98, 86, 111, 114, 98, 105, 115, 32, 73, 32, 50, 48, 49, 53, 48, 49, 48, 53, 32, 40, 63, 63, 93, 0, 0, 0, 0, 1 };


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
        private static IDictionary<string, byte> frameMapping;
        
        // Tweak to prevent/allow pictures to be written within the rest of metadata (OGG vs. FLAC behaviour)
        private readonly bool writePicturesWithMetadata;
        // Tweak to prevent/allow framing bit to be written at the end of the metadata block (OGG vs. FLAC behaviour)
        private readonly bool writeMetadataFramingBit;
        // Tweak to enable/disable core signature (OGG vs. FLAC behaviour)
        private readonly bool hasCoreSignature;

        // ---------- CONSTRUCTORS & INITIALIZERS

        static VorbisTag()
        {
            frameMapping = new Dictionary<string, byte>
            {
                { "DESCRIPTION", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
                { "ARTIST", TagData.TAG_FIELD_ARTIST },
                { "TITLE", TagData.TAG_FIELD_TITLE },
                { "ALBUM", TagData.TAG_FIELD_ALBUM },
                { "DATE", TagData.TAG_FIELD_RECORDING_DATE },
                { "GENRE", TagData.TAG_FIELD_GENRE },
                { "COMPOSER", TagData.TAG_FIELD_COMPOSER },
                { "TRACKNUMBER", TagData.TAG_FIELD_TRACK_NUMBER },
                { "DISCNUMBER", TagData.TAG_FIELD_DISC_NUMBER },
                { "COMMENT", TagData.TAG_FIELD_COMMENT },
                { "ALBUMARTIST", TagData.TAG_FIELD_ALBUM_ARTIST },
                { "CONDUCTOR", TagData.TAG_FIELD_CONDUCTOR },
                { "RATING", TagData.TAG_FIELD_RATING },
                { "COPYRIGHT", TagData.TAG_FIELD_COPYRIGHT },
                { "PUBLISHER", TagData.TAG_FIELD_PUBLISHER }
            };
        }

        public VorbisTag(bool writePicturesWithMetadata, bool writeMetadataFramingBit, bool hasCoreSignature)
        {
            this.writePicturesWithMetadata = writePicturesWithMetadata;
            this.writeMetadataFramingBit = writeMetadataFramingBit;
            this.hasCoreSignature = hasCoreSignature;
            ResetData();
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        protected override byte ratingConvention
        {
            get { return RC_APE; }
        }

        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;
            ID = ID.ToUpper();

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }


        // ---------- SPECIFIC MEMBERS

        public static VorbisMetaDataBlockPicture ReadMetadataBlockPicture(Stream s)
        {
            VorbisMetaDataBlockPicture result = new VorbisMetaDataBlockPicture();
            int stringLen;

            BinaryReader r = new BinaryReader(s);
            result.nativePicCode = StreamUtils.ReverseInt32(r.ReadInt32());
            result.picType = ID3v2.DecodeID3v2PictureType(result.nativePicCode);
            stringLen = StreamUtils.ReverseInt32(r.ReadInt32());
            result.mimeType = Utils.Latin1Encoding.GetString(r.ReadBytes(stringLen));
            stringLen = StreamUtils.ReverseInt32(r.ReadInt32());
            result.description = Encoding.UTF8.GetString(r.ReadBytes(stringLen));
            result.width = StreamUtils.ReverseInt32(r.ReadInt32());
            result.height = StreamUtils.ReverseInt32(r.ReadInt32());
            result.colorDepth = StreamUtils.ReverseInt32(r.ReadInt32());
            result.colorNum = StreamUtils.ReverseInt32(r.ReadInt32());
            result.picDataLength = StreamUtils.ReverseInt32(r.ReadInt32());

            result.picDataOffset = 4 + 4 + result.mimeType.Length + 4 + result.description.Length + 4 + 4 + 4 + 4 + 4;

            return result;
        }

        private void setChapter(string fieldName, string fieldValue)
        {
            if (null == tagData.Chapters) tagData.Chapters = new List<ChapterInfo>();

            // Capture numeric sequence within field name
            // NB : Handled this way to retrieve badly formatted chapter indexes (e.g. CHAPTER2, CHAPTER02NAME...)
            int i = 7;
            while (i < fieldName.Length && char.IsDigit(fieldName[i])) i++;
            int chapterIndex = int.Parse(fieldName.Substring(7, i - 7));

            // Ensure there is a slot to record the chapter
            while (tagData.Chapters.Count < chapterIndex) tagData.Chapters.Add(new ChapterInfo());

            if (fieldName.EndsWith("NAME",StringComparison.OrdinalIgnoreCase)) // Chapter name
            {
                tagData.Chapters[chapterIndex - 1].Title = fieldValue;
            }
            else if (fieldName.EndsWith("URL", StringComparison.OrdinalIgnoreCase)) // Chapter url
            {
                tagData.Chapters[chapterIndex - 1].Url = fieldValue;
            }
            else // Chapter start time
            {
                int result = Utils.DecodeTimecodeToMs(fieldValue);
                if (-1 == result)
                {
                    Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_WARNING, "Invalid timecode for chapter " + chapterIndex + " : " + fieldValue);
                }
                else
                {
                    tagData.Chapters[chapterIndex - 1].StartTime = (uint)result;
                }
            }
        }

        // Reads large data chunks by streaming
        private void SetExtendedTagItem(Stream Source, int size, ReadTagParams readTagParams)
        {
            const int KEY_BUFFER = 20;
            string tagId = "";
            byte[] stringData = new byte[KEY_BUFFER];
            int equalsIndex = -1;

            while (-1 == equalsIndex)
            {
                Source.Read(stringData, 0, KEY_BUFFER);

                for (int i = 0; i < KEY_BUFFER; i++)
                {
                    if (stringData[i] == 0x3D) // '=' character
                    {
                        equalsIndex = i;
                        break;
                    }
                }

                tagId += Utils.Latin1Encoding.GetString(stringData, 0, (-1 == equalsIndex)?KEY_BUFFER:equalsIndex);
            }
            Source.Seek(-(KEY_BUFFER - equalsIndex - 1), SeekOrigin.Current);

            if (tagId.Equals(PICTURE_METADATA_ID_NEW))
            {
                size = size - 1 - PICTURE_METADATA_ID_NEW.Length;
                // Make sure total size is a multiple of 4
                size = size - (size % 4);

                // Read the whole base64-encoded picture header _and_ binary data
                byte[] encodedData = new byte[size];
                Source.Read(encodedData, 0, size);

                // Gets rid of unwanted zeroes
                // 0x3D ('=' char) is the padding neutral character that has to replace zero, which is not part of base64 range
                for (int i = 0; i < encodedData.Length; i++) if (0 == encodedData[i]) encodedData[i] = 0x3D;

                using (MemoryStream mem = new MemoryStream(Utils.DecodeFrom64(encodedData)))
                {
                    mem.Seek(0, SeekOrigin.Begin);
                    ReadPicture(mem, readTagParams);
                }
            }
            else if (tagId.Equals(PICTURE_METADATA_ID_OLD)) // Deprecated picture info
            {
                int picturePosition = takePicturePosition(PictureInfo.PIC_TYPE.Generic);
                PictureInfo picInfo = new PictureInfo(ImageFormat.Undefined, PictureInfo.PIC_TYPE.Generic, getImplementedTagType(), 0, picturePosition);

                if (readTagParams.ReadPictures || readTagParams.PictureStreamHandler != null)
                {
                    size = size - 1 - PICTURE_METADATA_ID_OLD.Length;
                    // Make sure total size is a multiple of 4
                    size = size - (size % 4);

                    byte[] encodedData = new byte[size];
                    Source.Read(encodedData, 0, size);

                    // Read the whole base64-encoded picture binary data
                    picInfo.PictureData = Utils.DecodeFrom64(encodedData);
                    ImageFormat imgFormat = ImageUtils.GetImageFormatFromPictureHeader(picInfo.PictureData);
                    if (ImageFormat.Unsupported == imgFormat) imgFormat = ImageFormat.Png;
                    picInfo.NativeFormat = imgFormat;

                    tagData.Pictures.Add(picInfo);

                    if (readTagParams.PictureStreamHandler != null)
                    {
                        MemoryStream mem = new MemoryStream(picInfo.PictureData);
                        readTagParams.PictureStreamHandler(ref mem, picInfo.PicType, picInfo.NativeFormat, picInfo.TagType, picInfo.NativePicCode, picInfo.Position);
                        mem.Close();
                    }
                }
            }
        }

        public void ReadPicture(Stream s, ReadTagParams readTagParams)
        {
            int picturePosition;
            long initPosition = s.Position;
            VorbisMetaDataBlockPicture block = ReadMetadataBlockPicture(s);

            if (block.picType.Equals(PictureInfo.PIC_TYPE.Unsupported))
            {
                addPictureToken(getImplementedTagType(), (byte)block.nativePicCode);
                picturePosition = takePicturePosition(getImplementedTagType(), (byte)block.nativePicCode);
            }
            else
            {
                addPictureToken(block.picType);
                picturePosition = takePicturePosition(block.picType);
            }

            if (readTagParams.ReadPictures || readTagParams.PictureStreamHandler != null)
            {
                PictureInfo picInfo = new PictureInfo(ImageUtils.GetImageFormatFromMimeType(block.mimeType), block.picType, getImplementedTagType(), block.nativePicCode, picturePosition);
                picInfo.Description = block.description;
                picInfo.PictureData = new byte[block.picDataLength];
                s.Seek(initPosition + block.picDataOffset, SeekOrigin.Begin);
                s.Read(picInfo.PictureData, 0, block.picDataLength);

                tagData.Pictures.Add(picInfo);

                if (!tagExists) tagExists = true;


                if (readTagParams.PictureStreamHandler != null)
                {
                    MemoryStream mem = new MemoryStream(picInfo.PictureData);
                    readTagParams.PictureStreamHandler(ref mem, picInfo.PicType, picInfo.NativeFormat, picInfo.TagType, picInfo.NativePicCode, picInfo.Position);
                    mem.Close();
                }
            }
        }

        protected override bool read(BinaryReader Source, ReadTagParams readTagParams)
        {
            int size;
            string strData;
            long initialPos, position;
            long fieldCounterPos = 0;
            int nbFields = 0;
            int index = 0;
            bool result = true;

            // Read Vorbis tag
            ResetData();

            /// TODO - check if still useful
            if (readTagParams.PrepareForWriting && !readTagParams.ReadPictures)
            {
                readTagParams.ReadPictures = true;
            }

            initialPos = Source.BaseStream.Position;
            do
            {
                size = Source.ReadInt32();

                position = Source.BaseStream.Position;
                if (size < 500) // 'Small' field
                {
                    strData = Encoding.UTF8.GetString(Source.ReadBytes(size)).Trim();

                    int strIndex = strData.IndexOf('=');
                    if (strIndex > -1 && strIndex < strData.Length)
                    {
                        string fieldId = strData.Substring(0, strIndex);

                        if (fieldId.StartsWith("CHAPTER", StringComparison.OrdinalIgnoreCase)) // Chapter description
                        {
                            setChapter(fieldId, strData.Substring(strIndex + 1, strData.Length - strIndex - 1).Trim());
                        }
                        else // Standard textual field
                        {
                            SetMetaField(fieldId, strData.Substring(strIndex + 1, strData.Length - strIndex - 1).Trim(), readTagParams.ReadAllMetaFrames);
                        }
                    }
                    else if (0 == index) // Mandatory : first metadata has to be the Vorbis vendor string
                    {
                        SetMetaField(VENDOR_METADATA_ID, strData, readTagParams.ReadAllMetaFrames);
                    }
                }
                else // 'Large' field = picture
                {
                    SetExtendedTagItem(Source.BaseStream, size, readTagParams);
                }
                Source.BaseStream.Seek(position + size, SeekOrigin.Begin);

                if (0 == index)
                {
                    fieldCounterPos = Source.BaseStream.Position;
                    nbFields = Source.ReadInt32();
                }

                index++;
            } while (index <= nbFields);

            tagExists = (nbFields > 0); // If the only available field is the mandatory vendor field, tag is not considered existent
            structureHelper.AddZone(initialPos, (int)(Source.BaseStream.Position - initialPos), hasCoreSignature?CORE_SIGNATURE:new byte[0]);

            return result;
        }

        // TODO DOC
        public int Write(Stream s, TagData tag)
        {
            int result;
            TagData dataToWrite = tagData;
            dataToWrite.IntegrateValues(tag); // Write existing information + new tag information

            // Write new tag to a MemoryStream
            BinaryWriter msw = new BinaryWriter(s, Encoding.UTF8);

            result = write(dataToWrite, msw, DEFAULT_ZONE_NAME);

            if (result > -1) tagData = dataToWrite; // TODO - Isn't that a bit too soon ?

            return result;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            long counterPos;
            uint counter = 0;
            string vendor;

            if (AdditionalFields.ContainsKey(VENDOR_METADATA_ID))
            {
                vendor = AdditionalFields[VENDOR_METADATA_ID];
            } else
            {
                // Even when no existing field, vendor field is mandatory in OGG structure
                // => a file with no vendor is a FLAC file
                vendor = VENDOR_DEFAULT_FLAC;
            }

            w.Write((uint)vendor.Length);
            w.Write(Encoding.UTF8.GetBytes(vendor));

            counterPos = w.BaseStream.Position;
            w.Write((uint)0); // Tag counter placeholder to be rewritten in a few lines

            counter = writeFrames(tag, w);

            if (writeMetadataFramingBit) w.Write((byte)1); // Framing bit (mandatory for OGG container)

            // NB : Foobar2000 adds a padding block of 2048 bytes here for OGG container, regardless of the type or size of written fields
            if (Settings.EnablePadding) for (int i=0; i<2048;i++) w.Write((byte)0);

            long finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);
            w.BaseStream.Seek(finalPos, SeekOrigin.Begin);

            return (int)counter;
        }

        private uint writeFrames(TagData tag, BinaryWriter w)
        {
            bool doWritePicture;
            uint nbFrames = 0;

            IDictionary<byte, String> map = tag.ToMap();

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    if (frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = map[frameType];
                            if (TagData.TAG_FIELD_RATING == frameType) value = TrackUtils.EncodePopularity(value, ratingConvention).ToString();

                            writeTextFrame(w, s, value);
                            nbFrames++;
                        }
                        break;
                    }
                }
            }

            // Chapters
            if (Chapters.Count > 0)
            {
                writeChapters(w, Chapters);
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion && !fieldInfo.NativeFieldCode.Equals(VENDOR_METADATA_ID))
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                    nbFrames++;
                }
            }

            // Picture fields
            if (writePicturesWithMetadata)
            {
                foreach (PictureInfo picInfo in tag.Pictures)
                {
                    // Picture has either to be supported, or to come from the right tag standard
                    doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                    if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                    // It also has not to be marked for deletion
                    doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                    if (doWritePicture)
                    {
                        writePictureFrame(w, picInfo.PictureData, picInfo.NativeFormat, ImageUtils.GetMimeTypeFromImageFormat(picInfo.NativeFormat), picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? picInfo.NativePicCode : ID3v2.EncodeID3v2PictureType(picInfo.PicType), picInfo.Description);
                        nbFrames++;
                    }
                }
            }

            return nbFrames;
        }

        private void writeChapters(BinaryWriter writer, IList<ChapterInfo> chapters)
        {
            String chapterIndex;

            for (int i=0; i<chapters.Count; i++)
            {
                chapterIndex = Utils.BuildStrictLengthString((i + 1).ToString(), 3, '0', false);
                writeTextFrame(writer, "CHAPTER" + chapterIndex, Utils.EncodeTimecode_ms(chapters[i].StartTime));
                if (chapters[i].Title.Length > 0) writeTextFrame(writer, "CHAPTER" + chapterIndex + "NAME", chapters[i].Title);
                if (chapters[i].Title.Length > 0) writeTextFrame(writer, "CHAPTER" + chapterIndex + "URL", chapters[i].Url);
            }
        }

        private void writeTextFrame(BinaryWriter writer, String frameCode, String text)
        {
            long frameSizePos;
            long finalFramePos;

            frameSizePos = writer.BaseStream.Position;
            writer.Write((uint)0); // Frame size placeholder to be rewritten in a few lines

            // TODO : handle multi-line comments : comment[0], comment[1]...
            writer.Write(Utils.Latin1Encoding.GetBytes(frameCode+"="));
            writer.Write(Encoding.UTF8.GetBytes(text));

            // Go back to frame size location to write its actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write((uint)(finalFramePos-frameSizePos-4));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, string mimeType, int pictureTypeCode, string picDescription)
        {
            long frameSizePos;
            long finalFramePos;

            frameSizePos = writer.BaseStream.Position;
            writer.Write((uint)0); // Frame size placeholder to be rewritten in a few lines

            writer.Write(Utils.Latin1Encoding.GetBytes(PICTURE_METADATA_ID_NEW + "="));

            using (MemoryStream picStream = new MemoryStream(pictureData.Length + 60))
            using (BinaryWriter picW = new BinaryWriter(picStream))
            {
                WritePicture(picW, pictureData, picFormat, mimeType, pictureTypeCode, picDescription);
                writer.Write(Utils.EncodeTo64(picStream.ToArray()));
            }

            // Go back to frame size location to write its actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write((uint)(finalFramePos - frameSizePos - 4));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        public void WritePicture(BinaryWriter picW, byte[] pictureData, ImageFormat picFormat, string mimeType, int pictureTypeCode, string picDescription)
        {
            picW.Write(StreamUtils.ReverseInt32(pictureTypeCode));
            picW.Write(StreamUtils.ReverseInt32(mimeType.Length));
            picW.Write(Utils.Latin1Encoding.GetBytes(mimeType));
            picW.Write(StreamUtils.ReverseInt32(picDescription.Length));
            picW.Write(Encoding.UTF8.GetBytes(picDescription));

            ImageProperties props = ImageUtils.GetImageProperties(pictureData);

            picW.Write(StreamUtils.ReverseInt32(props.Width));
            picW.Write(StreamUtils.ReverseInt32(props.Height));
            picW.Write(StreamUtils.ReverseInt32(props.ColorDepth));
            if (props.Format.Equals(ImageFormat.Gif))
            {
                picW.Write(StreamUtils.ReverseInt32(props.NumColorsInPalette));    // Color num
            } else
            {
                picW.Write((int)0);
            }

            picW.Write(StreamUtils.ReverseInt32(pictureData.Length));
            picW.Write(pictureData);
        }

        public TagData GetDeletionTagData()
        {
            TagData tag = new TagData();

            foreach (byte b in frameMapping.Values)
            {
                tag.IntegrateValue(b, "");
            }

            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                if (!fieldInfo.NativeFieldCode.Equals(VENDOR_METADATA_ID))
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    tag.AdditionalFields.Add(emptyFieldInfo);
                }
            }

            return tag;
        }
    }
}
