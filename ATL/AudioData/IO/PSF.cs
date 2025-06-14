using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Text;
using static ATL.ChannelsArrangements;
using static ATL.TagData;
using System.Threading.Tasks;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Portable Sound Format files manipulation (extensions : .PSF, .PSF1, .PSF2, 
    /// .MINIPSF, .MINIPSF1, .MINIPSF2, .SSF, .MINISSF, .DSF, .MINIDSF, .GSF, .MINIGSF, .QSF, .MINISQF)
    /// According to Neil Corlett's specifications v. 1.6
    /// </summary>
    partial class PSF : MetaDataIO, IAudioDataIO
    {
        // Format Type Names
        public const string PSF_FORMAT_UNKNOWN = "Unknown";
        public const string PSF_FORMAT_PSF1 = "Playstation";
        public const string PSF_FORMAT_PSF2 = "Playstation 2";
        public const string PSF_FORMAT_SSF = "Saturn";
        public const string PSF_FORMAT_DSF = "Dreamcast";
        public const string PSF_FORMAT_USF = "Nintendo 64";
        public const string PSF_FORMAT_QSF = "Capcom QSound";

        // Tag predefined fields
        public const string TAG_LENGTH = "length";
        public const string TAG_FADE = "fade";

        private static readonly byte[] PSF_FORMAT_TAG = Utils.Latin1Encoding.GetBytes("PSF");
        private const string TAG_HEADER = "[TAG]";
        private const uint HEADER_LENGTH = 16;

        private const byte LINE_FEED = 0x0A;
        private const byte SPACE = 0x20;

        private const int PSF_DEFAULT_DURATION = 180000; // 3 minutes

        private byte version;

        private SizeInfo sizeInfo;
        private readonly AudioFormat audioFormat;

        // Mapping between PSF frame codes and ATL frame codes
        private static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { "title", Field.TITLE },
            { "game", Field.ALBUM }, // Small innocent semantic shortcut
            { "artist", Field.ARTIST },
            { "copyright", Field.COPYRIGHT },
            { "comment", Field.COMMENT },
            { "year", Field.RECORDING_YEAR },
            { "genre", Field.GENRE },
            { "rating", Field.RATING } // Does not belong to the predefined standard PSF tags
        };
        // Frames that are required for playback
        private static readonly IList<string> playbackFrames = new List<string>
        {
            "volume",
            "length",
            "fade",
            "filedir",
            "filename",
            "fileext"
        };


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // AudioDataIO
        public int SampleRate { get; private set; }

        public bool IsVBR => false;
        public AudioFormat AudioFormat
        {
            get
            {
                AudioFormat f = new AudioFormat(audioFormat);
                f.Name = f.Name + " (" + subformat() + ")";
                return f;
            }
        }
        public int CodecFamily => AudioDataIOFactory.CF_SEQ_WAV;
        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => -1; // Irrelevant for that format
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement => STEREO;
        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        // IMetaDataIO
        protected override int getDefaultTagOffset() => TO_BUILTIN;
        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID.ToLower())) supportedMetaId = frameMapping[ID.ToLower()];

            return supportedMetaId;
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private sealed class PSFHeader
        {
            public byte[] FormatTag = new byte[3];      // Format tag (should be PSF_FORMAT_TAG)
            public byte VersionByte;                    // Version mark
            public uint ReservedAreaLength;             // Length of reserved area (bytes)
            public uint CompressedProgramLength;        // Length of compressed program (bytes)

            public void Reset()
            {
                FormatTag = new byte[3];
                VersionByte = 0;
                ReservedAreaLength = 0;
                CompressedProgramLength = 0;
            }
        }

        private sealed class PSFTag
        {
            public string TagHeader;					// Tag header (should be TAG_HEADER)
            public int size;

            public void Reset()
            {
                TagHeader = "";
                size = 0;
            }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            SampleRate = 44100; // Seems to be de facto value for all PSF files, even though spec doesn't say anything about it
            version = 0;
            BitRate = 0;
            Duration = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public PSF(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            this.audioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private string subformat()
        {
            switch (version)
            {
                case 0x01: return PSF_FORMAT_PSF1;
                case 0x02: return PSF_FORMAT_PSF2;
                case 0x11: return PSF_FORMAT_SSF;
                case 0x12: return PSF_FORMAT_DSF;
                case 0x21: return PSF_FORMAT_USF;
                case 0x41: return PSF_FORMAT_QSF;
                default: return PSF_FORMAT_UNKNOWN;
            }
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, PSF_FORMAT_TAG);
        }

        private static bool readHeader(Stream source, ref PSFHeader header)
        {
            byte[] buffer = new byte[4];
            if (source.Read(header.FormatTag, 0, 3) < 3) return false;
            if (IsValidHeader(header.FormatTag))
            {
                if (source.Read(buffer, 0, 1) < 1) return false;
                header.VersionByte = buffer[0];
                if (source.Read(buffer, 0, 4) < 4) return false;
                header.ReservedAreaLength = StreamUtils.DecodeUInt32(buffer);
                if (source.Read(buffer, 0, 4) < 4) return false;
                header.CompressedProgramLength = StreamUtils.DecodeUInt32(buffer);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static string readPSFLine(Stream source, Encoding encoding)
        {
            long lineStart = source.Position;
            long lineEnd;
            bool hasEOL = true;

            if (StreamUtils.FindSequence(source, new byte[] { LINE_FEED }))
            {
                lineEnd = source.Position;
            }
            else
            {
                lineEnd = source.Length;
                hasEOL = false;
            }

            source.Seek(lineStart, SeekOrigin.Begin);

            byte[] data = new byte[lineEnd - lineStart];
            if (source.Read(data, 0, data.Length) < data.Length) return "";

            for (int i = 0; i < data.Length; i++) if (data[i] < SPACE) data[i] = SPACE; // According to spec : "All characters 0x01-0x20 are considered whitespace"

            return encoding.GetString(data, 0, data.Length - (hasEOL ? 1 : 0)).Trim(); // -1 because we don't want to include LINE_FEED in the result
        }

        private bool readTag(Stream source, ref PSFTag tag, ReadTagParams readTagParams)
        {
            long initialPosition = source.Position;
            Encoding encoding = Utils.Latin1Encoding;

            byte[] buffer = new byte[5];
            if (source.Read(buffer, 0, buffer.Length) < buffer.Length) return false;
            tag.TagHeader = Utils.Latin1Encoding.GetString(buffer);

            if (TAG_HEADER == tag.TagHeader)
            {
                string s = readPSFLine(source, encoding);

                string lastKey = "";
                string lastValue = "";
                bool lengthFieldFound = false;

                while (s != "")
                {
                    var equalIndex = s.IndexOf("=", StringComparison.Ordinal);
                    if (equalIndex != -1)
                    {
                        var keyStr = s.Substring(0, equalIndex).Trim();
                        var lowKeyStr = keyStr.ToLower();
                        var valueStr = s.Substring(equalIndex + 1, s.Length - (equalIndex + 1)).Trim();

                        if (lowKeyStr.Equals("utf8") && valueStr.Equals("1")) encoding = Encoding.UTF8;

                        if (lowKeyStr.Equals(TAG_LENGTH) || lowKeyStr.Equals(TAG_FADE))
                        {
                            if (lowKeyStr.Equals(TAG_LENGTH)) lengthFieldFound = true;
                            Duration += parsePSFDuration(valueStr);
                        }

                        // PSF specifics : a field appearing more than once is the same field, with values spanning over multiple lines
                        if (lastKey.Equals(keyStr))
                        {
                            lastValue += Environment.NewLine + valueStr;
                        }
                        else
                        {
                            SetMetaField(lastKey, lastValue, readTagParams.ReadAllMetaFrames);
                            lastValue = valueStr;
                        }
                        lastKey = keyStr;
                    }

                    s = readPSFLine(source, encoding);
                } // Metadata lines 
                SetMetaField(lastKey, lastValue, readTagParams.ReadAllMetaFrames);

                // PSF files without any 'length' tag take default duration, regardless of 'fade' value
                if (!lengthFieldFound) Duration = PSF_DEFAULT_DURATION;

                tag.size = (int)(source.Position - initialPosition);
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(initialPosition, tag.size);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private static double parsePSFDuration(string durationStr)
        {
            string hStr = "";
            string mStr = "";
            string sStr = "";
            string dStr = "";
            double result = 0;

            // decimal
            var sepIndex = durationStr.LastIndexOf('.');
            if (-1 == sepIndex) sepIndex = durationStr.LastIndexOf(',');

            if (-1 != sepIndex)
            {
                sepIndex++;
                dStr = durationStr.Substring(sepIndex, durationStr.Length - sepIndex);
                durationStr = durationStr[..Math.Max(0, sepIndex - 1)];
            }


            // seconds
            sepIndex = durationStr.LastIndexOf(':');

            sepIndex++;
            sStr = durationStr.Substring(sepIndex, durationStr.Length - sepIndex);

            durationStr = durationStr[..Math.Max(0, sepIndex - 1)];

            // minutes
            if (durationStr.Length > 0)
            {
                sepIndex = durationStr.LastIndexOf(':');

                sepIndex++;
                mStr = durationStr.Substring(sepIndex, durationStr.Length - sepIndex);

                durationStr = durationStr[..Math.Max(0, sepIndex - 1)];
            }

            // hours
            if (durationStr.Length > 0)
            {
                sepIndex = durationStr.LastIndexOf(':');

                sepIndex++;
                hStr = durationStr.Substring(sepIndex, durationStr.Length - sepIndex);
            }

            if (dStr != "") result += (int.Parse(dStr) * 100);
            if (sStr != "") result += (int.Parse(sStr) * 1000);
            if (mStr != "") result += (int.Parse(mStr) * 60000);
            if (hStr != "") result += (int.Parse(hStr) * 3600000);

            return result;
        }

        // === PUBLIC METHODS ===

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            PSFHeader header = new PSFHeader();
            PSFTag tag = new PSFTag();

            header.Reset();
            tag.Reset();
            resetData();

            if (!readHeader(source, ref header)) throw new InvalidDataException("Not a PSF file");

            AudioDataOffset = 0;

            if (source.Length > HEADER_LENGTH + header.CompressedProgramLength + header.ReservedAreaLength)
            {
                source.Seek((long)(4 + header.CompressedProgramLength + header.ReservedAreaLength), SeekOrigin.Current);

                if (!readTag(source, ref tag, readTagParams)) throw new InvalidDataException("Not a PSF tag");
            }

            AudioDataSize = sizeInfo.FileSize - tag.size;

            version = header.VersionByte;
            BitRate = AudioDataSize * 8 / Duration;

            return true;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true)) return write(tag, w);
        }

        private int write(TagData tag, BinaryWriter w)
        {
            int result = 0;
            // Keep these in memory to prevent setting them twice using AdditionalFields
            var writtenFieldCodes = new HashSet<string>();

            w.Write(Utils.Latin1Encoding.GetBytes(TAG_HEADER));

            // Announce UTF-8 support
            w.Write(Utils.Latin1Encoding.GetBytes("utf8=1"));
            w.Write(LINE_FEED);

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
                            writeTextFrame(w, s, map[frameType]);
                            writtenFieldCodes.Add(s.ToUpper());
                            result++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if (!fieldInfo.NativeFieldCode.Equals("utf8") // utf8 already written
                    && !writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToUpper())
                    )
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, FormatBeforeWriting(fieldInfo.Value));
                    result++;
                }
            }

            // Remove the last end-of-line character
            w.BaseStream.SetLength(w.BaseStream.Length - 1);

            return result;
        }

        private static void writeTextFrame(BinaryWriter writer, string frameCode, string text)
        {
            string[] textLines;
            if (text.Contains(Environment.NewLine))
            {
                // Split a multiple-line value into multiple frames with the same code
                textLines = text.Split(Environment.NewLine.ToCharArray());
            }
            else
            {
                textLines = new[] { text };
            }

            foreach (string s in textLines)
            {
                writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
                writer.Write('=');
                writer.Write(Encoding.UTF8.GetBytes(s));
                writer.Write(LINE_FEED);
            }
        }

        // Specific implementation for conservation of fields that are required for playback
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public override async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            TagData tag = prepareRemove();

            s.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            return await WriteAsync(s, tag, args);
        }

        private TagData prepareRemove()
        {
            TagData result = new TagData();

            foreach (Field b in frameMapping.Values)
            {
                result.IntegrateValue(b, "");
            }

            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                var fieldCode = fieldInfo.NativeFieldCode.ToLower();
                if (!fieldCode.StartsWith('_') && !playbackFrames.Contains(fieldCode))
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    result.AdditionalFields.Add(emptyFieldInfo);
                }
            }

            return result;
        }
    }
}
