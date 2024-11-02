using System.Collections.Generic;

namespace ATL.CatalogDataReaders
{
    /// <summary>
    /// Factory for Catalog data readers
    /// </summary>
    public class CatalogDataReaderFactory : Factory<Format>
    {
        // Defines the supported formats
        /// <summary>
        ///  Cuesheet
        /// </summary>
        public const int CR_CUE = 1;

        // The instance of this factory
        private static CatalogDataReaderFactory theFactory;

        private static readonly object _lockable = new object();

        /// <summary>
        /// Get the instance of the Factory singleton
        /// </summary>
        /// <returns>Instance of the Factory singleton</returns>
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

        /// <summary>
        /// Get the GetCatalogDataReader for the file at the given path
        /// </summary>
        /// <param name="path">Path of the file to read</param>
        /// <returns></returns>
        public ICatalogDataReader GetCatalogDataReader(string path)
        {
            IList<Format> formats = getFormatsFromPath(path);
            ICatalogDataReader result;

            if (formats != null && formats.Count > 0)
            {
                result = GetCatalogDataReader(formats[0].ID, path);
            }
            else
            {
                result = GetCatalogDataReader(Format.UNKNOWN_FORMAT.ID);
            }

            result.Path = path;
            return result;
        }

        private static ICatalogDataReader GetCatalogDataReader(int formatId, string path = "")
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
