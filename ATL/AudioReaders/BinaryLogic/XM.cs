using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using ATL.Logging;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Extended Module files manipulation (extensions : .XM)
    /// </summary>
    class TXM : AudioDataReader, IMetaDataReader
    {
        private const String XM_SIGNATURE = "Extended Module: ";

        // Effects (NB : very close to the MOD effect codes)
        private const byte EFFECT_POSITION_JUMP = 0xB;
        private const byte EFFECT_PATTERN_BREAK = 0xD;
        private const byte EFFECT_SET_SPEED = 0xF;
        private const byte EFFECT_EXTENDED = 0xE;

        private const byte EFFECT_EXTENDED_LOOP = 0x6;
        private const byte EFFECT_NOTE_CUT = 0xC;
        private const byte EFFECT_NOTE_DELAY = 0xD;
        private const byte EFFECT_PATTERN_DELAY = 0xE;


        // Standard fields
        private String FTitle;
        private String FComment;

        private IList<byte> FPatternTable;
        private IList<IList<IList<Event>>> FPatterns;
        private IList<Instrument> FInstruments;

        private ushort initialSpeed; // Ticks per line
        private ushort initialTempo; // BPM

        private byte nbChannels;
        private String trackerName;

        #region Public generic attributes
        public bool Exists // for compatibility with other tag readers
        {
            get { return true; }
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
            //public byte Type;  Useful for S3M but not for XM
            public String DisplayName;

            // Other fields not useful for ATL

            public void Reset()
            {
                //Type = 0;
                DisplayName = "";
            }
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

        public TXM()
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
            FSampleRate = 0;

            FPatternTable = new List<byte>();

            FPatterns = new List<IList<IList<Event>>>();
            FInstruments = new List<Instrument>();

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

        private void readInstruments(ref BinaryReader source, int nbInstruments)
        {
            uint instrumentHeaderSize;
            ushort nbSamples;

            IList<UInt32> sampleSizes = new List<uint>();

            for (int i = 0; i < nbInstruments; i++)
            {
                Instrument instrument = new Instrument();
                instrumentHeaderSize = source.ReadUInt32();
                instrument.DisplayName = new String(StreamUtils.ReadOneByteChars(source, 22)).Trim();
                instrument.DisplayName = instrument.DisplayName.Replace("\0", "");
                source.BaseStream.Seek(1, SeekOrigin.Current); // Instrument type; useless according to documentation
                nbSamples = source.ReadUInt16();
                source.BaseStream.Seek(instrumentHeaderSize - 29, SeekOrigin.Current);

                if (nbSamples > 0)
                {
                    sampleSizes.Clear();
                    for (int j = 0; j < nbSamples; j++) // Sample headers
                    {
                        sampleSizes.Add(source.ReadUInt32());
                        source.BaseStream.Seek(36, SeekOrigin.Current);
                    }
                    for (int j = 0; j < nbSamples; j++) // Sample data
                    {
                        source.BaseStream.Seek(sampleSizes[j], SeekOrigin.Current);
                    }
                }

                FInstruments.Add(instrument);
            }
        }

        private void readPatterns(ref BinaryReader source, int nbPatterns)
        {
            byte firstByte;
            IList<Event> aRow;
            IList<IList<Event>> aPattern;
            
            uint headerLength;
            ushort nbRows;
            uint packedDataSize;

            for (int i=0; i<nbPatterns;i++)
            {
                aPattern = new List<IList<Event>>();
                
                headerLength = source.ReadUInt32();
                source.BaseStream.Seek(1, SeekOrigin.Current); // Packing type
                nbRows = source.ReadUInt16();

                packedDataSize = source.ReadUInt16();

                if (packedDataSize > 0) // The patterns is not empty
                {
                    for (int j = 0; j < nbRows; j++)
                    {
                        aRow = new List<Event>();

                        for (int k=0; k<nbChannels;k++)
                        {
                            Event e = new Event();
                            e.Channel = k+1;

                            firstByte = source.ReadByte();
                            if ((firstByte & 0x80) > 0) // Most Significant Byte (MSB) is set => packed data layout
                            {
                                if ((firstByte & 0x1) > 0) source.BaseStream.Seek(1,SeekOrigin.Current); // Note
                                if ((firstByte & 0x2) > 0) source.BaseStream.Seek(1,SeekOrigin.Current); // Instrument
                                if ((firstByte & 0x4) > 0) source.BaseStream.Seek(1,SeekOrigin.Current); // Volume
                                if ((firstByte & 0x8) > 0) e.Command = source.ReadByte();                // Effect type
                                if ((firstByte & 0x10) > 0) e.Info = source.ReadByte();                  // Effect param

                            } else { // No MSB set => standard data layout
                                // firstByte is the Note
                                source.BaseStream.Seek(1,SeekOrigin.Current); // Instrument
                                source.BaseStream.Seek(1,SeekOrigin.Current); // Volume
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

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            bool result = true;

            ushort nbPatterns = 0;
            ushort nbInstruments = 0;

            ushort trackerVersion;

            uint headerSize = 0;
            uint songLength = 0;

            StringBuilder comment = new StringBuilder("");

            // File format signature
            if (!XM_SIGNATURE.Equals(new String(StreamUtils.ReadOneByteChars(source, 17))))
            {
                result = false;
                throw new Exception(FFileName + " : Invalid XM file (file signature String mismatch)");
            }

            FTitle = StreamUtils.ReadNullTerminatedStringFixed(source, System.Text.Encoding.ASCII, 20).Trim();

            // File format signature
            if (!0x1a.Equals(source.ReadByte()))
            {
                result = false;
                throw new Exception(FFileName + " : Invalid XM file (file signature ID mismatch)");
            }

            trackerName = StreamUtils.ReadNullTerminatedStringFixed(source, System.Text.Encoding.ASCII, 20).Trim();

            trackerVersion = source.ReadUInt16(); // hi-byte major and low-byte minor
            trackerName += (trackerVersion << 8) + "." + (trackerVersion & 0xFF00);

            headerSize = source.ReadUInt32(); // Calculated FROM THIS OFFSET, not from the beginning of the file
            songLength = source.ReadUInt16();

            source.BaseStream.Seek(2, SeekOrigin.Current); // Restart position

            nbChannels = (byte)Math.Min(source.ReadUInt16(),(ushort)0xFF);
            nbPatterns = source.ReadUInt16();
            nbInstruments = source.ReadUInt16();

            source.BaseStream.Seek(2, SeekOrigin.Current); // Flags for frequency tables; useless for ATL

            initialSpeed = source.ReadUInt16();
            initialTempo = source.ReadUInt16();

            // Pattern table
            for (int i = 0; i < 256; i++)
            {
                if (i < songLength) FPatternTable.Add(source.ReadByte()); else source.BaseStream.Seek(1, SeekOrigin.Current);
            }

            readPatterns(ref source, nbPatterns);
            readInstruments(ref source, nbInstruments);

            
            // == Computing track properties

            FDuration = calculateDuration();

            foreach (Instrument i in FInstruments)
            {
                if (i.DisplayName.Length > 0) comment.Append(i.DisplayName).Append("/");
            }
            if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);

            FComment = comment.ToString();
            FBitrate = FFileSize / FDuration;

            return result;
        }
    }

}