using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Audio Interchange File Format files manipulation (extension : .AIF, .AIFF, .AIFC)
    /// </summary>
	class TAIFF : AudioDataReader, IMetaDataReader
	{
        public const string AIFF_CONTAINER_ID = "FORM";

        private const string FORMTYPE_AIFF = "AIFF";
        private const string FORMTYPE_AIFC = "AIFC";

        private const string CHUNKTYPE_COMMON       = "COMM";
        private const string CHUNKTYPE_MARKER       = "MARK";
        private const string CHUNKTYPE_INSTRUMENT   = "INST";
        private const string CHUNKTYPE_SOUND        = "SSND";
        private const string CHUNKTYPE_COMMENTS     = "COMT";
        private const string CHUNKTYPE_NAME         = "NAME";
        private const string CHUNKTYPE_AUTHOR       = "AUTH";
        private const string CHUNKTYPE_COPYRIGHT    = "(c) ";
        private const string CHUNKTYPE_ANNOTATION   = "ANNO";

        private struct ChunkHeader
        {
            public String ID;
            public int Size;
        }

        // Private declarations 
        private uint FChannels;
		private uint FBits;
		private uint FSampleRate;
        private uint FSampleSize;
        private uint FNumSampleFrames;

        private bool FExists;
        private byte FVersionID;
        private long FSize;
        private String FTitle;
        private String FArtist;
        private String FComposer;
        private String FAlbum;
        private ushort FTrack;
        private ushort FDisc;
        private ushort FRating;
        private String FTrackString;
        private String FDiscString;
        private String FYear;
        private String FGenre;
        private String FComment;

        private IList<MetaReaderFactory.PIC_CODE> FPictures;

        public bool Exists // True if tag found
        {
            get { return this.FExists; }
        }
        public byte VersionID // Version code
        {
            get { return this.FVersionID; }
        }
        public long Size // Total tag size
        {
            get { return this.FSize; }
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
        public String Album // Album title
        {
            get { return this.FAlbum; }
        }
        public ushort Track // Track number 
        {
            get { return this.FTrack; }
        }
        public String TrackString // Track number (string)
        {
            get { return this.FTrackString; }
        }
        public ushort Disc // Disc number 
        {
            get { return this.FDisc; }
        }
        public String DiscString // Disc number (string)
        {
            get { return this.FDiscString; }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
        }
        public String Year // Release year
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
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Tags indicating the presence of embedded pictures
        {
            get { return this.FPictures; }
        }


        // Public declarations 
        public uint Channels
		{
			get { return FChannels; }
		}
		public uint Bits
		{
			get { return FBits; }
		}
		public uint SampleRate
		{
			get { return FSampleRate; }
		}
        public double CompressionRatio
        {
            get { return FGetCompressionRatio(); }
        }

    
        public override bool IsVBR
		{
			get { return false; }
		}
        public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSLESS; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		// ********************** Private functions & voids ********************* 

        protected override void resetSpecificData()
		{
			FChannels = 0;
			FBits = 0;
			FSampleRate = 0;

            FExists = false;
            FVersionID = 0;
            FSize = 0;
            FTitle = "";
            FArtist = "";
            FComposer = "";
            FAlbum = "";
            FTrack = 0;
            FDisc = 0;
            FRating = 0;
            FTrackString = "";
            FDiscString = "";
            FYear = "";
            FGenre = "";
            FComment = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
        }


        // ********************** Public functions & voids ********************** 

        public TAIFF()
		{
			resetData();
		}

		/* -------------------------------------------------------------------------- */

		// No explicit destructors with C#

		/* -------------------------------------------------------------------------- */

        private double FGetCompressionRatio()
        {
            // Get compression ratio 
            if (FValid)
                return (double)FFileSize / ((FDuration * FSampleRate) * (FChannels * FBits / 8) + 44) * 100;
            else
                return 0;
        }

        /// <summary>
        /// Reads ID and size of a local chunk and returns them in a dedicated structure _without_ reading nor skipping the data
        /// </summary>
        /// <param name="source">Source where to read header information</param>
        /// <returns>Local chunk header information</returns>
        private ChunkHeader readLocalChunkHeader(ref BinaryReader source)
        {
            ChunkHeader header;

            // Chunk ID
            char[] id = StreamUtils.ReadOneByteChars(source, 4);
            header.ID = new string(id);

            // Chunk size
            header.Size = source.ReadInt32();
            // Convert to big endian
            header.Size = StreamUtils.ReverseInt32(header.Size);

            return header;
        }

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			bool result = false;
            long position;
            FComment = "";

            if (StreamUtils.StringEqualsArr(AIFF_CONTAINER_ID,StreamUtils.ReadOneByteChars(source, 4)))
            {
		        // Container chunk size
				source.BaseStream.Seek(4, SeekOrigin.Current);

                // Form type
                char[] id = StreamUtils.ReadOneByteChars(source, 4);

                if (StreamUtils.StringEqualsArr(FORMTYPE_AIFF, id) || StreamUtils.StringEqualsArr(FORMTYPE_AIFF, id))
                {
                    FValid = true;
                    FExists = true;

                    position = source.BaseStream.Position;

                    while (position<source.BaseStream.Length)
                    {
                        ChunkHeader header = readLocalChunkHeader(ref source);

                        position = source.BaseStream.Position;

                        if (header.ID.Equals(CHUNKTYPE_COMMON))
                        {
                            FChannels = (uint)StreamUtils.ReverseInt16(source.ReadInt16());
                            FNumSampleFrames = StreamUtils.ReverseUInt32(source.ReadUInt32());
                            FSampleSize = (uint)StreamUtils.ReverseInt16(source.ReadInt16());
                            FSampleRate = (uint)Math.Round(Nato.LongDouble.BitConverter.ToDouble(source.ReadBytes(80), 0));
                        }
                        else if (header.ID.Equals(CHUNKTYPE_NAME))
                        {
                            FTitle = new string(StreamUtils.ReadOneByteChars(source,header.Size));
                        } else if (header.ID.Equals(CHUNKTYPE_AUTHOR))
                        {
                            FArtist = new string(StreamUtils.ReadOneByteChars(source, header.Size));
                        } else if (header.ID.Equals(CHUNKTYPE_ANNOTATION))
                        {
                            if (FComment.Length > 0) FComment += "/";
                            FComment += new string(StreamUtils.ReadOneByteChars(source, header.Size));
                        } else if (header.ID.Equals(CHUNKTYPE_COMMENTS))
                        {
                            ushort numComs = source.ReadUInt16();
                            numComs = StreamUtils.ReverseUInt16(numComs);

                            for (int i = 0; i < numComs; i++)
                            {
                                // Timestamp
                                source.BaseStream.Seek(4, SeekOrigin.Current);
                                // Marker ID (int16)
                                source.BaseStream.Seek(2, SeekOrigin.Current);
                                // Comments length
                                ushort comLength = source.ReadUInt16();
                                comLength = StreamUtils.ReverseUInt16(comLength);

                                if (FComment.Length > 0) FComment += "/";
                                FComment += new string(StreamUtils.ReadOneByteChars(source, comLength));
                            }
                        }

                        source.BaseStream.Seek(position + header.Size, SeekOrigin.Begin);
                    }
                    
                    // TODO
                    FBits = 16;
                    FDuration = (double)FFileSize * 8 / FBitrate;

                    result = true;
                }
			}
  
			return result;
		}
	}
}