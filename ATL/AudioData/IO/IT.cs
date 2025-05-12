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
    /// Class for Impulse Tracker Module files manipulation (extensions : .IT)
    /// </summary>
    class IT : MetaDataIO, IAudioDataIO
    {
        private static readonly byte[] IT_SIGNATURE = Utils.Latin1Encoding.GetBytes("IMPM");

        private const string ZONE_TITLE = "title";

        // Effects
        private const byte EFFECT_SET_SPEED = 0x01;
        private const byte EFFECT_ORDER_JUMP = 0x02;
        private const byte EFFECT_JUMP_TO_ROW = 0x03;
        private const byte EFFECT_EXTENDED = 0x13;
        private const byte EFFECT_SET_BPM = 0x14;

        private const byte EFFECT_EXTENDED_LOOP = 0xB;


        // Standard fields
        private IList<byte> patternTable;
        private IList<IList<IList<Event>>> patterns;
        private IList<Instrument> instruments;

        private byte initialSpeed;
        private byte initialTempo;

        private SizeInfo sizeInfo;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        /// <inheritdoc/>
        public int SampleRate => 0;

        /// <inheritdoc/>
        public bool IsVBR => false;

        /// <inheritdoc/>
        public AudioFormat AudioFormat { get; }
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
        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }
        /// <inheritdoc/>
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


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            Duration = 0;
            BitRate = 0;

            patternTable = new List<byte>();

            patterns = new List<IList<IList<Event>>>();
            instruments = new List<Instrument>();

            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public IT(string filePath, AudioFormat format)
        {
            FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private sealed class Instrument
        {
            public string FileName = "";
            public string DisplayName = "";
            // Other fields not useful for ATL
        }

        private sealed class Event
        {
            public int Channel = 0;
            public byte Command = 0;
            public byte Info = 0;
            // Other fields not useful for ATL
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
            double previousTempo = tempo;

            do // Patterns loop
            {
                do // Lines loop
                {
                    currentPattern = patternTable[currentPatternIndex];

                    while ((currentPattern > patterns.Count - 1) && (currentPatternIndex < patternTable.Count - 1))
                    {
                        if (currentPattern.Equals(255)) // End of song / sub-song
                        {
                            // Reset speed & tempo to file default (do not keep remaining values from previous sub-song)
                            speed = initialSpeed;
                            tempo = initialTempo;
                        }
                        currentPattern = patternTable[++currentPatternIndex];
                    }
                    if (currentPattern > patterns.Count - 1) return result;

                    row = patterns[currentPattern][currentRow];
                    foreach (Event theEvent in row) // Events loop
                    {

                        if (theEvent.Command.Equals(EFFECT_SET_SPEED))
                        {
                            if (theEvent.Info > 0) speed = theEvent.Info;
                        }
                        else if (theEvent.Command.Equals(EFFECT_SET_BPM))
                        {
                            if (theEvent.Info > 0x20)
                            {
                                tempo = theEvent.Info;
                            }
                            else
                            {
                                if (theEvent.Info.Equals(0))
                                {
                                    tempo = previousTempo;
                                }
                                else
                                {
                                    previousTempo = tempo;
                                    if (theEvent.Info < 0x10)
                                    {
                                        tempo -= theEvent.Info;
                                    }
                                    else
                                    {
                                        tempo += (theEvent.Info - 0x10);
                                    }
                                }
                            }
                        }
                        else if (theEvent.Command.Equals(EFFECT_ORDER_JUMP))
                        {
                            // Processes position jump only if the jump is forward
                            // => Prevents processing "forced" song loops ad infinitum
                            if (theEvent.Info > currentPatternIndex)
                            {
                                currentPatternIndex = Math.Min(theEvent.Info, patternTable.Count - 1);
                                currentRow = 0;
                                positionJump = true;
                            }
                        }
                        else if (theEvent.Command.Equals(EFFECT_JUMP_TO_ROW))
                        {
                            currentPatternIndex++;
                            currentRow = Math.Min(theEvent.Info, patterns[currentPattern].Count);
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
                } while (currentRow < patterns[currentPattern].Count);

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
            } while (currentPatternIndex < patternTable.Count); // end patterns loop


            return result;
        }

        private void readSamples(BufferedBinaryReader source, IList<uint> samplePointers)
        {
            foreach (uint pos in samplePointers)
            {
                source.Seek(pos, SeekOrigin.Begin);
                Instrument instrument = new Instrument();

                source.Seek(4, SeekOrigin.Current); // Signature
                instrument.FileName = Utils.Latin1Encoding.GetString(source.ReadBytes(12)).Trim();
                instrument.FileName = instrument.FileName.Replace("\0", "");

                source.Seek(4, SeekOrigin.Current); // Data not relevant for ATL

                instrument.DisplayName = StreamUtils.ReadNullTerminatedStringFixed(source, Utils.Latin1Encoding, 26);
                instrument.DisplayName = instrument.DisplayName.Replace("\0", "");

                instruments.Add(instrument);
            }
        }

        private void readInstruments(BufferedBinaryReader source, IList<uint> instrumentPointers)
        {
            foreach (uint pos in instrumentPointers)
            {
                source.Seek(pos, SeekOrigin.Begin);
                Instrument instrument = new Instrument();

                source.Seek(4, SeekOrigin.Current); // Signature
                instrument.FileName = Utils.Latin1Encoding.GetString(source.ReadBytes(12)).Trim();
                instrument.FileName = instrument.FileName.Replace("\0", "");

                source.Seek(16, SeekOrigin.Current); // Data not relevant for ATL

                instrument.DisplayName = StreamUtils.ReadNullTerminatedStringFixed(source, Utils.Latin1Encoding, 26);
                instrument.DisplayName = instrument.DisplayName.Replace("\0", "");

                instruments.Add(instrument);
            }
        }

        private void readInstrumentsOld(BufferedBinaryReader source, IList<uint> instrumentPointers)
        {
            // The fileName and displayName fields have the same offset in the new and old format
            readInstruments(source, instrumentPointers);
        }

        private void readPatterns(BufferedBinaryReader source, IList<uint> patternPointers)
        {
            ushort nbRows;
            ushort rowNum;
            byte what;
            byte maskVariable = 0;
            IList<Event> aRow;
            IList<IList<Event>> aPattern;
            IDictionary<int, byte> maskVariables = new Dictionary<int, byte>();

            foreach (uint pos in patternPointers)
            {
                aPattern = new List<IList<Event>>();
                if (pos > 0)
                {
                    source.Seek(pos, SeekOrigin.Begin);
                    aRow = new List<Event>();
                    rowNum = 0;
                    source.Seek(2, SeekOrigin.Current); // patternSize
                    nbRows = source.ReadUInt16();
                    source.Seek(4, SeekOrigin.Current); // unused data

                    do
                    {
                        what = source.ReadByte();

                        if (what > 0)
                        {
                            Event theEvent = new Event
                            {
                                Channel = (what - 1) & 63
                            };
                            if ((what & 128) > 0)
                            {
                                maskVariable = source.ReadByte();
                                maskVariables[theEvent.Channel] = maskVariable;
                            }
                            else if (maskVariables.ContainsKey(theEvent.Channel))
                            {
                                maskVariable = maskVariables[theEvent.Channel];
                            }
                            else
                            {
                                maskVariable = 0;
                            }

                            if ((maskVariable & 1) > 0) source.Seek(1, SeekOrigin.Current); // Note
                            if ((maskVariable & 2) > 0) source.Seek(1, SeekOrigin.Current); // Instrument
                            if ((maskVariable & 4) > 0) source.Seek(1, SeekOrigin.Current); // Volume/panning
                            if ((maskVariable & 8) > 0)
                            {
                                theEvent.Command = source.ReadByte();
                                theEvent.Info = source.ReadByte();
                            }

                            aRow.Add(theEvent);
                        }
                        else // what = 0 => end of row
                        {
                            aPattern.Add(aRow);
                            aRow = new List<Event>();
                            rowNum++;
                        }
                    } while (rowNum < nbRows);
                }

                patterns.Add(aPattern);
            }
        }


        // === PUBLIC METHODS ===

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, IT_SIGNATURE);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            bool result = true;

            ushort nbOrders = 0;
            ushort nbPatterns = 0;
            ushort nbSamples = 0;
            ushort nbInstruments = 0;

            ushort flags;
            ushort special;
            ushort trackerVersion;
            ushort trackerVersionCompatibility;

            bool useSamplesAsInstruments = false;

            ushort messageLength;
            uint messageOffset;
            string message = "";

            IList<uint> patternPointers = new List<uint>();
            IList<uint> instrumentPointers = new List<uint>();
            IList<uint> samplePointers = new List<uint>();

            resetData();
            BufferedBinaryReader bSource = new BufferedBinaryReader(source);


            if (!IsValidHeader(bSource.ReadBytes(4)))
            {
                throw new InvalidDataException(sizeInfo.FileSize + " : Invalid IT file (file signature mismatch)"); // TODO - might be a compressed file -> PK header
            }

            // Title = max first 26 chars after file signature; null-terminated
            string title = StreamUtils.ReadNullTerminatedStringFixed(bSource, Utils.Latin1Encoding, 26);
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddZone(4, 26, new byte[26] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, ZONE_TITLE);
            }
            tagData.IntegrateValue(TagData.Field.TITLE, title.Trim());
            bSource.Seek(2, SeekOrigin.Current); // Pattern row highlight information

            AudioDataOffset = bSource.Position;
            AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

            nbOrders = bSource.ReadUInt16();
            nbInstruments = bSource.ReadUInt16();
            nbSamples = bSource.ReadUInt16();
            nbPatterns = bSource.ReadUInt16();

            trackerVersion = bSource.ReadUInt16();
            trackerVersionCompatibility = bSource.ReadUInt16();

            flags = bSource.ReadUInt16();

            useSamplesAsInstruments = (flags & 0x04) <= 0;

            special = bSource.ReadUInt16();

            //            trackerName = "Impulse tracker"; // TODO use TrackerVersion to add version

            bSource.Seek(2, SeekOrigin.Current); // globalVolume (8b), masterVolume (8b)

            initialSpeed = bSource.ReadByte();
            initialTempo = bSource.ReadByte();

            bSource.Seek(2, SeekOrigin.Current); // panningSeparation (8b), pitchWheelDepth (8b)

            messageLength = bSource.ReadUInt16();
            messageOffset = bSource.ReadUInt32();
            bSource.Seek(132, SeekOrigin.Current); // reserved (32b), channel Pan (64B), channel Vol (64B)

            // Orders table
            for (int i = 0; i < nbOrders; i++)
            {
                patternTable.Add(bSource.ReadByte());
            }

            // Instruments pointers
            for (int i = 0; i < nbInstruments; i++)
            {
                instrumentPointers.Add(bSource.ReadUInt32());
            }

            // Samples pointers
            for (int i = 0; i < nbSamples; i++)
            {
                samplePointers.Add(bSource.ReadUInt32());
            }

            // Patterns pointers
            for (int i = 0; i < nbPatterns; i++)
            {
                patternPointers.Add(bSource.ReadUInt32());
            }

            if ((!useSamplesAsInstruments) && (instrumentPointers.Count > 0))
            {
                if (trackerVersionCompatibility < 0x200)
                {
                    readInstrumentsOld(bSource, instrumentPointers);
                }
                else
                {
                    readInstruments(bSource, instrumentPointers);
                }
            }
            else
            {
                readSamples(bSource, samplePointers);
            }
            readPatterns(bSource, patternPointers);

            // IT Message
            if ((special & 0x1) > 0)
            {
                bSource.Seek(messageOffset, SeekOrigin.Begin);
                message = StreamUtils.ReadNullTerminatedStringFixed(bSource, Utils.Latin1Encoding, messageLength);
            }


            // == Computing track properties

            Duration = calculateDuration() * 1000.0;

            string commentStr;
            if (messageLength > 0) // Get Comment from the "IT message" field
            {
                commentStr = message;
            }
            else // Get Comment from all the instrument names (common practice in the tracker community)
            {
                StringBuilder comment = new StringBuilder("");
                // NB : Whatever the value of useSamplesAsInstruments, FInstruments contain the right data
                foreach (var i in instruments.Where(i => i.DisplayName.Length > 0))
                {
                    comment.Append(i.DisplayName).Append(Settings.InternalValueSeparator);
                }

                if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);
                commentStr = comment.ToString();
            }
            tagData.IntegrateValue(TagData.Field.COMMENT, commentStr);

            BitRate = (double)sizeInfo.FileSize / Duration;

            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            int result = 0;

            if (ZONE_TITLE.Equals(zone))
            {
                string title = Utils.ProtectValue(tag[Field.TITLE]);
                if (title.Length > 26) title = title.Substring(0, 26);
                else if (title.Length < 26) title = Utils.BuildStrictLengthString(title, 26, '\0');
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(title));
                result = 1;
            }

            return result;
        }
    }

}