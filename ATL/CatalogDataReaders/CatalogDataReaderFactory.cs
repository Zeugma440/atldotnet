using System;
using System.Collections.Generic;

namespace ATL.CatalogDataReaders
{
    /// <summary>
    /// Factory for Catalog data readers
    /// </summary>
    public class CatalogDataReaderFactory : Factory
    {
        // Defines the supported formats
        public const int CR_CUE = 0;

        // The instance of this factory
        private static CatalogDataReaderFactory theFactory = null;

        private static readonly object _lockable = new object();


        public static CatalogDataReaderFactory GetInstance()
        {
            lock (_lockable)
            {
                if (null == theFactory)
                {
                    theFactory = new CatalogDataReaderFactory();
                    theFactory.formatListByExt = new Dictionary<string, IList<Format>>();

                    Format tempFmt = new Format(CR_CUE, "CUE sheet");
                    tempFmt.AddExtension(".cue");
                    theFactory.addFormat(tempFmt);
                }
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
                result = GetCatalogDataReader(UNKNOWN_FORMAT.ID);
            }

            result.Path = path;
            return result;
        }

        private ICatalogDataReader GetCatalogDataReader(int formatId, string path = "")
        {
            ICatalogDataReader theReader = null;

            if (CR_CUE == formatId)
            {
                theReader = new BinaryLogic.Cue(path);
            }

            if (null == theReader) theReader = new BinaryLogic.DummyReader();

            return theReader;
        }
    }
}
