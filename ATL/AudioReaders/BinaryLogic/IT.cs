using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using ATL.Logging;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Impulse Tracker Module files manipulation (extensions : .IT)
    /// </summary>
    class TIT : AudioDataReader, IMetaDataReader
    {
        private const String IT_SIGNATURE = "IMPM";
        private const String PACKED_SIGNATURE = "PK";

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

        private IList<byte> FOrdersTable;
        private IList<byte> FPatternTable;
        private IList<byte> FSampleTable;
        private IList<IList<IList<Event>>> FPatterns;
        private IList<Instrument> FInstruments;

        private byte initialSpeed;
        private byte initialTempo;

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
            public byte Type = 0;
            public String FileName = "";
            public String DisplayName = "";

            // Other fields not useful for ATL
        }

        private class Event
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

        public TIT()
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

            FOrdersTable = new List<byte>();
            FPatternTable = new List<byte>();
            FSampleTable = new List<byte>();

            FPatterns = new List<IList<IList<Event>>>();
            FInstruments = new List<Instrument>();
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

            IList<Event> row;

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
                                currentPatternIndex = Math.Min(theEvent.Info, FPatternTable.Count - 1);
                                currentRow = 0;
                                positionJump = true;
                            }
                        }
                        else if (theEvent.Command.Equals(EFFECT_JUMP_TO_ROW))
                        {
                            currentPatternIndex++;
                            currentRow = Math.Min(theEvent.Info, FPatterns[currentPattern].Count);
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

        private void readSamples(ref BinaryReader source, IList<UInt32> samplePointers)
        {
            foreach (UInt32 pos in samplePointers)
            {
                source.BaseStream.Seek(pos, SeekOrigin.Begin);
                Instrument instrument = new Instrument();

                source.BaseStream.Seek(4, SeekOrigin.Current); // Signature
                instrument.FileName = new String(StreamUtils.ReadOneByteChars(source, 12)).Trim();
                instrument.FileName = instrument.FileName.Replace("\0", "");

                source.BaseStream.Seek(4, SeekOrigin.Current); // Data not relevant for ATL

                instrument.DisplayName = StreamUtils.ReadNullTerminatedStringFixed(source, Encoding.ASCII, 26);
                instrument.DisplayName = instrument.DisplayName.Replace("\0", "");

                FInstruments.Add(instrument);
            }
        }

        private void readInstruments(ref BinaryReader source, IList<UInt32> instrumentPointers)
        {
            foreach (UInt32 pos in instrumentPointers)
            {
                source.BaseStream.Seek(pos, SeekOrigin.Begin);
                Instrument instrument = new Instrument();

                source.BaseStream.Seek(4, SeekOrigin.Current); // Signature
                instrument.FileName = new String(StreamUtils.ReadOneByteChars(source, 12)).Trim();
                instrument.FileName = instrument.FileName.Replace("\0", "");

                source.BaseStream.Seek(16, SeekOrigin.Current); // Data not relevant for ATL

                instrument.DisplayName = StreamUtils.ReadNullTerminatedStringFixed(source, Encoding.ASCII, 26);
                instrument.DisplayName = instrument.DisplayName.Replace("\0", "");

                FInstruments.Add(instrument);
            }
        }

        private void readInstrumentsOld(ref BinaryReader source, IList<UInt32> instrumentPointers)
        {
            // The fileName and displayName fields have the same offset in the new and old format
            readInstruments(ref source, instrumentPointers);
        }

        private void readPatterns(ref BinaryReader source, IList<UInt32> patternPointers)
        {
            ushort nbRows;
            byte rowNum;
            byte what;
            byte maskVariable = 0;
            IList<Event> aRow;
            IList<IList<Event>> aPattern;
            IDictionary<int, byte> maskVariables = new Dictionary<int, byte>();

            foreach (UInt32 pos in patternPointers)
            {
                aPattern = new List<IList<Event>>();
                if (pos > 0)
                {
                    source.BaseStream.Seek(pos, SeekOrigin.Begin);
                    aRow = new List<Event>();
                    rowNum = 0;
                    source.BaseStream.Seek(2, SeekOrigin.Current); // patternSize
                    nbRows = source.ReadUInt16();
                    source.BaseStream.Seek(4, SeekOrigin.Current); // unused data

                    do
                    {
                        what = source.ReadByte();

                        if (what > 0)
                        {
                            Event theEvent = new Event();
                            theEvent.Channel = (what - 1) & 63;
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

                            if ((maskVariable & 1) > 0) source.BaseStream.Seek(1, SeekOrigin.Current); // Note
                            if ((maskVariable & 2) > 0) source.BaseStream.Seek(1, SeekOrigin.Current); // Instrument
                            if ((maskVariable & 4) > 0) source.BaseStream.Seek(1, SeekOrigin.Current); // Volume/panning
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

                FPatterns.Add(aPattern);
            }
        }


        // === PUBLIC METHODS ===

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
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
            String message = "";

            StringBuilder comment = new StringBuilder("");

            IList<UInt32> patternPointers = new List<UInt32>();
            IList<UInt32> instrumentPointers = new List<UInt32>();
            IList<UInt32> samplePointers = new List<UInt32>();


            if (!IT_SIGNATURE.Equals(new String(StreamUtils.ReadOneByteChars(source, 4))))
            {
                result = false;
                throw new Exception(FFileName + " : Invalid IT file (file signature mismatch)");
            }

            // Title = first 26 chars
            FTitle = StreamUtils.ReadNullTerminatedStringFixed(source, System.Text.Encoding.ASCII, 26).Trim();
            source.BaseStream.Seek(2, SeekOrigin.Current); // Pattern row highlight information

            nbOrders = source.ReadUInt16();
            nbInstruments = source.ReadUInt16();
            nbSamples = source.ReadUInt16();
            nbPatterns = source.ReadUInt16();

            trackerVersion = source.ReadUInt16();
            trackerVersionCompatibility = source.ReadUInt16();

            flags = source.ReadUInt16();

            useSamplesAsInstruments = !((flags & 0x2) > 0);

            special = source.ReadUInt16();

//            trackerName = "Impulse tracker"; // TODO use TrackerVersion to add version

            source.BaseStream.Seek(1, SeekOrigin.Current); // globalVolume (8b)
            source.BaseStream.Seek(1, SeekOrigin.Current); // masterVolume (8b)

            initialSpeed = source.ReadByte();
            initialTempo = source.ReadByte();

            source.BaseStream.Seek(1, SeekOrigin.Current); // panningSeparation (8b)
            source.BaseStream.Seek(1, SeekOrigin.Current); // pitchWheelDepth (8b)

            messageLength = source.ReadUInt16();
            messageOffset = source.ReadUInt32();
            source.BaseStream.Seek(4, SeekOrigin.Current); // reserved (32b)

            source.BaseStream.Seek(64, SeekOrigin.Current); // channel Pan
            source.BaseStream.Seek(64, SeekOrigin.Current); // channel Vol

            // Orders table
            for (int i = 0; i < nbOrders; i++)
            {
                FPatternTable.Add(source.ReadByte());
            }

            // Instruments pointers
            for (int i = 0; i < nbInstruments; i++)
            {
                instrumentPointers.Add(source.ReadUInt32());
            }

            // Samples pointers
            for (int i = 0; i < nbSamples; i++)
            {
                samplePointers.Add(source.ReadUInt32());
            }

            // Patterns pointers
            for (int i = 0; i < nbPatterns; i++)
            {
                patternPointers.Add(source.ReadUInt32());
            }

            if ( (!useSamplesAsInstruments) && (instrumentPointers.Count > 0) )
            {
                if (trackerVersionCompatibility < 0x200)
                {
                    readInstrumentsOld(ref source, instrumentPointers);
                }
                else
                {
                    readInstruments(ref source, instrumentPointers);
                }
            }
            else
            {
                readSamples(ref source, samplePointers);
            }
            readPatterns(ref source, patternPointers);

            // IT Message
            if ((special & 0x1) > 0)
            {
                source.BaseStream.Seek(messageOffset, SeekOrigin.Begin);
                //message = new String( StreamUtils.ReadOneByteChars(source, messageLength) );
                message = StreamUtils.ReadNullTerminatedStringFixed(source, Encoding.ASCII, messageLength);
            }


            // == Computing track properties

            FDuration = calculateDuration();

            if (messageLength > 0)
            {
                FComment = message;
            }
            else 
            {
                // NB : Whatever the value of useSamplesAsInstruments, FInstruments contain the right data
                foreach (Instrument i in FInstruments)
                {
                    if (i.DisplayName.Length > 0) comment.Append(i.DisplayName).Append("/");
                }
                if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);
                FComment = comment.ToString();
            }
            
            FBitrate = FFileSize / FDuration;

            return result;
        }
    }

}