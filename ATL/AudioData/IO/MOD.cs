using System;
using System.IO;
using System.Collections.Generic;
using ATL.Logging;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using System.Linq;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Noisetracker/Soundtracker/Protracker Module files manipulation (extensions : .MOD)
    /// Based on info obtained from Thunder's readme (MODFIL10.TXT - Version 1.0)
    /// </summary>
    class MOD : MetaDataIO, IAudioDataIO
    {
        private const string ZONE_TITLE = "title";

        private const string SIG_POWERPACKER = "PP20";
        private const byte NB_CHANNELS_DEFAULT = 4;
        private const byte MAX_ROWS = 64;

        private const byte DEFAULT_TICKS_PER_ROW = 6;
        private const byte DEFAULT_BPM = 125;

        // Effects
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable IDE0051 // Remove unused private members
        private const byte EFFECT_POSITION_JUMP = 0xB;
        private const byte EFFECT_PATTERN_BREAK = 0xD;
        private const byte EFFECT_SET_SPEED = 0xF;
        private const byte EFFECT_EXTENDED = 0xE;

        private const byte EFFECT_EXTENDED_LOOP = 0x6;
        private const byte EFFECT_NOTE_CUT = 0xC;
        private const byte EFFECT_NOTE_DELAY = 0xD;
        private const byte EFFECT_PATTERN_DELAY = 0xE;
        private const byte EFFECT_INVERT_LOOP = 0xF;
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore S1144 // Unused private types or members should be removed

        private static readonly IDictionary<string, ModFormat> modFormats = new Dictionary<string, ModFormat>
        {
            { "M.K.", new ModFormat("ProTracker", "M.K.", 31, 4) },
            { "M!K!", new ModFormat("ProTracker", "M!K!", 31, 4) },
            { "FLT4", new ModFormat("StarTrekker", "FLT4", 31, 4)},
            { "2CHN", new ModFormat("FastTracker", "2CHN", 31, 2)},
            { "4CHN", new ModFormat("FastTracker", "4CHN", 31, 4)},
            { "6CHN", new ModFormat("FastTracker", "6CHN", 31, 6)},
            { "8CHN", new ModFormat("FastTracker", "8CHN", 31, 8)},
            { "OCTA", new ModFormat("FastTracker", "OCTA", 31, 8)},
            { "FLT8", new ModFormat("StarTrekker", "FLT8", 31, 8)},
            { "CD81", new ModFormat("Falcon", "CD81", 31, 8) },
            { "10CH", new ModFormat("FastTracker", "10CH", 31, 10)},
            { "12CH", new ModFormat("FastTracker", "12CH", 31, 12)},
            { "14CH", new ModFormat("FastTracker", "14CH", 31, 14)},
            { "11CH", new ModFormat("TakeTracker", "11CH", 31, 11)},
            { "13CH", new ModFormat("TakeTracker", "13CH", 31, 13)},
            { "15CH", new ModFormat("TakeTracker", "15CH", 31, 15)},
            { "16CH", new ModFormat("FastTracker", "16CH", 31, 16)},
            { "17CH", new ModFormat("FastTracker", "17CH", 31, 17)},
            { "18CH", new ModFormat("FastTracker", "18CH", 31, 18)},
            { "19CH", new ModFormat("FastTracker", "19CH", 31, 19)},
            { "20CH", new ModFormat("FastTracker", "20CH", 31, 20)},
            { "21CH", new ModFormat("FastTracker", "21CH", 31, 21)},
            { "22CH", new ModFormat("FastTracker", "22CH", 31, 22)},
            { "23CH", new ModFormat("FastTracker", "23CH", 31, 23)},
            { "24CH", new ModFormat("FastTracker", "24CH", 31, 24)},
            { "25CH", new ModFormat("FastTracker", "25CH", 31, 25)},
            { "26CH", new ModFormat("FastTracker", "26CH", 31, 26)},
            { "27CH", new ModFormat("FastTracker", "27CH", 31, 27)},
            { "28CH", new ModFormat("FastTracker", "28CH", 31, 28)},
            { "29CH", new ModFormat("FastTracker", "29CH", 31, 29)},
            { "30CH", new ModFormat("FastTracker", "30CH", 31, 30)},
            { "31CH", new ModFormat("FastTracker", "31CH", 31, 31)},
            { "32CH", new ModFormat("FastTracker", "32CH", 31, 32)},
            { "33CH", new ModFormat("FastTracker", "33CH", 31, 33)},
            { "TDZ1", new ModFormat("TakeTracker", "TDZ1", 31, 1)},
            { "TDZ2", new ModFormat("TakeTracker", "TDZ2", 31, 2)},
            { "TDZ3", new ModFormat("TakeTracker", "TDZ3", 31, 3)},
            { "5CHN", new ModFormat("TakeTracker", "5CHN", 31, 5)},
            { "7CHN", new ModFormat("TakeTracker", "7CHN", 31, 7)},
            { "9CHN", new ModFormat("TakeTracker", "9CHN", 31, 9)}
        };

        // Standard fields
        private IList<Sample> FSamples;
        private IList<IList<IList<int>>> FPatterns;
        private IList<byte> FPatternTable;
        private byte nbValidPatterns;
        private string formatTag;
        private byte nbChannels;

        private SizeInfo sizeInfo;
        private readonly AudioFormat audioFormat;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        /// <inheritdoc/>
        public int SampleRate => 0;
        /// <inheritdoc/>
        public bool IsVBR => false;
        /// <inheritdoc/>
        public AudioFormat AudioFormat
        {
            get
            {
                AudioFormat f = new AudioFormat(audioFormat);
                if (modFormats.TryGetValue(formatTag, out var format))
                    f.Name = f.Name + " (" + format.Name + ")";
                else
                    f.Name = f.Name + " (Unknown)";
                return f;
            }
        }
        /// <inheritdoc/>
        public int CodecFamily => AudioDataIOFactory.CF_SEQ_WAV;
        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public double BitRate { get; private set; }

        /// <inheritdoc/>
        public int BitDepth => -1; // Irrelevant for that format
        /// <inheritdoc/>
        public double Duration { get; private set; }

        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement => STEREO;
        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // IMetaDataIO
        /// <inheritdoc/>
        protected override int getDefaultTagOffset() => TO_BUILTIN;
        /// <inheritdoc/>
        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;
        /// <inheritdoc/>
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            throw new NotImplementedException();
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        internal class Sample
        {
            public string Name;
            public int Size;
            public SByte Finetune;
            public byte Volume;

            public int RepeatOffset;
            public int RepeatLength;

            public void Reset()
            {
                Name = "";
                Size = 0;
                Finetune = 0;
                Volume = 0;

                RepeatLength = 0;
                RepeatOffset = 0;
            }
        }

        internal class ModFormat
        {
            public readonly string Name;
            public readonly string Signature;
            public readonly byte NbSamples = 0;
            public readonly byte NbChannels = 0;

            public ModFormat(string name, string sig, byte nbSamples, byte nbChannels)
            {
                Name = name;
                Signature = sig;
                NbSamples = nbSamples;
                NbChannels = nbChannels;
            }
        }

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;

            FSamples = new List<Sample>();
            FPatterns = new List<IList<IList<int>>>();
            FPatternTable = new List<byte>();
            nbValidPatterns = 0;
            formatTag = "";
            nbChannels = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public MOD(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            this.audioFormat = format;
            resetData();
        }



        // ---------- SUPPORT METHODS

        //                 24 * beats/minute
        // lines/minute = -----------------
        //                    ticks/line
        private double calculateDuration()
        {
            double result = 0;

            // Jump and break control variables
            int currentPattern = 0;
            int currentRow = 0;
            bool positionJump = false;
            bool patternBreak = false;

            // Loop control variables
            bool isInsideLoop = false;
            double loopDuration = 0;

            IList<int> row;

            int temp;
            double ticksPerRow = DEFAULT_TICKS_PER_ROW;
            double bpm = DEFAULT_BPM;

            int effect;
            int arg1;
            int arg2;

            do // Patterns loop
            {
                do // Rows loop
                {
                    row = FPatterns[FPatternTable[currentPattern]][currentRow];
                    foreach (int note in row) // Channels loop
                    {
                        effect = (note & 0xF00) >> 8;
                        arg1 = (note & 0xF0) >> 4;
                        arg2 = note & 0xF;

                        if (effect.Equals(EFFECT_SET_SPEED))
                        {
                            temp = arg1 * 16 + arg2;
                            if (temp > 32) // BPM
                            {
                                bpm = temp;
                            }
                            else // ticks per row
                            {
                                ticksPerRow = temp;
                            }
                        }
                        else if (effect.Equals(EFFECT_POSITION_JUMP))
                        {
                            temp = arg1 * 16 + arg2;

                            // Processes position jump only if the jump is forward
                            // => Prevents processing "forced" song loops ad infinitum
                            if (temp > currentPattern)
                            {
                                currentPattern = temp;
                                currentRow = 0;
                                positionJump = true;
                            }
                        }
                        else if (effect.Equals(EFFECT_PATTERN_BREAK))
                        {
                            currentPattern++;
                            currentRow = arg1 * 10 + arg2;
                            patternBreak = true;
                        }
                        else if (effect.Equals(EFFECT_EXTENDED))
                        {
                            if (arg1.Equals(EFFECT_EXTENDED_LOOP))
                            {
                                if (arg2.Equals(0)) // Beginning of loop
                                {
                                    loopDuration = 0;
                                    isInsideLoop = true;
                                }
                                else // End of loop + nb. repeat indicator
                                {
                                    result += loopDuration * arg2;
                                    isInsideLoop = false;
                                }
                            }
                        }
                        if (positionJump || patternBreak) break;
                    } // end channels loop

                    result += 60 * (ticksPerRow / (24 * bpm));
                    if (isInsideLoop) loopDuration += 60 * (ticksPerRow / (24 * bpm));

                    if (positionJump || patternBreak) break;

                    currentRow++;
                } while (currentRow < MAX_ROWS);

                if (positionJump || patternBreak)
                {
                    positionJump = false;
                    patternBreak = false;
                }
                else
                {
                    currentPattern++;
                    currentRow = 0;
                }
            } while (currentPattern < nbValidPatterns); // end patterns loop

            return result * 1000.0;
        }

        private byte detectNbSamples(BufferedBinaryReader source)
        {
            byte result = 31;
            long position = source.Position;

            source.Seek(1080, SeekOrigin.Begin);

            formatTag = Utils.Latin1Encoding.GetString(source.ReadBytes(4)).Trim();

            if (!modFormats.ContainsKey(formatTag)) result = 15;

            source.Seek(position, SeekOrigin.Begin);

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
            bool result = true;
            int maxPatterns = -1;
            byte nbSamples;

            string readString;
            StringBuilder comment = new StringBuilder("");

            Sample sample;
            IList<IList<int>> pattern;
            IList<int> row;

            resetData();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            // == TITLE ==
            readString = Utils.Latin1Encoding.GetString(reader.ReadBytes(4));
            if (readString.Equals(SIG_POWERPACKER))
            {
                throw new InvalidDataException("MOD files compressed with PowerPacker are not supported yet");
            }

            // Restart from beginning, else parser might miss empty titles
            reader.Seek(0, SeekOrigin.Begin);

            // Title = max first 20 chars; null-terminated
            string title = StreamUtils.ReadNullTerminatedStringFixed(reader, Encoding.ASCII, 20);
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddZone(0, 20, new byte[20] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, ZONE_TITLE);
            }
            tagData.IntegrateValue(Field.TITLE, title.Trim());

            AudioDataOffset = reader.Position;
            AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

            // == SAMPLES ==
            nbSamples = detectNbSamples(reader);
            string charOne = Utils.Latin1Encoding.GetString(new byte[] { 1 });

            for (int i = 0; i < nbSamples; i++)
            {
                sample = new Sample();
                sample.Name = StreamUtils.ReadNullTerminatedStringFixed(reader, Encoding.ASCII, 22).Trim();
                sample.Name = sample.Name.Replace("\0", "");
                sample.Name = sample.Name.Replace(charOne, "");
                sample.Size = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2)) * 2;
                sample.Finetune = reader.ReadSByte();
                sample.Volume = reader.ReadByte();
                sample.RepeatOffset = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2)) * 2;
                sample.RepeatLength = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2)) * 2;
                FSamples.Add(sample);
            }


            // == SONG ==
            nbValidPatterns = reader.ReadByte();
            reader.Seek(1, SeekOrigin.Current); // Controversial byte; no real use here
            for (int i = 0; i < 128; i++) FPatternTable.Add(reader.ReadByte()); // Pattern table

            // File format tag
            formatTag = Utils.Latin1Encoding.GetString(reader.ReadBytes(4)).Trim();
            if (modFormats.ContainsKey(formatTag))
            {
                nbChannels = modFormats[formatTag].NbChannels;
            }
            else // Default
            {
                nbChannels = NB_CHANNELS_DEFAULT;
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "MOD format tag '" + formatTag + "'not recognized");
            }

            // == PATTERNS ==
            // Some extra information about the "FLT8" -type MOD's:
            //
            // These MOD's have 8 channels, still the format isn't the same as the
            // other 8 channel formats ("OCTA", "CD81", "8CHN"): instead of storing
            // ONE 8-track pattern, it stores TWO 4-track patterns per logical pattern.
            // i.e. The first 4 channels of the first logical pattern are stored in
            // the first physical 4-channel pattern (size 1kb) whereas channel 5 until
            // channel 8 of the first logical pattern are stored as the SECOND physical
            // 4-channel pattern. Got it? ;-).
            // If you convert all the 4 channel patterns to 8 channel patterns, do not
            // forget to divide each pattern nr by 2 in the pattern sequence table!

            foreach (byte b in FPatternTable) maxPatterns = Math.Max(maxPatterns, b);

            for (int p = 0; p < maxPatterns + 1; p++) // Patterns loop
            {
                FPatterns.Add(new List<IList<int>>());
                pattern = FPatterns[FPatterns.Count - 1];
                // Rows loop
                for (int l = 0; l < MAX_ROWS; l++)
                {
                    pattern.Add(new List<int>());
                    row = pattern[pattern.Count - 1];
                    for (int c = 0; c < nbChannels; c++) // Channels loop
                    {
                        row.Add(StreamUtils.DecodeBEInt32(reader.ReadBytes(4)));
                    } // end channels loop
                } // end rows loop
            } // end patterns loop


            // == Computing track properties

            Duration = calculateDuration();
            foreach (var aSample in FSamples.Where(aSample => aSample.Name.Length > 0))
            {
                comment.Append(aSample.Name).Append(Settings.InternalValueSeparator);
            }

            if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);

            tagData.IntegrateValue(Field.COMMENT, comment.ToString());

            BitRate = sizeInfo.FileSize / Duration;

            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            int result = 0;

            if (ZONE_TITLE.Equals(zone))
            {
                string title = tag[Field.TITLE];
                if (title.Length > 20) title = title.Substring(0, 20);
                else if (title.Length < 20) title = Utils.BuildStrictLengthString(title, 20, '\0');
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(title));
                result = 1;
            }

            return result;
        }
    }

}