using System;
using System.Collections;
using System.Collections.Generic;

namespace ATL
{
    public enum EPlayListFormats
    {
        PL_M3U,
        PL_PLS,
        PL_FPL,
        PL_XSPF,
        PL_SMIL,
        PL_ASX,
        PL_B4S
    }
    /// <summary>
    /// Describes a file format
    /// </summary>
    public class Format : IEnumerable
    {
        /// <summary>
        ///  Name of the format
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// ID of the format
        /// </summary>
        public int ID { get; set; }
        /// <summary>
        /// true if the format is readable by ATL
        /// </summary>
        public bool Readable { get; set; }

        // MIME type of the format
        protected IDictionary<string, int> mimeList;
        // List of file extensions proper to this format
        protected IDictionary<string, int> extList;

        public Format() { }

        public Format(string iName)
        {
            init(iName);
        }

        protected void init(string iName)
        {
            Name = iName;
            Readable = true;
            extList = new Dictionary<string, int>();
            mimeList = new Dictionary<string, int>();
        }

        protected void copyFrom(Format iFormat)
        {
            Name = iFormat.Name;
            ID = iFormat.ID;
            Readable = iFormat.Readable;
            extList = new Dictionary<string, int>(iFormat.extList);
            mimeList = new Dictionary<string, int>(iFormat.mimeList);
        }
   
        public ICollection<string> MimeList
        {
            get { return mimeList.Keys; }
        }

        #region Code for IEnumerable implementation

        // NB : Same principle as in Collection		

        public IEnumerator GetEnumerator()
        {
            return extList.Keys.GetEnumerator();
        }

        #endregion

        // Adds the MimeTypes  to the MimeTypes list of this Format
        public void AddMimeTypes(params string[] mimeTypes)
        {
            foreach (var mimeType in mimeTypes)
                mimeList[mimeType.ToLower()] = 0; 
        }

        // Tests if the extension ext is a valid extension of the current Format
        public bool IsValidMimeType(string mimeType)
        {
            return mimeList.ContainsKey(mimeType.ToLower());
        }

        // Adds the extensions exts to the extensions list of this Format
        public void AddExtensions(params string[] exts)
        {
            foreach (var ext in exts)
                extList[ext.ToLower()] = 0;
        }
        // Tests if the extension ext is a valid extension of the current Format
        public bool IsValidExtension(string ext)
        {
            return extList.ContainsKey(ext.ToLower());
        }
    }
}
