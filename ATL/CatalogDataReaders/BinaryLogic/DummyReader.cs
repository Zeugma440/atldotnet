using System;
using System.Collections.Generic;
using System.Text;

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
