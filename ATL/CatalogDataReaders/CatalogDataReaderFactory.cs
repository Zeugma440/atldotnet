using System;
using System.Collections.Generic;

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
                theFactory.formatListByExt = new Dictionary<string, IList<Format>>();

                Format tempFmt = new Format("CUE sheet");
                tempFmt.ID = CR_CUE;
                tempFmt.AddExtensions(".cue");
                theFactory.addFormat(tempFmt);
			}

			return theFactory;
		}

        public ICatalogDataReader GetCatalogDataReader(string path, int alternate = 0)
		{
            IList<Format> formats = getFormatsFromPath(path);
            int formatId = formats.Count > alternate ? formats[alternate].ID : NO_FORMAT;
            return GetCatalogDataReader(formatId, path);
        }

        private ICatalogDataReader GetCatalogDataReader(int formatId, string path = "")
        {
            switch (formatId)
            {
                case CR_CUE: return new BinaryLogic.Cue(path); //new BinaryLogic.CueAdapter();
                default: return new BinaryLogic.DummyReader() { Path = path }; 
            }
		}
	}
}
