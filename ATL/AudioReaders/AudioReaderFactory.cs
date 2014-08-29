using System;
using System.IO;
using System.Collections;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.AudioReaders
{
	/// <summary>
	/// Factory for audio data readers
	/// </summary>
	public class AudioReaderFactory : ReaderFactory
	{
		// Codec families
		public const int CF_LOSSY		= 0; // Streamed, lossy data
		public const int CF_LOSSLESS	= 1; // Streamed, lossless data
		public const int CF_SEQ_WAV		= 2; // Sequenced with embedded sound library
		public const int CF_SEQ			= 3; // Sequenced with codec-dependent sound library

		public const int NB_CODEC_FAMILIES = 4;

		// The instance of this factory
		private static AudioReaderFactory theFactory = null;
	
		// Codec IDs
		public const int CID_MP3		= 0;
		public const int CID_OGG		= 1;
		public const int CID_MPC		= 2;
		public const int CID_FLAC		= 3;
		public const int CID_APE		= 4;
		public const int CID_WMA		= 5;
		public const int CID_MIDI		= 6;
		public const int CID_AAC		= 7;
		public const int CID_AC3		= 8;
		public const int CID_OFR		= 9;
		public const int CID_WAVPACK	= 10;
		public const int CID_WAV		= 11;
		public const int CID_PSF		= 12;
		public const int CID_SPC		= 13;
		public const int CID_DTS		= 14;
		public const int CID_VQF		= 15;
        public const int CID_TTA        = 16;

		public const int NB_CODECS = 17;

		// ------------------------------------------------------------------------------------------
		
		/// <summary>
		/// Gets the instance of this factory (Singleton pattern) 
		/// </summary>
		/// <returns>Instance of the AudioReaderFactory of the application</returns>
		public static AudioReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new AudioReaderFactory();

                theFactory.formatList = new Dictionary<string, Format>();

                Format tempFmt = new Format("MPEG Audio Layer");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_MP3;
                tempFmt.AddExtension(".mp1");
                tempFmt.AddExtension(".mp2");
                tempFmt.AddExtension(".mp3");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("OGG Vorbis");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_OGG;
                tempFmt.AddExtension(".ogg");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Musepack / MPEGplus");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_MPC;
                tempFmt.AddExtension(".mp+");
                tempFmt.AddExtension(".mpc");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Windows Media Audio");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_WMA;
                tempFmt.AddExtension(".wma");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Advanced Audio Coding");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_AAC;
                tempFmt.AddExtension(".aac");
                tempFmt.AddExtension(".mp4");
                tempFmt.AddExtension(".m4a");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Dolby Digital");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_AC3;
                tempFmt.AddExtension(".ac3");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Digital Theatre System");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_DTS;
                tempFmt.AddExtension(".dts");
                tempFmt.Readable = false;
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("TwinVQ");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_VQF;
                tempFmt.AddExtension(".vqf");
                tempFmt.Readable = false;
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Free Lossless Audio Codec");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_FLAC;
                tempFmt.AddExtension(".flac");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Monkey's Audio");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_APE;
                tempFmt.AddExtension(".ape");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("OptimFROG");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_OFR;
                tempFmt.AddExtension(".ofr");
                tempFmt.AddExtension(".ofs");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("WAVPack");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_WAVPACK;
                tempFmt.AddExtension(".wv");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("PCM (uncompressed audio)");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_WAV;
                tempFmt.AddExtension(".wav");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Musical Instruments Digital Interface");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_MIDI;
                tempFmt.AddExtension(".mid");
                tempFmt.AddExtension(".midi");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Portable Sound Format");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_PSF;
                tempFmt.AddExtension(".psf");
                tempFmt.AddExtension(".psf1");
                tempFmt.AddExtension(".minipsf");
                tempFmt.AddExtension(".minipsf1");
                tempFmt.AddExtension(".psf2");
                tempFmt.AddExtension(".minipsf2");
                tempFmt.AddExtension(".ssf");
                tempFmt.AddExtension(".minissf");
                tempFmt.AddExtension(".dsf");
                tempFmt.AddExtension(".minidsf");
                tempFmt.AddExtension(".gsf");
                tempFmt.AddExtension(".minigsf");
                tempFmt.AddExtension(".qsf");
                tempFmt.AddExtension(".miniqsf");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("SPC700 Sound Files");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_SPC;
                tempFmt.AddExtension(".spc");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("True Audio");
                tempFmt.ID = ATL.AudioReaders.AudioReaderFactory.CID_TTA;
                tempFmt.AddExtension(".tta");
                theFactory.addFormat(tempFmt);
			}

			return theFactory;
		}

		public IAudioDataReader GetDataReader(String path)
		{
			return GetDataReader(getFormatIDFromPath(path));
		}

		/// <summary>
		/// Gets the appropriate physical data reader for a given file
		/// </summary>
		/// <param name="formatId">ID of the format</param>
		/// <param name="path">Path of the file</param>
		/// <returns>AudioDataReader able to give info about the file's contents (or the dummy reader if the format is unknown)</returns>
		public IAudioDataReader GetDataReader(int formatId)
		{
			IAudioDataReader theDataReader = null;
			
			switch ( formatId )
			{
				case CID_MP3 :		
					theDataReader = new BinaryLogic.TMPEGaudio();
					break;

				case CID_OGG :		
					theDataReader = new BinaryLogic.TOggVorbis();
					break;

				case CID_MPC :		
					theDataReader = new BinaryLogic.TMPEGplus();
					break;

				case CID_FLAC :		
					theDataReader = new BinaryLogic.TFLACFile();
					break;

				case CID_APE :		
					theDataReader = new BinaryLogic.TMonkey();
					break;

				case CID_WMA :		
					theDataReader = new BinaryLogic.TWMAfile();
					break;

				case CID_MIDI :		
					theDataReader = new BinaryLogic.Midi();
					break;

				case CID_AAC :		
					theDataReader = new BinaryLogic.TAACfile();
					break;

				case CID_AC3 :		
					theDataReader = new BinaryLogic.TAC3();
					break;

				case CID_OFR :		
					theDataReader = new BinaryLogic.TOptimFrog();
					break;

				case CID_WAVPACK :		
					theDataReader = new BinaryLogic.TWAVPackfile();
					break;

				case CID_WAV :		
					theDataReader = new BinaryLogic.TWAVfile();
					break;

				case CID_PSF :		
					theDataReader = new BinaryLogic.TPSFFile();
					break;

				case CID_SPC :		
					theDataReader = new BinaryLogic.TSPCFile();
					break;

				default:
					theDataReader = new BinaryLogic.DummyReader();
					break;
			}

			return theDataReader;
		}

	}
}
