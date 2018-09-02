using System;
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
        protected IDictionary<String, IList<ATL.Format>> formatListByExt;

        // List of all formats supported by this kind of data reader
        // They are indexed by MIME-type to speed up matching
        protected IDictionary<String, IList<ATL.Format>> formatListByMime;
   


        /// <summary>
        /// Adds a format to the supported formats
        /// </summary>
        /// <param name="f">Format to be added</param>
        protected void addFormat(Format f)
        {
            IList<ATL.Format> matchingFormats;

            foreach (string ext in f)
            {
                if (!formatListByExt.ContainsKey(ext))
                {
                    matchingFormats = new List<ATL.Format>();
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
                    matchingFormats = new List<ATL.Format>();
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
        protected IList<ATL.Format> getFormatsFromPath(string path)
        {
            IList<ATL.Format> result = null;
            string extension = path.Substring(path.LastIndexOf('.'), path.Length - path.LastIndexOf('.')).ToLower();

            if (formatListByExt.ContainsKey(extension))
            {
                IList<Format> formats = formatListByExt[extension];
                if (formats != null && formats.Count > 0)
                {
                    result = formats;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the valid formats from the given MIME-type
        /// </summary>
        /// <param name="mimeType">MIME-type to recognize</param>
        /// <returns>List of the valid formats matching the MIME-type of the given file, 
        /// or null if none recognized</returns>
        protected IList<ATL.Format> getFormatsFromMimeType(string mimeType)
        {
            IList<ATL.Format> result = null;
            string mime = mimeType.ToLower();

            if (formatListByMime.ContainsKey(mime))
            {
                IList<Format> formats = formatListByMime[mime];
                if (formats != null && formats.Count > 0)
                {
                    result = formats;
                }
            }

            return result;
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
