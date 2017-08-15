using Commons;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Vorbistags manipulation
    /// </summary>
    class VorbisTag : MetaDataIO
    {
        private const String PICTURE_METADATA_ID_NEW = "METADATA_BLOCK_PICTURE";
        private const String PICTURE_METADATA_ID_OLD = "COVERART";

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
            result.description = Utils.Latin1Encoding.GetString(r.ReadBytes(stringLen));
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
                // TODO document this treatment - why the 61 ?
                for (int i = 0; i < encodedData.Length; i++) if (0 == encodedData[i]) encodedData[i] = 61;

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
            long initialPos, position;
            string strData;
            int nbFields = 0;
            bool result = true;

            // Read Vorbis tag
            index = 0;
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
                        setMetaField(strData.Substring(0,strIndex), strData.Substring(strIndex+1,strData.Length-strIndex-1), readTagParams.ReadAllMetaFrames);
                    }
                }
                else // 'Large' field = picture
                {
                    SetExtendedTagItem(Source.BaseStream, size, readTagParams);
                }
                Source.BaseStream.Seek(position + size, SeekOrigin.Begin);
                if (0 == index) nbFields = Source.ReadInt32();

                index++;
            } while (index <= nbFields);

            structureHelper.AddZone(initialPos, (int)(Source.BaseStream.Position - initialPos));

            return result;
       }

        protected override bool write(TagData tag, BinaryWriter w, string zone)
        {
            throw new NotImplementedException();
        }
    }
}
