using System;
using System.IO;
using System.Collections;
using ATL.Logging;
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
        protected IDictionary<String, IList<ATL.Format>> formatList;

        /// <summary>
        /// Adds a format to the supported formats
        /// </summary>
        /// <param name="f">Format to be added</param>
        protected void addFormat(Format f)
        {
            IList<ATL.Format> matchingFormats;

            foreach (String ext in f)
            {
                if (!formatList.ContainsKey(ext))
                {
                    matchingFormats = new List<ATL.Format>();
                    matchingFormats.Add(f);
                    formatList.Add(ext, matchingFormats);
                } else {
                    matchingFormats = formatList[ext];
                    matchingFormats.Add(f);
                    //formatList.Remove(ext);
                    //formatList.Add(ext, matchingFormats);
                }
            }
        }

        /// <summary>
        /// Gets the valid formats from the file path, using the file extension as key
        /// </summary>
        /// <param name="path">Path of the file which format to recognize</param>
        /// <returns>List of the valid formats matching the extension of the given file, 
        /// or null if none recognized or the file does not exist</returns>
        protected IList<ATL.Format> getFormatsFromPath(String path)
        {
            IList<ATL.Format> result = null;

            if (File.Exists(path))
            {
                if (formatList.ContainsKey(Path.GetExtension(path).ToUpper()))
                {
                    IList<Format> formats = formatList[Path.GetExtension(path).ToUpper()];
                    if (formats != null && formats.Count > 0)
                    {
                        result = formats;
                    }
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
            foreach (IList<Format> formats in formatList.Values)
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
