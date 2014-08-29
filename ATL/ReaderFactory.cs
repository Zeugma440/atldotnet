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
        protected IDictionary<String, ATL.Format> formatList;

        /// <summary>
        /// Adds a format to the supported formats
        /// </summary>
        /// <param name="f">Format to be added</param>
        protected void addFormat(Format f)
        {
            foreach (String ext in f)
            {
                if (!formatList.ContainsKey(ext)) formatList.Add(ext, f);
            }
        }

        /// <summary>
        /// Gets the format ID from the file path, using the file extension as key
        /// </summary>
        /// <param name="path">Path of the file which format to recognize</param>
        /// <returns>Identifier of the format of the given file, 
        /// or NO_FORMAT if none recognized or the file does not exist</returns>
        protected int getFormatIDFromPath(String path)
        {
            int result = NO_FORMAT;

            if (File.Exists(path))
            {
                Format f = formatList[Path.GetExtension(path).ToUpper()];
                if (f != null) result = (f != null) ? f.ID : NO_FORMAT;
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
            foreach (Format f in formatList.Values)
            {
                // Filter duplicates "caused by" indexing formats by extension
                if (!result.ContainsKey(f.ID)) result.Add(f.ID, f);
            }
            return result.Values;
        }

	}
}
