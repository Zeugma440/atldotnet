using System;
using System.IO;

namespace ATL.CatalogDataReaders
{
	/// <summary>
	/// Description r¨¦sum¨¦e de CatalogDataReaderFactory.
	/// </summary>
	public class CatalogDataReaderFactory
	{
		// Defines the supported formats
		public const int CR_CUE = 0;

		// The instance of this factory
		private static CatalogDataReaderFactory theFactory = null;


		public static CatalogDataReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new CatalogDataReaderFactory();
			}

			return theFactory;
		}


		public ICatalogDataReader GetCatalogDataReader(String path)
		{
			ICatalogDataReader theReader = null;

			String ext = Path.GetExtension(path).ToUpper();

			if (".CUE" == ext)
			{
				theReader = new BinaryLogic.CueAdapter(path);
			}

			return theReader;
		}
	}
}
