using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using ATL.Logging;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
	/// <summary>
    /// Class for Noisetracker/Soundtracker/Protracker Module files manipulation (extensions : .MOD)
    /// Based on info obtained from Thunder's readme (MODFIL10.TXT - Version 1.0)
	/// </summary>
	class TMOD : AudioDataReader, IMetaDataReader
	{
        private const String SIG_POWERPACKER = "PP20";
        private const byte NB_CHANNELS_DEFAULT = 4;

        private const byte DEFAULT_TICKS_PER_LINE = 6;
        private const byte DEFAULT_BPM = 125;

        // Effects
        private const byte EFFECT_POSITION_JUMP = 0xB;
        private const byte EFFECT_PATTERN_BREAK = 0xD;
        private const byte EFFECT_SET_SPEED     = 0xF;
        private const byte EFFECT_EXTENDED      = 0xE;

        private const byte EFFECT_EXTENDED_LOOP = 0x6;
        private const byte EFFECT_NOTE_CUT      = 0xC;
        private const byte EFFECT_NOTE_DELAY    = 0xD;
        private const byte EFFECT_PATTERN_DELAY = 0xE;
        private const byte EFFECT_INVERT_LOOP   = 0xF;

        private static IDictionary<String, ModFormat> modFormats;

		// Standard fields
		private int FSampleRate;
		private bool FTagExists;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private int FTrack;
        private int FDisc;
		private String FYear;
		private String FGenre;
		private String FComment;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;

        private IList<Sample> FSamples;
        private IList<IList<IList<int>>> FPatterns;
        private IList<byte> FPatternTable;
        private byte nbValidPatterns;
        private String formatTag;
        private byte nbChannels;
        private String trackerName;


		public bool Exists // for compatibility with other tag readers
		{
			get { return FTagExists; }
		}
		public int SampleRate // Sample rate (hz)
		{
			get { return this.FSampleRate; }
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
			get { return this.FArtist; }
		}
        public String Composer // Composer name
        {
            get { return this.FComposer; }
        }
		public String Album // Album name
		{
			get { return this.FAlbum; }
		}	
		public ushort Track // Track number
		{
			get { return (ushort)this.FTrack; }
		}
        public ushort Disc // Disc number
        {
            get { return (ushort)this.FDisc; }
        }
        public ushort Rating // Rating; not in SPC tag standard
        {
            get { return 0; }
        }
		public String Year // Year
		{
			get { return this.FYear; }
		}	
		public String Genre // Genre name
		{
			get { return this.FGenre; }
		}	
		public String Comment // Comment
		{
			get { return this.FComment; }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating presence of pictures
        {
            get { return this.FPictures; }
        }


		// === PRIVATE STRUCTURES/SUBCLASSES ===

		private class Sample
		{
            public String Name;
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

        private class ModFormat
        {
            public String Name = "";
            public String Signature = "";
            public byte NbSamples = 0;
            public byte NbChannels = 0;

            public ModFormat(String name, String sig, byte nbSamples, byte nbChannels)
            {
                Name = name;
                Signature = sig;
                NbSamples = nbSamples;
                NbChannels = nbChannels;
            }
        }

		// === CONSTRUCTOR ===

		public TMOD()
		{
			// Create object
			resetData();
		}

        static TMOD()
		{
            modFormats = new Dictionary<string, ModFormat>();

            modFormats.Add("M.K.",new ModFormat("ProTracker","M.K.",31,4));
            modFormats.Add("M!K!",new ModFormat("ProTracker", "M!K!", 31, 4));
            modFormats.Add("FLT4", new ModFormat("StarTrekker", "FLT4", 31, 4));
            modFormats.Add("2CHN", new ModFormat("FastTracker", "2CHN", 31, 2));
            modFormats.Add("4CHN", new ModFormat("FastTracker", "4CHN", 31, 4));
            modFormats.Add("6CHN", new ModFormat("FastTracker", "6CHN", 31, 6));
            modFormats.Add("8CHN", new ModFormat("FastTracker", "8CHN", 31, 8));
            modFormats.Add("OCTA", new ModFormat("FastTracker", "OCTA", 31, 8));
            modFormats.Add("FLT8", new ModFormat("StarTrekker", "FLT8", 31, 8));
            modFormats.Add("CD81", new ModFormat("Falcon", "CD81", 31, 8));
            modFormats.Add("10CH", new ModFormat("FastTracker", "10CH", 31, 10));
            modFormats.Add("12CH", new ModFormat("FastTracker", "12CH", 31, 12));
            modFormats.Add("14CH", new ModFormat("FastTracker", "14CH", 31, 14));
            modFormats.Add("11CH", new ModFormat("TakeTracker", "11CH", 31, 11));
            modFormats.Add("13CH", new ModFormat("TakeTracker", "13CH", 31, 13));
            modFormats.Add("15CH", new ModFormat("TakeTracker", "15CH", 31, 15));
            for (byte i = 16; i < 33; i++)
            {
                modFormats.Add(i+"CH", new ModFormat("FastTracker", i+"CH", 31, i));
            }
            modFormats.Add("TDZ1", new ModFormat("TakeTracker", "TDZ1", 31, 1));
            modFormats.Add("TDZ2", new ModFormat("TakeTracker", "TDZ2", 31, 2));
            modFormats.Add("TDZ3", new ModFormat("TakeTracker", "TDZ3", 31, 3));
            modFormats.Add("5CHN", new ModFormat("TakeTracker", "5CHN", 31, 5));
            modFormats.Add("7CHN", new ModFormat("TakeTracker", "7CHN", 31, 7));
            modFormats.Add("9CHN", new ModFormat("TakeTracker", "9CHN", 31, 9));
		}


		// === PRIVATE METHODS ===

		protected override void resetSpecificData()
		{
			// Reset variables
			FSampleRate = 0;
			FDuration = 0;
			FTagExists = false;
			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FTrack = 0;
            FDisc = 0;
			FYear = "";
			FGenre = "";
			FComment = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();

            FSamples = new List<Sample>();
            FPatterns = new List<IList<IList<int>>>();
            FPatternTable = new List<byte>();
            nbValidPatterns = 0;
            formatTag = "";
            trackerName = "";
            nbChannels = 0;
		}

        //                 24 * beats/minute
        // lines/minute = -----------------
        //                    ticks/line
        private double calculateDuration()
        {
            double result = 0;

            int currentPattern = 0;
            int currentLine = 0;
            bool positionJump = false;
            bool patternBreak = false;

            IList<int> line;
            
            int temp;
            double ticksPerLine = DEFAULT_TICKS_PER_LINE;
            double bpm = DEFAULT_BPM;

            int effect;
            int arg1;
            int arg2;

            do // Patterns loop
            {
                do // Lines loop
                {
                    line = FPatterns[FPatternTable[currentPattern]][currentLine];
                    foreach (int note in line) // Channels loop
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
                            else // ticks per line
                            {
                                ticksPerLine = temp;
                            }
                        }
                        else if (effect.Equals(EFFECT_POSITION_JUMP))
                        {
                            currentPattern = arg1 * 16 + arg2;
                            currentLine = 0;
                            positionJump = true;
                        }
                        else if (effect.Equals(EFFECT_PATTERN_BREAK))
                        {
                            currentPattern++;
                            currentLine = arg1 * 10 + arg2;
                            patternBreak = true;
                        }
                        else if ((effect > 10) && (effect != 12) && (effect != 14))
                        {
                            result += 0; // TODO implement
                        }
                        if (positionJump || patternBreak) break;
                    } // end channels loop
                    result += 60 * (ticksPerLine / (24 * bpm));
                    if (positionJump || patternBreak) break;

                    currentLine++;
                } while (currentLine < 64);

                if (positionJump || patternBreak)
                {
                    positionJump = false;
                    patternBreak = false;
                }
                else
                {
                    currentPattern++;
                    currentLine = 0;
                }
            } while (currentPattern < nbValidPatterns); // end patterns loop

            return result;
        }

        private byte detectNbSamples(BinaryReader source)
        {
            byte result = 31;
            long position = source.BaseStream.Position;

            source.BaseStream.Seek(1080, SeekOrigin.Begin);

            formatTag = new String(StreamUtils.ReadOneByteChars(source, 4)).Trim();

            if (!modFormats.ContainsKey(formatTag)) result = 15;

            source.BaseStream.Seek(position, SeekOrigin.Begin);

            return result;
        }


		// === PUBLIC METHODS ===

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			bool result = true;
            int maxPatterns = -1;
            byte nbSamples = 31;

            String readString;
            StringBuilder comment = new StringBuilder("");

            Sample sample;
            IList<IList<int>> pattern;
            IList<int> line;

            // == TITLE ==
            readString = new String(StreamUtils.ReadOneByteChars(source, 4));
            if (readString.Equals(SIG_POWERPACKER))
            {
                result = false;
                throw new Exception("MOD files compressed with PowerPacker are not supported yet");
            }

            // Title = first 20 chars
            FTitle = readString + StreamUtils.ReadNullTerminatedStringFixed(source, System.Text.Encoding.ASCII, 16).Trim();

            // == SAMPLES ==
            nbSamples = detectNbSamples(source);

            for (int i = 0; i < nbSamples; i++)
            {
                sample = new Sample();
                sample.Name = StreamUtils.ReadNullTerminatedStringFixed(source, System.Text.Encoding.ASCII, 22).Trim();
                sample.Size = StreamUtils.ReverseInt16(source.ReadUInt16())*2;
                sample.Finetune = source.ReadSByte();
                sample.Volume = source.ReadByte();
                sample.RepeatOffset = StreamUtils.ReverseInt16(source.ReadUInt16())*2;
                sample.RepeatLength = StreamUtils.ReverseInt16(source.ReadUInt16())*2;
                FSamples.Add(sample);
            }


            // == SONG ==
            nbValidPatterns = source.ReadByte();
            source.BaseStream.Seek(1, SeekOrigin.Current); // Controversial byte; no real use here
            for (int i = 0; i < 128; i++) FPatternTable.Add(source.ReadByte()); // Pattern table

            // File format tag
            formatTag = new String(StreamUtils.ReadOneByteChars(source, 4)).Trim();
            if (modFormats.ContainsKey(formatTag))
            {
                nbChannels = modFormats[formatTag].NbChannels;
                trackerName = modFormats[formatTag].Name;
            }
            else // Default
            {
                nbChannels = NB_CHANNELS_DEFAULT;
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, FFileName + " : MOD format tag '" + formatTag + "'not recognized");
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

            for (int p = 0; p < maxPatterns+1; p++) // Patterns loop
            {
                FPatterns.Add(new List<IList<int>>());
                pattern = FPatterns[FPatterns.Count - 1];
                // Lines loop
                for (int l = 0; l < 64; l++)
                {
                    pattern.Add(new List<int>());
                    line = pattern[pattern.Count - 1];
                    for (int c = 0; c < nbChannels; c++) // Channels loop
                    {
                        line.Add( StreamUtils.ReverseInt32(source.ReadInt32()) );
                    } // end channels loop
                } // end lines loop
            } // end patterns loop

            FDuration = calculateDuration();

            foreach (Sample aSample in FSamples)
            {
                if (aSample.Name.Length > 0) comment.Append(aSample.Name).Append("/");
            }
            if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);
            
            FComment = comment.ToString();
            FBitrate = FFileSize / FDuration;

            return result;
		}
	}

}