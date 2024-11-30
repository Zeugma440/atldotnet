using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for ScreamTracker Module files manipulation (extensions : .S3M)
    /// 
    /// Note : Parsing as it is considers the file as one single song. 
    /// Modules with song delimiters (pattern code 0xFF) are supported, but displayed as one track
    /// instead of multiple tracks (behaviour of foobar2000).
    /// 
    /// As a consequence, modules containing multiple songs and exotic loops (i.e. looping from song 2 to song 1)
    /// might not be detected with their exact duration.
    /// </summary>
    class S3M : MetaDataIO, IAudioDataIO
    {
        private const string ZONE_TITLE = "title";

        private const string S3M_SIGNATURE = "SCRM";
        private const byte MAX_ROWS = 64;

        // Effects
        private const byte EFFECT_SET_SPEED = 0x01;
        private const byte EFFECT_ORDER_JUMP = 0x02;
        private const byte EFFECT_JUMP_TO_ROW = 0x03;
        private const byte EFFECT_EXTENDED = 0x13;
        private const byte EFFECT_SET_BPM = 0x14;

        private const byte EFFECT_EXTENDED_LOOP = 0xB;


        // Standard fields
        private IList<byte> FChannelTable;
        private IList<byte> FPatternTable;
        private IList<IList<IList<S3MEvent>>> FPatterns;
        private IList<Instrument> FInstruments;

        private byte initialSpeed;
        private byte initialTempo;
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
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        // IMetaDataIO
        protected override int getDefaultTagOffset() => TO_BUILTIN;
        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            throw new NotImplementedException();
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private sealed class Instrument
        {
            public byte Type = 0;
            public string FileName = "";
            public string DisplayName = "";
            // Other fields not useful for ATL
        }

        private sealed class S3MEvent
        {
            public byte Command = 0;
            public byte Info = 0;
            // Other fields not useful for ATL
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            // Reset variables
            Duration = 0;
            BitRate = 0;

            FPatternTable = new List<byte>();
            FChannelTable = new List<byte>();

            FPatterns = new List<IList<IList<S3MEvent>>>();
            FInstruments = new List<Instrument>();

            trackerName = "";

            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public S3M(string filePath, AudioFormat format)
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

            double speed = initialSpeed;
            double tempo = initialTempo;
            double previousTempo = tempo;

            do // Patterns loop
            {
                do // Lines loop
                {
                    currentPattern = FPatternTable[currentPatternIndex];

                    while (currentPattern > FPatterns.Count - 1 && currentPatternIndex < FPatternTable.Count - 1)
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

                    var row = FPatterns[currentPattern][currentRow];
                    foreach (S3MEvent theEvent in row) // Events loop
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
                                currentPatternIndex = Math.Min(theEvent.Info, FPatternTable.Count - 1);
                                currentRow = 0;
                                positionJump = true;
                            }
                        }
                        else if (theEvent.Command.Equals(EFFECT_JUMP_TO_ROW))
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
                } while (currentRow < MAX_ROWS);

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

        private static string getTrackerName(ushort trackerVersion)
        {
            string result = "";

            switch ((trackerVersion & 0xF000) >> 12)
            {
                case 0x1: result = "ScreamTracker"; break;
                case 0x2: result = "Imago Orpheus"; break;
                case 0x3: result = "Impulse Tracker"; break;
                case 0x4: result = "Schism Tracker"; break;
                case 0x5: result = "OpenMPT"; break;
                case 0xC: result = "Camoto/libgamemusic"; break;
            }

            return result;
        }

        private void readInstruments(BufferedBinaryReader source, IList<ushort> instrumentPointers)
        {
            foreach (ushort pos in instrumentPointers)
            {
                source.Seek(pos << 4, SeekOrigin.Begin);
                Instrument instrument = new Instrument
                {
                    Type = source.ReadByte(),
                    FileName = Utils.Latin1Encoding.GetString(source.ReadBytes(12)).Trim().Replace("\0", "")
                };
                if (instrument.Type > 0) // Same offsets for PCM and AdLib display names
                {
                    source.Seek(35, SeekOrigin.Current);
                    instrument.DisplayName = StreamUtils.ReadNullTerminatedStringFixed(source, Encoding.ASCII, 28);
                    instrument.DisplayName = instrument.DisplayName.Replace("\0", "");
                    source.Seek(4, SeekOrigin.Current);
                }

                FInstruments.Add(instrument);
            }
        }

        private void readPatterns(BufferedBinaryReader source, IList<ushort> patternPointers)
        {
            foreach (ushort pos in patternPointers)
            {
                IList<IList<S3MEvent>> aPattern = new List<IList<S3MEvent>>();

                source.Seek(pos << 4, SeekOrigin.Begin);
                IList<S3MEvent> aRow = new List<S3MEvent>();
                byte rowNum = 0;
                source.Seek(2, SeekOrigin.Current); // patternSize

                do
                {
                    var what = source.ReadByte();

                    if (what > 0)
                    {
                        S3MEvent theEvent = new S3MEvent();
                        //                        theEvent.Channel = what & 0x1F;

                        if ((what & 0x20) > 0) source.Seek(2, SeekOrigin.Current); // Note & Instrument
                        if ((what & 0x40) > 0) source.Seek(1, SeekOrigin.Current); // Volume
                        if ((what & 0x80) > 0)
                        {
                            theEvent.Command = source.ReadByte();
                            theEvent.Info = source.ReadByte();
                        }

                        aRow.Add(theEvent);
                    }
                    else // what = 0 => end of row
                    {
                        aPattern.Add(aRow);
                        aRow = new List<S3MEvent>();
                        rowNum++;
                    }
                } while (rowNum < MAX_ROWS);

                FPatterns.Add(aPattern);
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
            StringBuilder comment = new StringBuilder("");

            IList<ushort> patternPointers = new List<ushort>();
            IList<ushort> instrumentPointers = new List<ushort>();

            resetData();
            BufferedBinaryReader bSource = new BufferedBinaryReader(source);

            // Title = first 28 chars
            string title = StreamUtils.ReadNullTerminatedStringFixed(bSource, Encoding.ASCII, 28);
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddZone(0, 28, new byte[28] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, ZONE_TITLE);
            }
            tagData.IntegrateValue(Field.TITLE, title.Trim());
            bSource.Seek(4, SeekOrigin.Current);

            AudioDataOffset = bSource.Position;
            AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

            var nbOrders = bSource.ReadUInt16();
            var nbInstruments = bSource.ReadUInt16();
            var nbPatterns = bSource.ReadUInt16();

            bSource.ReadUInt16();
            var trackerVersion = bSource.ReadUInt16();

            trackerName = getTrackerName(trackerVersion);

            bSource.Seek(2, SeekOrigin.Current); // sampleType (16b)
            if (!S3M_SIGNATURE.Equals(Utils.Latin1Encoding.GetString(bSource.ReadBytes(4))))
            {
                throw new InvalidDataException("Invalid S3M file (file signature mismatch)");
            }
            bSource.Seek(1, SeekOrigin.Current); // globalVolume (8b)

            initialSpeed = bSource.ReadByte();
            initialTempo = bSource.ReadByte();

            bSource.Seek(1, SeekOrigin.Current); // masterVolume (8b)
            bSource.Seek(1, SeekOrigin.Current); // ultraClickRemoval (8b)
            bSource.Seek(1, SeekOrigin.Current); // defaultPan (8b)
            bSource.Seek(8, SeekOrigin.Current); // defaultPan (64b)
            bSource.Seek(2, SeekOrigin.Current); // ptrSpecial (16b)

            // Channel table
            for (int i = 0; i < 32; i++)
            {
                FChannelTable.Add(bSource.ReadByte());
                // if (FChannelTable[^1] < 30) nbChannels++;
            }

            // Pattern table
            for (int i = 0; i < nbOrders; i++) FPatternTable.Add(bSource.ReadByte());

            // Instruments pointers
            for (int i = 0; i < nbInstruments; i++) instrumentPointers.Add(bSource.ReadUInt16());

            // Patterns pointers
            for (int i = 0; i < nbPatterns; i++) patternPointers.Add(bSource.ReadUInt16());

            readInstruments(bSource, instrumentPointers);
            readPatterns(bSource, patternPointers);


            // == Computing track properties

            Duration = calculateDuration() * 1000.0;

            foreach (Instrument i in FInstruments)
            {
                string displayName = i.DisplayName.Trim();
                if (displayName.Length > 0) comment.Append(displayName).Append(Settings.InternalValueSeparator);
            }
            if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);

            tagData.IntegrateValue(Field.COMMENT, comment.ToString());
            BitRate = sizeInfo.FileSize / Duration;

            return true;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            int result = 0;

            if (ZONE_TITLE.Equals(zone))
            {
                string title = tag[Field.TITLE];
                if (title.Length > 28) title = title[..28];
                else if (title.Length < 28) title = Utils.BuildStrictLengthString(title, 28, '\0');
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(title));
                result = 1;
            }

            return result;
        }
    }

}