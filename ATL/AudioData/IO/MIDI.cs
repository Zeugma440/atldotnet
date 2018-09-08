/****************************************************************************
Software: Midi Class
Version:  1.5
Date:     2005/04/25
Author:   Valentin Schmidt
Contact:  fluxus@freenet.de
License:  Freeware

You may use and modify this software as you wish.

Translated to C# by Zeugma 440

TODO : http://www.musicxml.com/for-developers/ ?

****************************************************************************/

/*
==== SOME NOTES CONCERNING LENGTH CALCULATION - Z440 ====

- The calculated length is not "intelligent", i.e. the whole MIDI file is taken as musical material, even if there
is only one note followed by three minutes of TimeSig's and tempo changes. It may be a choice of policy, but
it appears that Winamp uses "intelligent" parsing, which ends the song whith the last _note_ event.
*/

using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Musical Instruments Digital Interface files manipulation (extension : .MID, .MIDI)
    /// </summary>
    public class Midi : MetaDataIO, IAudioDataIO
    {
        //Private properties
        private IList<MidiTrack> tracks;    // Tracks of the song
        private int timebase;           // timebase = ticks per frame (quarter note) a.k.a. PPQN (Pulses Per Quarter Note)
        private int tempoMsgNum;        // position of tempo event in track 0

        private long tempo;             // tempo (0 for unknown)
        // NB : there is no such thing as an uniform "song tempo" since tempo can change over time within each track !

        private byte type;              // MIDI STRUCTURE TYPE
        // 0 - single-track 
        // 1 - multiple tracks, synchronous
        // 2 - multiple tracks, asynchronous


        private static class MidiEvents
        {
            // Standard events
            public const int EVT_PROGRAM_CHANGE = 0x0C;
            public const int EVT_NOTE_ON = 0x09;
            public const int EVT_NOTE_OFF = 0x08;
            public const int EVT_POLY_PRESSURE = 0x0A;
            public const int EVT_CONTROLLER_CHANGE = 0x0B;
            public const int EVT_CHANNEL_PRESSURE = 0x0D;
            public const int EVT_PITCH_BEND = 0x0E;
            public const int EVT_META = 0xFF;
            public const int EVT_SYSEX = 0xF0;

            // Meta events
            public const int META_SEQUENCE_NUM = 0x00;
            public const int META_TEXT = 0x01;
            public const int META_COPYRIGHT = 0x02;
            public const int META_TRACK_NAME = 0x03;
            public const int META_INSTRUMENT_NAME = 0x04;
            public const int META_LYRICS = 0x05;
            public const int META_MARKER = 0x06;
            public const int META_CUE = 0x07;
            public const int META_CHANNEL_PREFIX = 0x20;
            public const int META_CHANNEL_PREFIX_PORT = 0x21;
            public const int META_TRACK_END = 0x2F;
            public const int META_TEMPO = 0x51;
            public const int META_SMPTE_OFFSET = 0x54;
            public const int META_TIME_SIGNATURE = 0x58;
            public const int META_KEY_SIGNATURE = 0x59;
            public const int META_SEQUENCER_DATA = 0x7F; // Sequencer specific data
        }

        private class MidiEvent
        {
            public long TickOffset = 0;
            public int Type = 0;
            public bool isMetaEvent = false;
            public int Channel = 0;

            public int Param0 = 0;
            public int Param1 = 0;
            public string Description;

            public MidiEvent(long tickOffset, int type, int channel, int param0, int param1 = 0)
            {
                TickOffset = tickOffset;
                Type = type;
                Channel = channel;
                Param0 = param0;
                Param1 = param1;
            }
        }

        private class MidiTrack
        {
            public long Duration = 0;
            public long Ticks = 0;
            public long LastSignificantEventTicks = -1;

            public MidiEvent LastEvent = null;

            public IList<MidiEvent> events = new List<MidiEvent>();

            public void Add(MidiEvent evt)
            {
                events.Add(evt);
                LastEvent = evt;

                if (MidiEvents.EVT_NOTE_ON == evt.Type
                    || MidiEvents.EVT_NOTE_OFF == evt.Type
                    || MidiEvents.EVT_CONTROLLER_CHANGE == evt.Type
                    || MidiEvents.EVT_CHANNEL_PRESSURE == evt.Type
                    || MidiEvents.EVT_POLY_PRESSURE == evt.Type
                    || MidiEvents.EVT_PITCH_BEND == evt.Type
                    || MidiEvents.META_TRACK_END == evt.Type
                    )
                {
                    LastSignificantEventTicks = evt.TickOffset;
                }
            }
        }

        #region instruments
        private static readonly string[] instrumentList = new string[128] { "Piano",
															  "Bright Piano",
															  "Electric Grand",
															  "Honky Tonk Piano",
															  "Electric Piano 1",
															  "Electric Piano 2",
															  "Harpsichord",
															  "Clavinet",
															  "Celesta",
															  "Glockenspiel",
															  "Music Box",
															  "Vibraphone",
															  "Marimba",
															  "Xylophone",
															  "Tubular Bell",
															  "Dulcimer",
															  "Hammond Organ",
															  "Perc Organ",
															  "Rock Organ",
															  "Church Organ",
															  "Reed Organ",
															  "Accordion",
															  "Harmonica",
															  "Tango Accordion",
															  "Nylon Str Guitar",
															  "Steel String Guitar",
															  "Jazz Electric Gtr",
															  "Clean Guitar",
															  "Muted Guitar",
															  "Overdrive Guitar",
															  "Distortion Guitar",
															  "Guitar Harmonics",
															  "Acoustic Bass",
															  "Fingered Bass",
															  "Picked Bass",
															  "Fretless Bass",
															  "Slap Bass 1",
															  "Slap Bass 2",
															  "Syn Bass 1",
															  "Syn Bass 2",
															  "Violin",
															  "Viola",
															  "Cello",
															  "Contrabass",
															  "Tremolo Strings",
															  "Pizzicato Strings",
															  "Orchestral Harp",
															  "Timpani",
															  "Ensemble Strings",
															  "Slow Strings",
															  "Synth Strings 1",
															  "Synth Strings 2",
															  "Choir Aahs",
															  "Voice Oohs",
															  "Syn Choir",
															  "Orchestra Hit",
															  "Trumpet",
															  "Trombone",
															  "Tuba",
															  "Muted Trumpet",
															  "French Horn",
															  "Brass Ensemble",
															  "Syn Brass 1",
															  "Syn Brass 2",
															  "Soprano Sax",
															  "Alto Sax",
															  "Tenor Sax",
															  "Baritone Sax",
															  "Oboe",
															  "English Horn",
															  "Bassoon",
															  "Clarinet",
															  "Piccolo",
															  "Flute",
															  "Recorder",
															  "Pan Flute",
															  "Bottle Blow",
															  "Shakuhachi",
															  "Whistle",
															  "Ocarina",
															  "Syn Square Wave",
															  "Syn Saw Wave",
															  "Syn Calliope",
															  "Syn Chiff",
															  "Syn Charang",
															  "Syn Voice",
															  "Syn Fifths Saw",
															  "Syn Brass and Lead",
															  "Fantasia",
															  "Warm Pad",
															  "Polysynth",
															  "Space Vox",
															  "Bowed Glass",
															  "Metal Pad",
															  "Halo Pad",
															  "Sweep Pad",
															  "Ice Rain",
															  "Soundtrack",
															  "Crystal",
															  "Atmosphere",
															  "Brightness",
															  "Goblins",
															  "Echo Drops",
															  "Sci Fi",
															  "Sitar",
															  "Banjo",
															  "Shamisen",
															  "Koto",
															  "Kalimba",
															  "Bag Pipe",
															  "Fiddle",
															  "Shanai",
															  "Tinkle Bell",
															  "Agogo",
															  "Steel Drums",
															  "Woodblock",
															  "Taiko Drum",
															  "Melodic Tom",
															  "Syn Drum",
															  "Reverse Cymbal",
															  "Guitar Fret Noise",
															  "Breath Noise",
															  "Seashore",
															  "Bird",
															  "Telephone",
															  "Helicopter",
															  "Applause",
															  "Gunshot"};
        #endregion

        private const string MIDI_FILE_HEADER = "MThd";
        private const string MIDI_TRACK_HEADER = "MTrk";

        private const int DEFAULT_TEMPO = 500; // Default MIDI tempo is 120bpm, 500ms per beat

        public StringBuilder comment;

        private double duration;
        private double bitrate;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate
        {
            get { return 0; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_SEQ; }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE); // Only for comments
        }

        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }
        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            throw new NotImplementedException();
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            duration = 0;
            bitrate = 0;

            ResetData();
        }

        public Midi(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }

        
        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private double getDuration()
        {
            long maxTicks = 0;
            double maxDuration = 0; // Longest duration among all tracks

            if (tracks.Count > 0)
            {
                // Look for the position of the latest "significant event" of the entire song (including TrackEnd)
                foreach (MidiTrack aTrack in tracks)
                {
                    maxTicks = Math.Max(maxTicks, aTrack.LastSignificantEventTicks);
                }

                long currentDuration = 0;
                long lastTempoTicks = 0;
                int currentTempo = DEFAULT_TEMPO;

                // Build "duration chunks" using the "master tempo map" provided in track 0
                foreach (MidiEvent evt in tracks[0].events)
                {
                    if (MidiEvents.META_TEMPO == evt.Type)
                    {
                        currentDuration += (evt.TickOffset - lastTempoTicks) * currentTempo;

                        currentTempo = evt.Param0;
                        lastTempoTicks = evt.TickOffset;
                    }
                }
                // Make sure the song lasts until the latest event
                if (maxTicks > lastTempoTicks) currentDuration += (maxTicks - lastTempoTicks) * currentTempo;
                maxDuration = currentDuration;
            }

            // For an obscure reason, this algorithm constantly calculates
            // a duration equals to (actual duration calculated by BASSMIDI - 1 second), hence this ugly " + 1000"
            return (maxDuration / timebase / 1000.00) + 1000;
        }

        /****************************************************************************
		*                                                                           *
		*                              Public methods                               *
		*                                                                           *
		****************************************************************************/

        //---------------------------------------------------------------
        // Imports Standard MIDI File (type 0 or 1) (and RMID)
        // (if optional parameter $tn set, only track $tn is imported)
        //---------------------------------------------------------------
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] header;
            string trigger;

            IList<string> trackStrings = new List<string>();
            IList<MidiTrack> tracks = new List<MidiTrack>();

            resetData();

            // Ignores everything (comments) written before the MIDI header
            /*
             *          trigger = "";
                        while (trigger != MIDI_FILE_HEADER)
                        {
                            trigger += new String(StreamUtils.ReadOneByteChars(source, 1));
                            if (trigger.Length > 4) trigger = trigger.Remove(0, 1);
                        }
            */
            StreamUtils.FindSequence(source.BaseStream, Utils.Latin1Encoding.GetBytes(MIDI_FILE_HEADER));

            // Ready to read header data...
            header = source.ReadBytes(10);
            if ((header[0] != 0) ||
                (header[1] != 0) ||
                (header[2] != 0) ||
                (header[3] != 6)
                )
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR,"Wrong MIDI header");
                return false;
            }
            type = header[5];

            // MIDI STRUCTURE TYPE
            // 0 - single-track 
            // 1 - multiple tracks, synchronous
            // 2 - multiple tracks, asynchronous
            if (type > 1)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "SMF type 2 MIDI files are partially supported; results may be approximate");
            }

            tagExists = true;

            timebase = (header[8] << 8) + header[9];

            tempo = 0; // maybe (hopefully!) overwritten by parseTrack

            int trackSize = 0;
            int nbTrack = 0;
            comment = new StringBuilder("");

            // Ready to read track data...
            while (source.BaseStream.Position < sizeInfo.FileSize - 4)
            {
                trigger = Utils.Latin1Encoding.GetString(source.ReadBytes(4));

                if (trigger != MIDI_TRACK_HEADER)
                {
                    source.BaseStream.Seek(-3, SeekOrigin.Current);
                    if (!StreamUtils.FindSequence(source.BaseStream, Utils.Latin1Encoding.GetBytes(MIDI_TRACK_HEADER))) break;
                }

                // trackSize is stored in big endian -> needs inverting
                trackSize = StreamUtils.ReverseInt32(source.ReadInt32());

                tracks.Add(parseTrack(source.ReadBytes(trackSize), nbTrack));
                nbTrack++;
            }

            this.tracks = tracks;

            if (comment.Length > 0) comment.Remove(comment.Length - 1, 1);
            tagData.IntegrateValue(TagData.TAG_FIELD_COMMENT, comment.ToString());

            duration = getDuration();
            bitrate = (double) sizeInfo.FileSize / duration;

            return true;
        }

        #region Legacy Utilities
        //***************************************************************
        // PUBLIC UTILITIES
        //***************************************************************

        //---------------------------------------------------------------
        // returns list of drumset instrument names
        //---------------------------------------------------------------
        /* ## TO DO
        function getDrumset(){
            return array(
            35=>'Acoustic Bass Drum',
            36=>'Bass Drum 1',
            37=>'Side Stick',
            38=>'Acoustic Snare',
            39=>'Hand Clap',
            40=>'Electric Snare',
            41=>'Low Floor Tom',
            42=>'Closed Hi-Hat',
            43=>'High Floor Tom',
            44=>'Pedal Hi-Hat',
            45=>'Low Tom',
            46=>'Open Hi-Hat',
            47=>'Low Mid Tom',
            48=>'High Mid Tom',
            49=>'Crash Cymbal 1',
            50=>'High Tom',
            51=>'Ride Cymbal 1',
            52=>'Chinese Cymbal',
            53=>'Ride Bell',
            54=>'Tambourine',
            55=>'Splash Cymbal',
            56=>'Cowbell',
            57=>'Crash Cymbal 2',
            58=>'Vibraslap',
            59=>'Ride Cymbal 2',
            60=>'High Bongo',
            61=>'Low Bongo',
            62=>'Mute High Conga',
            63=>'Open High Conga',
            64=>'Low Conga',
            65=>'High Timbale',
            66=>'Low Timbale',
            //35..66
            67=>'High Agogo',
            68=>'Low Agogo',
            69=>'Cabase',
            70=>'Maracas',
            71=>'Short Whistle',
            72=>'Long Whistle',
            73=>'Short Guiro',
            74=>'Long Guiro',
            75=>'Claves',
            76=>'High Wood Block',
            77=>'Low Wood Block',
            78=>'Mute Cuica',
            79=>'Open Cuica',
            80=>'Mute Triangle',
            81=>'Open Triangle');
        }
        */

        //---------------------------------------------------------------
        // returns list of standard drum kit names
        //---------------------------------------------------------------
        /* ## TO DO 
        function getDrumkitList(){
            return array(
                1   => 'Dry',
                9   => 'Room',
                19  => 'Power',
                25  => 'Electronic',
                33  => 'Jazz',
                41  => 'Brush',
                57  => 'SFX',
                128 => 'Default'
            );
        }
        */

        //---------------------------------------------------------------
        // returns list of note names
        //---------------------------------------------------------------
        /*
        function getNoteList(){
          //note 69 (A6) = A440
          //note 60 (C6) = Middle C
            return array(
            //Do          Re           Mi    Fa           So           La           Ti
            'C0', 'Cs0', 'D0', 'Ds0', 'E0', 'F0', 'Fs0', 'G0', 'Gs0', 'A0', 'As0', 'B0',
            'C1', 'Cs1', 'D1', 'Ds1', 'E1', 'F1', 'Fs1', 'G1', 'Gs1', 'A1', 'As1', 'B1',
            'C2', 'Cs2', 'D2', 'Ds2', 'E2', 'F2', 'Fs2', 'G2', 'Gs2', 'A2', 'As2', 'B2',
            'C3', 'Cs3', 'D3', 'Ds3', 'E3', 'F3', 'Fs3', 'G3', 'Gs3', 'A3', 'As3', 'B3',
            'C4', 'Cs4', 'D4', 'Ds4', 'E4', 'F4', 'Fs4', 'G4', 'Gs4', 'A4', 'As4', 'B4',
            'C5', 'Cs5', 'D5', 'Ds5', 'E5', 'F5', 'Fs5', 'G5', 'Gs5', 'A5', 'As5', 'B5',
            'C6', 'Cs6', 'D6', 'Ds6', 'E6', 'F6', 'Fs6', 'G6', 'Gs6', 'A6', 'As6', 'B6',
            'C7', 'Cs7', 'D7', 'Ds7', 'E7', 'F7', 'Fs7', 'G7', 'Gs7', 'A7', 'As7', 'B7',
            'C8', 'Cs8', 'D8', 'Ds8', 'E8', 'F8', 'Fs8', 'G8', 'Gs8', 'A8', 'As8', 'B8',
            'C9', 'Cs9', 'D9', 'Ds9', 'E9', 'F9', 'Fs9', 'G9', 'Gs9', 'A9', 'As9', 'B9',
            'C10','Cs10','D10','Ds10','E10','F10','Fs10','G10');
        }
        */
        #endregion

        private MidiTrack parseTrack(byte[] data, int trackNumber)
        {
            MidiTrack track = new MidiTrack();

            int trackLen = data.Length;

            int position = 0;
            long currentTicks = 0;
            int currentDelta;
            byte eventType;
            int eventTypeHigh;
            int eventTypeLow;
            byte meta;
            int num;
            int len;
            byte tmp;
            byte c;
            string txt;
            MidiEvent evt;

            int currentTempo = 0;
            long lastTempoTicks = -1;

            while (position < trackLen)
            {
                // timedelta
                currentDelta = readVarLen(ref data, ref position);
                currentTicks += currentDelta;

                eventType = data[position];
                eventTypeHigh = (eventType >> 4);
                eventTypeLow = (eventType - eventTypeHigh * 16);
                switch (eventTypeHigh)
                {
                    case MidiEvents.EVT_PROGRAM_CHANGE: //PrCh = ProgramChange
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, data[position + 1]);
                        evt.Description = " PrCh ch=" + evt.Channel + " p=" + evt.Param0;
                        track.Add(evt);

                        position += 2;
                        break;
                    case MidiEvents.EVT_NOTE_ON: //On
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, data[position + 1], data[position + 2]);
                        evt.Description = " On ch=" + evt.Channel + " n=" + evt.Param0 + " v=" + evt.Param1;
                        track.Add(evt);

                        position += 3;
                        break;
                    case MidiEvents.EVT_NOTE_OFF: //Off
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, data[position + 1], data[position + 2]);
                        evt.Description = " Off ch=" + evt.Channel + " n=" + evt.Param0 + " v=" + evt.Param1;
                        track.Add(evt);

                        position += 3;
                        break;
                    case MidiEvents.EVT_POLY_PRESSURE: //PoPr = PolyPressure
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, data[position + 1], data[position + 2]);
                        evt.Description = " PoPr ch=" + evt.Channel + " n=" + evt.Param0 + " v=" + evt.Param1;
                        track.Add(evt);

                        position += 3;
                        break;
                    case MidiEvents.EVT_CONTROLLER_CHANGE: //Par = ControllerChange
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, data[position + 1], data[position + 2]);
                        evt.Description = " Par ch=" + evt.Channel + " c=" + evt.Param0 + " v=" + evt.Param1;
                        track.Add(evt);

                        position += 3;
                        break;
                    case MidiEvents.EVT_CHANNEL_PRESSURE: //ChPr = ChannelPressure
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, data[position + 1]);
                        evt.Description = " ChPr ch=" + evt.Channel + " v=" + evt.Param0;
                        track.Add(evt);

                        position += 2;
                        break;
                    case MidiEvents.EVT_PITCH_BEND: //Pb = PitchBend
                        evt = new MidiEvent(currentTicks, eventTypeHigh, eventTypeLow + 1, (data[position + 1] & 0x7F) | ((data[position + 2] & 0x7F) << 7));
                        evt.Description = " Pb ch=" + evt.Channel + " v=" + evt.Param0;
                        track.Add(evt);

                        position += 3;
                        break;
                    default:
                        switch (eventType)
                        {
                            case 0xFF: // Meta
                                meta = data[position + 1];
                                switch (meta)
                                {
                                    case MidiEvents.META_SEQUENCE_NUM: // sequence_number
                                        tmp = data[position + 2];
                                        if (tmp == 0x00) { num = trackNumber; position += 3; }
                                        else { num = 1; position += 5; }

                                        evt = new MidiEvent(currentTicks, meta, -1, num);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Seqnr " + evt.Param0;
                                        track.Add(evt);

                                        break;

                                    case MidiEvents.META_TEXT: // Meta Text
                                    case MidiEvents.META_COPYRIGHT: // Meta Copyright
                                    case MidiEvents.META_TRACK_NAME: // Meta TrackName ???sequence_name???
                                    case MidiEvents.META_INSTRUMENT_NAME: // Meta InstrumentName
                                    case MidiEvents.META_LYRICS: // Meta Lyrics
                                    case MidiEvents.META_MARKER: // Meta Marker
                                    case MidiEvents.META_CUE: // Meta Cue
                                        string[] texttypes = new string[7] { "Text", "Copyright", "TrkName", "InstrName", "Lyric", "Marker", "Cue" };

                                        string type = texttypes[meta - 1];
                                        position += 2;
                                        len = readVarLen(ref data, ref position);
                                        if ((len + position) > trackLen) throw new Exception("Meta " + type + " has corrupt variable length field (" + len + ") [track: " + trackNumber + " dt: " + currentDelta + "]");

                                        txt = Encoding.ASCII.GetString(data, position, len);
                                        if (MidiEvents.META_TEXT == meta || MidiEvents.META_TRACK_NAME == meta || MidiEvents.META_MARKER == meta) comment.Append(txt).Append(Settings.InternalValueSeparator);
                                        else if (MidiEvents.META_COPYRIGHT == meta) tagData.IntegrateValue(TagData.TAG_FIELD_COPYRIGHT, txt);

                                        evt = new MidiEvent(currentTicks, meta, -1, meta - 1);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta " + type + " \"" + txt + "\"";
                                        track.Add(evt);

                                        position += len;
                                        break;
                                    case MidiEvents.META_CHANNEL_PREFIX: // ChannelPrefix
                                        evt = new MidiEvent(currentTicks, meta, -1, data[position + 3]);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta ChannelPrefix " + evt.Param0;
                                        track.Add(evt);

                                        position += 4;
                                        break;
                                    case MidiEvents.META_CHANNEL_PREFIX_PORT: // ChannelPrefixOrPort
                                        evt = new MidiEvent(currentTicks, meta, -1, data[position + 3]);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta ChannelPrefixOrPort " + evt.Param0;
                                        track.Add(evt);

                                        position += 4;
                                        break;
                                    case MidiEvents.META_TRACK_END: // Meta TrkEnd
                                        evt = new MidiEvent(currentTicks, meta, -1, -1);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta TrkEnd";
                                        track.Add(evt);

                                        track.Ticks = currentTicks;
                                        if (lastTempoTicks > -1) // there has been at least one tempo change in the track
                                        {
                                            track.Duration += (currentTicks - lastTempoTicks) * currentTempo;
                                        }
                                        else
                                        {
                                            track.Duration = currentTicks * this.tempo;
                                        }
                                        return track;//ignore rest
                                    //break;
                                    case MidiEvents.META_TEMPO: // Tempo
                                        // Adds (ticks since last tempo event)*current tempo to track duration
                                        if (lastTempoTicks > -1)
                                        {
                                            track.Duration += (currentTicks - lastTempoTicks) * currentTempo;
                                        }
                                        lastTempoTicks = currentTicks;

                                        currentTempo = data[position + 3] * 0x010000 + data[position + 4] * 0x0100 + data[position + 5];
                                        if (0 == currentTempo) currentTempo = DEFAULT_TEMPO;

                                        evt = new MidiEvent(currentTicks, meta, -1, currentTempo);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta Tempo " + evt.Param0 + " (duration :" + track.Duration + ")";
                                        track.Add(evt);

                                        // Sets song tempo as last tempo event of 1st track
                                        // according to some MIDI files convention
                                        if (0 == trackNumber/* && 0 == this.tempo*/)
                                        {
                                            this.tempo = currentTempo;
                                            this.tempoMsgNum = track.events.Count - 1;
                                        }
                                        position += 6;
                                        break;
                                    case MidiEvents.META_SMPTE_OFFSET: // SMPTE offset
                                        byte h = data[position + 3];
                                        byte m = data[position + 4];
                                        byte s = data[position + 5];
                                        byte f = data[position + 6];
                                        byte fh = data[position + 7];

                                        // TODO : store the arguments in a solid structure within MidiEvent
                                        evt = new MidiEvent(currentTicks, meta, -1, -1);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta SMPTE " + h + " " + m + " " + s + " " + f + " " + fh;
                                        track.Add(evt);

                                        position += 8;
                                        break;
                                    case MidiEvents.META_TIME_SIGNATURE: // TimeSig
                                        byte z = data[position + 3];
                                        int t = 2 ^ data[position + 4];
                                        byte mc = data[position + 5];
                                        c = data[position + 6];

                                        // TODO : store the arguments in a solid structure within MidiEvent
                                        evt = new MidiEvent(currentTicks, meta, -1, -1);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta TimeSig " + z + "/" + t + " " + mc + " " + c;
                                        track.Add(evt);

                                        position += 7;
                                        break;
                                    case MidiEvents.META_KEY_SIGNATURE: // KeySig
                                        evt = new MidiEvent(currentTicks, meta, -1, data[position + 3], data[position + 4]);
                                        evt.isMetaEvent = true;
                                        evt.Description = " Meta KeySig vz=" + evt.Param0 + " " + (evt.Param1 == 0 ? "major" : "minor");
                                        track.Add(evt);

                                        position += 5;
                                        break;
                                    case MidiEvents.META_SEQUENCER_DATA: // Sequencer specific data
                                        position += 2;
                                        len = readVarLen(ref data, ref position);
                                        if ((len + position) > trackLen) throw new Exception("SeqSpec has corrupt variable length field (" + len + ") [track: " + trackNumber + " dt: " + currentDelta + "]");
                                        position -= 3;
                                        {
                                            //String str = Encoding.ASCII.GetString(data, position + 3, len); //data.=' '.sprintf("%02x",(byte)($data[$p+3+$i]));
                                            evt = new MidiEvent(currentTicks, meta, -1, currentTempo);
                                            evt.isMetaEvent = true;
                                            evt.Description = " Meta SeqSpec";
                                            track.Add(evt);
                                        }
                                        position += len + 3;
                                        break;

                                    default:
                                        // "unknown" Meta-Events
                                        byte metacode = data[position + 1];
                                        position += 2;
                                        len = readVarLen(ref data, ref position);
                                        if ((len + position) > trackLen) throw new Exception("Meta " + metacode + " has corrupt variable length field (" + len + ") [track: " + trackNumber + " dt: " + currentDelta + "]");
                                        position -= 3;
                                        {
                                            string str = Encoding.ASCII.GetString(data, position + 3, len); //sprintf("%02x",(byte)($data[$p+3+$i]));
                                            evt = new MidiEvent(currentTicks, meta, -1, currentTempo);
                                            evt.isMetaEvent = true;
                                            evt.Description = " Meta 0x" + metacode + " " + str;
                                            track.Add(evt);
                                        }
                                        position += len + 3;

                                        break;
                                } // switch meta
                                break; // End Meta

                            case MidiEvents.EVT_SYSEX: // SysEx
                                position += 1;
                                len = readVarLen(ref data, ref position);
                                if ((len + position) > trackLen) throw new Exception("SysEx has corrupt variable length field (" + len + ") [track: " + trackNumber + " dt: " + currentDelta + " p: " + position + "]");
                                {
                                    //String str = "f0" + Encoding.ASCII.GetString(data, position + 2, len); //str+=' '.sprintf("%02x",(byte)(data[p+2+i]));
                                    evt = new MidiEvent(currentTicks, eventTypeHigh, -1, currentTempo);
                                    evt.isMetaEvent = true;
                                    evt.Description = " SysEx";
                                    track.Add(evt);
                                }
                                position += len;
                                break;
                            default: // Repetition of last event?
                                if ((track.LastEvent.Type == MidiEvents.EVT_NOTE_ON) || (track.LastEvent.Type == MidiEvents.EVT_NOTE_OFF))
                                {
                                    evt = new MidiEvent(currentTicks, track.LastEvent.Type, track.LastEvent.Channel, data[position], data[position + 1]);
                                    evt.Description = " " + (track.LastEvent.Type == MidiEvents.EVT_NOTE_ON ? "On" : "Off") + " ch=" + evt.Channel + " n=" + evt.Param0 + " v=" + evt.Param1;
                                    track.Add(evt);

                                    position += 2;
                                }
                                else if (track.LastEvent.Type == MidiEvents.EVT_PROGRAM_CHANGE)
                                {
                                    evt = new MidiEvent(currentTicks, track.LastEvent.Type, track.LastEvent.Channel, data[position]);
                                    evt.Description = " PrCh ch=" + evt.Channel + " p=" + evt.Param0;
                                    track.Add(evt);

                                    position += 1;
                                }
                                else if (track.LastEvent.Type == MidiEvents.EVT_POLY_PRESSURE)
                                {
                                    evt = new MidiEvent(currentTicks, track.LastEvent.Type, track.LastEvent.Channel, data[position + 1], data[position + 2]);
                                    evt.Description = " PoPr ch=" + evt.Channel + " n=" + evt.Param0 + " v=" + evt.Param1;
                                    track.Add(evt);

                                    position += 2;
                                }
                                else if (track.LastEvent.Type == MidiEvents.EVT_CHANNEL_PRESSURE)
                                {
                                    evt = new MidiEvent(currentTicks, track.LastEvent.Type, track.LastEvent.Channel, data[position]);
                                    evt.Description = " ChPr ch=" + evt.Channel + " v=" + evt.Param0;
                                    track.Add(evt);

                                    position += 1;
                                }
                                else if (track.LastEvent.Type == MidiEvents.EVT_CONTROLLER_CHANGE)
                                {
                                    evt = new MidiEvent(currentTicks, track.LastEvent.Type, track.LastEvent.Channel, data[position], data[position + 1]);
                                    evt.Description = " Par ch=" + evt.Channel + " c=" + evt.Param0 + " v=" + evt.Param1;
                                    track.Add(evt);

                                    position += 2;
                                }
                                else if (track.LastEvent.Type == MidiEvents.EVT_PITCH_BEND)
                                {
                                    evt = new MidiEvent(currentTicks, track.LastEvent.Type, track.LastEvent.Channel, (data[position] & 0x7F) | ((data[position + 1] & 0x7F) << 7));
                                    evt.Description = " Pb ch=" + evt.Channel + " v=" + evt.Param0;
                                    track.Add(evt);

                                    position += 2;
                                }
                                //default:
                                // MM: ToDo: Repetition of SysEx and META-events? with <last>?? \n";
                                //  _err("unknown repetition: $last");
                                break;
                        } // switch (eventType)
                        break;
                } // switch ($high)
            } // while ( p < trackLen )
            track.Ticks = currentTicks;
            if (lastTempoTicks > -1)
            {
                track.Duration += (currentTicks - lastTempoTicks) * currentTempo;
            }
            else
            {
                track.Duration = currentTicks * this.tempo;
            }

            return track;
        }

        //---------------------------------------------------------------
        // variable length string to int (+repositioning)
        //---------------------------------------------------------------
        //# TO LOOK AFTER CAREFULLY <.<
        private int readVarLen(ref byte[] data, ref int pos)
        {
            int value;
            int c;

            value = data[pos++];
            if ((value & 0x80) != 0)
            {
                value &= 0x7F;
                do
                {
                    c = data[pos++];
                    value = (value << 7) + (c & 0x7F);
                } while ((c & 0x80) != 0);
            }
            return (value);
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            // Not implemented, as it would require a whole new set of metadata related to tracks and their names
            return 0;
        }
    }
}