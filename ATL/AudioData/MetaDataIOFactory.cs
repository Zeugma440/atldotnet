using System;

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
        public const int TAG_ANY = 99;      // Whenever tag type is not known in advance and may apply to any available tag

        // Count of the types defined above, excluding "any" type
        public static int TAG_TYPE_COUNT = 4;

        // Defines the default reading priority of the metadata
        private int[] tagPriority;

		// Defines whether the next created metadatareaders should use cross-tag reading
		private bool m_enableCrossReading = true;

		// The instance of this factory
		private static MetaDataIOFactory theFactory = null;


		/// <summary>
		/// Sets whether the next created metadatareaders should use cross-tag reading
        ///   - false           :  the most important tagging standard (according to priorities)
        ///                        detected on the track is exclusively used to populate fields
        ///   - true (default)  :  for each field, the most important tagging standard (according to
        ///                        priorities) is first read. If the value is empty, the next
        ///                        tagging standard (according to priorities) is read, and so on...
        /// </summary>
		public bool CrossReading
		{
			get { return m_enableCrossReading; }
			set { m_enableCrossReading = value; }
		}

        public int[] TagPriority
        {
            get { return tagPriority; }
            set { tagPriority = value; }
        }

		// ------------------------------------------------------------------------------------------

		/// <summary>
		/// Gets the instance of this factory (Singleton pattern) 
		/// </summary>
		/// <returns>Instance of the MetaReaderFactory of the application</returns>
		public static MetaDataIOFactory GetInstance()
		{
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Big-endian based platforms are not supported by ATL");

            if (null == theFactory)
			{
				theFactory = new MetaDataIOFactory();
                theFactory.tagPriority = new int[TAG_TYPE_COUNT];
                theFactory.tagPriority[0] = TAG_ID3V2;
                theFactory.tagPriority[1] = TAG_APE;
                theFactory.tagPriority[2] = TAG_NATIVE;
                theFactory.tagPriority[3] = TAG_ID3V1;
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
        /// <param name="theDataManager">AudioDataReader produced for this file</param>
        /// <param name="forceTagType">Forces a certain tag type to be read regardless of the current "cross reading" settings</param>
        /// <returns>Metadata reader able to give metadata info for this file (or the dummy reader if the format is unknown)</returns>
        public IMetaDataIO GetMetaReader(AudioDataManager theDataManager, int forceTagType = -1)
		{
            IMetaDataIO theMetaReader = null;
            
            int tagCount = 0;
            if (theDataManager.HasNativeMeta()) tagCount++;
            if (theDataManager.ID3v1.Exists) tagCount++;
            if (theDataManager.ID3v2.Exists) tagCount++;
            if (theDataManager.APEtag.Exists) tagCount++;
			
			if (m_enableCrossReading && (tagCount > 1) && (-1 == forceTagType) )
			{
				theMetaReader = new CrossMetadataReader(theDataManager, tagPriority);
			}
            else
			{
				for (int i=0; i<TAG_TYPE_COUNT; i++)
				{
                    if ( ((TAG_NATIVE == tagPriority[i] && -1 == forceTagType) || (TAG_NATIVE == forceTagType) ) && (theDataManager.HasNativeMeta()))
                    {
                        theMetaReader = theDataManager.NativeTag; break;
                    }
                    if (((TAG_ID3V1 == tagPriority[i] && -1 == forceTagType) || (TAG_ID3V1 == forceTagType)) && (theDataManager.ID3v1.Exists) )
					{
                        theMetaReader = theDataManager.ID3v1; break;
					}
					if (((TAG_ID3V2 == tagPriority[i] && -1 == forceTagType) || (TAG_ID3V2 == forceTagType)) && (theDataManager.ID3v2.Exists) )
					{
                        theMetaReader = theDataManager.ID3v2; break;
					}
					if (((TAG_APE == tagPriority[i] && -1 == forceTagType) || (TAG_APE == forceTagType)) && (theDataManager.APEtag.Exists) )
					{
                        theMetaReader = theDataManager.APEtag; break;
					}
				}
			}

            if (null == theMetaReader) theMetaReader = new IO.DummyTag();

			return theMetaReader;
		}
	}
}
