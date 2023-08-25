using System.Collections.Generic;

namespace ATL
{
    /// <summary>
    /// Abstract factory for data readers, containing shared methods and members
    /// </summary>
    public abstract class Factory
    {
        /// <summary>
        /// Represents an unknown generic format
        /// </summary>
        public static readonly Format UNKNOWN_FORMAT = new Format(-1, "Unknown");

        /// <summary>
        /// List of all formats supported by this kind of data reader
        /// They are indexed by file extension to speed up matching
        /// </summary>
        protected IDictionary<string, IList<Format>> formatListByExt;

        /// <summary>
        /// List of all formats supported by this kind of data reader 
        /// They are indexed by MIME-type to speed up matching
        /// </summary>
        protected IDictionary<string, IList<Format>> formatListByMime;



        /// <summary>
        /// Adds a format to the supported formats
        /// </summary>
        /// <param name="f">Format to be added</param>
        protected void addFormat(Format f)
        {
            IList<Format> matchingFormats;

            foreach (string ext in f)
            {
                if (!formatListByExt.ContainsKey(ext))
                {
                    matchingFormats = new List<Format>();
                    matchingFormats.Add(f);
                    formatListByExt.Add(ext, matchingFormats);
                }
                else
                {
                    matchingFormats = formatListByExt[ext];
                    matchingFormats.Add(f);
                }
            }

            foreach (string mimeType in f.MimeList)
            {
                if (!formatListByMime.ContainsKey(mimeType))
                {
                    matchingFormats = new List<Format>();
                    matchingFormats.Add(f);
                    formatListByMime.Add(mimeType, matchingFormats);
                }
                else
                {
                    matchingFormats = formatListByMime[mimeType];
                    matchingFormats.Add(f);
                }
            }
        }

        /// <summary>
        /// Gets the valid formats from the given file path, using the file extension as key
        /// </summary>
        /// <param name="path">Path of the file which format to recognize</param>
        /// <returns>List of the valid formats matching the extension of the given file, 
        /// or null if none recognized or the file does not exist</returns>
        public IList<Format> getFormatsFromPath(string path)
        {
            IList<Format> result = null;
            string extension;
            if (path.Contains("."))
                extension = path.Substring(path.LastIndexOf('.'), path.Length - path.LastIndexOf('.')).ToLower();
            else
                extension = path;

            if (formatListByExt.TryGetValue(extension, out var formats) && formats != null && formats.Count > 0)
            {
                result = formats;
            }

            return result;
        }

        /// <summary>
        /// Gets the valid formats from the given MIME-type
        /// </summary>
        /// <param name="mimeType">MIME-type to recognize</param>
        /// <returns>List of the valid formats matching the MIME-type of the given file, 
        /// or null if none recognized</returns>
        public IList<Format> getFormatsFromMimeType(string mimeType)
        {
            IList<Format> result = null;
            string mime = mimeType.ToLower();

            if (formatListByMime.TryGetValue(mime, out var formats) && formats != null && formats.Count > 0)
            {
                result = formats;
            }

            return result;
        }

        /// <summary>
        /// Gets a list of all supported formats
        /// </summary>
        /// <returns>List of all supported formats</returns>
        public ICollection<Format> getFormats()
        {
            Dictionary<int, Format> result = new Dictionary<int, Format>();
            foreach (IList<Format> formats in formatListByExt.Values)
            {
                foreach (Format f in formats)
                {
                    // Filter duplicates "caused by" indexing formats by extension
                    result.TryAdd(f.ID, f);
                }
            }
            return result.Values;
        }
    }
}
