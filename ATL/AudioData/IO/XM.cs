using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using System.Linq;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Extended Module files manipulation (extensions : .XM)
    /// </summary>
    class XM : MetaDataIO, IAudioDataIO
    {
        private const string ZONE_TITLE = "title";

        private static readonly byte[] XM_SIGNATURE = Utils.Latin1Encoding.GetBytes("Extended Module: ");

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable IDE0051 // Remove unused private members
        // Effects (NB : very close to the MOD effect codes)
        private const byte EFFECT_POSITION_JUMP = 0xB;
        private const byte EFFECT_PATTERN_BREAK = 0xD;
        private const byte EFFECT_SET_SPEED = 0xF;
        private const byte EFFECT_EXTENDED = 0xE;

        private const byte EFFECT_EXTENDED_LOOP = 0x6;
        private const byte EFFECT_NOTE_CUT = 0xC;
        private const byte EFFECT_NOTE_DELAY = 0xD;
        private const byte EFFECT_PATTERN_DELAY = 0xE;
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore S1144 // Unused private types or members should be removed


        // Standard fields
        private IList<byte> FPatternTable;
        private IList<IList<IList<Event>>> FPatterns;
        private IList<Instrument> FInstruments;

        private ushort initialSpeed; // Ticks per line
        private ushort initialTempo; // BPM

        private byte nbChannels;
        private string trackerName;

        private SizeInfo sizeInfo;
        private readonly AudioFormat audioFormat;

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate => 0;
        public bool IsVBR => false;
        public AudioFormat AudioFormat
        {
            get
            {
                AudioFormat f = new AudioFormat(audioFormat);
                f.Name = f.Name + " (" + trackerName + ")";
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

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        // IMetaDataIO
        protected override int getDefaultTagOffset() => TO_BUILTIN;
        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            throw new NotImplementedException();
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private sealed class Instrument
        {
            public String DisplayName = "";
            // Other fields not useful for ATL
        }

        private sealed class Event
        {
            public byte Command = 0;
            public byte Info = 0;
            // Other fields not useful for ATL
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;

            FPatternTable = new List<byte>();

            FPatterns = new List<IList<IList<Event>>>();
            FInstruments = new List<Instrument>();

            trackerName = "";
            nbChannels = 0;

            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public XM(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            audioFormat = format;
            resetData();
        }


        // === PRIVATE METHODS ===

        private double calculateDuration()
        {
            double result = 0;

            // Jump and break control variables
            int currentPatternIndex = 0;    // Index in the pattern table
            int currentPattern = 0;         // Pattern number per se
            int currentRow = 0;
            bool positionJump = false;
            bool patternBreak = false;

            // Loop control variables
            bool isInsideLoop = false;
            double loopDuration = 0;

            IList<Event> row;

            double speed = initialSpeed;
            double tempo = initialTempo;

            do // Patterns loop
            {
                do // Lines loop
                {
                    currentPattern = FPatternTable[currentPatternIndex];

                    while ((currentPattern > FPatterns.Count - 1) && (currentPatternIndex < FPatternTable.Count - 1))
                    {
                        if (currentPattern.Equals(255)) // End of song / sub-song
                        {
                            // Reset speed & tempo to file default (do not keep remaining values from previous sub-song)
                            speed = initialSpeed;
                            tempo = initialTempo;
                        }
                        currentPattern = FPatternTable[++currentPatternIndex];
                    }
                    if (currentPattern > FPatterns.Count - 1) return result;

                    row = FPatterns[currentPattern][currentRow];
                    foreach (Event theEvent in row) // Events loop
                    {

                        if (theEvent.Command.Equals(EFFECT_SET_SPEED))
                        {
                            if (theEvent.Info > 0)
                            {
                                if (theEvent.Info > 32) // BPM
                                {
                                    tempo = theEvent.Info;
                                }
                                else // ticks per line
                                {
                                    speed = theEvent.Info;
                                }
                            }
                        }
                        else if (theEvent.Command.Equals(EFFECT_POSITION_JUMP))
                        {
                            // Processes position jump only if the jump is forward
                            // => Prevents processing "forced" song loops ad infinitum
                            if (theEvent.Info > currentPatternIndex)
                            {
                                currentPatternIndex = Math.Min(theEvent.Info, FPatternTable.Count - 1);
                                currentRow = 0;
                                positionJump = true;
                            }
                        }
                        else if (theEvent.Command.Equals(EFFECT_PATTERN_BREAK))
                        {
                            currentPatternIndex++;
                            currentRow = Math.Min(theEvent.Info, (byte)63);
                            patternBreak = true;
                        }
                        else if (theEvent.Command.Equals(EFFECT_EXTENDED))
                        {
                            if ((theEvent.Info >> 4).Equals(EFFECT_EXTENDED_LOOP))
                            {
                                if ((theEvent.Info & 0xF).Equals(0)) // Beginning of loop
                                {
                                    loopDuration = 0;
                                    isInsideLoop = true;
                                }
                                else // End of loop + nb. repeat indicator
                                {
                                    result += loopDuration * (theEvent.Info & 0xF);
                                    isInsideLoop = false;
                                }
                            }
                        }

                        if (positionJump || patternBreak) break;
                    } // end Events loop

                    result += 60 * (speed / (24 * tempo));
                    if (isInsideLoop) loopDuration += 60 * (speed / (24 * tempo));

                    if (positionJump || patternBreak) break;

                    currentRow++;
                } while (currentRow < FPatterns[currentPattern].Count);

                if (positionJump || patternBreak)
                {
                    positionJump = false;
                    patternBreak = false;
                }
                else
                {
                    currentPatternIndex++;
                    currentRow = 0;
                }
            } while (currentPatternIndex < FPatternTable.Count); // end patterns loop


            return result;
        }

        private void readInstruments(BufferedBinaryReader source, int nbInstruments)
        {
            IList<UInt32> sampleSizes = new List<uint>();

            for (int i = 0; i < nbInstruments; i++)
            {
                Instrument instrument = new Instrument();
                var instrumentHeaderSize = source.ReadUInt32();
                instrument.DisplayName = Utils.Latin1Encoding.GetString(source.ReadBytes(22)).Trim();
                instrument.DisplayName = instrument.DisplayName.Replace("\0", "");
                source.Seek(1, SeekOrigin.Current); // Instrument type; useless according to documentation
                var nbSamples = source.ReadUInt16();
                source.Seek(instrumentHeaderSize - 29, SeekOrigin.Current);

                if (nbSamples > 0)
                {
                    sampleSizes.Clear();
                    for (int j = 0; j < nbSamples; j++) // Sample headers
                    {
                        sampleSizes.Add(source.ReadUInt32());
                        source.Seek(36, SeekOrigin.Current);
                    }
                    for (int j = 0; j < nbSamples; j++) // Sample data
                    {
                        source.Seek(sampleSizes[j], SeekOrigin.Current);
                    }
                }

                FInstruments.Add(instrument);
            }
        }

        private void readPatterns(BufferedBinaryReader source, int nbPatterns)
        {
            byte firstByte;
            IList<Event> aRow;
            IList<IList<Event>> aPattern;

            ushort nbRows;
            uint packedDataSize;

            for (int i = 0; i < nbPatterns; i++)
            {
                aPattern = new List<IList<Event>>();

                source.Seek(4, SeekOrigin.Current); // Header length
                source.Seek(1, SeekOrigin.Current); // Packing type
                nbRows = source.ReadUInt16();

                packedDataSize = source.ReadUInt16();

                if (packedDataSize > 0) // The patterns is not empty
                {
                    for (int j = 0; j < nbRows; j++)
                    {
                        aRow = new List<Event>();

                        for (int k = 0; k < nbChannels; k++)
                        {
                            Event e = new Event();
                            firstByte = source.ReadByte();
                            if ((firstByte & 0x80) > 0) // Most Significant Byte (MSB) is set => packed data layout
                            {
                                if ((firstByte & 0x1) > 0) source.Seek(1, SeekOrigin.Current); // Note
                                if ((firstByte & 0x2) > 0) source.Seek(1, SeekOrigin.Current); // Instrument
                                if ((firstByte & 0x4) > 0) source.Seek(1, SeekOrigin.Current); // Volume
                                if ((firstByte & 0x8) > 0) e.Command = source.ReadByte();                // Effect type
                                if ((firstByte & 0x10) > 0) e.Info = source.ReadByte();                  // Effect param

                            }
                            else
                            { // No MSB set => standard data layout
                                // firstByte is the Note
                                source.Seek(1, SeekOrigin.Current); // Instrument
                                source.Seek(1, SeekOrigin.Current); // Volume
                                e.Command = source.ReadByte();
                                e.Info = source.ReadByte();
                            }

                            aRow.Add(e);
                        }

                        aPattern.Add(aRow);
                    }
                }

                FPatterns.Add(aPattern);
            }
        }


        // === PUBLIC METHODS ===

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, XM_SIGNATURE);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            bool result = true;
            ushort trackerVersion;
            StringBuilder comment = new StringBuilder("");

            resetData();
            BufferedBinaryReader bSource = new BufferedBinaryReader(source);

            // File format signature
            if (!IsValidHeader(bSource.ReadBytes(17)))
            {
                throw new InvalidDataException("Invalid XM file (file signature String mismatch)");
            }

            // Title = chars 17 to 37 (length 20)
            string title = StreamUtils.ReadNullTerminatedStringFixed(bSource, System.Text.Encoding.ASCII, 20);
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddZone(17, 20, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, ZONE_TITLE);
            }
            tagData.IntegrateValue(Field.TITLE, title.Trim());

            // File format signature
            if (!0x1a.Equals(bSource.ReadByte()))
            {
                throw new InvalidDataException("Invalid XM file (file signature ID mismatch)");
            }

            trackerName = StreamUtils.ReadNullTerminatedStringFixed(bSource, Encoding.ASCII, 20).Trim();

            bSource.Seek(2, SeekOrigin.Current); // Tracker version (unused)

            AudioDataOffset = bSource.Position;
            AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

            uint headerSize = bSource.ReadUInt32();
            uint songLength = bSource.ReadUInt16();
            bSource.Seek(2, SeekOrigin.Current); // Restart position

            nbChannels = (byte)Math.Min(bSource.ReadUInt16(), (ushort)0xFF);
            ushort nbPatterns = bSource.ReadUInt16();
            ushort nbInstruments = bSource.ReadUInt16();
            bSource.Seek(2, SeekOrigin.Current); // Flags for frequency tables; useless for ATL

            initialSpeed = bSource.ReadUInt16();
            initialTempo = bSource.ReadUInt16();

            // Pattern table
            for (int i = 0; i < headerSize - 20; i++) // 20 being the number of bytes read since the header size marker
            {
                if (i < songLength) FPatternTable.Add(bSource.ReadByte()); else bSource.Seek(1, SeekOrigin.Current);
            }

            readPatterns(bSource, nbPatterns);
            readInstruments(bSource, nbInstruments);


            // == Computing track properties

            Duration = calculateDuration() * 1000.0;
            foreach (var i in FInstruments.Where(i => i.DisplayName.Length > 0))
            {
                comment.Append(i.DisplayName).Append(Settings.InternalValueSeparator);
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