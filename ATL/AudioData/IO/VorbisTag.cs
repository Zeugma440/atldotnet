using Commons;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Vorbistags manipulation
    /// </summary>
    class VorbisTag : MetaDataIO
    {
        private const string PICTURE_METADATA_ID_NEW = "METADATA_BLOCK_PICTURE";
        private const string PICTURE_METADATA_ID_OLD = "COVERART";

        private const string VENDOR_METADATA_ID = "VENDOR";

        // "Xiph.Org libVorbis I 20150105" vendor with zero fields
        private static readonly byte[] CORE_SIGNATURE = new byte[43] { 34, 0, 0, 0, 88, 105, 112, 104, 46, 79, 114, 103, 32, 108, 105, 98, 86, 111, 114, 98, 105, 115, 32, 73, 32, 50, 48, 49, 53, 48, 49, 48, 53, 32, 40, 63, 63, 93, 0, 0, 0, 0, 1 };


        // Reference : https://xiph.org/flac/format.html#metadata_block_picture
        public class VorbisMetaDataBlockPicture
        {
            public TagData.PIC_TYPE picType;
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


        // ---------- CONSTRUCTORS & INITIALIZERS

        static VorbisTag()
        {
            frameMapping = new Dictionary<string, byte>
            {
                { "DESCRIPTION", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
                { "TITLE", TagData.TAG_FIELD_TITLE },
                { "ARTIST", TagData.TAG_FIELD_ARTIST },
                { "CONDUCTOR", TagData.TAG_FIELD_CONDUCTOR },
                { "ALBUM", TagData.TAG_FIELD_ALBUM },
                { "TRACKNUMBER", TagData.TAG_FIELD_TRACK_NUMBER },
                { "DISCNUMBER", TagData.TAG_FIELD_DISC_NUMBER },
                { "DATE", TagData.TAG_FIELD_RECORDING_DATE },
                { "COMMENT", TagData.TAG_FIELD_COMMENT },
                { "COMPOSER", TagData.TAG_FIELD_COMPOSER },
                { "RATING", TagData.TAG_FIELD_RATING },
                { "GENRE", TagData.TAG_FIELD_GENRE },
                { "COPYRIGHT", TagData.TAG_FIELD_COPYRIGHT },
                { "PUBLISHER", TagData.TAG_FIELD_PUBLISHER }
            };
        }

        protected override void resetSpecificData()
        {
            // Nothing special to reset here
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

        private void setMetaField(string FieldName, string FieldValue, bool readAllMetaFrames)
        {
            byte supportedMetaId = 255;
            FieldName = FieldName.ToUpper();

            // Finds the ATL field identifier according to the APE version
            if (frameMapping.ContainsKey(FieldName)) supportedMetaId = frameMapping[FieldName];

            TagData.MetaFieldInfo fieldInfo;
            // If ID has been mapped with an ATL field, store it in the dedicated place...
            if (supportedMetaId < 255)
            {
                tagData.IntegrateValue(supportedMetaId, FieldValue);
            }
            else if (readAllMetaFrames) // ...else store it in the additional fields Dictionary
            {
                fieldInfo = new TagData.MetaFieldInfo(getImplementedTagType(), FieldName, FieldValue);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Replace current value, since there can be no duplicate fields in APE
                {
                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                tagData.AdditionalFields.Add(fieldInfo);
            }
        }

        // Reads large data chunks by streaming
        private void SetExtendedTagItem(Stream Source, int size, ReadTagParams readTagParams)
        {
            string tagId = "";
            int IdSize = 1;
            char c = StreamUtils.ReadOneByteChar(Source);

            while (c != '=')
            {
                tagId += c;
                IdSize++;
                c = StreamUtils.ReadOneByteChar(Source);
            }

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

                int picturePosition;
                MemoryStream mem = new MemoryStream(Utils.DecodeFrom64(encodedData));
                mem.Seek(0, SeekOrigin.Begin);

                VorbisMetaDataBlockPicture block = ReadMetadataBlockPicture(mem);

                if (block.picType.Equals(TagData.PIC_TYPE.Unsupported))
                {
                    addPictureToken(getImplementedTagType(), (byte)block.nativePicCode);
                    picturePosition = takePicturePosition(getImplementedTagType(), (byte)block.nativePicCode);
                }
                else
                {
                    addPictureToken(block.picType);
                    picturePosition = takePicturePosition(block.picType);
                }

                if (readTagParams.PictureStreamHandler != null)
                {
                    MemoryStream picMem = new MemoryStream(block.picDataLength);
                    mem.Seek(block.picDataOffset, SeekOrigin.Begin);
                    StreamUtils.CopyStream(mem, picMem, block.picDataLength);

                    picMem.Seek(0, SeekOrigin.Begin);
                    readTagParams.PictureStreamHandler(ref picMem, block.picType, Utils.GetImageFormatFromMimeType(block.mimeType), getImplementedTagType(), block.nativePicCode, picturePosition);

                    picMem.Close();
                }
                mem.Close();
            }
            else if (tagId.Equals(PICTURE_METADATA_ID_OLD)) // Deprecated picture info
            {
                int picturePosition = takePicturePosition(TagData.PIC_TYPE.Generic);
                TagData.PictureInfo picInfo = new TagData.PictureInfo(null, TagData.PIC_TYPE.Generic, picturePosition);

                if (readTagParams.PictureStreamHandler != null)
                {
                    size = size - 1 - PICTURE_METADATA_ID_OLD.Length;
                    // Make sure total size is a multiple of 4
                    size = size - (size % 4);

                    byte[] encodedData = new byte[size];
                    Source.Read(encodedData, 0, size);

                    // Read the whole base64-encoded picture binary data
                    MemoryStream mem = new MemoryStream(Utils.DecodeFrom64(encodedData));

                    mem.Seek(0, SeekOrigin.Begin);
                    byte[] imgHeader = new byte[3];
                    mem.Read(imgHeader, 0, 3);
                    ImageFormat imgFormat = Utils.GetImageFormatFromPictureHeader(imgHeader);
                    mem.Seek(0, SeekOrigin.Begin);

                    readTagParams.PictureStreamHandler(ref mem, TagData.PIC_TYPE.Generic, imgFormat, getImplementedTagType(), 0, picturePosition);
                    mem.Close();
                }
            }
        }

        // ---------------------------------------------------------------------------

        public override bool Read(BinaryReader Source, ReadTagParams readTagParams)
        {
            int index, size;
            long initialPos, position, fieldCounterPos;
            string strData;
            int nbFields = 0;
            bool result = true;

            // Read Vorbis tag
            index = 0;
            fieldCounterPos = 0;
            tagExists = true;

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
                        setMetaField(strData.Substring(0, strIndex), strData.Substring(strIndex + 1, strData.Length - strIndex - 1), readTagParams.ReadAllMetaFrames);
                    }
                    else if (0 == index) // Mandatory : first metadata has to be the Vorbis vendor string
                    {
                        setMetaField(VENDOR_METADATA_ID, strData, readTagParams.ReadAllMetaFrames);
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

            structureHelper.AddZone(initialPos, (int)(Source.BaseStream.Position - initialPos),CORE_SIGNATURE);

            return result;
        }

        // TODO DOC
        // Simplified implementation of MetaDataIO tweaked for OGG-Vorbis specifics, i.e.
        //  - tag spans over multiple pages, each having its own header
        //  - last page may include whole or part of 3rd Vorbis header (setup header)
        public override bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            long oldTagSize;
            long newTagSize;
            long cumulativeDelta = 0;
            bool result = true;

            tagData.Pictures.Clear();

            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true);
            readTagParams.PrepareForWriting = true;
            this.Read(r, readTagParams);

            TagData dataToWrite;
            dataToWrite = tagData;
            dataToWrite.IntegrateValues(tag); // Write existing information + new tag information

            Zone zone = structureHelper.GetZone(FileStructureHelper.DEFAULT_ZONE_NAME);

            oldTagSize = zone.Size;

            // Write new tag to a MemoryStream
            using (MemoryStream s = new MemoryStream(zone.Size))
            using (BinaryWriter msw = new BinaryWriter(s, Encoding.UTF8))
            {
                if (write(dataToWrite, msw, zone.Name))
                {
                    newTagSize = s.Length;
                }
                else
                {
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
                }
                else
                {
                    if (zone.CoreSignature.Length > 0) msw.Write(zone.CoreSignature);
                }

                int delta = (int)(newTagSize - oldTagSize);
                cumulativeDelta += delta;
            }
            tagData = dataToWrite;

            return result;
        }

        protected override bool write(TagData tag, BinaryWriter w, string zone)
        {
            long counterPos;
            uint counter = 0;
            bool result = true;
            string vendor = AdditionalFields[VENDOR_METADATA_ID];

            w.Write((uint)vendor.Length);
            w.Write(Encoding.UTF8.GetBytes(vendor));

            counterPos = w.BaseStream.Position;
            w.Write((uint)0); // Tag counter placeholder to be rewritten in a few lines

            counter = writeFrames(ref tag, ref w);

            w.Write((byte)1); // Mandatory framing bit

            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);

            return result;
        }

        private uint writeFrames(ref TagData tag, ref BinaryWriter w)
        {
            bool doWritePicture;
            uint nbFrames = 0;

            // Picture fields (first before textual fields, since APE tag is located on the footer)
            foreach (TagData.PictureInfo picInfo in tag.Pictures)
            {
                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !picInfo.PicType.Equals(TagData.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                if (doWritePicture)
                {
                    writePictureFrame(ref w, picInfo.PictureData, picInfo.NativeFormat, Utils.GetMimeTypeFromImageFormat(picInfo.NativeFormat), picInfo.PicType.Equals(TagData.PIC_TYPE.Unsupported) ? picInfo.NativePicCode : ID3v2.EncodeID3v2PictureType(picInfo.PicType), "");
                    nbFrames++;
                }
            }

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
                            writeTextFrame(ref w, s, map[frameType]);
                            nbFrames++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (TagData.MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if (fieldInfo.TagType.Equals(getImplementedTagType()) && !fieldInfo.MarkedForDeletion)
                {
                    writeTextFrame(ref w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                    nbFrames++;
                }
            }

            return nbFrames;
        }

        private void writeTextFrame(ref BinaryWriter writer, String frameCode, String text)
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
            writer.Write(finalFramePos-frameSizePos-4);
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(ref BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, string mimeType, int pictureTypeCode, string picDescription)
        {
            long frameSizePos;
            long finalFramePos;

            frameSizePos = writer.BaseStream.Position;
            writer.Write((uint)0); // Frame size placeholder to be rewritten in a few lines

            writer.Write(StreamUtils.ReverseInt32(pictureTypeCode));
            writer.Write(StreamUtils.ReverseInt32(mimeType.Length));
            writer.Write(Utils.Latin1Encoding.GetBytes(mimeType));
            writer.Write(StreamUtils.ReverseInt32(picDescription.Length));
            writer.Write(Encoding.UTF8.GetBytes(picDescription));

            // TODO - write custom code to parse picture binary data instead of instanciating a .NET Image
            using (Image img = (Image)((new ImageConverter()).ConvertFrom(pictureData)))
            {
                writer.Write(StreamUtils.ReverseInt32(img.Width));
                writer.Write(StreamUtils.ReverseInt32(img.Height));
                writer.Write(StreamUtils.ReverseInt32(Image.GetPixelFormatSize(img.PixelFormat)));
                writer.Write(StreamUtils.ReverseInt32(img.Palette.Entries.Length));
            }

            byte[] base64PictureData = Utils.EncodeTo64(pictureData);

            writer.Write(StreamUtils.ReverseInt32(base64PictureData.Length));
            writer.Write(base64PictureData);

            // Go back to frame size location to write its actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write(finalFramePos - frameSizePos - 4);
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }
    }
}
