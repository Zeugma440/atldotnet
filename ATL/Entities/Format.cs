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
        // Name of the format
        protected string fName;
        // ID of the format
        protected int fID;
        // MIME type of the format
        protected IDictionary<string, int> mimeList;
        // List of file extensions proper to this format
        protected IDictionary<string, int> extList;
        // true if the format is readable by ATL
        protected bool fReadable;

        public Format() { }

        public Format(string iName)
        {
            init(iName);
        }

        protected void init(string iName)
        {
            fName = iName;
            fReadable = true;
            extList = new Dictionary<string, int>();
            mimeList = new Dictionary<string, int>();
        }

        protected void copyFrom(Format iFormat)
        {
            this.fName = iFormat.fName;
            this.fID = iFormat.fID;
            this.fReadable = iFormat.fReadable;
            this.extList = new Dictionary<string, int>(iFormat.extList);
            this.mimeList = new Dictionary<string, int>(iFormat.mimeList);
        }

        public String Name
        {
            get { return fName; }
            set { fName = value; }
        }

        public int ID
        {
            get { return fID; }
            set { fID = value; }
        }

        public bool Readable
        {
            get { return fReadable; }
            set { fReadable = value; }
        }

        public ICollection<String> MimeList
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
