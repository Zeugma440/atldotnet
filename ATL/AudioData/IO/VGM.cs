using System;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Text;
using System.IO.Compression;
using static ATL.ChannelsArrangements;
using static ATL.TagData;
using System.Globalization;
using System.Linq;

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
        private static readonly byte[] VGM_SIGNATURE = Utils.Latin1Encoding.GetBytes("Vgm ");
        private static readonly byte[] GD3_SIGNATURE = Utils.Latin1Encoding.GetBytes("Gd3 ");

        private const int VGM_HEADER_SIZE = 256;

        private static int LOOP_COUNT_DEFAULT = 1;              // Default loop count
        private static int FADEOUT_DURATION_DEFAULT = 10000;    // Default fadeout duration, in milliseconds (10s)
        private static int RECORDING_RATE_DEFAULT = 60;         // Default playback rate for v1.00 files

        // Standard fields
        private int sampleRate;
        private double bitrate;
        private double duration;

        private int gd3TagOffset;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // AudioDataIO
        public int SampleRate => sampleRate; // Sample rate (hz)

        public bool IsVBR => false;

        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily => AudioDataIOFactory.CF_SEQ_WAV;

        public string FileName => filePath;

        public double BitRate => bitrate;
        public int BitDepth => -1; // Irrelevant for that format
        public double Duration => duration;

        public ChannelsArrangement ChannelsArrangement => STEREO;

        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return metaDataType == MetaDataIOFactory.TagType.NATIVE;
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

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
        public override string EncodeDate(DateTime date)
        {
            // Shitty convention for Year <-> DateTime conversion
            if (1 == date.Month && 1 == date.Day)
                return date.Year.ToString();
            else
                // According to GD3 spec for release date
                return date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            // Reset variables
            sampleRate = 44100; // Default value for all VGM files, according to v1.70 spec 
            bitrate = 0;
            duration = 0;

            gd3TagOffset = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public VGM(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, VGM_SIGNATURE);
        }


        // === PRIVATE METHODS ===

        private bool readHeader(BufferedBinaryReader source, ReadTagParams readTagParams)
        {
            int nbSamples, loopNbSamples;
            int nbLoops = LOOP_COUNT_DEFAULT;
            int recordingRate = RECORDING_RATE_DEFAULT;

            byte[] headerSignature = source.ReadBytes(VGM_SIGNATURE.Length);
            if (IsValidHeader(headerSignature))
            {
                AudioDataOffset = source.Position;

                source.Seek(4, SeekOrigin.Current); // EOF offset
                int version = source.ReadInt32();
                source.Seek(8, SeekOrigin.Current); // Clocks
                gd3TagOffset = source.ReadInt32();

                if (gd3TagOffset > 0)
                {
                    gd3TagOffset += (int)source.Position - 4;
                    AudioDataSize = gd3TagOffset;
                }
                else
                    AudioDataSize = sizeInfo.FileSize;

                if (/*gd3TagOffset > 0 && */readTagParams.PrepareForWriting)
                {
                    if (gd3TagOffset > VGM_HEADER_SIZE)
                    {
                        structureHelper.AddZone(gd3TagOffset, (int)sizeInfo.FileSize - gd3TagOffset);
                        structureHelper.AddIndex(source.Position - 4, gd3TagOffset, true);
                    }
                    else
                    {
                        structureHelper.AddZone(sizeInfo.FileSize, 0);
                        structureHelper.AddIndex(source.Position - 4, (int)sizeInfo.FileSize, true);
                    }
                }

                nbSamples = source.ReadInt32();

                source.Seek(4, SeekOrigin.Current); // Loop offset

                loopNbSamples = source.ReadInt32();
                if (version >= 0x00000101)
                {
                    recordingRate = source.ReadInt32();
                }
                if (version >= 0x00000160)
                {
                    source.Seek(0x7E, SeekOrigin.Begin);
                    nbLoops -= source.ReadSByte();                  // Loop base
                }
                if (version >= 0x00000151)
                {
                    source.Seek(0x7F, SeekOrigin.Begin);
                    nbLoops *= source.ReadByte();          // Loop modifier
                }

                duration = nbSamples * 1000.0 / sampleRate + nbLoops * (loopNbSamples * 1000.0 / sampleRate);
                if (Settings.GYM_VGM_playbackRate > 0)
                {
                    duration *= (Settings.GYM_VGM_playbackRate / (double)recordingRate);
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

        private void readGd3Tag(BufferedBinaryReader source, int offset)
        {
            source.Seek(offset, SeekOrigin.Begin);
            string str;

            if (GD3_SIGNATURE.SequenceEqual(source.ReadBytes(GD3_SIGNATURE.Length)))
            {
                source.Seek(4, SeekOrigin.Current); // Version number
                source.Seek(4, SeekOrigin.Current); // Length

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Title (english)
                tagData.IntegrateValue(Field.TITLE, str);
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Title (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "TITLE_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Game name (english)
                tagData.IntegrateValue(Field.ALBUM, str);
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Game name (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "GAME_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // System name (english)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "SYSTEM", str));
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // System name (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "SYSTEM_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Author (english)
                tagData.IntegrateValue(Field.ARTIST, str);
                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Author (japanese)
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "AUTHOR_J", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Release date
                tagData.IntegrateValue(Field.RECORDING_DATE, str);

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Dumper
                tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), "DUMPER", str));

                str = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // Notes
                tagData.IntegrateValue(Field.COMMENT, str);
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Not a GD3 footer");
            }
        }

        // === PUBLIC METHODS ===

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            bool result = true;

            resetData();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            reader.Seek(0, SeekOrigin.Begin);

            MemoryStream memStream;
            BufferedBinaryReader usedSource = reader;

            byte[] headerSignature = reader.ReadBytes(2);
            reader.Seek(0, SeekOrigin.Begin);
            if (headerSignature[0] == 0x1f && headerSignature[1] == 0x8b) // File is GZIP-compressed
            {
                if (readTagParams.PrepareForWriting)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Writing metadata to gzipped VGM files is not supported yet.");
                    return false;
                }

                using (GZipStream gzStream = new GZipStream(reader, CompressionMode.Decompress))
                {
                    memStream = new MemoryStream();
                    StreamUtils.CopyStream(gzStream, memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                    usedSource = new BufferedBinaryReader(memStream);
                }
            }

            if (readHeader(usedSource, readTagParams) && gd3TagOffset > VGM_HEADER_SIZE)
            {
                tagExists = true;
                readGd3Tag(usedSource, gd3TagOffset);
            }

            return result;
        }

        // Write GD3 tag
        protected override int write(TagData tag, Stream s, string zone)
        {
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true)) return write(tag, w);
        }

        private int write(TagData tag, BinaryWriter w)
        {
            byte[] endString = new byte[2] { 0, 0 };
            int result = 11; // 11 field to write
            long sizePos;
            string str;
            Encoding unicodeEncoder = Encoding.Unicode;

            w.Write(GD3_SIGNATURE);
            w.Write(0x00000100); // Version number

            sizePos = w.BaseStream.Position;
            w.Write(0);

            w.Write(unicodeEncoder.GetBytes(tag[Field.TITLE]));
            w.Write(endString); // Strings must be null-terminated
            str = "";
            if (AdditionalFields.ContainsKey("TITLE_J")) str = AdditionalFields["TITLE_J"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            w.Write(unicodeEncoder.GetBytes(tag[Field.ALBUM]));
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

            w.Write(unicodeEncoder.GetBytes(tag[Field.ARTIST]));
            w.Write(endString);
            str = "";
            if (AdditionalFields.ContainsKey("AUTHOR_J")) str = AdditionalFields["AUTHOR_J"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            w.Write(unicodeEncoder.GetBytes(EncodeDate(Date)));
            w.Write(endString);

            str = "";
            if (AdditionalFields.ContainsKey("DUMPER")) str = AdditionalFields["DUMPER"];
            w.Write(unicodeEncoder.GetBytes(str));
            w.Write(endString);

            w.Write(unicodeEncoder.GetBytes(tag[Field.COMMENT]));
            w.Write(endString);

            w.Write(endString); // Is supposed to be there, according to sample files

            int size = (int)(w.BaseStream.Position - sizePos - 4);
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            w.Write(size);

            return result;
        }
    }

}