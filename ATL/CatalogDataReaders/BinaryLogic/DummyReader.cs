using System.Collections.Generic;

namespace ATL.CatalogDataReaders.BinaryLogic
{
    /// <summary>
    /// Dummy Catalog data provider
    /// </summary>
    public class DummyReader : ICatalogDataReader
    {
        /// <inheritdoc/>
        public string Path { get; set; } = "";

        /// <inheritdoc/>
        public string Title => "";

        /// <inheritdoc/>
        public string Artist => "";

        /// <inheritdoc/>
        public string Comments => "";

        /// <inheritdoc/>
        public IList<Track> Tracks => new List<Track>();
    }
}
