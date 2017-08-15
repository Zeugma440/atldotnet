using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;

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
            public int Position;                           // Position of the picture among pictures of the same generic type / native code (default 1 if the picture is one of its kind)

            public int TagType;                             // Tag type where the picture originates from
            public byte NativePicCode;                      // Native picture code according to TagType convention (byte : e.g. ID3v2)
            public string NativePicCodeStr;                 // Native picture code according to TagType convention (string : e.g. APEtag)

            public byte[] PictureData;                      // Binary picture data

            public bool MarkedForDeletion = false;          // Marked for deletion flag

            // ---------------- CONSTRUCTORS

            public PictureInfo(ImageFormat nativeFormat, PIC_TYPE picType, int tagType, object nativePicCode, int position = 1)
            {
                PicType = picType; NativeFormat = nativeFormat; TagType = tagType; Position = position;
                if (nativePicCode is string)
                {
                    NativePicCodeStr = (string)nativePicCode;
                } else if (nativePicCode is byte)
                {
                    NativePicCode = (byte)nativePicCode;
                } else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "nativePicCode type is not supported; expected byte or string; found "+nativePicCode.GetType().Name);
                }
            }
            public PictureInfo(ImageFormat nativeFormat, PIC_TYPE picType, int position = 1) { PicType = picType; NativeFormat = nativeFormat; Position = position; }
            public PictureInfo(ImageFormat nativeFormat, int tagType, object nativePicCode, int position = 1)
            {
                PicType = PIC_TYPE.Unsupported; NativeFormat = nativeFormat; TagType = tagType; Position = position;
                if (nativePicCode is string)
                {
                    NativePicCodeStr = (string)nativePicCode;
                }
                else if (nativePicCode is byte)
                {
                    NativePicCode = (byte)nativePicCode;
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "nativePicCode type is not supported; expected byte or string; found " + nativePicCode.GetType().Name);
                }
            }
            public PictureInfo(ImageFormat nativeFormat, int tagType, byte nativePicCode, int position = 1) { PicType = PIC_TYPE.Unsupported; NativePicCode = nativePicCode; NativeFormat = nativeFormat; TagType = tagType; Position = position; }
            public PictureInfo(ImageFormat nativeFormat, int tagType, string nativePicCode, int position = 1) { PicType = PIC_TYPE.Unsupported; NativePicCodeStr = nativePicCode; NativeFormat = nativeFormat; TagType = tagType; Position = position; }

            // ---------------- OVERRIDES FOR DICTIONARY STORING

            public override string ToString()
            {
                string result = Utils.BuildStrictLengthString(Position.ToString(), 2, '0', false) + Utils.BuildStrictLengthString(((int)PicType).ToString(), 2, '0', false);

                if (PicType.Equals(PIC_TYPE.Unsupported))
                {
                    if (NativePicCode > 0)
                        result = result + ((100 * TagType) + NativePicCode).ToString();
                    else if ((NativePicCodeStr != null) && (NativePicCodeStr.Length > 0))
                        result = result + (100 * TagType).ToString() + NativePicCodeStr;
                    else
                        LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Non-supported picture detected, but no native picture code found");
                }

                return result;
            }

            public override int GetHashCode()
            {
                return Utils.GetInt32MD5Hash(ToString());
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
            public ushort StreamNumber;                     // Index of the stream the field is attached to
            public string Language;                         // Language the value is written in

            public string Value;                            // Field value
            public string Zone;                             // File zone where the value is supposed to appear (ASF format I'm looking at you...)

            public bool MarkedForDeletion = false;          // Marked for deletion flag

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
                return Utils.GetInt32MD5Hash(ToString());
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
        public const byte TAG_FIELD_PICTURE_DATA            = 13; // TODO ? - Differentiate front, back, CD
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
                case TAG_FIELD_GENERAL_DESCRIPTION:     GeneralDescription = value; break;
                case TAG_FIELD_TITLE:                   Title = value; break;
                case TAG_FIELD_ARTIST:                  Artist= value; break;
                case TAG_FIELD_COMPOSER:                Composer = value; break;
                case TAG_FIELD_COMMENT:                 Comment = value; break;
                case TAG_FIELD_GENRE:                   Genre = value; break;
                case TAG_FIELD_ALBUM:                   Album = value; break;
                case TAG_FIELD_RECORDING_DATE:          RecordingDate = value; break;
                case TAG_FIELD_RECORDING_YEAR:          RecordingYear = value; break;
                case TAG_FIELD_RECORDING_DAYMONTH:      RecordingDayMonth = value; break;
                case TAG_FIELD_TRACK_NUMBER:            TrackNumber = value; break;
                case TAG_FIELD_DISC_NUMBER:             DiscNumber = value; break;
                case TAG_FIELD_RATING:                  Rating = value; break;
                    // Picture data integration has a specific routine
                case TAG_FIELD_ORIGINAL_ARTIST:         OriginalArtist = value; break;
                case TAG_FIELD_ORIGINAL_ALBUM:          OriginalAlbum = value; break;
                case TAG_FIELD_COPYRIGHT:               Copyright = value; break;
                case TAG_FIELD_ALBUM_ARTIST:            AlbumArtist = value; break;
                case TAG_FIELD_PUBLISHER:               Publisher = value; break;
                case TAG_FIELD_CONDUCTOR:               Conductor = value; break;
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

            bool found;
            // Additional textual fields
            foreach (MetaFieldInfo newMetaInfo in data.AdditionalFields)
            {
                found = false;
                foreach (MetaFieldInfo metaInfo in AdditionalFields)
                {
                    if (metaInfo.EqualsWithoutZone(newMetaInfo)) // New MetaFieldInfo field type+streamNumber+language already exists in current TagData
                    {
                        if (newMetaInfo.MarkedForDeletion) metaInfo.MarkedForDeletion = true; // New MetaFieldInfo is a demand for deletion
                        else
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!newMetaInfo.MarkedForDeletion && !found) // New MetaFieldInfo type+streamNumber+language does not exist in current TagData
                {
                    AdditionalFields.Add(newMetaInfo);
                }
                else // New MetaFieldInfo is a X-th field of the same type+streamNumber+language : unsupported
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Field type "+newMetaInfo.NativeFieldCode+" already exists for tag type "+newMetaInfo.TagType+ " on current TagData");
                }
            }
        }

        public IDictionary<byte,String> ToMap()
        {
            IDictionary<byte, String> result = new Dictionary<byte, String>();

            // Supported fields
            addIfConsistent(GeneralDescription, TAG_FIELD_GENERAL_DESCRIPTION, ref result);
            addIfConsistent(Title, TAG_FIELD_TITLE, ref result);
            addIfConsistent(Artist, TAG_FIELD_ARTIST, ref result);
            addIfConsistent(Composer, TAG_FIELD_COMPOSER, ref result);
            addIfConsistent(Comment, TAG_FIELD_COMMENT, ref result);
            addIfConsistent(Genre, TAG_FIELD_GENRE, ref result);
            addIfConsistent(Album, TAG_FIELD_ALBUM, ref result);
            addIfConsistent(RecordingDate, TAG_FIELD_RECORDING_DATE, ref result);
            addIfConsistent(RecordingYear, TAG_FIELD_RECORDING_YEAR, ref result);
            addIfConsistent(RecordingDayMonth, TAG_FIELD_RECORDING_DAYMONTH, ref result);
            addIfConsistent(TrackNumber, TAG_FIELD_TRACK_NUMBER, ref result);
            addIfConsistent(DiscNumber, TAG_FIELD_DISC_NUMBER, ref result);
            addIfConsistent(Rating, TAG_FIELD_RATING, ref result);
            addIfConsistent(OriginalArtist, TAG_FIELD_ORIGINAL_ARTIST, ref result);
            addIfConsistent(OriginalAlbum, TAG_FIELD_ORIGINAL_ALBUM, ref result);
            addIfConsistent(Copyright, TAG_FIELD_COPYRIGHT, ref result);
            addIfConsistent(AlbumArtist, TAG_FIELD_ALBUM_ARTIST, ref result);
            addIfConsistent(Publisher, TAG_FIELD_PUBLISHER, ref result);
            addIfConsistent(Conductor, TAG_FIELD_CONDUCTOR, ref result);

            return result;
        }

        private void addIfConsistent(String data, byte id, ref IDictionary<byte,String> map)
        {
            if (data != null) map[id] = data;
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
