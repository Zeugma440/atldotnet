using System;
using System.Collections.Generic;

namespace ATL.CatalogDataReaders.BinaryLogic
{
    public class DummyReader : ICatalogDataReader
    {
        String path = "";

        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
            }
        }

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
