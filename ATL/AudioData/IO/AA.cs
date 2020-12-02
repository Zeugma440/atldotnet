using System;
using System.IO;
using System.Text;
using static ATL.ChannelsArrangements;
using System.Collections.Generic;
using System.Globalization;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Audible Formats 2 to 4 files manipulation (extensions : .AA)
    /// 
    /// Implementation notes
    /// 
    ///   - Only the editing of existing zones has been tested, not the adding of new zones (e.g. tagging a tagless AA, adding a picture to a pictureless AA)
    ///   due to the lack of empty test files
    ///   
    /// </summary>
	class AA : MetaDataIO, IAudioDataIO
    {

        public const int AA_MAGIC_NUMBER = 1469084982;

        public const int TOC_HEADER_TERMINATOR = 1;
        public const int TOC_CONTENT_TAGS = 2;
        public const int TOC_AUDIO = 10;
        public const int TOC_COVER_ART = 11;

        public const string CODEC_MP332 = "mp332";
        public const string CODEC_ACELP85 = "acelp85";
        public const string CODEC_ACELP16 = "acelp16";

        public const string ZONE_TOC = "toc";
        public const string ZONE_TAGS = "2";
        public const string ZONE_IMAGE = "11";


        // Mapping between MP4 frame codes and ATL frame codes
        private static Dictionary<string, byte> frameMapping = new Dictionary<string, byte>() {
            { "title", TagData.TAG_FIELD_TITLE },
            { "parent_title", TagData.TAG_FIELD_ALBUM },
            { "narrator", TagData.TAG_FIELD_ARTIST },
            { "description", TagData.TAG_FIELD_COMMENT },
            { "pubdate", TagData.TAG_FIELD_PUBLISHING_DATE},
            { "provider", TagData.TAG_FIELD_PUBLISHER},
            { "author", TagData.TAG_FIELD_COMPOSER },
            { "long_description", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
            { "copyright", TagData.TAG_FIELD_COPYRIGHT },
        };


        private long audioSize;
        private string codec;

        private AudioDataManager.SizeInfo sizeInfo;
        private readonly string fileName;
        private readonly Format audioFormat;

        private Dictionary<int, Tuple<uint, uint>> toc;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR
        {
            get { return false; }
        }
        public Format AudioFormat
        {
            get
            {
                Format f = new Format(audioFormat);
                if (codec.Length > 0)
                    f.Name = f.Name + " (" + codec + ")";
                else
                    f.Name = f.Name + " (Unknown)";
                return f;
            }
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        public double BitRate
        {
            get
            {
                switch (codec)
                {
                    case CODEC_MP332:
                        return 32 / 8.0;
                    case CODEC_ACELP16:
                        return 16 / 8.0;
                    case CODEC_ACELP85:
                        return 8.5 / 8.0;
                    default:
                        return 1;
                }
            }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public int SampleRate
        {
            get
            {
                switch (codec)
                {
                    case CODEC_MP332:
                        return 22050;
                    case CODEC_ACELP16:
                        return 16000;
                    case CODEC_ACELP85:
                        return 8500;
                    default:
                        return 1;
                }
            }
        }
        public string FileName
        {
            get { return fileName; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE);
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return MONO; }
        }

        // MetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;

            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }
        protected override bool isLittleEndian
        {
            get { return false; }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            codec = "";
            audioSize = 0;
        }

        public AA(string fileName, Format format)
        {
            this.fileName = fileName;
            this.audioFormat = format;
            resetData();
        }

        // ********************** Private functions & procedures *********************

        // Calculate duration time
        private double getDuration()
        {
            if (0 == BitRate)
                return 0;
            else
                return audioSize / (BitRate * 1000);
        }

        // Read header data
        private void readHeader(BinaryReader source)
        {
            uint fileSize = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            int magicNumber = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            if (magicNumber != AA_MAGIC_NUMBER) return;

            tagExists = true;
            int tocSize = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            source.BaseStream.Seek(4, SeekOrigin.Current); // Even FFMPeg doesn't know what this integer is

            // The table of contents describes the layout of the file as triples of integers (<section>, <offset>, <length>)
            toc = new Dictionary<int, Tuple<uint, uint>>();
            for (int i = 0; i < tocSize; i++)
            {
                int section = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                uint tocEntryOffset = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                uint tocEntrySize = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                Tuple<uint, uint> data = new Tuple<uint, uint>(tocEntryOffset, tocEntrySize);
                toc[section] = data;
                structureHelper.AddZone(tocEntryOffset, (int)tocEntrySize, section.ToString());
                structureHelper.AddIndex(source.BaseStream.Position - 8, tocEntryOffset, false, section.ToString());
                if (TOC_AUDIO == section)
                {
                    audioSize = tocEntrySize;
                }
                if (TOC_CONTENT_TAGS == section)
                {
                    structureHelper.AddSize(source.BaseStream.Position - 4, tocEntrySize, section.ToString());
                    structureHelper.AddSize(0, fileSize, section.ToString());
                }
                if (TOC_COVER_ART == section)
                {
                    structureHelper.AddSize(source.BaseStream.Position - 4, tocEntrySize, section.ToString());
                    structureHelper.AddIndex(source.BaseStream.Position - 8, tocEntryOffset, false, section.ToString());
                    structureHelper.AddSize(0, fileSize, section.ToString());
                }
            }
        }

        private void readTags(BinaryReader source, long offset, long size, ReadTagParams readTagParams)
        {
            source.BaseStream.Seek(offset, SeekOrigin.Begin);
            int nbTags = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            for (int i = 0; i < nbTags; i++)
            {
                source.BaseStream.Seek(1, SeekOrigin.Current); // No idea what this byte is
                int keyLength = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                int valueLength = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                string key = Encoding.UTF8.GetString(source.ReadBytes(keyLength));
                string value = Encoding.UTF8.GetString(source.ReadBytes(valueLength)).Trim();
                SetMetaField(key, value, readTagParams.ReadAllMetaFrames);
                if ("codec".Equals(key)) codec = value;
            }
        }

        private void readCover(BinaryReader source, long offset, PictureInfo.PIC_TYPE pictureType)
        {
            source.BaseStream.Seek(offset, SeekOrigin.Begin);
            int pictureSize = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            int picOffset = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            structureHelper.AddIndex(source.BaseStream.Position - 4, (uint)picOffset, false, ZONE_IMAGE);
            source.BaseStream.Seek(picOffset, SeekOrigin.Begin);

            PictureInfo picInfo = PictureInfo.fromBinaryData(source.BaseStream, pictureSize, pictureType, getImplementedTagType(), TOC_COVER_ART);
            tagData.Pictures.Add(picInfo);
        }

        private void readChapters(BinaryReader source, long offset, long size)
        {
            source.BaseStream.Seek(offset, SeekOrigin.Begin);
            if (null == tagData.Chapters) tagData.Chapters = new List<ChapterInfo>(); else tagData.Chapters.Clear();
            double cumulatedDuration = 0;
            int idx = 1;
            while (source.BaseStream.Position < offset + size)
            {
                uint chapterSize = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                uint chapterOffset = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                structureHelper.AddZone(chapterOffset, (int)chapterSize, "chp" + idx);
                structureHelper.AddIndex(source.BaseStream.Position - 4, chapterOffset, false, "chp" + idx);

                ChapterInfo chapter = new ChapterInfo();
                chapter.Title = "Chapter " + idx++; // Chapters have no title metatada in the AA format
                chapter.StartTime = (uint)Math.Round(cumulatedDuration);
                cumulatedDuration += chapterSize / (BitRate * 1000);
                chapter.EndTime = (uint)Math.Round(cumulatedDuration);
                tagData.Chapters.Add(chapter);

                source.BaseStream.Seek(chapterSize, SeekOrigin.Current);
            }
        }

        // Read data from file
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, ReadTagParams readTagParams)
        {
            bool result = true;

            ResetData();
            readHeader(source);
            if (toc.ContainsKey(TOC_CONTENT_TAGS))
            {
                readTags(source, toc[TOC_CONTENT_TAGS].Item1, toc[TOC_CONTENT_TAGS].Item2, readTagParams);
            }
            if (toc.ContainsKey(TOC_COVER_ART))
            {
                if (readTagParams.ReadPictures)
                    readCover(source, toc[TOC_COVER_ART].Item1, PictureInfo.PIC_TYPE.Generic);
                else
                    addPictureToken(PictureInfo.PIC_TYPE.Generic);
            }
            readChapters(source, toc[TOC_AUDIO].Item1, toc[TOC_AUDIO].Item2);

            return result;
        }

        protected new string formatBeforeWriting(byte frameType, TagData tag, IDictionary<byte, string> map)
        {
            string result = base.formatBeforeWriting(frameType, tag, map);

            // Convert to expected date format
            if (TagData.TAG_FIELD_PUBLISHING_DATE == frameType)
            {
                DateTime date = DateTime.Parse(result);
                result = date.ToString("dd-MMM-yyyy", CultureInfo.CreateSpecificCulture("en-US")).ToUpper();
            }

            return result;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = -1; // Default : leave as is

            if (zone.Equals(ZONE_TAGS))
            {
                long nbTagsOffset = w.BaseStream.Position;
                w.Write(0); // Number of tags; will be rewritten at the end of the method

                // Mapped textual fields
                IDictionary<byte, string> map = tag.ToMap();
                foreach (byte frameType in map.Keys)
                {
                    if (map[frameType].Length > 0) // No frame with empty value
                    {
                        foreach (string s in frameMapping.Keys)
                        {
                            if (frameType == frameMapping[s])
                            {
                                string value = formatBeforeWriting(frameType, tag, map);
                                writeTagField(w, s, value);
                                result++;
                                break;
                            }
                        }
                    }
                }

                // Other textual fields
                foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
                {
                    if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion)
                    {
                        writeTagField(w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                        result++;
                    }
                }

                w.BaseStream.Seek(nbTagsOffset, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEInt32(result)); // Number of tags
            }
            if (zone.Equals(ZONE_IMAGE))
            {
                result = 0;
                foreach (PictureInfo picInfo in tag.Pictures)
                {
                    // Picture has either to be supported, or to come from the right tag standard
                    bool doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                    if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                    // It also has not to be marked for deletion
                    doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                    if (doWritePicture)
                    {
                        writePictureFrame(w, picInfo.PictureData);
                        return 1; // Stop here; there can only be one picture in an AA file
                    }
                }
            }

            return result;
        }

        private void writeTagField(BinaryWriter w, string key, string value)
        {
            w.Write('\0'); // Unknown byte; always zero
            byte[] keyB = Encoding.UTF8.GetBytes(key);
            byte[] valueB = Encoding.UTF8.GetBytes(value);
            w.Write(StreamUtils.EncodeBEInt32(keyB.Length)); // Key length
            w.Write(StreamUtils.EncodeBEInt32(valueB.Length)); // Value length
            w.Write(keyB);
            w.Write(valueB);
        }

        private void writePictureFrame(BinaryWriter w, byte[] pictureData)
        {
            w.Write(StreamUtils.EncodeBEInt32(pictureData.Length)); // Pic size
            w.Write(0); // Pic data absolute offset; to be rewritten later

            w.Write(pictureData);
        }
    }
}