using System.Collections.Generic;

namespace ATL
{
    /// <summary>
    /// Abstract factory for data readers, containing shared methods and members
    /// </summary>
    public abstract class Factory<T> where T : Format
    {
        /// <summary>
        /// List of all formats supported by this kind of data reader
        /// They are indexed by file extension to speed up matching
        /// </summary>
        protected IDictionary<string, IList<T>> formatListByExt;

        /// <summary>
        /// List of all formats supported by this kind of data reader 
        /// They are indexed by MIME-type to speed up matching
        /// </summary>
        protected IDictionary<string, IList<T>> formatListByMime;



        /// <summary>
        /// Adds a format to the supported formats
        /// </summary>
        /// <param name="f">Format to be added</param>
        protected void addFormat(T f)
        {
            IList<T> matchingFormats;

            foreach (string ext in f)
            {
                if (!formatListByExt.ContainsKey(ext))
                {
                    matchingFormats = new List<T> { f };
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
                    matchingFormats = new List<T> { f };
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
        public IList<T> getFormatsFromPath(string path)
        {
            IList<T> result = null;
            string extension = path.Contains('.') ? path.Substring(path.LastIndexOf('.'), path.Length - path.LastIndexOf('.')).ToLower() : path;

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
        public IList<T> getFormatsFromMimeType(string mimeType)
        {
            IList<T> result = null;
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
        public ICollection<T> getFormats()
        {
            Dictionary<int, T> result = new Dictionary<int, T>();
            foreach (IList<T> formats in formatListByExt.Values)
            {
                foreach (T f in formats)
                {
                    // Filter duplicates "caused by" indexing formats by extension
                    result.TryAdd(f.ID, f);
                }
            }
            return result.Values;
        }

        /// <summary>
        /// Gets the format matching the given ID
        /// </summary>
        /// <returns>Format matching the given ID; null if not found</returns>
        public T getFormat(int ID)
        {
            foreach (IList<T> formats in formatListByExt.Values)
            {
                foreach (T f in formats)
                {
                    if (f.ID == ID) return f;
                }
            }
            return null;
        }
    }
}
