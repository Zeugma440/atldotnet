using ATL.Logging;
using Commons;
using HashDepot;

namespace ATL
{
    public class PictureInfo
    {
        public enum PIC_TYPE { Unsupported = 99, Generic = 1, Front = 2, Back = 3, CD = 4 };

        public PIC_TYPE PicType;                        // Normalized picture type
        public ImageFormat NativeFormat;                // Native image format
        public int Position;                            // Position of the picture among pictures of the same generic type / native code (default 1 if the picture is one of its kind)

        public int TagType;                             // Tag type where the picture originates from
        public int NativePicCode;                       // Native picture code according to TagType convention (numeric : e.g. ID3v2)
        public string NativePicCodeStr;                 // Native picture code according to TagType convention (string : e.g. APEtag)

        // TODO - add a description field

        public byte[] PictureData;                      // Binary picture data
        public uint PictureHash;                        // Hash of binary picture data

        public bool MarkedForDeletion = false;          // True if the field has to be deleted in the next IMetaDataIO.Write operation
        public int Flag;                                // Freeform value to be used by other parts of the library

        // ---------------- CONSTRUCTORS

        public PictureInfo(PictureInfo picInfo)
        {
            this.PicType = picInfo.PicType;
            this.NativeFormat = picInfo.NativeFormat;
            this.Position = picInfo.Position;
            this.TagType = picInfo.TagType;
            this.NativePicCode = picInfo.NativePicCode;
            this.NativePicCodeStr = picInfo.NativePicCodeStr;
            if (picInfo.PictureData != null)
            {
                this.PictureData = new byte[picInfo.PictureData.Length];
                picInfo.PictureData.CopyTo(this.PictureData, 0);
            }
            this.PictureHash = picInfo.PictureHash;
            this.MarkedForDeletion = picInfo.MarkedForDeletion;
            this.Flag = picInfo.Flag;
        }
        public PictureInfo(ImageFormat nativeFormat, PIC_TYPE picType, int tagType, object nativePicCode, int position = 1)
        {
            PicType = picType; NativeFormat = nativeFormat; TagType = tagType; Position = position;
            if (nativePicCode is string)
            {
                NativePicCodeStr = (string)nativePicCode;
                NativePicCode = -1;
            }
            else if (nativePicCode is byte)
            {
                NativePicCode = (byte)nativePicCode;
            }
            else if (nativePicCode is int)
            {
                NativePicCode = (int)nativePicCode;
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "nativePicCode type is not supported; expected byte, int or string; found " + nativePicCode.GetType().Name);
            }
        }
        public PictureInfo(ImageFormat nativeFormat, PIC_TYPE picType, int position = 1) { PicType = picType; NativeFormat = nativeFormat; Position = position; }
        public PictureInfo(ImageFormat nativeFormat, int tagType, object nativePicCode, int position = 1)
        {
            PicType = PIC_TYPE.Unsupported; NativeFormat = nativeFormat; TagType = tagType; Position = position;
            if (nativePicCode is string)
            {
                NativePicCodeStr = (string)nativePicCode;
                NativePicCode = -1;
            }
            else if (nativePicCode is byte)
            {
                NativePicCode = (byte)nativePicCode;
            }
            else if (nativePicCode is int)
            {
                NativePicCode = (int)nativePicCode;
            }
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "nativePicCode type is not supported; expected byte, int or string; found " + nativePicCode.GetType().Name);
            }
        }
        public PictureInfo(ImageFormat nativeFormat, int tagType, byte nativePicCode, int position = 1) { PicType = PIC_TYPE.Unsupported; NativePicCode = nativePicCode; NativeFormat = nativeFormat; TagType = tagType; Position = position; }
        public PictureInfo(ImageFormat nativeFormat, int tagType, string nativePicCode, int position = 1) { PicType = PIC_TYPE.Unsupported; NativePicCodeStr = nativePicCode; NativePicCode = -1; NativeFormat = nativeFormat; TagType = tagType; Position = position; }


        // ---------------- OVERRIDES FOR DICTIONARY STORING & UTILS

        public override string ToString()
        {
            string result = Utils.BuildStrictLengthString(Position.ToString(), 2, '0', false) + Utils.BuildStrictLengthString(((int)PicType).ToString(), 2, '0', false);

            if (PicType.Equals(PIC_TYPE.Unsupported))
            {
                if (NativePicCode > 0)
                    result = result + ((10000000 * TagType) + NativePicCode).ToString();
                else if ((NativePicCodeStr != null) && (NativePicCodeStr.Length > 0))
                    result = result + (10000000 * TagType).ToString() + NativePicCodeStr;
                else
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Non-supported picture detected, but no native picture code found");
            }

            return result;
        }

        public override int GetHashCode()
        {
            return (int)Fnv1a.Hash32(Utils.Latin1Encoding.GetBytes(ToString()));
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

        public void ComputePicHash()
        {
            PictureHash = Fnv1a.Hash32(PictureData);
        }
    }
}
