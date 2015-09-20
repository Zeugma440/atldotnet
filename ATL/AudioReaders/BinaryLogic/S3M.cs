using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using ATL.Logging;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
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
    class TS3M : AudioDataReader, IMetaDataReader
    {
        private const String S3M_SIGNATURE = "SCRM";

        // Effects
        private const byte EFFECT_SET_SPEED = 0x01;
        private const byte EFFECT_ORDER_JUMP = 0x02;
        private const byte EFFECT_JUMP_TO_ROW = 0x03;
        private const byte EFFECT_EXTENDED = 0x13;
        private const byte EFFECT_SET_BPM = 0x14;

        private const byte EFFECT_EXTENDED_LOOP = 0xB;


        // Standard fields
        private String FTitle;
        private String FComment;

        private IList<byte> FChannelTable;
        private IList<byte> FPatternTable;
        private IList<IList<IList<S3MEvent>>> FPatterns;
        private IList<Instrument> FInstruments;

        private byte initialSpeed;
        private byte initialTempo;

        private String formatTag;
        private byte nbChannels;
        private String trackerName;

        #region Public attributes
        public bool Exists // for compatibility with other tag readers
        {
            get { return true; }
        }
        public int SampleRate // Sample rate (hz)
        {
            get { return 0; }
        }

        public override bool IsVBR
        {
            get { return false; }
        }
        public override int CodecFamily
        {
            get { return AudioReaderFactory.CF_SEQ_WAV; }
        }
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

        public String Title // Song title
        {
            get { return this.FTitle; }
        }
        public String Artist // Artist name
        {
            get { return ""; }
        }
        public String Composer // Composer name
        {
            get { return ""; }
        }
        public String Album // Album name
        {
            get { return ""; }
        }
        public ushort Track // Track number
        {
            get { return 0; }
        }
        public ushort Disc // Disc number
        {
            get { return 0; }
        }
        public ushort Rating // Rating; not in SPC tag standard
        {
            get { return 0; }
        }
        public String Year // Year
        {
            get { return ""; }
        }
        public String Genre // Genre name
        {
            get { return ""; }
        }
        public String Comment // Comment
        {
            get { return this.FComment; }
        }
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating presence of pictures
        {
            get { return new List<MetaReaderFactory.PIC_CODE>(); }
        }
        #endregion

        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private class Instrument
        {
            public byte Type;
            public String FileName;
            public String DisplayName;

            // Other fields not useful for ATL

            public void Reset()
            {
                Type = 0;
                FileName = "";
                DisplayName = "";
            }
        }

        private class S3MEvent
        {
            // Commented fields below not useful for ATL
            public int Channel;
            //public byte Note;
            //public byte Instrument;
            //public byte Volume;
            public byte Command;
            public byte Info;

            public void Reset()
            {
                Channel = 0;
                Command = 0;
                Info = 0;
            }
        }

        // === CONSTRUCTOR ===

        public TS3M()
        {
            // Create object
            resetData();
        }


        // === PRIVATE METHODS ===

        protected override void resetSpecificData()
        {
            // Reset variables
            FDuration = 0;
            FTitle = "";
            FComment = "";

            FPatternTable = new List<byte>();
            FChannelTable = new List<byte>();

            FPatterns = new List<IList<IList<S3MEvent>>>();
            FInstruments = new List<Instrument>();

            formatTag = "";
            trackerName = "";
            nbChannels = 0;
        }

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

            IList<S3MEvent> row;

            double speed = initialSpeed;
            double tempo = initialTempo;
            double previousTempo = tempo;

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
                                // TODO implement other extended effects
                            }
                        }

                        if (positionJump || patternBreak) break;
                    } // end Events loop

                    result += 60 * (speed / (24 * tempo));
                    if (isInsideLoop) loopDuration += 60 * (speed / (24 * tempo));

                    if (positionJump || patternBreak) break;

                    currentRow++;
                } while (currentRow < 64);

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

        private byte detectNbSamples(BinaryReader source)
        {
            byte result = 31;
            long position = source.BaseStream.Position;

            source.BaseStream.Seek(1080, SeekOrigin.Begin);

            formatTag = new String(StreamUtils.ReadOneByteChars(source, 4)).Trim();

            source.BaseStream.Seek(position, SeekOrigin.Begin);

            return result;
        }

        private String getTrackerName(ushort trackerVersion)
        {
            String result = "";

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

        private void readInstruments(ref BinaryReader source, IList<ushort> instrumentPointers)
        {
            foreach (ushort pos in instrumentPointers)
            {
                source.BaseStream.Seek(pos << 4, SeekOrigin.Begin);
                Instrument instrument = new Instrument();
                instrument.Type = source.ReadByte();
                instrument.FileName = new String(StreamUtils.ReadOneByteChars(source, 12)).Trim();

                if (instrument.Type > 0) // Same offsets for PCM and AdLib display names
                {
                    source.BaseStream.Seek(35, SeekOrigin.Current);
                    instrument.DisplayName = StreamUtils.ReadNullTerminatedStringFixed(source, Encoding.ASCII, 28);
                    source.BaseStream.Seek(4, SeekOrigin.Current);
                }

                FInstruments.Add(instrument);
            }
        }

        private void readPatterns(ref BinaryReader source, IList<ushort> patternPointers)
        {
            byte rowNum;
            byte what;
            IList<S3MEvent> aRow;
            IList<IList<S3MEvent>> aPattern;

            foreach (ushort pos in patternPointers)
            {
                aPattern = new List<IList<S3MEvent>>();

                source.BaseStream.Seek(pos << 4, SeekOrigin.Begin);
                aRow = new List<S3MEvent>();
                rowNum = 0;
                source.BaseStream.Seek(2, SeekOrigin.Current); // patternSize

                do
                {
                    what = source.ReadByte();

                    if (what > 0)
                    {
                        S3MEvent theEvent = new S3MEvent();
                        theEvent.Channel = what & 0x1F;

                        if ((what & 0x20) > 0) source.BaseStream.Seek(2, SeekOrigin.Current); // Note & Instrument
                        if ((what & 0x40) > 0) source.BaseStream.Seek(1, SeekOrigin.Current); // Volume
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
                } while (rowNum < 64);

                FPatterns.Add(aPattern);
            }
        }


        // === PUBLIC METHODS ===

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            bool result = true;

            ushort nbOrders = 0;
            ushort nbPatterns = 0;
            ushort nbInstruments = 0;

            ushort flags;
            ushort trackerVersion;

            StringBuilder comment = new StringBuilder("");

            IList<ushort> patternPointers = new List<ushort>();
            IList<ushort> instrumentPointers = new List<ushort>();


            // Title = first 28 chars
            FTitle = StreamUtils.ReadNullTerminatedStringFixed(source, System.Text.Encoding.ASCII, 28).Trim();
            source.BaseStream.Seek(4, SeekOrigin.Current);

            nbOrders = source.ReadUInt16();
            nbInstruments = source.ReadUInt16();
            nbPatterns = source.ReadUInt16();

            flags = source.ReadUInt16();
            trackerVersion = source.ReadUInt16();

            trackerName = getTrackerName(trackerVersion);

            source.BaseStream.Seek(2, SeekOrigin.Current); // sampleType (16b)
            if (!S3M_SIGNATURE.Equals(new String(StreamUtils.ReadOneByteChars(source, 4))))
            {
                result = false;
                throw new Exception(FFileName + " : Invalid S3M file (file signature mismatch)");
            }
            source.BaseStream.Seek(1, SeekOrigin.Current); // globalVolume (8b)

            initialSpeed = source.ReadByte();
            initialTempo = source.ReadByte();

            source.BaseStream.Seek(1, SeekOrigin.Current); // masterVolume (8b)
            source.BaseStream.Seek(1, SeekOrigin.Current); // ultraClickRemoval (8b)
            source.BaseStream.Seek(1, SeekOrigin.Current); // defaultPan (8b)
            source.BaseStream.Seek(8, SeekOrigin.Current); // defaultPan (64b)
            source.BaseStream.Seek(2, SeekOrigin.Current); // ptrSpecial (16b)

            // Channel table
            for (int i = 0; i < 32; i++)
            {
                FChannelTable.Add(source.ReadByte());
                if (FChannelTable[FChannelTable.Count - 1] < 30) nbChannels++;
            }

            // Pattern table
            for (int i = 0; i < nbOrders; i++)
            {
                FPatternTable.Add(source.ReadByte());
            }

            // Instruments pointers
            for (int i = 0; i < nbInstruments; i++)
            {
                instrumentPointers.Add(source.ReadUInt16());
            }

            // Patterns pointers
            for (int i = 0; i < nbPatterns; i++)
            {
                patternPointers.Add(source.ReadUInt16());
            }

            readInstruments(ref source, instrumentPointers);
            readPatterns(ref source, patternPointers);

            FDuration = calculateDuration();

            foreach (Instrument i in FInstruments)
            {
                if (i.FileName.Length > 0) comment.Append(i.DisplayName).Append("/");
            }
            if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);

            FComment = comment.ToString();
            FBitrate = FFileSize / FDuration;

            return result;
        }
    }

}