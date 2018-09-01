using System;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Text;
using System.IO.Compression;

namespace ATL.AudioData.IO
{
	/// <summary>
    /// Class for Video Game Music files (Master System, Game Gear, SG1000, Genesis) manipulation (extensions : .VGM)
    /// According to file format v1.70
    /// 
    /// Implementation notes :
    ///   1/ GD3 tag format is directly implemented in here, since it is not a "real" standard and is only used for VGM files
    ///   
    ///   2/ Gzipped files are currently supported in read-only mode (i.e. ATL cannot write metadata to a GYM file containing gzipped data)
	/// </summary>
	class VGM : MetaDataIO, IAudioDataIO
	{
        private const string VGM_SIGNATURE = "Vgm ";
        private const string GD3_SIGNATURE = "Gd3 ";

        private const int VGM_HEADER_SIZE = 256;

        private static int LOOP_COUNT_DEFAULT = 1;              // Default loop count
        private static int FADEOUT_DURATION_DEFAULT = 10000;    // Default fadeout duration, in milliseconds (10s)
        private static int RECORDING_RATE_DEFAULT = 60;         // Default playback rate for v1.00 files

        // Standard fields
        private int version;
        private int sampleRate;
        private double bitrate;
        private double duration;
        private bool isValid;

        private int gd3TagOffset;

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

        private void resetData()
        {
            // Reset variables
            sampleRate = 44100; // Default value for all VGM files, according to v1.70 spec 
            bitrate = 0;
            duration = 0;
            version = 0;

            gd3TagOffset = 0;

            ResetData();
        }

        public VGM(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


		// === PRIVATE METHODS ===

		private bool readHeader(BinaryReader source, ReadTagParams readTagParams)
		{
            int nbSamples, loopNbSamples;
            int nbLoops = LOOP_COUNT_DEFAULT;
            int recordingRate = RECORDING_RATE_DEFAULT;

            long initialPosition = source.BaseStream.Position;
            byte[] headerSignature = source.ReadBytes(VGM_SIGNATURE.Length);
            if (VGM_SIGNATURE.Equals(Utils.Latin1Encoding.GetString(headerSignature)))
			{
				source.BaseStream.Seek(4,SeekOrigin.Current); // EOF offset
                version = source.ReadInt32();
                source.BaseStream.Seek(8, SeekOrigin.Current); // Clocks
                gd3TagOffset = source.ReadInt32() + (int)source.BaseStream.Position - 4;

                if (/*gd3TagOffset > 0 && */readTagParams.PrepareForWriting)
                {
                    if (gd3TagOffset > VGM_HEADER_SIZE)
                    {
                        structureHelper.AddZone(gd3TagOffset, (int)sizeInfo.FileSize - gd3TagOffset);
                        structureHelper.AddIndex(source.BaseStream.Position - 4, gd3TagOffset, true);
                    } else
                    {
                        structureHelper.AddZone(sizeInfo.FileSize, 0);
                        structureHelper.AddIndex(source.BaseStream.Position - 4, 0, true);
                    }
                }

                nbSamples = source.ReadInt32();

                source.BaseStream.Seek(4, SeekOrigin.Current); // Loop offset

                loopNbSamples = source.ReadInt32();
                if (version >= 0x00000101)
                {
                    recordingRate = source.ReadInt32();
                }
                if (version >= 0x00000160)
                {
                    source.BaseStream.Seek(0x7E, SeekOrigin.Begin);
                    nbLoops -= source.ReadSByte();                  // Loop base
                }
                if (version >= 0x00000151)
                {
                    source.BaseStream.Seek(0x7F, SeekOrigin.Begin);
                    nbLoops = nbLoops * source.ReadByte();          // Loop modifier
                }

                duration = (nbSamples * 1000.0 / sampleRate) + (nbLoops * (loopNbSamples * 1000.0 / sampleRate));
                if (Settings.GYM_VGM_playbackRate > 0)
                {
                    duration = duration * (Settings.GYM_VGM_playbackRate / (double)recordingRate);
                }
                if (nbLoops > 0) duration += FADEOUT_DURATION_DEFAULT;

                bitrate = (sizeInfo.FileSize - VGM_HEADER_SIZE) * 8 / duration; // TODO - use unpacked size if applicable, and not raw file size

                return true;
			}
            else
			{
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Not a VGM file");
                return false;
			}
		}

        private void readGd3Tag(BinaryReader source, int offset)
        {
            source.BaseStream.Seek(offset, SeekOrigin.Begin);
            string str;

            if (GD3_SIGNATURE.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(GD3_SIGNATURE.Length))))
            {
                source.BaseStream.Seek(4, SeekOrigin.Current); // Version number
                source.BaseStream.Seek(4, SeekOrigin.Current); // Length

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Title (english)
                tagData.IntegrateValue(TagData.TAG_FIELD_TITLE, str);
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Title (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "TITLE_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Game name (english)
                tagData.IntegrateValue(TagData.TAG_FIELD_ALBUM,  str);
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Game name (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "GAME_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // System name (english)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "SYSTEM", str));
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // System name (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "SYSTEM_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Author (english)
                tagData.IntegrateValue(TagData.TAG_FIELD_ARTIST, str);
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Author (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "AUTHOR_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Release date
                tagData.IntegrateValue(TagData.TAG_FIELD_RECORDING_DATE, str);

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Dumper
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "DUMPER", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Notes
                tagData.IntegrateValue(TagData.TAG_FIELD_COMMENT, str);
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Not a GD3 footer");
            }
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

            resetData();

            source.BaseStream.Seek(0, SeekOrigin.Begin);

            MemoryStream memStream = null;
            BinaryReader usedSource = source;

            byte[] headerSignature = source.ReadBytes(2);
            source.BaseStream.Seek(0, SeekOrigin.Begin);
            if (headerSignature[0] == 0x1f && headerSignature[1] == 0x8b) // File is GZIP-compressed
            {
                if (readTagParams.PrepareForWriting)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Writing metadata to gzipped VGM files is not supported yet.");
                    return false;
                }

                using (GZipStream gzStream = new GZipStream(source.BaseStream, CompressionMode.Decompress))
                {
                    memStream = new MemoryStream();
                    StreamUtils.CopyStream(gzStream, memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                    usedSource = new BinaryReader(memStream);
                }
            }

            isValid = readHeader(usedSource, readTagParams);

            if (isValid && gd3TagOffset > VGM_HEADER_SIZE)
            {
                tagExists = true;
                readGd3Tag(usedSource, gd3TagOffset);
            }

            return result;
		}

        // Write GD3 tag
        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            byte[] endString = new byte[2] { 0, 0 };
            int result = 11; // 11 field to write
            long sizePos;
            string str;
            Encoding unicodeEncoder = Encoding.Unicode;

            w.Write(Utils.Latin1Encoding.GetBytes(GD3_SIGNATURE));
            w.Write(0x00000100); // Version number

            sizePos = w.BaseStream.Position;
            w.Write((int)0);

            w.Write(unicodeEncoder.GetBytes(tag.Title));
            w.Write(endString); // Strings must be null-terminated
            str = "";
            if (AdditionalFields.ContainsKey("TITLE_J")) str = AdditionalFields["TITLE_J"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            w.Write(unicodeEncoder.GetBytes(tag.Album));
            w.Write(endString);
            str = "";
            if (AdditionalFields.ContainsKey("GAME_J")) str = AdditionalFields["GAME_J"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            str = "";
            if (AdditionalFields.ContainsKey("SYSTEM")) str = AdditionalFields["SYSTEM"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);
            str = "";
            if (AdditionalFields.ContainsKey("SYSTEM_J")) str = AdditionalFields["SYSTEM_J"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            w.Write(unicodeEncoder.GetBytes(tag.Artist));
            w.Write(endString);
            str = "";
            if (AdditionalFields.ContainsKey("AUTHOR_J")) str = AdditionalFields["AUTHOR_J"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            string dateStr = "";
            if (Date != DateTime.MinValue) dateStr = Date.ToString("yyyy/MM/dd");
            else if (tag.RecordingYear != null && tag.RecordingYear.Length == 4)
            {
                dateStr = tag.RecordingYear;
                if (tag.RecordingDayMonth != null && tag.RecordingDayMonth.Length >= 4)
                {
                    dateStr += "/" + tag.RecordingDayMonth.Substring(tag.RecordingDayMonth.Length - 2, 2) + "/" + tag.RecordingDayMonth.Substring(0, 2);
                }
            }
            w.Write(unicodeEncoder.GetBytes(dateStr));
            w.Write(endString);

            str = "";
            if (AdditionalFields.ContainsKey("DUMPER")) str = AdditionalFields["DUMPER"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            w.Write(unicodeEncoder.GetBytes(tag.Comment));
            w.Write(endString);

            w.Write(endString); // Is supposed to be there, according to sample files

            int size = (int)(w.BaseStream.Position - sizePos - 4);
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            w.Write(size);

            return result;
        }
    }

}