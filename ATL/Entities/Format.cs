using System.Collections;
using System.Collections.Generic;

namespace ATL
{
    /// <summary>
    /// Describes a file format
    /// </summary>
    public class Format : IEnumerable
    {
        /// <summary>
        /// MIME types associated with the format
        /// </summary>
        protected IDictionary<string, int> mimeList;
        /// <summary>
        /// List of file extensions proper to this format
        /// </summary>
        protected IDictionary<string, int> extList;

        /// <summary>
        /// Construct a format
        /// </summary>
        /// <param name="ID">Unique ID</param>
        /// <param name="Name">Name</param>
        public Format(int ID, string Name)
        {
            init(ID, Name);
        }

        /// <summary>
        /// Construct a format by copying data from the given Format object
        /// </summary>
        /// <param name="f">Format to copy data from</param>
        public Format(Format f)
        {
            copyFrom(f);
        }

        /// <summary>
        /// Integrate data from the given Format object
        /// </summary>
        /// <param name="iFormat">Format to copy data from</param>
        protected virtual void copyFrom(Format iFormat)
        {
            this.Name = iFormat.Name;
            this.ID = iFormat.ID;
            this.mimeList = new Dictionary<string, int>(iFormat.mimeList);
            this.extList = new Dictionary<string, int>(iFormat.extList);
            this.Readable = iFormat.Readable;
        }

        /// <summary>
        /// Initialize the object from its parts
        /// </summary>
        /// <param name="ID">Unique ID</param>
        /// <param name="Name">Name</param>
        protected virtual void init(int ID, string Name)
        {
            this.ID = ID;
            this.Name = Name;
            this.Readable = true;
            extList = new Dictionary<string, int>();
            mimeList = new Dictionary<string, int>();
        }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Internal unique ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// True if the format is readable by ATL
        /// </summary>
        public bool Readable { get; set; }

        /// <summary>
        /// MIME types associated with the format
        /// </summary>
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

        /// <summary>
        /// Add the given MIME-type to the supported MIME-types of this Format
        /// </summary>
        /// <param name="mimeType">MIME-type to add</param>
        public void AddMimeType(string mimeType)
        {
            if (!mimeList.ContainsKey(mimeType.ToLower()))
                mimeList.Add(mimeType.ToLower(), 0);
        }

        /// <summary>
        /// Test if the given MIME-type is associated with the current Format
        /// </summary>
        /// <param name="mimeType">MIME-type to test</param>
        /// <returns>True if the given MIME-type is associated with the current Format; false if not</returns>
        public bool IsValidMimeType(string mimeType)
        {
            return mimeList.ContainsKey(mimeType.ToLower());
        }

        /// <summary>
        /// Add the given extension to the supported extensions list
        /// </summary>
        /// <param name="ext">Extension to add to the supported extensions list (e.g. "bmp" for the Bitmap image format)</param>
        public void AddExtension(string ext)
        {
            if (!extList.ContainsKey(ext.ToLower()))
                extList.Add(ext.ToLower(), 0);
        }

        /// <summary>
        /// Test if the given extension is a valid extension of the current Format
        /// </summary>
        /// <param name="ext">Extension to test (e.g. "bmp" for the Bitmap image format)</param>
        /// <returns>True if the given extension is a valid extension of the current Format; false if not</returns>
        public bool IsValidExtension(string ext)
        {
            return extList.ContainsKey(ext.ToLower());
        }
    }
}
