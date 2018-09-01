using System;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Text;

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
        private const string GYM_SIGNATURE = "GYMX";

        private const int GYM_HEADER_SIZE = 428;

        private static uint LOOP_COUNT_DEFAULT = 1;         // Default loop count
        private static uint FADEOUT_DURATION_DEFAULT = 0;   // Default fadeout duration, in seconds
        private static uint PLAYBACK_RATE_DEFAULT = 60;     // Default playback rate if no preference set (Hz)

        private static byte[] CORE_SIGNATURE;

        // Standard fields
        private int sampleRate;
        private double bitrate;
        private double duration;
        private bool isValid;

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
        public double Duration
        {
            get { return duration; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE);
        }

        // IMetaDataIO
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
            throw new NotImplementedException();
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        static GYM()
        {
            CORE_SIGNATURE = new byte[416];
            Array.Clear(CORE_SIGNATURE, 0, 416);
        }

        private void resetData()
        {
            // Reset variables
            sampleRate = 44100; // Seems to be default value according to foobar2000
            bitrate = 0;
            duration = 0;

            loopStart = 0;

            ResetData();
        }

        public GYM(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


		// === PRIVATE METHODS ===

		private bool readHeader(BufferedBinaryReader source, ReadTagParams readTagParams)
		{
            string str;

            long initialPosition = source.Position;
            if (GYM_SIGNATURE.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(GYM_SIGNATURE.Length))))
			{
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(source.Position, 416, CORE_SIGNATURE);
                }

                tagExists = true;

                str = Utils.StripEndingZeroChars( Encoding.UTF8.GetString(source.ReadBytes(32)) ).Trim();
                tagData.IntegrateValue(TagData.TAG_FIELD_TITLE, str);
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.IntegrateValue(TagData.TAG_FIELD_ALBUM, str);
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.IntegrateValue(TagData.TAG_FIELD_COPYRIGHT, str);
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "EMULATOR", str));
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(32))).Trim();
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "DUMPER", str));
                str = Utils.StripEndingZeroChars(Encoding.UTF8.GetString(source.ReadBytes(256))).Trim();
                tagData.IntegrateValue(TagData.TAG_FIELD_COMMENT, str);

                loopStart = source.ReadUInt32();
                uint packedSize = source.ReadUInt32();

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

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = true;
            BufferedBinaryReader bufferedSource = new BufferedBinaryReader(source.BaseStream); // Optimize parsing speed

            resetData();

            source.BaseStream.Seek(0, SeekOrigin.Begin);

            isValid = readHeader(bufferedSource, readTagParams);

            if (isValid)
            {
                duration = calculateDuration(bufferedSource, loopStart, LOOP_COUNT_DEFAULT) * 1000.0;

                bitrate = (sizeInfo.FileSize - GYM_HEADER_SIZE) * 8 / duration; // TODO - use unpacked size if applicable, and not raw file size
            }

            return result;
		}

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 6;

            w.Write(Utils.BuildStrictLengthStringBytes(tag.Title, 32, 0, Encoding.UTF8));
            w.Write(Utils.BuildStrictLengthStringBytes(tag.Album, 32, 0, Encoding.UTF8));
            w.Write(Utils.BuildStrictLengthStringBytes(tag.Copyright, 32, 0, Encoding.UTF8));
            string str = "";
            if (AdditionalFields.ContainsKey("EMULATOR")) str = AdditionalFields["EMULATOR"];
            w.Write(Utils.BuildStrictLengthStringBytes(str, 32, 0, Encoding.UTF8));
            str = "";
            if (AdditionalFields.ContainsKey("DUMPER")) str = AdditionalFields["DUMPER"];
            w.Write(Utils.BuildStrictLengthStringBytes(str, 32, 0, Encoding.UTF8));
            w.Write(Utils.BuildStrictLengthStringBytes(tag.Comment, 256, 0, Encoding.UTF8));

            return result;
        }
    }

}