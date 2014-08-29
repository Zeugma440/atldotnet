using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.CatalogDataReaders
{
	/// <summary>
	/// Factory for Catalog data readers
	/// </summary>
    public class CatalogDataReaderFactory : ReaderFactory
	{
		// Defines the supported formats
        public const int CR_CUE     = 0;

		// The instance of this factory
		private static CatalogDataReaderFactory theFactory = null;


		public static CatalogDataReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new CatalogDataReaderFactory();
                theFactory.formatList = new Dictionary<String,ATL.Format>();

                Format tempFmt = new Format("CUE sheet");
                tempFmt.ID = CR_CUE;
                tempFmt.AddExtension(".cue");
                theFactory.addFormat(tempFmt);
			}

			return theFactory;
		}
        
		public ICatalogDataReader GetCatalogDataReader(String path)
		{
			ICatalogDataReader theReader = GetCatalogDataReader(getFormatIDFromPath(path));
            theReader.Path = path;
            return theReader;
        }

        private ICatalogDataReader GetCatalogDataReader(int formatId)
        {
            ICatalogDataReader theReader = null;

            if (CR_CUE == formatId)
            {
				theReader = new BinaryLogic.CueAdapter();
			}

			return theReader;
		}
	}
}
