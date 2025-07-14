using ATL.AudioData;
using Commons;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static ATL.TagData;

namespace ATL
{
    /// <summary>
    /// Represents a set of metadata (abstract; use TagHolder if you're looking for the instanciable class)
    /// </summary>
    public abstract class MetaDataHolder : IMetaData, IEquatable<MetaDataHolder>
    {
        /// <summary>
        /// Prefix to add to AdditionalFields value to mark it as a date
        /// </summary>
        public const string DATETIME_PREFIX = "[DateTime]";

        /// <summary>
        /// Reference metadata (for internal use only)
        /// </summary>
        internal readonly TagData tagData;

        /// <summary>
        /// Implemented tag type
        /// </summary>
        /// <returns></returns>
        protected abstract MetaDataIOFactory.TagType getImplementedTagType();


        /// <summary>
        /// Default constructor
        /// </summary>
        protected MetaDataHolder()
        {
            tagData = new TagData();
        }

        /// <summary>
        /// Instanciate a new MetaDataHolder populated with the given TagData
        /// </summary>
        /// <param name="tagData">Data to use to populate the new instance</param>
        protected MetaDataHolder(TagData tagData)
        {
            this.tagData = tagData;
        }

        /// <inheritdoc/>
        public string Title
        {
            get => Utils.ProtectValue(tagData[Field.TITLE]);
            set => tagData.IntegrateValue(Field.TITLE, value);
        }
        /// <inheritdoc/>
        public string Artist
        {
            get => Utils.ProtectValue(tagData[Field.ARTIST]);
            set => tagData.IntegrateValue(Field.ARTIST, value);
        }
        /// <inheritdoc/>
        public string Composer
        {
            get => Utils.ProtectValue(tagData[Field.COMPOSER]);
            set => tagData.IntegrateValue(Field.COMPOSER, value);
        }
        /// <inheritdoc/>
        public string Comment
        {
            get => Utils.ProtectValue(tagData[Field.COMMENT]);
            set => tagData.IntegrateValue(Field.COMMENT, value);
        }
        /// <inheritdoc/>
        public string Genre
        {
            get => Utils.ProtectValue(tagData[Field.GENRE]);
            set => tagData.IntegrateValue(Field.GENRE, value);
        }
        /// <inheritdoc/>
        public string TrackNumber
        {
            get
            {
                if (tagData[Field.TRACK_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackNumberStr(tagData[Field.TRACK_NUMBER_TOTAL]);
                return TrackUtils.ExtractTrackNumberStr(tagData[Field.TRACK_NUMBER]);
            }
            set => tagData.IntegrateValue(Field.TRACK_NUMBER, value);
        }
        /// <inheritdoc/>
        public ushort TrackTotal
        {
            get
            {
                if (tagData[Field.TRACK_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackTotal(tagData[Field.TRACK_NUMBER_TOTAL]);
                if (Utils.IsNumeric(tagData[Field.TRACK_TOTAL]))
                    return ushort.Parse(tagData[Field.TRACK_TOTAL]);
                return TrackUtils.ExtractTrackTotal(tagData[Field.TRACK_NUMBER]);
            }
            set => tagData.IntegrateValue(Field.TRACK_TOTAL, value.ToString());
        }
        /// <inheritdoc/>
        public ushort DiscNumber
        {
            get
            {
                if (tagData[Field.DISC_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackNumber(tagData[Field.DISC_NUMBER_TOTAL]);
                return TrackUtils.ExtractTrackNumber(tagData[Field.DISC_NUMBER]);
            }
            set => tagData.IntegrateValue(Field.DISC_NUMBER, value.ToString());
        }
        /// <inheritdoc/>
        public ushort DiscTotal
        {
            get
            {
                if (tagData[Field.DISC_NUMBER_TOTAL] != null)
                    return TrackUtils.ExtractTrackTotal(tagData[Field.DISC_NUMBER_TOTAL]);
                if (Utils.IsNumeric(tagData[Field.DISC_TOTAL]))
                    return ushort.Parse(tagData[Field.DISC_TOTAL]);
                return TrackUtils.ExtractTrackTotal(tagData[Field.DISC_NUMBER]);
            }
            set => tagData.IntegrateValue(Field.DISC_TOTAL, value.ToString());
        }
        /// <inheritdoc/>
        public DateTime Date
        {
            get
            {
                DateTime result = DateTime.MinValue;
                bool success = false;

                // The field may be holding multiple values => split it and order it by size desc
                string[] dateValues = Array.Empty<string>();
                if (tagData.hasKey(Field.RECORDING_DATE))
                {
                    dateValues = tagData[Field.RECORDING_DATE]
                        .Split(Settings.InternalValueSeparator)
                        .OrderByDescending(s => s.Length)
                        .ToArray();

                    // First try with a proper Recording date field
                    foreach (var dateValue in dateValues)
                    {
                        success = DateTime.TryParse(Utils.ProtectValue(dateValue), out result);
                        if (success) break;
                    }
                }

                // If not, try to assemble year and dateMonth (e.g. ID3v2)
                if (!success)
                {
                    string dayMonth = Utils.ProtectValue(tagData[Field.RECORDING_DAYMONTH]);
                    string year = Utils.ProtectValue(tagData[Field.RECORDING_YEAR]);
                    if (4 == dayMonth.Length && 4 == year.Length)
                    {
                        StringBuilder dateTimeBuilder = new StringBuilder();
                        dateTimeBuilder.Append(year).Append('-');
                        dateTimeBuilder.Append(dayMonth.AsSpan(2, 2)).Append('-');
                        dateTimeBuilder.Append(dayMonth[..2]);
                        string time = Utils.ProtectValue(tagData[Field.RECORDING_TIME]); // Try to add time if available
                        if (time.Length >= 4)
                        {
                            dateTimeBuilder.Append('T');
                            dateTimeBuilder.Append(time[..2]).Append(':');
                            dateTimeBuilder.Append(time.AsSpan(2, 2)).Append(':');
                            dateTimeBuilder.Append((6 == time.Length) ? time.Substring(4, 2) : "00");
                        }
                        success = DateTime.TryParse(dateTimeBuilder.ToString(), out result);
                    }

                    // Year only
                    if (!success)
                    {
                        if (!Utils.TryExtractDateTimeFromDigits(year, out var dateTimeFromYear))
                        {
                            // ...then try with RecordingDate
                            foreach (var dateValue in dateValues)
                            {
                                if (Utils.TryExtractDateTimeFromDigits(dateValue, out dateTimeFromYear)) break;
                            }
                        }

                        // We have a valid value !
                        if (!dateTimeFromYear.HasValue) return result;

                        result = dateTimeFromYear.Value;
                        string time = Utils.ProtectValue(tagData[Field.RECORDING_TIME]); // Try to add time if available
                        if (time.Length < 4 || !Utils.IsNumeric(time)) return result;

                        result.AddHours(int.Parse(time[..2]));
                        result.AddMinutes(int.Parse(time.AsSpan(2, 2)));
                        if (6 == time.Length) result.AddSeconds(int.Parse(time.Substring(4, 2)));
                    }
                }
                return result;
            }
            set
            {
                tagData.IntegrateValue(Field.RECORDING_DATE, value > DateTime.MinValue ? TrackUtils.FormatISOTimestamp(value) : null);
                tagData.IntegrateValue(Field.RECORDING_YEAR, value > DateTime.MinValue ? value.Year.ToString() : null);
                tagData.IntegrateValue(Field.RECORDING_DATE_OR_YEAR, value > DateTime.MinValue ? value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : null);
                tagData.IntegrateValue(Field.RECORDING_DAYMONTH, value > DateTime.MinValue ? value.ToString("ddMM", CultureInfo.InvariantCulture) : null);
                tagData.IntegrateValue(Field.RECORDING_TIME, value > DateTime.MinValue ? value.ToString("HHmm", CultureInfo.InvariantCulture) : null);
            }
        }
        /// <inheritdoc/>
        public bool IsDateYearOnly
        {
            get
            {
                if (Utils.ProtectValue(tagData[Field.RECORDING_DATE]).Length > 4) return false;
                if (Utils.ProtectValue(tagData[Field.RECORDING_DATE_OR_YEAR]).Length > 4) return false;
                return Utils.ProtectValue(tagData[Field.RECORDING_YEAR]).Length > 0;
            }
        }
        /// <inheritdoc/>
        public DateTime OriginalReleaseDate
        {
            get
            {
                // First try with a proper date field
                if (DateTime.TryParse(Utils.ProtectValue(tagData[Field.ORIG_RELEASE_DATE]), out var result)) return result;
                bool success = false;
                string year = Utils.ProtectValue(tagData[Field.ORIG_RELEASE_YEAR]);

                // ...then with OriginalReleaseDate
                if (!Utils.TryExtractDateTimeFromDigits(year, out var dateTimeFromYear))
                {
                    year = Utils.ProtectValue(tagData[Field.ORIG_RELEASE_DATE]);
                    Utils.TryExtractDateTimeFromDigits(year, out dateTimeFromYear);
                }
                // We have a valid value !
                if (dateTimeFromYear.HasValue)
                {
                    result = dateTimeFromYear.Value;
                    success = true;
                }
                if (!success) result = DateTime.MinValue;
                return result;
            }
            set
            {
                tagData.IntegrateValue(Field.ORIG_RELEASE_DATE, value > DateTime.MinValue ? TrackUtils.FormatISOTimestamp(value) : null);
                tagData.IntegrateValue(Field.ORIG_RELEASE_YEAR, value > DateTime.MinValue ? value.Year.ToString() : null);
            }
        }
        /// <inheritdoc/>
        public bool IsOriginalReleaseDateYearOnly
        {
            get
            {
                if (Utils.ProtectValue(tagData[Field.ORIG_RELEASE_DATE]).Length > 4) return false;
                return Utils.ProtectValue(tagData[Field.ORIG_RELEASE_YEAR]).Length > 0;
            }
        }
        /// <inheritdoc/>
        public DateTime PublishingDate
        {
            get
            {
                DateTime result;
                if (!DateTime.TryParse(tagData[Field.PUBLISHING_DATE], out result))
                    result = DateTime.MinValue;
                return result;
            }
            set => tagData.IntegrateValue(Field.PUBLISHING_DATE, (value > DateTime.MinValue) ? TrackUtils.FormatISOTimestamp(value) : null);
        }
        /// <inheritdoc/>
        public string Album
        {
            get => Utils.ProtectValue(tagData[Field.ALBUM]);
            set => tagData.IntegrateValue(Field.ALBUM, value);
        }
        /// <inheritdoc/>
        public float? Popularity
        {
            get
            {
                if (!float.TryParse(tagData[Field.RATING], out var result))
                {
                    if (Settings.NullAbsentValues) return null;
                    return 0f;
                }
                return result;
            }
            set => tagData.IntegrateValue(Field.RATING, (null == value) ? null : value.ToString());
        }
        /// <inheritdoc/>
        public string Copyright
        {
            get => Utils.ProtectValue(tagData[Field.COPYRIGHT]);
            set => tagData.IntegrateValue(Field.COPYRIGHT, value);
        }
        /// <inheritdoc/>
        public string Language
        {
            get => Utils.ProtectValue(tagData[Field.LANGUAGE]);
            set => tagData.IntegrateValue(Field.LANGUAGE, value);
        }
        /// <inheritdoc/>
        public string OriginalArtist
        {
            get => Utils.ProtectValue(tagData[Field.ORIGINAL_ARTIST]);
            set => tagData.IntegrateValue(Field.ORIGINAL_ARTIST, value);
        }
        /// <inheritdoc/>
        public string OriginalAlbum
        {
            get => Utils.ProtectValue(tagData[Field.ORIGINAL_ALBUM]);
            set => tagData.IntegrateValue(Field.ORIGINAL_ALBUM, value);
        }
        /// <inheritdoc/>
        public string GeneralDescription
        {
            get => Utils.ProtectValue(tagData[Field.GENERAL_DESCRIPTION]);
            set => tagData.IntegrateValue(Field.GENERAL_DESCRIPTION, value);
        }
        /// <inheritdoc/>
        public string Publisher
        {
            get => Utils.ProtectValue(tagData[Field.PUBLISHER]);
            set => tagData.IntegrateValue(Field.PUBLISHER, value);
        }
        /// <inheritdoc/>
        public string AlbumArtist
        {
            get => Utils.ProtectValue(tagData[Field.ALBUM_ARTIST]);
            set => tagData.IntegrateValue(Field.ALBUM_ARTIST, value);
        }
        /// <inheritdoc/>
        public string Conductor
        {
            get => Utils.ProtectValue(tagData[Field.CONDUCTOR]);
            set => tagData.IntegrateValue(Field.CONDUCTOR, value);
        }
        /// <inheritdoc/>
        public string Lyricist
        {
            get => Utils.ProtectValue(tagData[Field.LYRICIST]);
            set => tagData.IntegrateValue(Field.LYRICIST, value);
        }
        /// <inheritdoc/>
        public string InvolvedPeople
        {
            get => Utils.ProtectValue(tagData[Field.INVOLVED_PEOPLE]);
            set => tagData.IntegrateValue(Field.INVOLVED_PEOPLE, value);
        }

        /// <inheritdoc/>
        public string ProductId
        {
            get => Utils.ProtectValue(tagData[Field.PRODUCT_ID]);
            set => tagData.IntegrateValue(Field.PRODUCT_ID, value);
        }
        /// <inheritdoc/>
        public string ISRC
        {
            get => Utils.ProtectValue(tagData[Field.ISRC]);
            set => tagData.IntegrateValue(Field.ISRC, value);
        }
        /// <inheritdoc/>
        public string CatalogNumber
        {
            get => Utils.ProtectValue(tagData[Field.CATALOG_NUMBER]);
            set => tagData.IntegrateValue(Field.CATALOG_NUMBER, value);
        }
        /// <inheritdoc/>
        public string AudioSourceUrl
        {
            get => Utils.ProtectValue(tagData[Field.AUDIO_SOURCE_URL]);
            set => tagData.IntegrateValue(Field.AUDIO_SOURCE_URL, value);
        }

        /// <inheritdoc/>
        public string SortAlbum
        {
            get => Utils.ProtectValue(tagData[Field.SORT_ALBUM]);
            set => tagData.IntegrateValue(Field.SORT_ALBUM, value);
        }
        /// <inheritdoc/>
        public string SortAlbumArtist
        {
            get => Utils.ProtectValue(tagData[Field.SORT_ALBUM_ARTIST]);
            set => tagData.IntegrateValue(Field.SORT_ALBUM_ARTIST, value);
        }
        /// <inheritdoc/>
        public string SortArtist
        {
            get => Utils.ProtectValue(tagData[Field.SORT_ARTIST]);
            set => tagData.IntegrateValue(Field.SORT_ARTIST, value);
        }
        /// <inheritdoc/>
        public string SortTitle
        {
            get => Utils.ProtectValue(tagData[Field.SORT_TITLE]);
            set => tagData.IntegrateValue(Field.SORT_TITLE, value);
        }
        /// <inheritdoc/>
        public string Group
        {
            get => Utils.ProtectValue(tagData[Field.GROUP]);
            set => tagData.IntegrateValue(Field.GROUP, value);
        }
        /// <inheritdoc/>
        public string SeriesTitle
        {
            get => Utils.ProtectValue(tagData[Field.SERIES_TITLE]);
            set => tagData.IntegrateValue(Field.SERIES_TITLE, value);
        }
        /// <inheritdoc/>
        public string SeriesPart
        {
            get => Utils.ProtectValue(tagData[Field.SERIES_PART]);
            set => tagData.IntegrateValue(Field.SERIES_PART, value);
        }
        /// <inheritdoc/>
        public string LongDescription
        {
            get => Utils.ProtectValue(tagData[Field.LONG_DESCRIPTION]);
            set => tagData.IntegrateValue(Field.LONG_DESCRIPTION, value);
        }
        /// <inheritdoc/>
        public float? BPM
        {
            get
            {
                var dbl = Utils.ParseDouble(tagData[Field.BPM]);
                if (!double.IsNaN(dbl)) return (float)dbl;
                if (Settings.NullAbsentValues) return null;
                return 0;
            }
            set => tagData.IntegrateValue(Field.BPM, (null == value) ? null : value.ToString());
        }
        /// <inheritdoc/>
        public string EncodedBy
        {
            get => Utils.ProtectValue(tagData[Field.ENCODED_BY]);
            set => tagData.IntegrateValue(Field.ENCODED_BY, value);
        }
        /// <inheritdoc/>
        public string Encoder
        {
            get => Utils.ProtectValue(tagData[Field.ENCODER]);
            set => tagData.IntegrateValue(Field.ENCODER, value);
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
                    if (!result.ContainsKey(fieldInfo.NativeFieldCode) && !fieldInfo.MarkedForDeletion) result.Add(fieldInfo.NativeFieldCode, fieldInfo.Value);
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
                    && -1 == streamNumber || streamNumber == fieldInfo.StreamNumber
                    && (0 == language.Length || language.Equals(fieldInfo.Language))
                    )
                {
                    result.Add(fieldInfo);
                }
            }

            return result;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IList<ChapterInfo> Chapters
        {
            get => tagData.Chapters != null ? new List<ChapterInfo>(tagData.Chapters) : new List<ChapterInfo>();
            set => tagData.Chapters = value;
        }

        /// <inheritdoc/>
        public string ChaptersTableDescription
        {
            get => Utils.ProtectValue(tagData[Field.CHAPTERS_TOC_DESCRIPTION]);
            set => tagData.IntegrateValue(Field.CHAPTERS_TOC_DESCRIPTION, value);
        }

        /// <inheritdoc/>
        public IList<LyricsInfo> Lyrics
        {
            get => tagData.Lyrics != null ? new List<LyricsInfo>(tagData.Lyrics) : new List<LyricsInfo>();
            set => tagData.Lyrics = value;
        }

        /// <inheritdoc/>
        public virtual IList<Format> MetadataFormats => throw new NotImplementedException();


        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return !ReferenceEquals(obj, null) && obj.GetType() == this.GetType() &&
                   ((MetaDataHolder)obj).Equals(this);
        }

        /// <inheritdoc />
        public bool Equals(MetaDataHolder other)
        {
            return null != other && tagData.Equals(other.tagData);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return tagData.GetHashCode();
        }
    }
}