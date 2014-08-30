using ATL.Logging;
using System;

namespace ATL.AudioReaders
{
	/// <summary>
	/// Factory for metadata (tag) readers
	/// </summary>
	public class MetaReaderFactory
	{
        public enum PIC_CODE { Generic, Front, Back, CD };

		// Defines the three types of supported "cross-format" metadata
		public const int TAG_ID3V1 = 0;
		public const int TAG_ID3V2 = 1;
		public const int TAG_APE = 2;

		// Count of the types defined above
		public const int TAG_TYPE_COUNT = 3;

		// Defines the default reading priority of the metadata
		private int[] tagPriority = new int[TAG_TYPE_COUNT] { TAG_ID3V2, TAG_APE, TAG_ID3V1 };

		// Defines whether the next created metadatareaders should use cross-tag reading
		private bool m_enableCrossReading = false;

		// The instance of this factory
		private static MetaReaderFactory theFactory = null;


		/// <summary>
		/// Sets whether the next created metadatareaders should use cross-tag reading
        ///   - false (default) :  the most important tagging standard (according to priorities)
        ///                        detected on the track is exclusively used to populate fields
        ///   - true            :  for each field, the most important tagging standard (according to
        ///                        priorities) is first read. If the value is empty, the next
        ///                        tagging standard (according to priorities) is read, and so on...
        /// </summary>
		public bool CrossReading
		{
			get { return m_enableCrossReading; }
			set { m_enableCrossReading = value; }
		}

		// ------------------------------------------------------------------------------------------

		/// <summary>
		/// Gets the instance of this factory (Singleton pattern) 
		/// </summary>
		/// <returns>Instance of the MetaReaderFactory of the application</returns>
		public static MetaReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new MetaReaderFactory();
			}

			return theFactory;
		}


		/// <summary>
		/// Modifies the default reading priority of the metadata
		/// </summary>
		/// <param name="tag">Identifier of the metadata type</param>
		/// <param name="rank">Reading priority (0..TAG_TYPE_COUNT-1)</param>
		public void SetTagPriority(int tag, int rank)
		{
			if ((rank > -1) && (rank < tagPriority.Length))
				tagPriority[rank] = tag;
		}

		/// <summary>
		/// Gets the appropriate metadata reader for a given file / physical data reader
		/// </summary>
		/// <param name="theDataReader">AudioDataReader produced for this file</param>
		/// <returns>Metadata reader able to give metadata info for this file (or the dummy reader if the format is unknown)</returns>
		public IMetaDataReader GetMetaReader(ref IAudioDataReader theDataReader)
		{
			IMetaDataReader theMetaReader = null;
            
            int tagCount = 0;
            if (theDataReader.ID3v1.Exists) tagCount++;
            if (theDataReader.ID3v2.Exists) tagCount++;
            if (theDataReader.APEtag.Exists) tagCount++;
			
			// Step 1 : The physical reader may have already parsed the metadata if it belongs
			// to cross-format tagging systems
			if ( m_enableCrossReading && (tagCount > 1) )
			{
				theMetaReader = new CrossMetadataReader(ref theDataReader, tagPriority);
			}
            else
			{
				for (int i=0; i<TAG_TYPE_COUNT; i++)
				{
					if ( (TAG_ID3V1 == tagPriority[i]) && (theDataReader.ID3v1.Exists) )
					{
                        theMetaReader = theDataReader.ID3v1; break;
					}
					if ( (TAG_ID3V2 == tagPriority[i]) && (theDataReader.ID3v2.Exists) )
					{
                        theMetaReader = theDataReader.ID3v2; break;
					}
					if ( (TAG_APE == tagPriority[i]) && (theDataReader.APEtag.Exists) )
					{
                        theMetaReader = theDataReader.APEtag; break;
					}
				}
			}

			// Step 2 : Nothing found in step 1 -> consider specific tagging (data+meta file formats)
            // TODO : what is cross-tagging is enabled _and_ additional info exists in specific tagging ?
            // => Need to consider specific tagging information withing CrossMetadataReader
			if (null == theMetaReader)
			{
				if ((theDataReader is BinaryLogic.TOggVorbis) ||
					(theDataReader is BinaryLogic.TWMAfile) ||
                    (theDataReader is BinaryLogic.TAACfile) ||
					(theDataReader is BinaryLogic.TFLACFile) ||
					(theDataReader is BinaryLogic.TPSFFile) ||
					(theDataReader is BinaryLogic.TSPCFile) )
				{
					theMetaReader = (IMetaDataReader)theDataReader; // Boorish but correct cast
				}
			}

			// Step 3 : default (no tagging at all - provides the dummy reader)
            if (null == theMetaReader) theMetaReader = new BinaryLogic.DummyTag();

			return theMetaReader;
		}
	}
}
