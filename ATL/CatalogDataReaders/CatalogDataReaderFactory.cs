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
                theFactory.formatListByExt = new Dictionary<string, IList<Format>>();

                Format tempFmt = new Format("CUE sheet");
                tempFmt.ID = CR_CUE;
                tempFmt.AddExtension(".cue");
                theFactory.addFormat(tempFmt);
			}

			return theFactory;
		}

        public ICatalogDataReader GetCatalogDataReader(String path, int alternate = 0)
		{
            IList<Format> formats = getFormatsFromPath(path);
            ICatalogDataReader result;

            if (formats != null && formats.Count > alternate)
            {
                result = GetCatalogDataReader(formats[alternate].ID, path);
            }
            else
            {
                result = GetCatalogDataReader(NO_FORMAT);
            }

            result.Path = path;
            return result;
        }

        private ICatalogDataReader GetCatalogDataReader(int formatId, string path = "")
        {
            ICatalogDataReader theReader = null;

            if (CR_CUE == formatId)
            {
                theReader = new BinaryLogic.Cue(path); //new BinaryLogic.CueAdapter();
			}

            if (null == theReader) theReader = new BinaryLogic.DummyReader();

			return theReader;
		}
	}
}
