using System.Drawing.Imaging;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// Factory for metadata (tag) readers
	/// </summary>
	public class MetaDataIOFactory
	{
        // Defines the types of supported "cross-format" metadata
        public const int TAG_ID3V1 = 0;
		public const int TAG_ID3V2 = 1;
		public const int TAG_APE = 2;
        public const int TAG_NATIVE = 3;    // Native tag format associated with the audio container (ex : MP4 built-in tagging format)
        public const int TAG_UNKNOWN = 99;  // Whenever tag types have to be used out of context

        // Count of the types defined above, excluding unknown type
        public const int TAG_TYPE_COUNT = 4;

		// Defines the default reading priority of the metadata
		private int[] tagPriority = new int[TAG_TYPE_COUNT] { TAG_NATIVE, TAG_ID3V2, TAG_APE, TAG_ID3V1 };

		// Defines whether the next created metadatareaders should use cross-tag reading
		private bool m_enableCrossReading = false;

		// The instance of this factory
		private static MetaDataIOFactory theFactory = null;


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
		public static MetaDataIOFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new MetaDataIOFactory();
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
        /// <param name="theDataIO">AudioDataReader produced for this file</param>
        /// <param name="forceTagType">Forces a certain tag type to be read regardless of the current "cross reading" settings</param>
        /// <returns>Metadata reader able to give metadata info for this file (or the dummy reader if the format is unknown)</returns>
        public IMetaDataIO GetMetaReader(ref AudioDataIO theDataIO, int forceTagType = -1)
		{
            IMetaDataIO theMetaReader = null;
            
            int tagCount = 0;
            if (theDataIO.HasNativeMeta()) tagCount++;
            if (theDataIO.ID3v1.Exists) tagCount++;
            if (theDataIO.ID3v2.Exists) tagCount++;
            if (theDataIO.APEtag.Exists) tagCount++;
			
			// Step 1 : The physical reader may have already parsed the metadata if it belongs
			// to cross-format tagging systems
			if (m_enableCrossReading && (tagCount > 1) && (-1 == forceTagType) )
			{
				theMetaReader = new CrossMetadataReader(ref theDataIO, tagPriority);
			}
            else
			{
				for (int i=0; i<TAG_TYPE_COUNT; i++)
				{
                    if ( ((TAG_NATIVE == tagPriority[i] && -1 == forceTagType) || (TAG_NATIVE == forceTagType) ) && (theDataIO.HasNativeMeta()))
                    {
                        theMetaReader = theDataIO.NativeTag; break;
                    }
                    if (((TAG_ID3V1 == tagPriority[i] && -1 == forceTagType) || (TAG_ID3V1 == forceTagType)) && (theDataIO.ID3v1.Exists) )
					{
                        theMetaReader = theDataIO.ID3v1; break;
					}
					if (((TAG_ID3V2 == tagPriority[i] && -1 == forceTagType) || (TAG_ID3V2 == forceTagType)) && (theDataIO.ID3v2.Exists) )
					{
                        theMetaReader = theDataIO.ID3v2; break;
					}
					if (((TAG_APE == tagPriority[i] && -1 == forceTagType) || (TAG_APE == forceTagType)) && (theDataIO.APEtag.Exists) )
					{
                        theMetaReader = theDataIO.APEtag; break;
					}
				}
			}

			// Step 2 : Nothing found in step 1 -> consider specific tagging (data+meta file formats)
            // TODO : what if cross-tagging is enabled _and_ additional info exists in specific tagging ?
            // => Need to consider specific tagging information within the CrossMetadataReader code, above
			if (null == theMetaReader)
			{
				if (theDataIO is IMetaDataIO)
				{
					theMetaReader = (IMetaDataIO)theDataIO;
				}
			}

			// Step 3 : default (no tagging at all - provides the dummy reader)
            if (null == theMetaReader) theMetaReader = new IO.DummyTag();

			return theMetaReader;
		}
	}
}
