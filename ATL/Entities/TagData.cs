using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using HashDepot;
using ATL.AudioData;

namespace ATL
{
	/// <summary>
	/// Basic metadata fields container
    /// 
    /// TODO Document each member
	/// </summary>
	public class TagData
	{
        public enum PIC_TYPE { Unsupported = 99, Generic = 1, Front = 2, Back = 3, CD = 4 };

        // TODO - test memory usage with alternate signature using byte[], which could be simpler than current MemoryStream-based implementations
        public delegate void PictureStreamHandlerDelegate(ref MemoryStream stream, PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object nativePicCode, int position);

        public class PictureInfo
        {
            public PIC_TYPE PicType;                        // Normalized picture type
            public ImageFormat NativeFormat;                // Native image format
            public int Position;                            // Position of the picture among pictures of the same generic type / native code (default 1 if the picture is one of its kind)

            public int TagType;                             // Tag type where the picture originates from
            public int NativePicCode;                       // Native picture code according to TagType convention (numeric : e.g. ID3v2)
            public string NativePicCodeStr;                 // Native picture code according to TagType convention (string : e.g. APEtag)

            // TODO - add a description field

            public byte[] PictureData;                      // Binary picture data

            public bool MarkedForDeletion = false;          // True if the field has to be deleted in the next IMetaDataIO.Write operation

            // ---------------- CONSTRUCTORS

            public PictureInfo(ImageFormat nativeFormat, PIC_TYPE picType, int tagType, object nativePicCode, int position = 1)
            {
                PicType = picType; NativeFormat = nativeFormat; TagType = tagType; Position = position;
                if (nativePicCode is string)
                {
                    NativePicCodeStr = (string)nativePicCode;
                    NativePicCode = -1;
                } else if (nativePicCode is byte)
                {
                    NativePicCode = (byte)nativePicCode;
                }
                else if (nativePicCode is int)
                {
                    NativePicCode = (int)nativePicCode;
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "nativePicCode type is not supported; expected byte, int or string; found "+nativePicCode.GetType().Name);
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

            // ---------------- OVERRIDES FOR DICTIONARY STORING

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
        }

        public class MetaFieldInfo
        {
            public int TagType;                             // Tag type where the picture originates from
            public string NativeFieldCode;                  // Native field code according to TagType convention
            public ushort StreamNumber;                     // Index of the stream the field is attached to (if applicable, i.e. for multi-stream files)
            public string Language;                         // Language the value is written in

            public string Value;                            // Field value
            public string Zone;                             // File zone where the value is supposed to appear (ASF format I'm looking at you...)

            public bool MarkedForDeletion = false;          // True if the field has to be deleted in the next IMetaDataIO.Write operation

            // ---------------- CONSTRUCTORS

            public MetaFieldInfo(int tagType, string nativeFieldCode, string value = "", ushort streamNumber = 0, string language = "", string zone = "")
            {
                TagType = tagType; NativeFieldCode = nativeFieldCode; Value = value; StreamNumber = streamNumber; Language = language; Zone = zone;
            }

            public MetaFieldInfo(MetaFieldInfo info)
            {
                TagType = info.TagType; NativeFieldCode = info.NativeFieldCode; Value = info.Value; StreamNumber = info.StreamNumber; Language = info.Language; Zone = info.Zone;
            }

            // ---------------- OVERRIDES FOR DICTIONARY STORING

            public string ToStringWithoutZone()
            {
                return (100 + TagType).ToString() + NativeFieldCode + Utils.BuildStrictLengthString(StreamNumber.ToString(), 5, '0') + Language;
            }

            public override string ToString()
            {
                return (100 + TagType).ToString() + NativeFieldCode + Utils.BuildStrictLengthString(StreamNumber.ToString(),5,'0') + Language + Zone;
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

        public const byte TAG_FIELD_GENERAL_DESCRIPTION     = 0;
        public const byte TAG_FIELD_TITLE                   = 1;
        public const byte TAG_FIELD_ARTIST                  = 2;
        public const byte TAG_FIELD_COMPOSER                = 3;
        public const byte TAG_FIELD_COMMENT                 = 4;
        public const byte TAG_FIELD_GENRE                   = 5;
        public const byte TAG_FIELD_ALBUM                   = 6;
        public const byte TAG_FIELD_RECORDING_DATE          = 7;
        public const byte TAG_FIELD_RECORDING_YEAR          = 8;
        public const byte TAG_FIELD_RECORDING_DAYMONTH      = 9;
        public const byte TAG_FIELD_TRACK_NUMBER            = 10;
        public const byte TAG_FIELD_DISC_NUMBER             = 11;
        public const byte TAG_FIELD_RATING                  = 12;
//        public const byte TAG_FIELD_PICTURE_DATA            = 13;  Managed through Pictures field
        public const byte TAG_FIELD_ORIGINAL_ARTIST         = 14;
        public const byte TAG_FIELD_ORIGINAL_ALBUM          = 15;
        public const byte TAG_FIELD_COPYRIGHT               = 16;
        public const byte TAG_FIELD_ALBUM_ARTIST            = 17;
        public const byte TAG_FIELD_PUBLISHER               = 18;
        public const byte TAG_FIELD_CONDUCTOR               = 19;


        public string GeneralDescription = null;
        public string Title = null;
		public string Artist = null;
        public string OriginalArtist = null;
        public string Composer = null;
		public string Comment = null;
        public string Genre = null;
        public string Album = null;
        public string OriginalAlbum = null;
        public string RecordingYear = null;
        public string RecordingDayMonth = null;
        public string RecordingDate = null;
        public string TrackNumber = null;
        public string DiscNumber = null;
        public string Rating = null;
        public string Copyright = null;
        public string AlbumArtist = null;
        public string Publisher = null;
        public string Conductor = null;

        public IList<PictureInfo> Pictures;
        public IList<MetaFieldInfo> AdditionalFields;


        public TagData()
        {
            Pictures = new List<PictureInfo>();
            AdditionalFields = new List<MetaFieldInfo>();
        }

        public void IntegrateValue(byte key, string value)
        {
            switch (key)
            {
                // Textual fields
                case TAG_FIELD_GENERAL_DESCRIPTION:     GeneralDescription = value; break;
                case TAG_FIELD_TITLE:                   Title = value; break;
                case TAG_FIELD_ARTIST:                  Artist= value; break;
                case TAG_FIELD_COMPOSER:                Composer = value; break;
                case TAG_FIELD_COMMENT:                 Comment = value; break;
                case TAG_FIELD_GENRE:                   Genre = value; break;
                case TAG_FIELD_ALBUM:                   Album = value; break;
                case TAG_FIELD_ORIGINAL_ARTIST:         OriginalArtist = value; break;
                case TAG_FIELD_ORIGINAL_ALBUM:          OriginalAlbum = value; break;
                case TAG_FIELD_COPYRIGHT:               Copyright = value; break;
                case TAG_FIELD_ALBUM_ARTIST:            AlbumArtist = value; break;
                case TAG_FIELD_PUBLISHER:               Publisher = value; break;
                case TAG_FIELD_CONDUCTOR:               Conductor = value; break;
                // Numeric fields (a value at zero mean nothing has been valued -> field should be empty)
                case TAG_FIELD_RECORDING_DATE:          RecordingDate = emptyIfZero(value); break;
                case TAG_FIELD_RECORDING_YEAR:          RecordingYear = emptyIfZero(value); break;
                case TAG_FIELD_RECORDING_DAYMONTH:      RecordingDayMonth = emptyIfZero(value); break;
                case TAG_FIELD_TRACK_NUMBER:            TrackNumber = emptyIfZero(value); break;
                case TAG_FIELD_DISC_NUMBER:             DiscNumber = emptyIfZero(value); break;
                case TAG_FIELD_RATING:                  Rating = emptyIfZero(value); break;
            }
        }

        public void IntegrateValues(TagData data)
        {
            IDictionary<PictureInfo, int> picturePositions = generatePicturePositions();

            // String values
            IDictionary<byte, String> newData = data.ToMap();
            foreach (byte key in newData.Keys)
            {
                IntegrateValue(key, newData[key]);
            }

            // Pictures
            if (data.Pictures != null)
            {
                foreach (PictureInfo newPicInfo in data.Pictures)
                {
                    // New PictureInfo picture type already exists in current TagData
                    if (picturePositions.ContainsKey(newPicInfo))
                    {
                        // New PictureInfo is a demand for deletion
                        if (newPicInfo.MarkedForDeletion)
                        {
                            foreach (PictureInfo picInfo in Pictures)
                            {
                                if (picInfo.ToString().Equals(newPicInfo.ToString()))
                                {
                                    picInfo.MarkedForDeletion = true;
                                }
                            }
                        }
                        else // New PictureInfo is a X-th picture of the same type
                        {
                            newPicInfo.Position = picturePositions[newPicInfo] + 1;
                            Pictures.Add(newPicInfo);
                        }
                    }
                    else // New PictureInfo picture type does not exist in current TagData
                    {
                        Pictures.Add(newPicInfo);
                    }
                }
            }

            bool found;
            // Additional textual fields
            foreach (MetaFieldInfo newMetaInfo in data.AdditionalFields)
            {
                found = false;
                foreach (MetaFieldInfo metaInfo in AdditionalFields)
                {
                    // New MetaFieldInfo tag type+field code+streamNumber+language already exists in current TagData
                    // or new MetaFieldInfo mimics an existing field (added or edited through simplified interface)
                    if (metaInfo.EqualsWithoutZone(newMetaInfo) || metaInfo.EqualsApproximate(newMetaInfo)) 
                    {
                        if (newMetaInfo.MarkedForDeletion) metaInfo.MarkedForDeletion = true; // New MetaFieldInfo is a demand for deletion
                        else
                        {
                            found = true;
                            metaInfo.Value = newMetaInfo.Value;
                            break;
                        }
                    }
                }

                if (!newMetaInfo.MarkedForDeletion && !found) // New MetaFieldInfo type+streamNumber+language does not exist in current TagData
                {
                    AdditionalFields.Add(newMetaInfo);
                }
                else if (!found) // Cannot delete a field that has not been found
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Field code "+newMetaInfo.NativeFieldCode+" cannot be deleted because it has not been found on current TagData.");
                }
            }
        }

        public IDictionary<byte,String> ToMap()
        {
            IDictionary<byte, String> result = new Dictionary<byte, String>();

            // Supported fields only
            // NB : The following block of code determines the order of appearance of fields within written files
            addIfConsistent(Artist, TAG_FIELD_ARTIST, result);
            addIfConsistent(Title, TAG_FIELD_TITLE, result);
            addIfConsistent(Album, TAG_FIELD_ALBUM, result);
            addIfConsistent(RecordingDate, TAG_FIELD_RECORDING_DATE, result);
            addIfConsistent(RecordingYear, TAG_FIELD_RECORDING_YEAR, result);
            addIfConsistent(RecordingDayMonth, TAG_FIELD_RECORDING_DAYMONTH, result);
            addIfConsistent(Genre, TAG_FIELD_GENRE, result);
            addIfConsistent(Composer, TAG_FIELD_COMPOSER, result);
            addIfConsistent(TrackNumber, TAG_FIELD_TRACK_NUMBER, result);
            addIfConsistent(DiscNumber, TAG_FIELD_DISC_NUMBER, result);
            addIfConsistent(Comment, TAG_FIELD_COMMENT, result);
            addIfConsistent(Rating, TAG_FIELD_RATING, result);
            addIfConsistent(OriginalArtist, TAG_FIELD_ORIGINAL_ARTIST, result);
            addIfConsistent(OriginalAlbum, TAG_FIELD_ORIGINAL_ALBUM, result);
            addIfConsistent(Copyright, TAG_FIELD_COPYRIGHT, result);
            addIfConsistent(AlbumArtist, TAG_FIELD_ALBUM_ARTIST, result);
            addIfConsistent(Publisher, TAG_FIELD_PUBLISHER, result);
            addIfConsistent(Conductor, TAG_FIELD_CONDUCTOR, result);
            addIfConsistent(GeneralDescription, TAG_FIELD_GENERAL_DESCRIPTION, result);

            return result;
        }

        private void addIfConsistent(String data, byte id, IDictionary<byte,String> map)
        {
            if (data != null) map[id] = data;
        }

        private string emptyIfZero(string s)
        {
            string result = s;

            if (s != null && s.Equals("0")) result = "";

            return result;
        }

        private IDictionary<PictureInfo,int> generatePicturePositions()
        {
            IDictionary<PictureInfo, int> result = new Dictionary<PictureInfo, int>();

            foreach (PictureInfo picInfo in Pictures)
            {
                if (result.ContainsKey(picInfo)) result[picInfo] = Math.Max(result[picInfo],picInfo.Position);
                else result.Add(picInfo, picInfo.Position);
            }

            return result;
        }
    }
}
