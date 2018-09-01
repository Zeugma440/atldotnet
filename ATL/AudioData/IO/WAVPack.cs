using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for WavPack files manipulation (extension : .WV)
    /// </summary>
	class WAVPack : IAudioDataIO
	{
		private int formatTag;
		private int version;
		private int channels;
		private int bits;
	
		private string encoder;
		private long tagSize;

		private long samples;
		private long bSamples;

        private int sampleRate;
        private double bitrate;
        private double duration;
        private int codecFamily;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        /*
        public int FormatTag
		{
			get  { return formatTag; }
		}
		public int Version
		{
			get  { return version; }
		}    
		public int Channels
		{
			get { return channels; }
		}
		public int Bits
		{
			get { return bits; }
		}	
		public long Samples
		{
			get { return samples; }
		}
		public long BSamples
		{
			get { return bSamples; }
		}
		public double CompressionRation
		{
			get { return getCompressionRatio(); }
		}
		public String Encoder
		{
			get { return encoder; }
		}
        public String ChannelMode
        {
            get { return getChannelMode(); }
        }
        */


        private class wavpack_header3
		{
			public char[] ckID = new char[4];
			public uint ckSize;
			public ushort version;
			public ushort bits;
			public ushort flags;
			public ushort shift;
			public uint total_samples;
			public uint crc;
			public uint crc2;
			public char[] extension = new char[4];
			public byte extra_bc;
			public char[] extras = new char[3];
		
			public void Reset()
			{
				Array.Clear(ckID,0,4);
				ckSize = 0;
				version = 0;
				bits = 0;
				flags = 0;
				shift = 0;
				total_samples = 0;
				crc = 0;
				crc2 = 0;
				Array.Clear(extension,0,4);
				extra_bc = 0;
				Array.Clear(extras,0,3);
			}
		}

		private class wavpack_header4
		{
			public char[] ckID = new char[4];
			public uint ckSize;
			public ushort version;
			public byte track_no;
			public byte index_no;
			public uint total_samples;
			public uint block_index;
			public uint block_samples;
			public uint flags;
			public uint crc;
		
			public void Reset()
			{
				Array.Clear(ckID,0,4);
				ckSize = 0;
				version = 0;
				track_no = 0;
				index_no = 0;
				total_samples = 0;
				block_index = 0;
				block_samples = 0;
				flags = 0;
				crc = 0;
			}
		}

		private struct fmt_chunk
		{
			public ushort wformattag;
			public ushort wchannels;
			public uint dwsamplespersec;
			public uint dwavgbytespersec;
			public ushort wblockalign;
			public ushort wbitspersample;
		}

		private class riff_chunk
		{
			public char[] id = new char[4];
			public uint size;
		
			public void Reset()
			{
				Array.Clear(id,0,3);
				size = 0;
			}
		}

		//version 3 flags
		private const 	int 	MONO_FLAG_v3		= 1;		// not stereo
		private const 	int 	FAST_FLAG_v3		= 2;   		// non-adaptive predictor and stereo mode
		//private const 	int 	RAW_FLAG_v3			= 4;   		// raw mode (no .wav header)
		//private const 	int 	CALC_NOISE_v3		= 8;   		// calc noise in lossy mode (no longer stored)
		private const 	int 	HIGH_FLAG_v3		= 0x10;		// high quality mode (all modes)
		//private const 	int 	BYTES_3_v3			= 0x20;		// files have 3-byte samples
		//private const 	int 	OVER_20_v3			= 0x40;		// samples are over 20 bits
		private const 	int 	WVC_FLAG_v3			= 0x80;		// create/use .wvc (no longer stored)
		//private const 	int 	LOSSY_SHAPE_v3		= 0x100;	// noise shape (lossy mode only)
		//private const 	int 	VERY_FAST_FLAG_v3	= 0x200;	// double fast (no longer stored)
		private const 	int 	NEW_HIGH_FLAG_v3	= 0x400;	// new high quality mode (lossless only)
		//private const 	int 	CANCEL_EXTREME_v3	= 0x800;	// cancel EXTREME_DECORR
		//private const 	int 	CROSS_DECORR_v3		= 0x1000;	// decorrelate chans (with EXTREME_DECORR flag)
		//private const 	int 	NEW_DECORR_FLAG_v3	= 0x2000;	// new high-mode decorrelator
		//private const 	int 	JOINT_STEREO_v3		= 0x4000;	// joint stereo (lossy and high lossless)
		private const 	int 	EXTREME_DECORR_v3	= 0x8000;	// extra decorrelation (+ enables other flags)

		private static readonly int[] sample_rates = new int[15] {  6000, 8000, 9600, 11025, 12000, 16000, 22050,
									        						24000, 32000, 44100, 48000, 64000, 88200, 96000, 192000 };

        
        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate
        {
            get { return sampleRate; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public int CodecFamily
        {
            get { return codecFamily; }
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
            return (metaDataType == MetaDataIOFactory.TAG_APE);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
		{
            duration = 0;
            bitrate = 0;
            codecFamily = AudioDataIOFactory.CF_LOSSLESS;

            tagSize = 0;
			formatTag = 0;
			channels = 0;
			sampleRate = 0;
			bits = 0;
			version = 0;
			encoder = "";
			samples = 0;
			bSamples = 0;
        }

		public WAVPack(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


        // ---------- SUPPORT METHODS

            /* unused for now
             * 
        public String getChannelMode()
		{
			switch( channels )
			{
				case 1: return "Mono"; 
				case 2: return "Stereo";
				default : return "Surround";
			}
		}

        private double getCompressionRatio()
        {
            if (isValid)
                return (double)sizeInfo.FileSize / (samples * (channels * bits / 8) + 44) * 100;
            else
                return 0;
        }
        */

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
			char[] marker = new char[4];
  
			bool result = false;

            this.sizeInfo = sizeInfo;
            resetData();

            source.BaseStream.Seek(0, SeekOrigin.Begin);
			marker = source.ReadChars(4);
            source.BaseStream.Seek(0, SeekOrigin.Begin );
				
			if ( StreamUtils.StringEqualsArr("RIFF",marker) )
			{
				result = _ReadV3( source );
			}
			else 
			{
				if ( StreamUtils.StringEqualsArr("wvpk",marker) )
				{
					result = _ReadV4( source );
				}
			}

			return result;
		}

		private bool _ReadV4( BinaryReader r )
		{
			wavpack_header4 wvh4 = new wavpack_header4();
			byte[] EncBuf = new byte[4096];
			int tempo;
			byte encoderbyte;

			bool result = false;
  
  
			wvh4.Reset();
  
			wvh4.ckID = r.ReadChars(4);
			wvh4.ckSize = r.ReadUInt32();
			wvh4.version = r.ReadUInt16();
			wvh4.track_no = r.ReadByte();
			wvh4.index_no = r.ReadByte();  
			wvh4.total_samples = r.ReadUInt32();
			wvh4.block_index = r.ReadUInt32();
			wvh4.block_samples = r.ReadUInt32();
			wvh4.flags = r.ReadUInt32();
			wvh4.crc = r.ReadUInt32();  

			if ( StreamUtils.StringEqualsArr("wvpk",wvh4.ckID) )  // wavpack header found  -- TODO handle exceptions better
			{
				result = true;
				version = (wvh4.version >> 8);
				channels = (int)(2 - (wvh4.flags & 4));  // mono flag

				bits = (int)((wvh4.flags & 3) * 16);   // bytes stored flag
				samples = wvh4.total_samples;
				bSamples = wvh4.block_samples;
				sampleRate = (int)((wvh4.flags & (0x1F << 23)) >> 23);
				if ( (sampleRate > 14) || (sampleRate < 0) )
				{
					sampleRate = 44100;
				}
				else
				{
					sampleRate = sample_rates[ sampleRate ];
				}

				if (8 == (wvh4.flags & 8) )  // hybrid flag
				{
					encoder = "hybrid lossy";
                    codecFamily = AudioDataIOFactory.CF_LOSSY;
				}
				else
				{ //if (2 == (wvh4.flags & 2) )  {  // lossless flag
					encoder = "lossless";
                    codecFamily = AudioDataIOFactory.CF_LOSSLESS;
                }

				/*
					if ((wvh4.flags & 0x20) > 0)  // MODE_HIGH
					{
					  FEncoder = FEncoder + " (high)";
					end
					else if ((wvh4.flags & 0x40) > 0)  // MODE_FAST
					{
					  FEncoder = FEncoder + " (fast)";
					}
				*/

				duration = (double)wvh4.total_samples * 1000.0 / sampleRate;
				if ( duration > 0 ) bitrate = (sizeInfo.FileSize - tagSize) * 8 / (double)(samples * 1000.0 / (double)sampleRate);
    
				Array.Clear(EncBuf,0,4096);
				EncBuf = r.ReadBytes(4096);    
    
				for (tempo=0; tempo<4096; tempo++)
				{
					if (0x65 == EncBuf[tempo] )
					{
						if (0x02 == EncBuf[tempo + 1] )
						{
							encoderbyte = EncBuf[tempo + 2];
							switch (encoderbyte)
							{
								case 8: encoder = encoder + " (high)"; break;
								case 0: encoder = encoder + " (normal)"; break;
								case 2: encoder = encoder + " (fast)"; break;
								case 6: encoder = encoder + " (very fast)"; break;
							}
							break;
						}
					}	
				}

			}
			return result;
		}

		private bool _ReadV3( BinaryReader r )
		{
			riff_chunk chunk = new riff_chunk();
			char[] wavchunk = new char[4];
			fmt_chunk fmt;
			bool hasfmt;
			long fpos;
			wavpack_header3 wvh3 = new wavpack_header3();
			bool result = false;
  
  
			hasfmt = false;

			// read and evaluate header
			chunk.Reset();
  
			chunk.id = r.ReadChars(4);
			chunk.size = r.ReadUInt32();
			wavchunk = r.ReadChars(4);
  
			if (!StreamUtils.StringEqualsArr("WAVE",wavchunk) ) return result;

			// start looking for chunks
			chunk.Reset();
  
			while (r.BaseStream.Position < r.BaseStream.Length) 
			{
				chunk.id = r.ReadChars(4);
				chunk.size = r.ReadUInt32();
  	
				if (chunk.size <= 0) break;
     	
				fpos = r.BaseStream.Position;

				if ( StreamUtils.StringEqualsArr("fmt ",chunk.id) )  // Format chunk found read it
				{
					if ( chunk.size >= 16/*sizeof(fmt_chunk)*/ )
					{
						fmt.wformattag = r.ReadUInt16();
						fmt.wchannels = r.ReadUInt16();
						fmt.dwsamplespersec = r.ReadUInt32();
						fmt.dwavgbytespersec = r.ReadUInt32();
						fmt.wblockalign = r.ReadUInt16();
						fmt.wbitspersample = r.ReadUInt16();

						hasfmt = true;
						result = true;
						formatTag = fmt.wformattag;
						channels = fmt.wchannels;
						sampleRate = (int)fmt.dwsamplespersec;
						bits = fmt.wbitspersample;
                        bitrate = (double)fmt.dwavgbytespersec * 8;
					} 
					else 
					{
						break;
					}
				}
				else         
				{
					if ( ( StreamUtils.StringEqualsArr("data",chunk.id)) && hasfmt )
					{
						wvh3.Reset();
        		
						wvh3.ckID = r.ReadChars(4);
						wvh3.ckSize = r.ReadUInt32();
						wvh3.version = r.ReadUInt16();
						wvh3.bits = r.ReadUInt16();
						wvh3.flags = r.ReadUInt16();
						wvh3.shift = r.ReadUInt16();
						wvh3.total_samples = r.ReadUInt32();
						wvh3.crc = r.ReadUInt32();
						wvh3.crc2 = r.ReadUInt32();
						wvh3.extension = r.ReadChars(4);
						wvh3.extra_bc = r.ReadByte();
						wvh3.extras = r.ReadChars(3);
        								
						if ( StreamUtils.StringEqualsArr("wvpk",wvh3.ckID) )  // wavpack header found
						{
							result = true;
							version = wvh3.version;
							channels = 2 - (wvh3.flags & 1);  // mono flag
							samples = wvh3.total_samples;

                            codecFamily = AudioDataIOFactory.CF_LOSSLESS;

                            // Encoder guess
                            if (wvh3.bits > 0)
							{
								if ( (wvh3.flags & NEW_HIGH_FLAG_v3) > 0 )
								{
									encoder = "hybrid";
									if ( (wvh3.flags & WVC_FLAG_v3) > 0 )
									{
										encoder += " lossless";
                                    } 
									else 
									{
										encoder += " lossy";
                                        codecFamily = AudioDataIOFactory.CF_LOSSY;
                                    }
                 			
									if ((wvh3.flags & EXTREME_DECORR_v3) > 0) 
										encoder = encoder + " (high)";
								}
								else
								{
									if ( (wvh3.flags & (HIGH_FLAG_v3 | FAST_FLAG_v3)) == 0 )
									{
										encoder = ( wvh3.bits + 3 ).ToString() + "-bit lossy";
                                        codecFamily = AudioDataIOFactory.CF_LOSSY;
                                    } 
									else 
									{
										encoder = ( wvh3.bits + 3 ).ToString() + "-bit lossy";
                                        codecFamily = AudioDataIOFactory.CF_LOSSY;

                                        if ( (wvh3.flags & HIGH_FLAG_v3) > 0 )
										{
											encoder += " high";
										} 
										else 
										{
											encoder += " fast";
										}
									}
								}
							} 
							else 
							{
                                if ( (wvh3.flags & HIGH_FLAG_v3) == 0 )  
								{
									encoder = "lossless (fast mode)";
								} 
								else 
								{
									if ( (wvh3.flags & EXTREME_DECORR_v3) > 0 )  
									{
										encoder = "lossless (high mode)";
									} 
									else 
									{
										encoder = "lossless";
									}
              				
								}
							}
				
							if (sampleRate <= 0)  sampleRate = 44100;
							duration = (double)wvh3.total_samples * 1000.0 / sampleRate;
							if (duration > 0)  bitrate = 8.0 * (sizeInfo.FileSize - tagSize - (double)wvh3.ckSize) / duration;
						}
						break;
					} 
					else // not a wv file
					{ 
						break;
					}
				}
			} // while

			return result;
		}
	}
}