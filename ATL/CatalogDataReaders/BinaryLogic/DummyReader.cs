using System;
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
        public string Title
        {
            get { return ""; }
        }

        /// <inheritdoc/>
        public string Artist
        {
            get { return ""; }
        }

        /// <inheritdoc/>
        public string Comments
        {
            get { return ""; }
        }

        /// <inheritdoc/>
        public IList<Track> Tracks
        {
            get { return new List<Track>(); }
        }
    }
}
