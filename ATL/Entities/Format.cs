using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ATL
{
    /// <summary>
    /// Describes a file format
    /// </summary>
    public class Format : IEnumerable
    {
        /// <summary>
        /// Represents an unknown generic format
        /// </summary>
        public static readonly Format UNKNOWN_FORMAT = new(0, "Unknown");

        /// <summary>
        /// Check if the given byte array matches the Format's signature (aka "magic number")
        /// NB : This operation has to be fast
        /// </summary>
        /// <param name="data">byte array to check</param>
        /// <returns>True if the given byte array matches the Format's signature</returns>
        public delegate bool CheckHeaderDelegate(byte[] data);

        /// <summary>
        /// Search the Format's signature inside the given Stream
        /// NB : This operation may be slow
        /// </summary>
        /// <param name="data">Stream to search the signature with</param>
        /// <returns>True if the Format's signature has been found inside the given Stream; false if not</returns>
        public delegate bool SearchHeaderDelegate(Stream data);

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
        /// <param name="id">Unique ID</param>
        /// <param name="name">Name</param>
        /// <param name="shortName">Short name</param>
        /// <param name="writable">Indicate if ATL implements writing for this Format</param>
        public Format(int id, string name, string shortName = "", bool writable = true)
        {
            init(id, name, 0 == shortName.Length ? name : shortName, writable);
        }

        /// <summary>
        /// Construct a Format by copying data from the given Format object
        /// </summary>
        /// <param name="f">Format to copy data from</param>
        public Format(Format f)
        {
            copyFrom(f);
        }

        /// <summary>
        /// Base constructor
        /// </summary>
        protected Format() { }

        /// <summary>
        /// Integrate data from the given Format object
        /// </summary>
        /// <param name="f">Format to copy data from</param>
        protected void copyFrom(Format f)
        {
            ID = f.ID;
            Name = f.Name;
            ShortName = f.ShortName;
            mimeList = new Dictionary<string, int>(f.mimeList);
            extList = new Dictionary<string, int>(f.extList);
            Readable = f.Readable;
            Writable = f.Writable;
            CheckHeader = f.CheckHeader;
            SearchHeader = f.SearchHeader;
        }

        /// <summary>
        /// Initialize the object from its parts
        /// </summary>
        /// <param name="id">Unique ID</param>
        /// <param name="name">Name</param>
        /// <param name="shortName">Short name</param>
        /// <param name="writable">Indicate if ATL implements writing for this Format</param>
        protected void init(int id, string name, string shortName = "", bool writable = true)
        {
            ID = id;
            Name = name;
            ShortName = 0 == shortName.Length ? name : shortName;
            Readable = true;
            Writable = writable;
            extList = new Dictionary<string, int>();
            mimeList = new Dictionary<string, int>();
        }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Short name
        /// </summary>
        public string ShortName { get; set; }

        /// <summary>
        /// Internal unique ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Check if the given data matches that format's header signature
        /// </summary>
        public CheckHeaderDelegate CheckHeader { get; set; }

        /// <summary>
        /// Search the format's header signature inside the given Stream
        /// NB : May take a long time
        /// </summary>
        public SearchHeaderDelegate SearchHeader { get; set; }

        /// <summary>
        /// MIME types associated with the format
        /// </summary>
        public ICollection<string> MimeList => mimeList.Keys;

        /// <summary>
        /// True if the format is readable by ATL
        /// </summary>
        public bool Readable { get; set; }

        /// <summary>
        /// True if the format is writable by ATL
        /// </summary>
        public bool Writable { get; set; }


        #region Code for IEnumerable implementation

        // NB : Same principle as in Collection		

        /// <inheritdoc/>
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
