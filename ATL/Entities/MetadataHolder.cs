using ATL.AudioData;
using ATL.AudioData.IO;
using Commons;
using System;
using System.Collections.Generic;
using System.Text;
using static ATL.TagData;

namespace ATL
{
    public abstract class MetadataHolder : IMetaData
    {
        public TagData tagData { get; set; }
        /// <summary>
        /// Return the implemented tag type
        /// </summary>
        /// <returns></returns>
        abstract protected MetaDataIOFactory.TagType getImplementedTagType();
        /// <summary>
        /// Indicate which rating convention to apply (See MetaDataIO.RC_XXX static constants)
        /// </summary>
        /*
        protected virtual byte ratingConvention
        {
            get { return MetaDataIO.RC_ID3v2; }
        }
        */

        public string Title
        {
            get => Utils.ProtectValue(tagData[Field.TITLE]);
            set => tagData.IntegrateValue(Field.TITLE, value);
        }
        public string Artist
        {
            get => Utils.ProtectValue(tagData[Field.ARTIST]);
            set => tagData.IntegrateValue(Field.ARTIST, value);
        }
        public string Composer
        {
            get => Utils.ProtectValue(tagData[Field.COMPOSER]);
            set => tagData.IntegrateValue(Field.COMPOSER, value);
        }
        public string Comment
        {
            get => Utils.ProtectValue(tagData[Field.COMMENT]);
            set => tagData.IntegrateValue(Field.COMMENT, value);
        }
        public string Genre
        {
            get => Utils.ProtectValue(tagData[Field.GENRE]);
            set => tagData.IntegrateValue(Field.GENRE, value);
        }
        public ushort Track
        {
            get
            {
                if (tagData[Field.TRACK_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackNumber(tagData[Field.TRACK_NUMBER_TOTAL]);
                else return TrackUtils.ExtractTrackNumber(tagData[Field.TRACK_NUMBER]);
            }
            set
            {
                tagData.IntegrateValue(Field.TRACK_NUMBER, value.ToString());
            }
        }
        public ushort TrackTotal
        {
            get
            {
                if (tagData[Field.TRACK_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackTotal(tagData[Field.TRACK_NUMBER_TOTAL]);
                else if (Utils.IsNumeric(tagData[Field.TRACK_TOTAL]))
                    return ushort.Parse(tagData[Field.TRACK_TOTAL]);
                else return TrackUtils.ExtractTrackTotal(tagData[Field.TRACK_NUMBER]);
            }
            set
            {
                tagData.IntegrateValue(Field.TRACK_TOTAL, value.ToString());
            }
        }
        public ushort Disc
        {
            get
            {
                if (tagData[Field.DISC_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackNumber(tagData[Field.DISC_NUMBER_TOTAL]);
                else return TrackUtils.ExtractTrackNumber(tagData[Field.DISC_NUMBER]);
            }
            set => tagData.IntegrateValue(Field.DISC_NUMBER, value.ToString());
        }
        public ushort DiscTotal
        {
            get
            {
                if (tagData[Field.DISC_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackTotal(tagData[Field.DISC_NUMBER_TOTAL]);
                else if (Utils.IsNumeric(tagData[Field.DISC_TOTAL]))
                    return ushort.Parse(tagData[Field.DISC_TOTAL]);
                else return TrackUtils.ExtractTrackTotal(tagData[Field.DISC_NUMBER]);
            }
            set => tagData.IntegrateValue(Field.DISC_TOTAL, value.ToString());
        }
        public DateTime Date
        {
            get
            {
                DateTime result;
                if (!DateTime.TryParse(Utils.ProtectValue(tagData[Field.RECORDING_DATE]), out result)) // First try with a proper Recording date field
                {
                    bool success = false;
                    string dayMonth = Utils.ProtectValue(tagData[Field.RECORDING_DAYMONTH]); // If not, try to assemble year and dateMonth (e.g. ID3v2)
                    string year = Utils.ProtectValue(tagData[Field.RECORDING_YEAR]);
                    if (4 == dayMonth.Length && 4 == year.Length)
                    {
                        StringBuilder dateTimeBuilder = new StringBuilder();
                        dateTimeBuilder.Append(year).Append("-");
                        dateTimeBuilder.Append(dayMonth.Substring(2, 2)).Append("-");
                        dateTimeBuilder.Append(dayMonth.Substring(0, 2));
                        string time = Utils.ProtectValue(tagData[Field.RECORDING_TIME]); // Try to add time if available
                        if (time.Length >= 4)
                        {
                            dateTimeBuilder.Append("T");
                            dateTimeBuilder.Append(time.Substring(0, 2)).Append(":");
                            dateTimeBuilder.Append(time.Substring(2, 2)).Append(":");
                            dateTimeBuilder.Append((6 == time.Length) ? time.Substring(4, 2) : "00");
                        }
                        success = DateTime.TryParse(dateTimeBuilder.ToString(), out result);
                    }
                    if (!success) // Year only
                    {
                        if (year.Length != 4) year = Utils.ProtectValue(tagData[Field.RECORDING_DATE]); // ...then with RecordingDate
                        if (4 == year.Length) // We have a year !
                        {
                            StringBuilder dateTimeBuilder = new StringBuilder();
                            dateTimeBuilder.Append(year).Append("-01-01");
                            string time = Utils.ProtectValue(tagData[Field.RECORDING_TIME]); // Try to add time if available
                            if (time.Length >= 4)
                            {
                                dateTimeBuilder.Append("T");
                                dateTimeBuilder.Append(time.Substring(0, 2)).Append(":");
                                dateTimeBuilder.Append(time.Substring(2, 2)).Append(":");
                                dateTimeBuilder.Append((6 == time.Length) ? time.Substring(4, 2) : "00");
                            }
                            success = DateTime.TryParse(dateTimeBuilder.ToString(), out result);
                        }
                    }
                    if (!success) result = DateTime.MinValue;
                }
                return result;
            }
            set
            {
                tagData.IntegrateValue(Field.RECORDING_DATE, TrackUtils.FormatISOTimestamp(value));
                tagData.IntegrateValue(Field.RECORDING_YEAR, value.Year.ToString());
                tagData.IntegrateValue(Field.RECORDING_YEAR_OR_DATE, value.ToShortDateString());
            }
        }
        public DateTime PublishingDate
        {
            get
            {
                DateTime result;
                if (!DateTime.TryParse(tagData[Field.PUBLISHING_DATE], out result))
                    result = DateTime.MinValue;
                return result;
            }
            set
            {
                tagData.IntegrateValue(Field.PUBLISHING_DATE, TrackUtils.FormatISOTimestamp(value));
            }
        }
        public string Album
        {
            get => Utils.ProtectValue(tagData[Field.ALBUM]);
            set => tagData.IntegrateValue(Field.ALBUM, value);
        }
        /*
        public float? Popularity
        {
            get
            {
                float? result = (float?)TrackUtils.DecodePopularity(tagData[Field.RATING], ratingConvention);
                if (!result.HasValue && !Settings.NullAbsentValues) result = 0;
                return result;
            }
            set => tagData.IntegrateValue(Field.RATING, (null == value) ? null : TrackUtils.EncodePopularity((double)value * 5.0, ratingConvention).ToString());
        }
        */
        public float? Popularity
        {
            get
            {
                float result;
                if (!float.TryParse(tagData[Field.RATING], out result))
                {
                    if (Settings.NullAbsentValues) return null;
                    else return 0f;
                }
                return result;
            }
            set => tagData.IntegrateValue(Field.RATING, (null == value) ? null : value.ToString());
        }
        public string Copyright
        {
            get => Utils.ProtectValue(tagData[Field.COPYRIGHT]);
            set => tagData.IntegrateValue(Field.COPYRIGHT, value);
        }
        public string OriginalArtist
        {
            get => Utils.ProtectValue(tagData[Field.ORIGINAL_ARTIST]);
            set => tagData.IntegrateValue(Field.ORIGINAL_ARTIST, value);
        }
        public string OriginalAlbum
        {
            get => Utils.ProtectValue(tagData[Field.ORIGINAL_ALBUM]);
            set => tagData.IntegrateValue(Field.ORIGINAL_ALBUM, value);
        }
        public string GeneralDescription
        {
            get => Utils.ProtectValue(tagData[Field.GENERAL_DESCRIPTION]);
            set => tagData.IntegrateValue(Field.GENERAL_DESCRIPTION, value);
        }
        public string Publisher
        {
            get => Utils.ProtectValue(tagData[Field.PUBLISHER]);
            set => tagData.IntegrateValue(Field.PUBLISHER, value);
        }
        public string AlbumArtist
        {
            get => Utils.ProtectValue(tagData[Field.ALBUM_ARTIST]);
            set => tagData.IntegrateValue(Field.ALBUM_ARTIST, value);
        }
        public string Conductor
        {
            get => Utils.ProtectValue(tagData[Field.CONDUCTOR]);
            set => tagData.IntegrateValue(Field.CONDUCTOR, value);
        }
        public string ProductId
        {
            get => Utils.ProtectValue(tagData[Field.PRODUCT_ID]);
            set => tagData.IntegrateValue(Field.PRODUCT_ID, value);
        }
        /// <summary>
        /// Collection of fields that are not supported by ATL (i.e. not implemented by a getter/setter of MetaDataIO class; e.g. custom fields such as "MOOD")
        /// NB : when querying multi-stream files (e.g. MP4, ASF), this attribute will only return stream-independent properties of the whole file, in the first language available
        /// For detailed, stream-by-stream and language-by-language properties, use GetAdditionalFields
        /// </summary>
        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                IDictionary<string, string> result = new Dictionary<string, string>();

                IList<MetaFieldInfo> additionalFields = GetAdditionalFields(0);
                foreach (MetaFieldInfo fieldInfo in additionalFields)
                {
                    if (!result.ContainsKey(fieldInfo.NativeFieldCode)) result.Add(fieldInfo.NativeFieldCode, fieldInfo.Value);
                }

                return result;
            }
            set
            {
                tagData.AdditionalFields.Clear();
                foreach (KeyValuePair<string, string> kvp in value)
                {
                    tagData.AdditionalFields.Add(new MetaFieldInfo(getImplementedTagType(), kvp.Key, kvp.Value));
                }
            }
        }
        /// <summary>
        /// Get additional fields for the given stream number and language
        /// </summary>
        /// <param name="streamNumber">Stream number to get additional fields for (optional)</param>
        /// <param name="language">Language to get additional fields for (optional)</param>
        /// <returns>Additional fields associated with the given stream and language</returns>
        public IList<MetaFieldInfo> GetAdditionalFields(int streamNumber = -1, string language = "")
        {
            IList<MetaFieldInfo> result = new List<MetaFieldInfo>();

            foreach (MetaFieldInfo fieldInfo in tagData.AdditionalFields)
            {
                if (
                    getImplementedTagType().Equals(fieldInfo.TagType)
                    && (-1 == streamNumber) || (streamNumber == fieldInfo.StreamNumber)
                    && ((0 == language.Length) || language.Equals(fieldInfo.Language))
                    )
                {
                    result.Add(fieldInfo);
                }
            }

            return result;
        }
        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                IList<PictureInfo> result = new List<PictureInfo>();

                foreach (PictureInfo picInfo in tagData.Pictures)
                {
                    if (!picInfo.MarkedForDeletion && (!picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) || (picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && picInfo.TagType.Equals(getImplementedTagType())))) result.Add(picInfo);
                }

                return result;
            }

            set
            {
                tagData.Pictures.Clear();
                foreach (PictureInfo picInfo in value)
                {
                    tagData.Pictures.Add(new PictureInfo(picInfo));
                }
            }
        }
        public IList<ChapterInfo> Chapters
        {
            get
            {
                if (tagData.Chapters != null)
                {
                    return new List<ChapterInfo>(tagData.Chapters);
                }
                else
                {
                    return new List<ChapterInfo>();
                }
            }
            set => tagData.Chapters = value;
        }
        public string ChaptersTableDescription
        {
            get => Utils.ProtectValue(tagData[Field.CHAPTERS_TOC_DESCRIPTION]);
            set => tagData.IntegrateValue(Field.CHAPTERS_TOC_DESCRIPTION, value);
        }
        public LyricsInfo Lyrics
        {
            get
            {
                if (tagData.Lyrics != null)
                {
                    return new LyricsInfo(tagData.Lyrics);
                }
                else
                {
                    return new LyricsInfo();
                }
            }
            set
            {
                tagData.Lyrics = value;
            }
        }

        public virtual IList<Format> MetadataFormats => throw new NotImplementedException();
    }
}