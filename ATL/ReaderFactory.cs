using System.Collections.Generic;

namespace ATL
{
    /// <summary>
    /// Abstract factory for data readers, containing shared methods and members
    /// </summary>
    public abstract class ReaderFactory
    {
        // ID representing the absence of format
        public const int NO_FORMAT = -1;

        // List of all formats supported by this kind of data reader
        // They are indexed by file extension to speed up matching
        protected IDictionary<string, IList<ATL.Format>> formatListByExt;

        // List of all formats supported by this kind of data reader
        // They are indexed by MIME-type to speed up matching
        protected IDictionary<string, IList<ATL.Format>> formatListByMime;

        /// <summary>
        /// Adds a format to the supported formats
        /// </summary>
        /// <param name="f">Format to be added</param>
        protected void addFormat(Format f)
        {
            addExtensions(f);
            addMimeType(f);
        }

        /// <summary>
        /// Adds format's extensions  
        /// </summary>
        /// <param name="f">Format to be added</param>
        private void addExtensions(Format f)
        {
            foreach (string ext in f)
            {
                if (!formatListByExt.ContainsKey(ext))
                    formatListByExt[ext] = new List<ATL.Format>();
                formatListByExt[ext].Add(f);
            }
        }
        /// <summary>
        /// Adds MimeTypes of Format
        /// </summary>
        /// <param name="f">Format to be addeds</param>
        private void addMimeType(Format f)
        {
            foreach (string mimeType in f.MimeList)
            {
                if (!formatListByMime.ContainsKey(mimeType))
                    formatListByMime[mimeType] = new List<ATL.Format>();
                formatListByMime[mimeType].Add(f);
            }
        }

        /// <summary>
        /// Gets the valid formats from the given file path, using the file extension as key
        /// </summary>
        /// <param name="path">Path of the file which format to recognize</param>
        /// <returns>List of the valid formats matching the extension of the given file, 
        /// or null if none recognized or the file does not exist</returns>
        protected IList<ATL.Format> getFormatsFromPath(string path)
        {
            var extension = System.IO.Path.GetExtension(path).ToLower();
            return formatListByExt.ContainsKey(extension) ? formatListByExt[extension] : new List<ATL.Format>();
        }

        /// <summary>
        /// Gets the valid formats from the given MIME-type
        /// </summary>
        /// <param name="mimeType">MIME-type to recognize</param>
        /// <returns>List of the valid formats matching the MIME-type of the given file, 
        /// or null if none recognized</returns>
        protected IList<ATL.Format> getFormatsFromMimeType(string mimeType)
        {
            string mime = mimeType.ToLower();
            return formatListByMime.ContainsKey(mime) ? formatListByMime[mime] : new List<ATL.Format>();
        }

        /// <summary>
        /// Gets a list of all supported formats
        /// </summary>
        /// <returns>List of all supported formats</returns>
        public ICollection<ATL.Format> getFormats()
        {
            Dictionary<int, Format> result = new Dictionary<int, Format>();
            foreach (IList<Format> formats in formatListByExt.Values)
            {
                foreach (Format f in formats)
                {
                    // Filter duplicates "caused by" indexing formats by extension
                    if (!result.ContainsKey(f.ID)) result.Add(f.ID, f);
                }
            }
            return result.Values;
        }

    }
}
