using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for APEtag 1.0 and 2.0 tags manipulation
    /// </summary>
	public class APEtag : MetaDataIO
    {
        /// <summary>
        /// Tag ID / magic number
        /// </summary>
        public const string APE_ID = "APETAGEX";

        // Size constants
        /// <summary>
        /// APE tag footer size
        /// </summary>
        public const byte APE_TAG_FOOTER_SIZE = 32;
        /// <summary>
        /// APE tag header size
        /// </summary>
        public const byte APE_TAG_HEADER_SIZE = 32;

        // Version values
        /// <summary>
        /// APE v1
        /// </summary>
        public const int APE_VERSION_1_0 = 1000;
        /// <summary>
        /// APE v2
        /// </summary>
        public const int APE_VERSION_2_0 = 2000;


        // Mapping between standard ATL fields and APE identifiers
        /*
         * Note : APE tag standard being a little loose, field codes vary according to the various implementations that have been made
         * => Some fields can be found in multiple frame code variants
         *      - Rating : "rating", "preference" frames
         *      - Disc number : "disc", "discnumber" frames
         *      - Album Artist : "albumartist", "album artist" frames
         */
        private static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { "TITLE", Field.TITLE },
            { "ARTIST", Field.ARTIST },
            { "ALBUM", Field.ALBUM },
            { "TRACK", Field.TRACK_NUMBER_TOTAL },
            { "YEAR", Field.RECORDING_YEAR },
            { "GENRE", Field.GENRE },
            { "COMMENT", Field.COMMENT },
            { "COPYRIGHT", Field.COPYRIGHT },
            { "COMPOSER", Field.COMPOSER },
            { "RATING", Field.RATING },
            { "PREFERENCE", Field.RATING },
            { "DISCNUMBER", Field.DISC_NUMBER_TOTAL },
            { "DISC", Field.DISC_NUMBER_TOTAL },
            { "ALBUMARTIST", Field.ALBUM_ARTIST },
            { "ALBUM ARTIST", Field.ALBUM_ARTIST },
            { "CONDUCTOR", Field.CONDUCTOR },
            { "LYRICS", Field.LYRICS_UNSYNCH },
            { "PUBLISHER", Field.PUBLISHER },
            { "BPM", Field.BPM },
            { "ENCODEDBY", Field.ENCODED_BY },
            { "LANGUAGE", Field.LANGUAGE },
            { "ISRC", Field.ISRC },
            { "CATALOGNUMBER", Field.CATALOG_NUMBER },
            { "LYRICIST", Field.LYRICIST }
        };

        // Mix of classic APE mapping and its variations
        private static readonly IDictionary<string, PictureInfo.PIC_TYPE> picMapping = new Dictionary<string, PictureInfo.PIC_TYPE>
        {
            { "Cover Art (Icon)", PictureInfo.PIC_TYPE.Icon},
            { "Cover Art (Front)", PictureInfo.PIC_TYPE.Front},
            { "Front Cover", PictureInfo.PIC_TYPE.Front},
            { "Cover Art (Back)", PictureInfo.PIC_TYPE.Back},
            { "Back Cover", PictureInfo.PIC_TYPE.Back},
            { "Cover Art (Leaflet)", PictureInfo.PIC_TYPE.Leaflet},
            { "Cover Art (Media)", PictureInfo.PIC_TYPE.CD},
            { "Cover Art (Lead artist)", PictureInfo.PIC_TYPE.LeadArtist},
            { "Cover Art (Lead)", PictureInfo.PIC_TYPE.LeadArtist},
            { "Cover Art (Artist)", PictureInfo.PIC_TYPE.Artist},
            { "Cover Art (Conductor)", PictureInfo.PIC_TYPE.Conductor},
            { "Cover Art (Band)", PictureInfo.PIC_TYPE.Band},
            { "Cover Art (Composer)", PictureInfo.PIC_TYPE.Composer},
            { "Cover Art (Lyricist)", PictureInfo.PIC_TYPE.Lyricist},
            { "Cover Art (Recording location)", PictureInfo.PIC_TYPE.RecordingLocation},
            { "Cover Art (Studio)", PictureInfo.PIC_TYPE.RecordingLocation},
            { "Cover Art (During recording)", PictureInfo.PIC_TYPE.DuringRecording},
            { "Cover Art (Recording)", PictureInfo.PIC_TYPE.DuringRecording},
            { "Cover Art (During performance)", PictureInfo.PIC_TYPE.DuringPerformance},
            { "Cover Art (Performance)", PictureInfo.PIC_TYPE.DuringPerformance},
            { "Cover Art (Movie scene)", PictureInfo.PIC_TYPE.MovieCapture},
            { "Cover Art (Movie capture)", PictureInfo.PIC_TYPE.MovieCapture},
            { "Cover Art (Video capture)", PictureInfo.PIC_TYPE.MovieCapture},
            { "Cover Art (A bright coloured fish)", PictureInfo.PIC_TYPE.Fishie},
            { "Cover Art (Colored fish)", PictureInfo.PIC_TYPE.Fishie},
            { "Cover Art (Fish)", PictureInfo.PIC_TYPE.Fishie},
            { "Cover Art (Illustration)", PictureInfo.PIC_TYPE.Illustration},
            { "Cover Art (Band logotype)", PictureInfo.PIC_TYPE.BandLogo},
            { "Cover Art (Band logo)", PictureInfo.PIC_TYPE.BandLogo},
            { "Cover Art (Publisher logotype)", PictureInfo.PIC_TYPE.PublisherLogo},
            { "Cover Art (Publisher logo)", PictureInfo.PIC_TYPE.PublisherLogo},
            { "Cover Art (Other)", PictureInfo.PIC_TYPE.Generic},
            { "Cover Art", PictureInfo.PIC_TYPE.Generic},
            { "Cover", PictureInfo.PIC_TYPE.Generic}
        };


        // APE tag data - for internal use
        private sealed class TagInfo
        {
            // Real structure of APE footer
            public char[] ID = new char[8];                              // Always "APETAGEX"
            public int Version;                                                // Tag version
            public int Size;                   // Tag size including footer, excluding header
            public int FrameCount;                                        // Number of fields
            // Flags (unused)
            public char[] Reserved = new char[8];                  // Reserved for later use
                                                                   // Extended data
            public byte DataShift;                                 // Used if ID3v1 tag found
            public long FileSize;		                                 // File size (bytes)

            public void Reset()
            {
                Array.Clear(ID, 0, ID.Length);
                Version = 0;
                FrameCount = 0;
                Size = 0;
                Array.Clear(Reserved, 0, Reserved.Length);
                DataShift = 0;
                FileSize = 0;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public APEtag()
        {
            ResetData();
        }

        // --------------- OPTIONAL INFORMATIVE OVERRIDES

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                Format format = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("ape")[0]);
                format.Name = format.Name + " v" + m_tagVersion / 1000f;
                format.ID += m_tagVersion / 10; // e.g. 1250 -> 125
                return new List<Format>(new[] { format });
            }
        }

        // --------------- MANDATORY INFORMATIVE OVERRIDES

        /// <inheritdoc/>
        protected override int getDefaultTagOffset()
        {
            return TO_EOF;
        }

        /// <inheritdoc/>
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.APE;
        }

        /// <inheritdoc/>
        protected override byte ratingConvention => RC_APE;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        /// <inheritdoc/>
        protected override bool supportsPictures => true;

        /// <inheritdoc/>
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;
            ID = ID.Replace("\0", "").ToUpper();

            // Finds the ATL field identifier according to the APE version
            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        // ********************* Auxiliary functions & voids ********************

        private static bool readFooter(BufferedBinaryReader source, TagInfo Tag)
        {
            bool result = true;

            // Load footer from file to variable
            Tag.FileSize = source.Length;

            // Check for existing ID3v1 tag in order to get the correct offset for APEtag packet
            source.Seek(Tag.FileSize - ID3v1.ID3V1_TAG_SIZE, SeekOrigin.Begin);
            var tagID = Utils.Latin1Encoding.GetString(source.ReadBytes(3));
            if (ID3v1.ID3V1_ID.Equals(tagID)) Tag.DataShift = ID3v1.ID3V1_TAG_SIZE;

            // Read APEtag footer data
            source.Seek(Tag.FileSize - Tag.DataShift - APE_TAG_FOOTER_SIZE, SeekOrigin.Begin);

            Tag.ID = Utils.Latin1Encoding.GetChars(source.ReadBytes(8));
            if (APE_ID.SequenceEqual(Tag.ID))
            {
                Tag.Version = source.ReadInt32();
                Tag.Size = source.ReadInt32();
                Tag.FrameCount = source.ReadInt32();
                source.Seek(4, SeekOrigin.Current); // Flags
                Tag.Reserved = Utils.Latin1Encoding.GetChars(source.ReadBytes(8));
            }
            else
            {
                result = false;
            }

            return result;
        }

        private bool readFrames(BufferedBinaryReader source, TagInfo Tag, ReadTagParams readTagParams)
        {
            source.Seek(Tag.FileSize - Tag.DataShift - Tag.Size, SeekOrigin.Begin);

            // Read all stored fields
            for (int iterator = 0; iterator < Tag.FrameCount; iterator++)
            {
                var frameDataSize = source.ReadInt32();
                source.Seek(4, SeekOrigin.Current); // Frame flags
                var frameName = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding);

                var valuePosition = source.Position;

                if (frameDataSize < 0 || valuePosition + frameDataSize > Tag.FileSize)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Invalid value found while reading APEtag frame");
                    return false;
                }

                if (frameDataSize > 0)
                {
                    PictureInfo.PIC_TYPE picType = decodeAPEPictureType(frameName);
                    if (picType == PictureInfo.PIC_TYPE.Unsupported)
                    {
                        /*
                         * According to spec : "Items are not zero-terminated like in C / C++.
                         * If there's a zero character, multiple items are stored under the key and the items are separated by zero characters."
                         *
                         * => Values have to be splitted
                         */
                        // Using Settings encoding to support rogue APE tags that use exotic charsets (though specs clearly say UTF-8)
                        var strValue =
                            Utils.StripEndingZeroChars(
                                Settings.DefaultTextEncoding.GetString(source.ReadBytes(frameDataSize)));
                        strValue = strValue.Replace('\0', Settings.InternalValueSeparator).Trim();
                        SetMetaField(frameName.Trim().ToUpper(), strValue, readTagParams.ReadAllMetaFrames);
                    }
                    else // Pictures
                    {
                        var picturePosition = picType.Equals(PictureInfo.PIC_TYPE.Unsupported)
                            ? takePicturePosition(getImplementedTagType(), frameName)
                            : takePicturePosition(picType);

                        if (readTagParams.ReadPictures)
                        {
                            // Description seems to be a null-terminated ANSI string containing 
                            //    * The frame name
                            //    * A byte (0x2E)
                            //    * The picture type (3 characters; similar to the 2nd part of the mime-type)
                            string description = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding);
                            PictureInfo picInfo = PictureInfo.fromBinaryData(source,
                                frameDataSize - description.Length - 1, picType, getImplementedTagType(), frameName,
                                picturePosition);
                            picInfo.Description = description;
                            tagData.Pictures.Add(picInfo);
                        }
                    }
                }

                source.Seek(valuePosition + frameDataSize, SeekOrigin.Begin);
            }

            return true;
        }

        /// <summary>
        /// Returns the ATL picture type corresponding to the given APE picture code
        /// </summary>
        /// <param name="picCode">AOE picture code</param>
        /// <returns>ATL picture type corresponding to the given APE picture code; PIC_TYPE.Unsupported by default</returns>
        private static PictureInfo.PIC_TYPE decodeAPEPictureType(string picCode)
        {
            var picTypePair = picMapping.FirstOrDefault(ptp =>
                ptp.Key.Equals(picCode, StringComparison.OrdinalIgnoreCase));

            return picTypePair.Equals(default(KeyValuePair<string, PictureInfo.PIC_TYPE>)) ? PictureInfo.PIC_TYPE.Unsupported : picTypePair.Value;
        }

        /// <summary>
        /// Returns the APE picture code corresponding to the given ATL picture type
        /// </summary>
        /// <param name="picType">ATL picture type</param>
        /// <returns>APE picture code corresponding to the given ATL picture type; "Cover Art (Other)" by default</returns>
        private static string encodeAPEPictureType(PictureInfo.PIC_TYPE picType)
        {
            var picTypePair = picMapping.FirstOrDefault(ptp => ptp.Value == picType);
            return picTypePair.Equals(default(KeyValuePair<string, PictureInfo.PIC_TYPE>)) ? picMapping.First(ptp => ptp.Value == PictureInfo.PIC_TYPE.Generic).Key : picTypePair.Key;
        }

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            TagInfo Tag = new TagInfo();

            // Reset data and load footer from file to variable
            ResetData();
            Tag.Reset();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            bool result = readFooter(reader, Tag);

            // Process data if loaded and footer valid
            if (!result) return false;

            // Fill properties with footer data
            m_tagVersion = Tag.Version;

            var tagSize = Tag.Size;
            if (m_tagVersion > APE_VERSION_1_0) tagSize += 32; // Even though APE standard prevents from counting header in its size descriptor, ATL needs it
            var tagOffset = Tag.FileSize - Tag.DataShift - Tag.Size;
            if (m_tagVersion > APE_VERSION_1_0) tagOffset -= 32; // Tag size does not include header size in APEv2

            structureHelper.AddZone(tagOffset, tagSize);

            // Get information from fields
            result = readFrames(reader, Tag, readTagParams);

            return result;
        }

        /// <summary>
        /// Writes the given tag into the given Writer using APEv2 conventions
        /// </summary>
        /// <param name="tag">Tag information to be written</param>
        /// <param name="s">Stream to write tag information to</param>
        /// <param name="zone">Code of the zone to write</param>
        /// <returns>True if writing operation succeeded; false if not</returns>
        protected override int write(TagData tag, Stream s, string zone)
        {
            using BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true);
            return write(tag, w);
        }

        private int write(TagData tag, BinaryWriter w)
        {
            uint flags = 0xA0000000; // Flag for "tag contains a footer, a header, and this is the header"


            // ============
            // == HEADER ==
            // ============
            w.Write(APE_ID.ToCharArray());
            w.Write(APE_VERSION_2_0); // Version 2

            // Keep position in mind to calculate final size and come back here to write it
            var tagSizePos = w.BaseStream.Position;
            w.Write(0); // Tag size placeholder to be rewritten in a few lines

            // Keep position in mind to calculate final item count and come back here to write it
            var itemCountPos = w.BaseStream.Position;
            w.Write(0); // Item count placeholder to be rewritten in a few lines

            w.Write(flags);

            // Reserved space
            w.Write((long)0);


            // ============
            // == FRAMES ==
            // ============
            long dataPos = w.BaseStream.Position;
            var itemCount = writeFrames(tag, w);

            // Record final size of tag into "tag size" field of header
            long finalTagPos = w.BaseStream.Position;
            w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
            var tagSize = (int)(finalTagPos - dataPos) + 32; // 32 being the size of the header
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
            int nbFrames = 0;
            // Keep these in memory to prevent setting them twice using AdditionalFields
            var writtenFieldCodes = new HashSet<string>();

            // Picture fields (first before textual fields, since APE tag is located on the footer)
            foreach (PictureInfo picInfo in tag.Pictures.Where(isPictureWritable))
            {
                writePictureFrame(w, picInfo.PictureData, picInfo.MimeType, picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? picInfo.NativePicCodeStr : encodeAPEPictureType(picInfo.PicType));
                nbFrames++;
            }

            IDictionary<Field, string> map = tag.ToMap();

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    if (frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, tag, map);
                            writeTextFrame(w, s, value);
                            writtenFieldCodes.Add(s.ToUpper());
                            nbFrames++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if (!writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToUpper()))
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, FormatBeforeWriting(fieldInfo.Value));
                    nbFrames++;
                }
            }

            return nbFrames;
        }

        private static void writeTextFrame(BinaryWriter writer, string frameCode, string text)
        {
            const int frameFlags = 0x0000;

            var frameSizePos = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines

            writer.Write(frameFlags);

            writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
            writer.Write('\0'); // String has to be null-terminated

            // Using Settings encoding to support rogue APE tags that use exotic charsets (though specs clearly say UTF-8)
            byte[] binaryValue = Settings.DefaultTextEncoding.GetBytes(text);
            writer.Write(binaryValue);

            // Go back to frame size location to write its actual size 
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write(binaryValue.Length);
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private static void writePictureFrame(BinaryWriter writer, byte[] pictureData, string mimeType, string pictureTypeCode)
        {
            const int frameFlags = 0x00000002; // This frame contains binary information (essential for pictures)

            var frameSizePos = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines

            writer.Write(frameFlags);

            writer.Write(Utils.Latin1Encoding.GetBytes(pictureTypeCode));
            writer.Write('\0'); // String has to be null-terminated

            long dataPos = writer.BaseStream.Position;
            // Description = picture code + 0x2E byte -?- + image type encoded in ISO-8859-1 (derived from mime-type without the first half)
            writer.Write(Utils.Latin1Encoding.GetBytes(pictureTypeCode));
            writer.Write((byte)0x2E);

            string[] tmp = mimeType.Split('/');
            var imageType = (tmp.Length > 1) ? tmp[1] : tmp[0];
            if ("jpeg".Equals(imageType)) imageType = "jpg";

            writer.Write(Utils.Latin1Encoding.GetBytes(imageType)); // Force ISO-8859-1 format for mime-type
            writer.Write('\0'); // String should be null-terminated

            writer.Write(pictureData);

            // Go back to frame size location to write its actual size
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            writer.Write((int)(finalFramePos - dataPos));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }
    }
}