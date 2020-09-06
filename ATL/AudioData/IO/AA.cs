using System;
using System.IO;
using System.Text;
using Commons;
using static ATL.ChannelsArrangements;
using static ATL.AudioData.FileStructureHelper;
using System.Collections.Generic;
using System.Xml.Schema;

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

        // Sample rate values
        private static readonly int[] SAMPLE_RATE = {   96000, 88200, 64000, 48000, 44100, 32000,
                                                        24000, 22050, 16000, 12000, 11025, 8000,
                                                        0, 0, 0, 0 };

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
        private double bitrate;
        private int sampleRate;
        private ChannelsArrangement channelsArrangement;

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
            get { return bitrate / 1000.0; }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public int SampleRate
        {
            get { return sampleRate; }
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
            get { return channelsArrangement; }
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
            bitrate = 0;
            sampleRate = 0;
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
            if (0 == bitrate)
                return 0;
            else
                return 8.0 * (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 1000 / bitrate;
        }

        // Read header data
        private void readHeader(BinaryReader Source)
        {
            Source.BaseStream.Seek(4, SeekOrigin.Begin); // File size
            int magicNumber = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
            if (magicNumber != AA_MAGIC_NUMBER) return;

            isValid = true;
            int tocSize = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
            Source.BaseStream.Seek(4, SeekOrigin.Current);
            // The table of contents describes the layout of the file as triples of integers (<section>, <offset>, <length>)
            toc = new Dictionary<int, Tuple<long, long>>();
            for (int i=0; i<tocSize; i++)
            {
                int section = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                long offset = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                long size = StreamUtils.DecodeBEInt32(Source.ReadBytes(4));
                Tuple<long, long> data = new Tuple<long, long>(offset, size);
                toc[section] = data;
                /*
                if (!toc.ContainsKey(section))
                    toc.Add(section, data);
                */
            }
        }

        private void readTags(BinaryReader Source)
        {
            
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

            resetData();
            readHeader(source);
            readTags(source);

            return result;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            throw new NotImplementedException();
        }
    }
}