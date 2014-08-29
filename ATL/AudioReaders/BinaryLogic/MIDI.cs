/****************************************************************************
Software: Midi Class
Version:  1.5
Date:     2005/04/25
Author:   Valentin Schmidt
Contact:  fluxus@freenet.de
License:  Freeware

You may use and modify this software as you wish.

Translated to C# by Zeugma 440

Last Changes:  - calculated duration now takes tempo changes into account
               - some fixes by Michael Mlivoncic (MM):
							  + PrCh added as shortened form (repetition)
				+ exception-handling for PHP 5: raise exception on corrupt MIDI-files
				+ download now sends gzip-Encoded (if supported  by browser)
				+ parser correctly reads field-length > 127 (several event types)
				+ fixed problem with fopen ("rb")
				+ PitchBend: correct values (writing back negative nums lead to corrupt files)
				+ parser now accepts unknown meta-events

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
using System.Collections;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Musical Instruments Digital Interface files manipulation (extension : .MID, .MIDI)
    /// </summary>
	class Midi : AudioDataReader
	{

		//Private properties
		private IList<MidiTrack> tracks;    // Tracks of the song
		private int timebase;           // timebase = ticks per frame (quarter note)
        private int tempoMsgNum;        // position of tempo event in track 0
		private long tempo;             // tempo (0 for unknown)
                                        // NB : there is no such thing as "song tempo" since tempo can change over time within each track !
		private byte type;              // MIDI STRUCTURE TYPE
                                        // 0 - single-track 
                                        // 1 - multiple tracks, synchronous
                                        // 2 - multiple tracks, asynchronous

        // MIDI track
        private class MidiTrack
        {
            public long duration = 0;
            public long ticks = 0;
            public IList<String> events;

            public MidiTrack()
            {
                events = new List<String>();
            }
            public void Add(String s)
            {
                events.Add(s);
            }
        }

    
		private static String[] instrumentList = new String[128] { "Piano",
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

        private const String MIDI_FILE_HEADER = "MThd";
        private const String MIDI_TRACK_HEADER = "MTrk";

    	public override bool IsVBR
		{
			get { return false; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_SEQ; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return false; }
        }

        private double getDuration()
		{
			long maxTime=0;

            foreach (MidiTrack aTrack in tracks)
            {
                maxTime = Math.Max(maxTime, aTrack.duration);
            }
            return maxTime / timebase / 1000000;
		}

        protected override void resetSpecificData()
        {
            // Nothing
        }

        /****************************************************************************
		*                                                                           *
		*                              Public methods                               *
		*                                                                           *
		****************************************************************************/

		//---------------------------------------------------------------
		// imports Standard MIDI File (typ 0 or 1) (and RMID)
		// (if optional parameter $tn set, only track $tn is imported)
		//---------------------------------------------------------------
		public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
            Stream fs = source.BaseStream;
			byte[] header;

			ArrayList trackStrings = new ArrayList();
			IList<MidiTrack> tracks = new List<MidiTrack>();

			String trigger = "";
        
			// Ignores everything (comments) written before the MIDI header
            while (trigger != MIDI_FILE_HEADER)
			{
				trigger += new String( StreamUtils.ReadOneByteChars(source,1) );
				if (trigger.Length > 4) trigger = trigger.Remove(0,1);
			}

			// Ready to read header data...
            header = source.ReadBytes(10);
			if ( (header[0] != 0) ||
				(header[1] != 0) ||
				(header[2] != 0) ||
				(header[3] != 6)
				)
			{
                System.Console.WriteLine("Wrong MIDI-header (" + FFileName + ")");
			}
			type = header[5];
    
			// MIDI STRUCTURE TYPE
			// 0 - single-track 
			// 1 - multiple tracks, synchronous
			// 2 - multiple tracks, asynchronous
			if (type > 1)
			{
				System.Console.WriteLine("[MIDI Reader] only SMF Type 0 and 1 supported ("+FFileName+")");
			}

            FValid = true;
    
			//$trackCnt = (byte)($header[10])*256 + (byte)($header[11]); //ignore
			this.timebase = ((byte)header[8]*256)+(byte)header[9];

			this.tempo = 0; // maybe (hopefully!) overwritten by _parseTrack

			int trackSize = 0;
			int nbTrack = 0;

			// Ready to read track data...
			while( fs.Position < FFileSize-4 )
			{
				trigger = new String( StreamUtils.ReadOneByteChars(source,4) );

                if (trigger != MIDI_TRACK_HEADER)
				{
                    // Track header/announced filesize might be corrupted; looking for next header
                    long initialPos = fs.Position - 4;
                    String window = "";
                    char c = '\0';
                    int additionalSize = 0;

                    while ((window != MIDI_TRACK_HEADER) && (fs.Position < FFileSize))
                    {
                        c = StreamUtils.ReadOneByteChar(source);
                        additionalSize++;
                        window += c;
                        if (window.Length > 4) window = window.Substring(1, 4);
                    }
                    long newPos = fs.Position;
                    if (window == MIDI_TRACK_HEADER) newPos = newPos - 4;

                    int newSize = (int)(trackSize+(newPos-initialPos));
                    tracks.RemoveAt(tracks.Count-1);

                    fs.Seek(-newSize, SeekOrigin.Current);
                    tracks.Add(_parseTrack(source.ReadBytes(newSize), nbTrack-1));
                        
                    /*
					System.Console.WriteLine("MIDI MTrk parsing error : aborting parsing here ("+iPath+")");
					break;
                        * */
				}
				else
				{
					// trackSize is stored in big endian -> needs inverting
					trackSize = StreamUtils.ReverseInt32( source.ReadInt32() );						

					tracks.Add( _parseTrack( source.ReadBytes(trackSize), nbTrack ) );
					nbTrack++;
				}
			}

            this.tracks = tracks;

            FDuration = getDuration();
            FBitrate = FFileSize / FDuration;

            return true;
		}

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

		/****************************************************************************
		*                                                                           *
		*                              Private methods                              *
		*                                                                           *
		****************************************************************************/

		//---------------------------------------------------------------
		// converts binary track string to track (list of msg strings)
		//---------------------------------------------------------------
		private MidiTrack _parseTrack(byte[] data, int tn)
		{
            MidiTrack track = new MidiTrack();

			//$trackLen2 =  ((( (( ((byte)($data[0]) << 8) | (byte)($data[1]))<<8) | (byte)($data[2]) ) << 8 ) |  (byte)($data[3]) );
			//$trackLen2 += 4;
			int trackLen = data.Length;
			// MM: ToDo: Warn if trackLen and trackLen2 are different!!!
			// if ($trackLen != $trackLen2) { echo "Warning: TrackLength is corrupt ($trackLen != $trackLen2)! \n"; }
    
			//# convert explicitly $xxx in strings to their actual values !
			int p=0; //4??
			long time = 0;
			int dt;
			byte tempByte;
			byte high;
			byte low;
			byte chan=0;
			byte prog;
			byte note;
			byte meta;
			byte vel;
			byte val;
			int num;
			int len;
			byte tmp;
			byte c;
			String txt;
			String last="";

            long iTempo = 0;
            long lastTempoTicks = -1;
    
			while ( p < trackLen )
			{
				// timedelta
				dt = _readVarLen(ref data,ref p);
				time += dt;

				tempByte = data[p];//#
				high = (byte)(tempByte >> 4);
				low = (byte)(tempByte - high*16);
				switch(high)
				{
					case 0x0C: //PrCh = ProgramChange
						chan = (byte)(low+1);
						prog = data[p+1];
						last = "PrCh";
						track.Add(time+"PrCh ch="+chan+" p="+prog);
						p+=2;
						break;
					case 0x09: //On
						chan = (byte)(low+1);
						note = data[p+1];
						vel = data[p+2];
						last = "On";
						track.Add(time+" On ch="+chan+" n="+note+" v="+vel);
						p+=3;
						break;
					case 0x08: //Off
						chan = (byte)(low+1);
						note = data[p+1];
						vel = data[p+2];
						last = "Off";
						track.Add(time+" Off ch="+chan+" n="+note+" v="+vel);
						p+=3;
						break;
					case 0x0A: //PoPr = PolyPressure
						chan = (byte)(low+1);
						note = data[p+1];
						val = data[p+2];
						last = "PoPr";
						track.Add(time+" PoPr ch="+chan+" n="+note+" v="+val);
						p+=3;
						break;
					case 0x0B: //Par = ControllerChange
						chan = (byte)(low+1);
						c = data[p+1];
						val = data[p+2];
						last = "Par";
						track.Add(time+" Par ch="+chan+" c="+c+" v="+val);
						p+=3;
						break;
					case 0x0D: //ChPr = ChannelPressure
						chan = (byte)(low+1);
						val = data[p+1];
						last = "ChPr";
						track.Add(time+" ChPr ch="+chan+" v="+val);
						p+=2;
						break;
					case 0x0E: //Pb = PitchBend
						chan = (byte)(low+1);
						val = (byte)( (data[p+1] & 0x7F) | ( (data[p+2] & 0x7F) << 7 ) );
						last = "Pb";
						track.Add(time+" Pb ch="+chan+" v="+val);
						p+=3;
						break;
					default:
					switch(tempByte)
					{
						case 0xFF: // Meta
							meta = data[p+1];
						switch (meta)
						{
							case 0x00: // sequence_number
								tmp = data[p+2];
								if (tmp==0x00) { num = tn; p+=3;}
								else { num= 1; p+=5; }
								track.Add(time+" Seqnr "+num);
								break;

							case 0x01: // Meta Text
							case 0x02: // Meta Copyright
							case 0x03: // Meta TrackName ???sequence_name???
							case 0x04: // Meta InstrumentName
							case 0x05: // Meta Lyrics
							case 0x06: // Meta Marker
							case 0x07: // Meta Cue
								String[] texttypes = new String[7] {"Text","Copyright","TrkName","InstrName","Lyric","Marker","Cue"};
								String type = texttypes[meta-1];
								p +=2;
								len = _readVarLen(ref data,ref p);
								if ( (len+p) > trackLen ) throw new Exception("Meta "+type+" has corrupt variable length field ("+len+") [track: "+tn+" dt: "+dt+"]");
										
								//txt = data.Substring(p,len);
								txt = "";
								for (int i=p; i<p+len; i++)
								{
									txt = txt + (char)data[i];
								}

								track.Add(time+" Meta "+type+" \""+txt+"\"");
								p+=len;
								break;
							case 0x20: // ChannelPrefix
								chan = data[p+3];
								//if (chan<10) chan = "0"+chanStr;//???
								track.Add(time+" Meta 0x20 "+chan);
								p+=4;
								break;
							case 0x21: // ChannelPrefixOrPort
								chan = data[p+3];
								//if (chan<10) chan = "0"+chan.ToString();//???
								track.Add(time+" Meta 0x21 "+chan);
								p+=4;
								break;
							case 0x2F: // Meta TrkEnd
								track.Add(time+" Meta TrkEnd");
                                track.ticks = time;
                                if (lastTempoTicks > -1)
                                {
                                    track.duration += (time - lastTempoTicks) * iTempo;
                                }
                                else
                                {
                                    track.duration = time * this.tempo;
                                }
								return track;//ignore rest
								//break;
							case 0x51: // Tempo
                                // Adds (ticks since last tempo)*last tempo to track duration
                                if (lastTempoTicks > -1)
                                {
                                    track.duration += (time - lastTempoTicks) * iTempo;
                                }
                                lastTempoTicks = time;

								iTempo = data[p+3]*0x010000 + data[p+4]*0x0100 + data[p+5];
                                track.Add("" + time + " Tempo " + iTempo);
                                
                                // Sets song tempo as last tempo event of 1st track
                                // according to some MIDI files convention
								if (0 == tn/* && 0 == this.tempo*/) 
								{
									this.tempo = iTempo; 
                                    this.tempoMsgNum = track.events.Count - 1;
								}
								p+=6;
								break;
							case 0x54: // SMPTE offset
								byte h = data[p+3];
								byte m = data[p+4];
								byte s = data[p+5];
								byte f = data[p+6];
								byte fh = data[p+7];
								track.Add(time+" SMPTE "+h+" "+m+" "+s+" "+f+" "+fh);
								p+=8;
								break;
							case 0x58: // TimeSig
								byte z = data[p+3];
								int t = 2^data[p+4];
								byte mc = data[p+5];
								c = data[p+6];
								track.Add(time+" TimeSig "+z+"/"+t+" "+mc+" "+c);
								p+=7;
								break;
							case 0x59: // KeySig
								byte vz = data[p+3];
								String g = data[p+4]==0?"major":"minor";
								track.Add(time+" KeySig "+vz+" "+g);
								p+=5;
								break;
							case 0x7F: // Sequencer specific data (string or hexString???)
								p +=2;
								len = _readVarLen(ref data,ref p);
								if ((len+p) > trackLen) throw new Exception("SeqSpec has corrupt variable length field ("+len+") [track: "+tn+" dt: "+dt+"]");
								p-=3;
							{
								String str="";
								//# some formatting to do here
								for (int i=0;i<len;i++) str += ' '+data[p+3+i]; //data.=' '.sprintf("%02x",(byte)($data[$p+3+$i]));
								track.Add(time+" SeqSpec"+str);
							}
								p+=len+3;
								break;

							default:
								// MM added: accept "unknown" Meta-Events
								//# here too
								byte metacode = data[p+1];//sprintf("%02x", (byte)($data[$p+1]) );
								p +=2;
								len = _readVarLen(ref data,ref p);
								if ((len+p) > trackLen) throw new Exception("Meta "+metacode+" has corrupt variable length field ("+len+") [track: "+tn+" dt: "+dt+"]");
								p -=3;
							{
								String str="";
								//# here as well
								for (int i=0;i<len;i++) str+=" "+data[p+3+i];//sprintf("%02x",(byte)($data[$p+3+$i]));
								track.Add(time+" Meta 0x"+metacode+" "+str);
							}
								p+=len+3;
								break;
						} // switch meta
							break; // End Meta

						case 0xF0: // SysEx
							p +=1;
							len = _readVarLen(ref data,ref p);
							if ((len+p) > trackLen) throw new Exception("SysEx has corrupt variable length field ("+len+") [track: "+tn+" dt: "+dt+" p: "+p+"]");
						{
							String str = "f0";
							for (int i=0;i<len;i++) str+=' '+data[p+2+i]; //str+=' '.sprintf("%02x",(byte)(data[p+2+i]));
							track.Add(time+" SysEx "+str);
						}
							p+=len;
							break;
						default: // Repetition of last event?
							if ((last == "On") || (last == "Off"))
							{
								note = data[p];
								vel = data[p+1];
								track.Add(time+" "+last+" ch="+chan+" n="+note+" v="+vel);
								p+=2;
							}
							if (last == "PrCh")
							{
								prog = data[p];
								track.Add(time+" PrCh ch="+chan+" p="+prog);
								p+=1;
							}
							if (last == "PoPr")
							{
								note = data[p+1];
								val = data[p+2];
								track.Add(time+" PoPr ch="+chan+" n="+note+" v="+val);
								p+=2;
							}
							if(last == "ChPr")
							{
								val = data[p];
								track.Add(time+" ChPr ch="+chan+" v="+val);
								p+=1;
							}
							if(last == "Par")
							{
								c = data[p];
								val = data[p+1];
								track.Add(time+" Par ch="+chan+" c="+c+" v="+val);
								p+=2;
							}
							if (last == "Pb")
							{
								val = (byte)( (data[p]  & 0x7F) | (( data[p+1] & 0x7F)<<7) );
								track.Add(time+" Pb ch="+chan+" v="+val);
								p+=2;
							}
							//default:
							// MM: ToDo: Repetition of SysEx and META-events? with <last>?? \n";
							//  _err("unknown repetition: $last");
							break;
					} // switch (tempByte)
						break;
				} // switch ($high)
            } // while ( p < trackLen )
            track.ticks = time;
            if (lastTempoTicks > -1)
            {
                track.duration += (time - lastTempoTicks) * iTempo;
            }
            else
            {
                track.duration = time * this.tempo;
            }

			return track;
		}

		//---------------------------------------------------------------
		// variable length string to int (+repositioning)
		//---------------------------------------------------------------
		//# TO LOOK AFTER CAREFULLY <.<
		private int _readVarLen(ref byte[] data,ref int pos)
		{
			int value;
			int c;
    
			value = data[pos++];
			if ( ( value & 0x80 ) != 0)
			{
				value &= 0x7F;
				do 
				{
					c = data[pos++];
					value = (value << 7) + (c & 0x7F);
				} while ((c & 0x80) != 0);
			}
			return(value);
		}
	} // END OF CLASS
}