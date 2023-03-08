using System;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Text;
using static ATL.ChannelsArrangements;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Genesis YM2612 files manipulation (extensions : .GYM)
    /// 
    /// Implementation notes
    /// 
    ///     1/ Looping : I have yet to find a GYM file that actually contains a loop.
    ///     All archives I found so far are direct recording of game audio instructions
    ///     that actually repeat the same pattern twice (looping data is not used at all)
    ///     
    ///     2/ Gzipped stream : I have yet to find a GYM file that contains gzipped data.
    ///     => Rather than to make a theoretical implementation, there is no implementation at all.
    /// 
    /// </summary>
    class GYM : MetaDataIO, IAudioDataIO
    {
        private static readonly byte[] GYM_SIGNATURE = Utils.Latin1Encoding.GetBytes("GYMX");

        private const int GYM_HEADER_SIZE = 428;

        private static uint LOOP_COUNT_DEFAULT = 1;         // Default loop count
        private static uint FADEOUT_DURATION_DEFAULT = 0;   // Default fadeout duration, in seconds
        private static uint PLAYBACK_RATE_DEFAULT = 60;     // Default playback rate if no preference set (Hz)

        private static byte[] CORE_SIGNATURE = new byte[416];

        // Standard fields
        private int sampleRate;
        private double bitrate;
        private double duration;

        uint loopStart;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // AudioDataIO
        public int SampleRate // Sample rate (hz)
        {
            get { return sampleRate; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_SEQ_WAV; }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }
        public int BitDepth => -1; // Irrelevant for that format
        public double Duration
        {
            get { return duration; }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return STEREO; }
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return metaDataType == MetaDataIOFactory.TagType.NATIVE;
        }

        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            throw new NotImplementedException();
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            // Reset variables
            sampleRate = 44100; // Seems to be default value according to foobar2000
            bitrate = 0;
            duration = 0;

            loopStart = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public GYM(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, GYM_SIGNATURE);
        }


        // === PRIVATE METHODS ===

        private bool readHeader(BufferedBinaryReader source, ReadTagParams readTagParams)
        {
            string str;

            if (IsValidHeader(source.ReadBytes(GYM_SIGNATURE.Length)))
            {
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(source.Position, 416, CORE_SIGNATURE);
                }

                tagExists = true;

                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.IntegrateValue(TagData.Field.TITLE, str);
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.IntegrateValue(TagData.Field.ALBUM, str);
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.IntegrateValue(TagData.Field.COPYRIGHT, str);
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "EMULATOR", str));
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "DUMPER", str));
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(256))).Trim();
                tagData.IntegrateValue(TagData.Field.COMMENT, str);

                loopStart = source.ReadUInt32();
                uint packedSize = source.ReadUInt32();
                AudioDataOffset = source.Position;
                AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

                if (packedSize > 0)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "GZIP-compressed files are not supported"); // will be as soon as I find a sample to test with
                    return false;
                }

                return true;
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Not a GYM file");
                return false;
            }
        }

        private uint calculateDuration(BufferedBinaryReader source, uint loopStart, uint nbLoops)
        {
            long streamSize = source.Length;
            byte frameType;
            uint frameIndex = 0;
            uint nbTicks_all = 0;
            uint nbTicks_loop = 0;
            bool loopReached = false;

            while (source.Position < streamSize)
            {
                frameIndex++;
                if (frameIndex == loopStart) loopReached = true;

                frameType = source.ReadByte();
                switch (frameType)
                {
                    case (0x00):
                        nbTicks_all++;
                        if (loopReached) nbTicks_loop++;
                        break;
                    case (0x01):
                    case (0x02): source.Seek(2, SeekOrigin.Current); break;
                    case (0x03): source.Seek(1, SeekOrigin.Current); break;
                }
            }

            uint result = (nbTicks_all - nbTicks_loop) + (nbLoops * nbTicks_loop);
            if (Settings.GYM_VGM_playbackRate > 0)
            {
                result = (uint)Math.Round(result * (1.0 / Settings.GYM_VGM_playbackRate));
            }
            else
            {
                result = (uint)Math.Round(result * (1.0 / PLAYBACK_RATE_DEFAULT));
            }
            if (loopStart > 0) result += FADEOUT_DURATION_DEFAULT;

            return result;
        }


        // === PUBLIC METHODS ===

        public bool Read(Stream source, SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            bool result = true;
            BufferedBinaryReader bufferedSource = new BufferedBinaryReader(source); // Optimize parsing speed

            resetData();

            source.Seek(0, SeekOrigin.Begin);

            if (readHeader(bufferedSource, readTagParams))
            {
                duration = calculateDuration(bufferedSource, loopStart, LOOP_COUNT_DEFAULT) * 1000.0;
                bitrate = (sizeInfo.FileSize - GYM_HEADER_SIZE) * 8 / duration; // TODO - use unpacked size if applicable, and not raw file size
            }

            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            StreamUtils.WriteBytes(s, Utils.BuildStrictLengthStringBytes(tag[Field.TITLE], 32, 0, Encoding.UTF8));
            StreamUtils.WriteBytes(s, Utils.BuildStrictLengthStringBytes(tag[Field.ALBUM], 32, 0, Encoding.UTF8));
            StreamUtils.WriteBytes(s, Utils.BuildStrictLengthStringBytes(tag[Field.COPYRIGHT], 32, 0, Encoding.UTF8));
            string str = "";
            if (AdditionalFields.ContainsKey("EMULATOR")) str = AdditionalFields["EMULATOR"];
            StreamUtils.WriteBytes(s, Utils.BuildStrictLengthStringBytes(str, 32, 0, Encoding.UTF8));
            str = "";
            if (AdditionalFields.ContainsKey("DUMPER")) str = AdditionalFields["DUMPER"];
            StreamUtils.WriteBytes(s, Utils.BuildStrictLengthStringBytes(str, 32, 0, Encoding.UTF8));
            StreamUtils.WriteBytes(s, Utils.BuildStrictLengthStringBytes(tag[Field.COMMENT], 256, 0, Encoding.UTF8));

            return 6;
        }
    }

}