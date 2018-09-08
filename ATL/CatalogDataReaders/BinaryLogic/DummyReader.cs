using System;
using System.Collections.Generic;

namespace ATL.CatalogDataReaders.BinaryLogic
{
    public class DummyReader : ICatalogDataReader
    {
        public string Path { get; set; } 

        public string Title
        {
            get { return ""; }
        }

        public string Artist
        {
            get { return ""; }
        }

        public string Comments
        {
            get { return ""; }
        }

        public IList<Track> Tracks
        {
            get { return new List<Track>(); }
        }
    }
}
