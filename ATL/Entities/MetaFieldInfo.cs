using ATL.AudioData;
using Commons;
using HashDepot;

namespace ATL
{
    public class MetaFieldInfo
    {
        public enum ORIGIN
        {
            Unknown = 0,            // Not valued
            Standard = 1,           // Standard field
            UnmappedStandard = 2,   // Unmapped standard field (e.g. ID3v2 "Mood" field TMOO)
            Comment = 3,            // Comment field with extended property parsed as field code (e.g. ID3v2 COMM)
            CustomStandard = 4,     // Custom field through standard "custom" field (e.g. ID3v2 TXXX)
            Custom = 5              // Custom non-standard field (i.e. any other fancy value written regardless of standard)
        };


        public int TagType;                             // Tag type where the picture originates from
        public string NativeFieldCode;                  // Native field code according to TagType convention
        public ushort StreamNumber;                     // Index of the stream the field is attached to (if applicable, i.e. for multi-stream files)
        public string Language;                         // Language the value is written in

        public string Value;                            // Field value
        public string Zone;                             // File zone where the value is supposed to appear (ASF format I'm looking at you...)

        public ORIGIN Origin = ORIGIN.Unknown;          // Origin of field

        public object SpecificData;                     // Attached data specific to the native format (e.g. AIFx Timestamp and Marker ID)

        public bool MarkedForDeletion = false;          // True if the field has to be deleted in the next IMetaDataIO.Write operation

        // ---------------- CONSTRUCTORS

        public MetaFieldInfo(int tagType, string nativeFieldCode, string value = "", ushort streamNumber = 0, string language = "", string zone = "")
        {
            TagType = tagType; NativeFieldCode = nativeFieldCode; Value = value; StreamNumber = streamNumber; Language = language; Zone = zone;
        }

        public MetaFieldInfo(MetaFieldInfo info)
        {
            TagType = info.TagType; NativeFieldCode = info.NativeFieldCode; Value = info.Value; StreamNumber = info.StreamNumber; Language = info.Language; Zone = info.Zone; Origin = info.Origin;
        }

        // ---------------- OVERRIDES FOR DICTIONARY STORING & UTILS

        public string ToStringWithoutZone()
        {
            return (100 + TagType).ToString() + NativeFieldCode + Utils.BuildStrictLengthString(StreamNumber.ToString(), 5, '0', false) + Language;
        }

        public override string ToString()
        {
            return (100 + TagType).ToString() + NativeFieldCode + Utils.BuildStrictLengthString(StreamNumber.ToString(), 5, '0', false) + Language + Zone;
        }

        public override int GetHashCode()
        {
            return (int)Fnv1a.Hash32(Utils.Latin1Encoding.GetBytes(ToString()));
        }

        public bool EqualsWithoutZone(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            // Actually check the type, should not throw exception from Equals override
            if (obj.GetType() != this.GetType()) return false;

            // Call the implementation from IEquatable
            return this.ToStringWithoutZone().Equals(((MetaFieldInfo)obj).ToStringWithoutZone());
        }

        public bool EqualsApproximate(MetaFieldInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            bool result = (MetaDataIOFactory.TAG_ANY == obj.TagType && obj.NativeFieldCode.Equals(this.NativeFieldCode));
            if (obj.StreamNumber > 0) result = result && (obj.StreamNumber == this.StreamNumber);
            if (obj.Language.Length > 0) result = result && obj.Language.Equals(this.Language);

            return result;
        }

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
