using System;
using System.IO;
using System.Text;
using static ATL.ChannelsArrangements;
using System.Collections.Generic;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Audible Format 4 files manipulation (extensions : .AA)
    /// 
    /// Implementation notes
    /// 
    ///     1. TODO
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


        private bool isValid;
        private long audioSize;
        private string codec;

        private AudioDataManager.SizeInfo sizeInfo;
        private readonly string fileName;

        private Dictionary<int, Tuple<long, long>> toc;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR
        {
            get { return false; }
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


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            codec = "";
            audioSize = 0;
            isValid = false;
        }

        public AA(string fileName)
        {
            this.fileName = fileName;
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
        private void readHeader(BinaryReader Source)
        {
            Source.BaseStream.Seek(4, SeekOrigin.Begin); // File size
            int magicNumber = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
            if (magicNumber != AA_MAGIC_NUMBER) return;

            isValid = true;
            int tocSize = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
            Source.BaseStream.Seek(4, SeekOrigin.Current); // Even FFMPeg doesn't know what this integer is

            // The table of contents describes the layout of the file as triples of integers (<section>, <offset>, <length>)
            toc = new Dictionary<int, Tuple<long, long>>();
            for (int i = 0; i < tocSize; i++)
            {
                int section = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                long offset = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                long size = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                Tuple<long, long> data = new Tuple<long, long>(offset, size);
                toc[section] = data;
                if (TOC_AUDIO == section)
                    audioSize = size;
            }
        }

        private void readTags(BinaryReader Source, long offset, long size, ReadTagParams readTagParams)
        {
            Source.BaseStream.Seek(offset, SeekOrigin.Begin);
            int nbTags = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
            for (int i = 0; i < nbTags; i++)
            {
                Source.BaseStream.Seek(1, SeekOrigin.Current); // No idea what this byte is
                int keyLength = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                int valueLength = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                string key = Encoding.UTF8.GetString(Source.ReadBytes(keyLength));
                string value = Encoding.UTF8.GetString(Source.ReadBytes(valueLength)).Trim();
                SetMetaField(key, value, readTagParams.ReadAllMetaFrames);
                if ("codec".Equals(key))
                {
                    codec = value;
                }
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

            return result;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            throw new NotImplementedException();
        }
    }
}