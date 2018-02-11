using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for APEtag 1.0 and 2.0 tags manipulation
    /// </summary>
	public class APEtag : MetaDataIO
    {
        // Tag ID
        public const String APE_ID = "APETAGEX";                            // APE

        // Size constants
        public const byte APE_TAG_FOOTER_SIZE = 32;                         // APE tag footer
        public const byte APE_TAG_HEADER_SIZE = 32;                         // APE tag header

        // First version of APE tag
        public const int APE_VERSION_1_0 = 1000;
        public const int APE_VERSION_2_0 = 2000;

        // List of standard fields
        private static ICollection<string> standardFrames;

        // Mapping between APE field IDs and ATL fields
        private static IDictionary<string, byte> frameMapping;


        // APE tag data - for internal use
        private class TagInfo
        {
            // Real structure of APE footer
            public char[] ID = new char[8];                              // Always "APETAGEX"
            public int Version;                                                // Tag version
            public int Size;                   // Tag size including footer, excluding header
            public int FrameCount;                                        // Number of fields
            public int Flags;                                                    // Tag flags
            public char[] Reserved = new char[8];                  // Reserved for later use
                                                                            // Extended data
            public byte DataShift;                                 // Used if ID3v1 tag found
            public long FileSize;		                                 // File size (bytes)

            public void Reset()
            {
                Array.Clear(ID, 0, ID.Length);
                Version = 0;
                Flags = 0;
                FrameCount = 0;
                Size = 0;
                Array.Clear(Reserved, 0, Reserved.Length);
                DataShift = 0;
                FileSize = 0;
            }
        }

        static APEtag()
        {
            standardFrames = new List<string>() { "Title", "Artist", "Album", "Track", "Year", "Genre", "Comment", "Copyright", "Composer", "rating", "preference", "Discnumber", "Album Artist", "Conductor", "Disc", "Albumartist" };

            // Mapping between standard ATL fields and APE identifiers
            /*
             * Note : APE tag standard being a little loose, field codes vary according to the various implementations that have been made
             * => Some fields can be found in multiple frame code variants
             *      - Rating : "rating", "preference" frames
             *      - Disc number : "disc", "discnumber" frames
             *      - Album Artist : "albumartist", "album artist" frames
             */
            frameMapping = new Dictionary<string, byte>();

            frameMapping.Add("TITLE", TagData.TAG_FIELD_TITLE);
            frameMapping.Add("ARTIST", TagData.TAG_FIELD_ARTIST);
            frameMapping.Add("ALBUM", TagData.TAG_FIELD_ALBUM);
            frameMapping.Add("TRACK", TagData.TAG_FIELD_TRACK_NUMBER);
            frameMapping.Add("YEAR", TagData.TAG_FIELD_RECORDING_YEAR);
            frameMapping.Add("GENRE", TagData.TAG_FIELD_GENRE);
            frameMapping.Add("COMMENT", TagData.TAG_FIELD_COMMENT);
            frameMapping.Add("COPYRIGHT", TagData.TAG_FIELD_COPYRIGHT);
            frameMapping.Add("COMPOSER", TagData.TAG_FIELD_COMPOSER);
            frameMapping.Add("RATING", TagData.TAG_FIELD_RATING);
            frameMapping.Add("PREFERENCE", TagData.TAG_FIELD_RATING);
            frameMapping.Add("DISCNUMBER", TagData.TAG_FIELD_DISC_NUMBER);
            frameMapping.Add("DISC", TagData.TAG_FIELD_DISC_NUMBER);
            frameMapping.Add("ALBUMARTIST", TagData.TAG_FIELD_ALBUM_ARTIST);
            frameMapping.Add("ALBUM ARTIST", TagData.TAG_FIELD_ALBUM_ARTIST);
            frameMapping.Add("CONDUCTOR", TagData.TAG_FIELD_CONDUCTOR);
        }

        public APEtag()
        {
            // Create object		
            ResetData();
        }

        // --------------- MANDATORY INFORMATIVE OVERRIDES

        protected override int getDefaultTagOffset()
        {
            return TO_EOF;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_APE;
        }

        protected override byte ratingConvention
        {
            get { return RC_APE; }
        }

        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;
            ID = ID.Replace("\0", "").ToUpper();

            // Finds the ATL field identifier according to the APE version
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }

        // ********************* Auxiliary functions & voids ********************

        private bool readFooter(BinaryReader SourceFile, TagInfo Tag)
        {
            string tagID;
            bool result = true;

            // Load footer from file to variable
            Tag.FileSize = SourceFile.BaseStream.Length;

            // Check for existing ID3v1 tag in order to get the correct offset for APEtag packet
            SourceFile.BaseStream.Seek(Tag.FileSize - ID3v1.ID3V1_TAG_SIZE, SeekOrigin.Begin);
            tagID = Utils.Latin1Encoding.GetString(SourceFile.ReadBytes(3));
            if (ID3v1.ID3V1_ID.Equals(tagID)) Tag.DataShift = ID3v1.ID3V1_TAG_SIZE;

            // Read footer data
            SourceFile.BaseStream.Seek(Tag.FileSize - Tag.DataShift - APE_TAG_FOOTER_SIZE, SeekOrigin.Begin);

            Tag.ID = Utils.Latin1Encoding.GetChars(SourceFile.ReadBytes(8));
            if (StreamUtils.StringEqualsArr(APE_ID, Tag.ID))
            {
                Tag.Version = SourceFile.ReadInt32();
                Tag.Size = SourceFile.ReadInt32();
                Tag.FrameCount = SourceFile.ReadInt32();
                Tag.Flags = SourceFile.ReadInt32();
                Tag.Reserved = Utils.Latin1Encoding.GetChars(SourceFile.ReadBytes(8));
            }
            else
            {
                result = false;
            }

            return result;
        }

        private void readFrames(BinaryReader source, TagInfo Tag, MetaDataIO.ReadTagParams readTagParams)
        {
            string frameName;
            string strValue;
            int frameDataSize;
            long valuePosition;
            int frameFlags;

            source.BaseStream.Seek(Tag.FileSize - Tag.DataShift - Tag.Size, SeekOrigin.Begin);
            // Read all stored fields
            for (int iterator = 0; iterator < Tag.FrameCount; iterator++)
            {
                frameDataSize = source.ReadInt32();
                frameFlags = source.ReadInt32();
                frameName = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding); // Slightly more permissive than what APE specs indicate in terms of allowed characters ("Space(0x20), Slash(0x2F), Digits(0x30...0x39), Letters(0x41...0x5A, 0x61...0x7A)")

                valuePosition = source.BaseStream.Position;

                if ((frameDataSize > 0) && (frameDataSize <= 500))
                {
                    /* 
                     * According to spec : "Items are not zero-terminated like in C / C++.
                     * If there's a zero character, multiple items are stored under the key and the items are separated by zero characters."
                     * 
                     * => Values have to be splitted
                     */
                    strValue = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(frameDataSize)));
                    strValue = strValue.Replace('\0', Settings.InternalValueSeparator).Trim();
                    SetMetaField(frameName.Trim().ToUpper(), strValue, readTagParams.ReadAllMetaFrames);
                }
                else if (frameDataSize > 0) // Size > 500 => Probably an embedded picture
                {
                    int picturePosition;
                    PictureInfo.PIC_TYPE picType = decodeAPEPictureType(frameName);

                    if (picType.Equals(PictureInfo.PIC_TYPE.Unsupported))
                    {
                        addPictureToken(getImplementedTagType(), frameName);
                        picturePosition = takePicturePosition(getImplementedTagType(), frameName);
                    }
                    else
                    {
                        addPictureToken(picType);
                        picturePosition = takePicturePosition(picType);
                    }

                    if (readTagParams.ReadPictures || readTagParams.PictureStreamHandler != null)
                    {
                        // Description seems to be a null-terminated ANSI string containing 
                        //    * The frame name
                        //    * A byte (0x2E)
                        //    * The picture type (3 characters; similar to the 2nd part of the mime-type)
                        String description = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding); 
                        ImageFormat imgFormat = ImageUtils.GetImageFormatFromMimeType(description.Substring(description.Length-3,3));

                        PictureInfo picInfo = new PictureInfo(imgFormat, picType, getImplementedTagType(), frameName, picturePosition);
                        picInfo.Description = description;
                        picInfo.PictureData = new byte[frameDataSize - description.Length - 1];
                        source.BaseStream.Read(picInfo.PictureData, 0, frameDataSize - description.Length - 1);

                        tagData.Pictures.Add(picInfo);

                        if (readTagParams.PictureStreamHandler != null)
                        {
                            MemoryStream mem = new MemoryStream(picInfo.PictureData);
                            readTagParams.PictureStreamHandler(ref mem, picInfo.PicType, picInfo.NativeFormat, picInfo.TagType, picInfo.NativePicCode, picInfo.Position);
                            mem.Close();
                        }
                    }
                }
                source.BaseStream.Seek(valuePosition + frameDataSize, SeekOrigin.Begin);
            }
        }

        private static PictureInfo.PIC_TYPE decodeAPEPictureType(string picCode)
        {
            picCode = picCode.Trim().ToUpper();
            if ("COVER ART (FRONT)".Equals(picCode)) return PictureInfo.PIC_TYPE.Front;
            else if ("COVER ART (BACK)".Equals(picCode)) return PictureInfo.PIC_TYPE.Back;
            else if ("COVER ART (MEDIA)".Equals(picCode)) return PictureInfo.PIC_TYPE.CD;
            else return PictureInfo.PIC_TYPE.Unsupported;
        }

        private static string encodeAPEPictureType(PictureInfo.PIC_TYPE picCode)
        {
            if (PictureInfo.PIC_TYPE.Front.Equals(picCode)) return "Cover Art (Front)";
            else if (PictureInfo.PIC_TYPE.Back.Equals(picCode)) return "Cover Art (Back)";
            else if (PictureInfo.PIC_TYPE.CD.Equals(picCode)) return "Cover Art (Media)";
            else return "Cover Art (Other)";
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            TagInfo Tag = new TagInfo();

            // Reset data and load footer from file to variable
            ResetData();
            Tag.Reset();

            bool result = readFooter(source, Tag);

            // Process data if loaded and footer valid
            if (result)
            {
                int tagSize;
                long tagOffset;

                tagExists = true;
                // Fill properties with footer data
                tagVersion = Tag.Version;

                tagSize = Tag.Size;
                if (tagVersion > APE_VERSION_1_0) tagSize += 32; // Even though APE standard prevents from counting header in its size descriptor, ATL needs it
                tagOffset = Tag.FileSize - Tag.DataShift - Tag.Size;
                if (tagVersion > APE_VERSION_1_0) tagOffset -= 32; // Tag size does not include header size in APEv2

                structureHelper.AddZone(tagOffset, tagSize);

                // Get information from fields
                readFrames(source, Tag, readTagParams);
            }

            return result;
        }

        /// <summary>
        /// Writes the given tag into the given Writer using APEv2 conventions
        /// </summary>
        /// <param name="tag">Tag information to be written</param>
        /// <param name="w">Stream to write tag information to</param>
        /// <returns>True if writing operation succeeded; false if not</returns>
        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int tagSize;
            long tagSizePos;

            long itemCountPos;
            int itemCount;

            uint flags = 0xA0000000; // Flag for "tag contains a footer, a header, and this is the header"


            // ============
            // == HEADER ==
            // ============
            w.Write(APE_ID.ToCharArray());
            w.Write(APE_VERSION_2_0); // Version 2

            // Keep position in mind to calculate final size and come back here to write it
            tagSizePos = w.BaseStream.Position;
            w.Write((int)0); // Tag size placeholder to be rewritten in a few lines

            // Keep position in mind to calculate final item count and come back here to write it
            itemCountPos = w.BaseStream.Position;
            w.Write((int)0); // Item count placeholder to be rewritten in a few lines

            w.Write(flags);

            // Reserved space
            w.Write((long)0);


            // ============
            // == FRAMES ==
            // ============
            long dataPos = w.BaseStream.Position;
            itemCount = writeFrames(tag, w);

            // Record final size of tag into "tag size" field of header
            long finalTagPos = w.BaseStream.Position;
            w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
            tagSize = (int)(finalTagPos - dataPos) + 32; // 32 being the size of the header
            w.Write(tagSize);
            w.BaseStream.Seek(itemCountPos, SeekOrigin.Begin);
            w.Write(itemCount);
            w.BaseStream.Seek(finalTagPos, SeekOrigin.Begin);


            // ============
            // == FOOTER ==
            // ============
            w.Write(APE_ID.ToCharArray());
            w.Write(APE_VERSION_2_0); // Version 2

            w.Write(tagSize);
            w.Write(itemCount);

            flags = 0x80000000; // Flag for "tag contains a footer, a header, and this is the footer"
            w.Write(flags);

            // Reserved space
            w.Write((long)0);


            return itemCount;
        }

        private int writeFrames(TagData tag, BinaryWriter w)
        {
            bool doWritePicture;
            int nbFrames = 0;

            // Picture fields (first before textual fields, since APE tag is located on the footer)
            foreach (PictureInfo picInfo in tag.Pictures)
            {
                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                if (doWritePicture)
                {
                    writePictureFrame(w, picInfo.PictureData, picInfo.NativeFormat, ImageUtils.GetMimeTypeFromImageFormat(picInfo.NativeFormat), picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? picInfo.NativePicCodeStr : encodeAPEPictureType(picInfo.PicType), picInfo.Description);
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
                            string value = map[frameType];
                            if (TagData.TAG_FIELD_RATING == frameType) value = TrackUtils.EncodePopularity(value, ratingConvention).ToString();

                            writeTextFrame(w, s, value);
                            nbFrames++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion)
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                    nbFrames++;
                }
            }

            return nbFrames;
        }

        private void writeTextFrame(BinaryWriter writer, String frameCode, String text)
        {
            long frameSizePos;
            long finalFramePos;

            int frameFlags = 0x0000;

            frameSizePos = writer.BaseStream.Position;
            writer.Write((int)0); // Frame size placeholder to be rewritten in a few lines

            writer.Write(frameFlags);

            writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
            writer.Write('\0'); // String has to be null-terminated

            byte[] binaryValue = Encoding.UTF8.GetBytes(text);
            writer.Write(binaryValue);

            // Go back to frame size location to write its actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write(binaryValue.Length);
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, string mimeType, string pictureTypeCode, String picDescription)
        {
            // Binary tag writing management
            long frameSizePos;
            long finalFramePos;

            int frameFlags = 0x00000002; // This frame contains binary information (essential for pictures)

            frameSizePos = writer.BaseStream.Position;
            writer.Write((int)0); // Frame size placeholder to be rewritten in a few lines

            writer.Write(frameFlags);

            writer.Write(Utils.Latin1Encoding.GetBytes(pictureTypeCode));
            writer.Write('\0'); // String has to be null-terminated

            long dataPos = writer.BaseStream.Position;
            // Description = picture code + 0x2E byte -?- + image type encoded in ISO-8859-1 (derived from mime-type without the first half)
            writer.Write(Utils.Latin1Encoding.GetBytes(pictureTypeCode));
            writer.Write((byte)0x2E);
            string imageType;
            string[] tmp = mimeType.Split('/');
            imageType = (tmp.Length > 1) ? tmp[1] : tmp[0];
            if ("jpeg".Equals(imageType)) imageType = "jpg";

            writer.Write(Utils.Latin1Encoding.GetBytes(imageType)); // Force ISO-8859-1 format for mime-type
            writer.Write('\0'); // String should be null-terminated

            writer.Write(pictureData);

            // Go back to frame size location to write its actual size
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write((int)(finalFramePos-dataPos));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }
    }
}