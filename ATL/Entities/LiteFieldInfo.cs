using System;
using System.Diagnostics.CodeAnalysis;
using ATL.AudioData;
using Commons;
using HashDepot;
using System.Linq;
using System.IO.Enumeration;

namespace ATL
{
    /// <summary>
    /// Information describing a metadata field
    /// </summary>
    public sealed class LiteFieldInfo
    {
        /// <summary>
        /// Language the value is written in
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Value of the field
        /// </summary>
        public string Value { get; set; }


        // ---------------- CONSTRUCTORS

        /// <summary>
        /// Construct the structure from its parts
        /// </summary>
        /// <param name="value">Value of the field</param>
        /// <param name="language">Language the value is written in</param>
        public LiteFieldInfo(string value, string language = "")
        {
            Value = value; Language = language;
        }

        /// <summary>
        /// Construct the structure by copying data from the given MetaFieldInfo object
        /// </summary>
        /// <param name="info">Object to copy data from</param>
        public LiteFieldInfo(LiteFieldInfo info)
        {
            Value = info.Value; Language = info.Language;
        }


        // ---------------- UTILS

        /// <summary>
        /// Indicates if the field has an empty value
        /// </summary>
        public bool isEmpty
        {
            get => Value.Trim().Length == 0;
        }


        // ---------------- OVERRIDES FOR DICTIONARY STORING

        /// <summary>
        /// Return the string representation of the object
        /// </summary>
        /// <returns>String representation of the object</returns>
        public override string ToString()
        {
            return Value + "." + Language;
        }

        /// <summary>
        /// Return the hash of the object
        /// </summary>
        /// <returns>Hash of the object</returns>
        public override int GetHashCode()
        {
            return (int)FNV1a.Hash32(Utils.Latin1Encoding.GetBytes(ToString()));
        }

        /// <summary>
        /// Compare with the given object
        /// </summary>
        /// <param name="obj">Object to compare with</param>
        /// <returns>Result of the comparison as per Equals convention</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            // Actually check the type, should not throw exception from Equals override
            if (obj.GetType() != this.GetType()) return false;

            // Call the implementation from IEquatable
            return this.ToString().Equals(obj.ToString());
        }
    }
}
