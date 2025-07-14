using System.Diagnostics.CodeAnalysis;
using ATL.AudioData;
using Commons;
using HashDepot;
using System.Linq;

namespace ATL
{
    /// <summary>
    /// Information describing a metadata field
    /// </summary>
    public class MetaFieldInfo
    {
        /// <summary>
        /// Origin of the field
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public enum ORIGIN
        {
            /// <summary>
            /// Not valued
            /// </summary>
            Unknown = 0,
            /// <summary>
            /// Standard field mapped in ATL
            /// </summary>
            Standard = 1,
            /// <summary>
            /// Standard field unmapped in ATL (e.g. ID3v2 "Mood" field TMOO)
            /// </summary>
            UnmappedStandard = 2,
            /// <summary>
            /// Comment field with extended property parsed as field code (e.g. ID3v2 COMM)
            /// </summary>
            Comment = 3,
            /// <summary>
            /// Custom field through standard "custom" field (e.g. ID3v2 TXXX)
            /// </summary>
            CustomStandard = 4,
            /// <summary>
            /// Custom non-standard field (i.e. any other fancy value written regardless of standard)
            /// </summary>
            Custom = 5
        };

        private static readonly string[] reservedNativePrefix = {
            "info", "adtl", "ixml", "disp", "cue", "bext", "sample", "xmp", "cart"
        };

        /// <summary>
        /// Tag type where the picture originates from (see <see cref="ATL.AudioData.MetaDataIOFactory"/> static fields)
        /// </summary>
        public MetaDataIOFactory.TagType TagType { get; set; }
        /// <summary>
        /// Native field code according to TagType convention
        /// </summary>
        public string NativeFieldCode { get; set; }
        /// <summary>
        /// Index of the stream the field is attached to (if applicable, i.e. for multi-stream files)
        /// </summary>
        public ushort StreamNumber { get; set; }
        /// <summary>
        /// Language the value is written in
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Value of the field
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// File zone where the value is supposed to appear (ASF format I'm looking at you...)
        /// </summary>
        public string Zone { get; set; }

        /// <summary>
        /// Origin of the field
        /// </summary>
        public ORIGIN Origin { get; set; } = ORIGIN.Unknown;

        /// <summary>
        /// Attached data specific to the native format (e.g. AIFx Timestamp and Marker ID)
        /// </summary>
        public object SpecificData { get; set; }

        /// <summary>
        /// True if the field has to be deleted during the next call to <see cref="IMetaDataIO.Write"/>
        /// </summary>
        public bool MarkedForDeletion { get; set; } = false;

        // ---------------- CONSTRUCTORS

        /// <summary>
        /// Construct the structure from its parts
        /// </summary>
        /// <param name="tagType">Tag type where the picture originates from</param>
        /// <param name="nativeFieldCode">Native field code according to TagType convention</param>
        /// <param name="value">Value of the field</param>
        /// <param name="streamNumber">Index of the stream the field is attached to (if applicable, i.e. for multi-stream files)</param>
        /// <param name="language">Language the value is written in</param>
        /// <param name="zone">File zone where the value is supposed to appear</param>
        public MetaFieldInfo(MetaDataIOFactory.TagType tagType, string nativeFieldCode, string value = "", ushort streamNumber = 0, string language = "", string zone = "")
        {
            TagType = tagType; NativeFieldCode = nativeFieldCode; Value = value; StreamNumber = streamNumber; Language = language; Zone = zone;
        }

        /// <summary>
        /// Construct the structure by copying data from the given MetaFieldInfo object
        /// </summary>
        /// <param name="info">Object to copy data from</param>
        public MetaFieldInfo(MetaFieldInfo info)
        {
            TagType = info.TagType; NativeFieldCode = info.NativeFieldCode; Value = info.Value; StreamNumber = info.StreamNumber; Language = info.Language; Zone = info.Zone; Origin = info.Origin;
        }

        internal static bool IsAdditionalDataNative(string key)
        {
            int dotIndex = key.IndexOf('.');
            if (-1 == dotIndex) return false;
            string prefix = key.Substring(0, dotIndex);
            return reservedNativePrefix.Contains(prefix);
        }

        // ---------------- OVERRIDES FOR DICTIONARY STORING & UTILS

        /// <summary>
        /// Return the string representation of the object without taking its zone into account
        /// </summary>
        /// <returns>String representation of the object that doesn't take its zone into account</returns>
        public string ToStringWithoutZone()
        {
            return 100 + TagType + NativeFieldCode + Utils.BuildStrictLengthString(StreamNumber.ToString(), 5, '0', false) + Language;
        }

        /// <summary>
        /// Return the string representation of the object
        /// </summary>
        /// <returns>String representation of the object</returns>
        public override string ToString()
        {
            return 100 + TagType + NativeFieldCode + Utils.BuildStrictLengthString(StreamNumber.ToString(), 5, '0', false) + Language + Zone;
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
        /// Compare with the given object without taking zones into account
        /// </summary>
        /// <param name="obj">Object to compare with</param>
        /// <returns>Result of the comparison as per Equals convention, without taking zones into account</returns>
        public bool EqualsWithoutZone(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            // Actually check the type, should not throw exception from Equals override
            if (obj.GetType() != this.GetType()) return false;

            // Call the implementation from IEquatable
            return this.ToStringWithoutZone().Equals(((MetaFieldInfo)obj).ToStringWithoutZone());
        }

        /// <summary>
        /// Compare with the given object using certain fields only (native field code, stream number, language)
        /// </summary>
        /// <param name="obj">Object to compare with</param>
        /// <returns>Result of the comparison as per Equals convention</returns>
        public bool EqualsApproximate(MetaFieldInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            bool result = MetaDataIOFactory.TagType.ANY == obj.TagType && obj.NativeFieldCode.Equals(this.NativeFieldCode);
            if (obj.StreamNumber > 0) result = result && obj.StreamNumber == this.StreamNumber;
            if (obj.Language.Length > 0) result = result && obj.Language.Equals(this.Language);

            return result;
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
