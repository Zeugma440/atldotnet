using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATL
{
    /// <summary>
    /// Basic metadata fields container
    /// 
    /// TagData aims at staying a basic, universal container, without any Property accessor layer nor any field interpretation logic
    /// </summary>
    public class TagData
    {
        public enum Field
        {
            NO_FIELD = -1,
            GENERAL_DESCRIPTION = 0,
            TITLE = 1,
            ARTIST = 2,
            COMPOSER = 3,
            COMMENT = 4,
            GENRE = 5,
            ALBUM = 6,
            RECORDING_YEAR = 7,
            RECORDING_DATE = 8,
            RECORDING_YEAR_OR_DATE = 9, // Alternate to RECORDING_YEAR and RECORDING_DATE where the field may contain both
            RECORDING_DAYMONTH = 10,
            RECORDING_TIME = 11,
            TRACK_NUMBER = 12,
            DISC_NUMBER = 13,
            RATING = 14,
            ORIGINAL_ARTIST = 15,
            ORIGINAL_ALBUM = 16,
            COPYRIGHT = 17,
            ALBUM_ARTIST = 18,
            PUBLISHER = 19,
            CONDUCTOR = 20,
            TRACK_TOTAL = 21,
            TRACK_NUMBER_TOTAL = 22, // Alternate to TRACK_NUMBER and TRACK_TOTAL where both are in the same field
            DISC_TOTAL = 23,
            DISC_NUMBER_TOTAL = 24, // Alternate to DISC_NUMBER and DISC_TOTAL where both are in the same field
            CHAPTERS_TOC_DESCRIPTION = 25,
            LYRICS_UNSYNCH = 26,
            LYRICS_SYNCH = 27,
            PUBLISHING_DATE = 28,
            PRODUCT_ID = 29
        }

        private static readonly ICollection<Field> numericFields = new HashSet<Field>() {
            Field.RECORDING_YEAR, Field.RECORDING_DATE, Field.RECORDING_DAYMONTH, Field.RECORDING_TIME, Field.TRACK_NUMBER, Field.DISC_NUMBER, Field.RATING, Field.TRACK_TOTAL, Field.TRACK_NUMBER_TOTAL, Field.DISC_TOTAL, Field.DISC_NUMBER_TOTAL, Field.PUBLISHING_DATE
        };

        /// <summary>
        /// Chapters 
        /// NB : The whole chapter list is processed as a whole
        /// </summary>
        public IList<ChapterInfo> Chapters = null;

        /// <summary>
        /// Lyrics
        /// </summary>
        public LyricsInfo Lyrics = null;


        /// <summary>
        /// Embedded pictures
        /// NB : Each entry is processed as a metadata field on its own
        /// </summary>
        public IList<PictureInfo> Pictures;

        public IDictionary<Field, string> Fields;

        /// <summary>
        /// Additional fields = non-classic fields
        /// NB : Each entry is processed as a metadata field on its own
        /// </summary>
        public IList<MetaFieldInfo> AdditionalFields;

        /// <summary>
        /// > 0 if Track field is formatted with leading zeroes over X digits
        /// </summary>
        public int TrackDigitsForLeadingZeroes = 0;
        /// <summary>
        /// > 0 if Disc field is formatted with leading zeroes over X digits
        /// </summary>
        public int DiscDigitsForLeadingZeroes = 0;

        /// <summary>
        /// Current difference between written data size vs. initial data size
        /// Used to calculate padding size variation when FileStructureHelper is unavailable
        /// TODO - this is ugly, remove that when FLAC has been redesigned to use a FileStructureHelper
        /// </summary>
        public long DataSizeDelta = 0;

        /// <summary>
        /// Size of padding area, if any (target size of padding area, if used as input)
        /// </summary>
        public long PaddingSize = -1;

        /// <summary>
        /// Duration of audio track, in milliseconds
        /// </summary>
        public double DurationMs = 0;

#pragma warning restore S1104 // Fields should not have public accessibility

        /// <summary>
        /// Construct an empty TagData
        /// </summary>
        public TagData()
        {
            Fields = new Dictionary<Field, string>();
            AdditionalFields = new List<MetaFieldInfo>();
            Pictures = new List<PictureInfo>();
        }

        /// <summary>
        /// Construct a TagData by copying the properties of the given TagData
        /// </summary>
        /// <param name="tagData">TagData to copy properties from</param>
        public TagData(TagData tagData)
        {
            Fields = new Dictionary<Field, string>();
            AdditionalFields = new List<MetaFieldInfo>();
            Pictures = new List<PictureInfo>();

            IntegrateValues(tagData);
        }

        private bool isNumeric(Field f) { return numericFields.Contains(f); }

        /// <summary>
        /// Stores a 'classic' metadata value into current TagData object according to its key
        /// 
        /// NB : This method cannot be used to store non-classic fields; use tagData.AdditionalFields instead
        /// </summary>
        /// <param name="key">Identifier describing the metadata to store (see TagData public consts)</param>
        /// <param name="value">Value of the metadata to store</param>
        public void IntegrateValue(Field key, string value)
        {
            if (null == value)
            {
                Fields.Remove(key);
            }
            else if (key == Field.LYRICS_UNSYNCH)
            {
                if (null == Lyrics) Lyrics = new LyricsInfo();
                Lyrics.UnsynchronizedLyrics = value;
            }
            else if (key == Field.RECORDING_YEAR_OR_DATE)
            {
                if (value.Length < 5) Fields[Field.RECORDING_YEAR] = emptyIfZero(value);
                else Fields[Field.RECORDING_DATE] = emptyIfZero(value);
            }
            else
            {
                Fields[key] = isNumeric(key) ? emptyIfZero(value) : value;
            }
        }

        /// <summary>
        /// Merge given TagData object with current TagData object
        /// </summary>
        /// <param name="targetData">TagData object to merge</param>
        /// <param name="integratePictures">Set to true to merge picture information (default : true)</param>
        /// <param name="mergeAdditionalData">Set to true to merge additional (i.e. non-TagData) fields (default : true)</param>
        public void IntegrateValues(TagData targetData, bool integratePictures = true, bool mergeAdditionalData = true)
        {
            // String values
            IDictionary<Field, string> newData = targetData.ToMap();
            foreach (KeyValuePair<Field, string> kvp in newData) IntegrateValue(kvp.Key, kvp.Value);

            // Force to input value, if any
            if (targetData.PaddingSize > -1) PaddingSize = targetData.PaddingSize; else PaddingSize = -1;

            // Pictures
            if (integratePictures && targetData.Pictures != null)
            {
                IList<PictureInfo> resultPictures = new List<PictureInfo>();

                foreach (PictureInfo newPicInfo in targetData.Pictures)
                {
                    newPicInfo.ComputePicHash();
                    int candidatePosition = 0;
                    bool added = false;
                    foreach (PictureInfo picInfo in Pictures)
                    {
                        picInfo.ComputePicHash();
                        // New PictureInfo picture type already exists in current TagData
                        if (picInfo.EqualsProper(newPicInfo))
                        {
                            // New PictureInfo is a demand for deletion
                            if (newPicInfo.MarkedForDeletion)
                            {
                                picInfo.MarkedForDeletion = true;
                                added = true;
                                resultPictures.Add(picInfo);
                            }
                            else
                            {
                                candidatePosition = Math.Max(candidatePosition, picInfo.Position);
                                // New picture is an existing picture -> keep the existing one
                                if (picInfo.PictureHash > 0 && picInfo.PictureHash == newPicInfo.PictureHash)
                                {
                                    added = true;
                                    PictureInfo targetPicture = picInfo;
                                    if (newPicInfo.Description != picInfo.Description)
                                    {
                                        targetPicture = new PictureInfo(picInfo, false);
                                        targetPicture.Description = newPicInfo.Description;
                                    }
                                    resultPictures.Add(targetPicture);
                                    break;
                                }
                            }
                        }
                    }
                    // New PictureInfo is a X-th picture of the same type
                    if (!added)
                    {
                        newPicInfo.Position = candidatePosition + 1;
                        resultPictures.Add(newPicInfo);
                    }
                }

                // Eventually add all existing pictures that are completely absent from target pictures
                // in the _beginning_, as they were there initially
                for (int i = Pictures.Count - 1; i >= 0; i--)
                {
                    bool found = false;
                    foreach (PictureInfo picInfo in targetData.Pictures)
                    {
                        if (picInfo.EqualsProper(Pictures[i]))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) resultPictures.Insert(0, Pictures[i]);
                }
                Pictures = resultPictures;
            }

            // Additional textual fields
            if (mergeAdditionalData)
            {
                foreach (MetaFieldInfo newMetaInfo in targetData.AdditionalFields)
                {
                    bool found = false;
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
                    else if (newMetaInfo.MarkedForDeletion && !found) // Cannot delete a field that has not been found
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Field code " + newMetaInfo.NativeFieldCode + " cannot be deleted because it has not been found on current TagData.");
                    }
                }
            }
            else
            {
                AdditionalFields = new List<MetaFieldInfo>(targetData.AdditionalFields);
            }

            // Chapters, processed as a whole
            if (targetData.Chapters != null)
            {
                // Sending an existing but empty chapter list counts as a "marked for deletion"
                if (Chapters != null)
                {
                    Chapters.Clear();
                }
                else
                {
                    Chapters = new List<ChapterInfo>();
                }

                foreach (ChapterInfo chapter in targetData.Chapters)
                {
                    Chapters.Add(new ChapterInfo(chapter));
                }
            }

            if (targetData.Lyrics != null)
                Lyrics = new LyricsInfo(targetData.Lyrics);

            DurationMs = targetData.DurationMs;
        }

        /// <summary>
        /// Indicate whether the current TagData stores a value for the given ATL field code
        /// </summary>
        /// <param name="id">Field code to search for</param>
        /// <returns>True if the current TagData stores a value for the given ATL field code; false if not</returns>
        public bool hasKey(Field id)
        {
            return ToMap().ContainsKey(id);
        }

        /// <summary>
        /// Indexer
        /// </summary>
        /// <param name="index">ATL field code to search for</param>
        /// <returns>Value associated with the given ATL field code</returns>
        public string this[Field index]
        {
            get => Fields.ContainsKey(index) ? Fields[index] : null;
        }

        /// <summary>
        /// Convert non-null 'classic' fields values into a properties Map
        /// 
        /// NB : Additional fields, pictures and chapters won't be part of the Map
        /// </summary>
        /// <returns>Map containing all 'classic' metadata fields</returns>
        public IDictionary<Field, string> ToMap()
        {
            IDictionary<Field, string> result = new Dictionary<Field, string>();

            foreach (KeyValuePair<Field, string> kvp in Fields) result[kvp.Key] = kvp.Value;

            if (result.ContainsKey(Field.RECORDING_YEAR_OR_DATE))
            {
                string recYearOrDate = result[Field.RECORDING_YEAR_OR_DATE];
                if (null == result[Field.RECORDING_YEAR]) result[Field.RECORDING_YEAR] = recYearOrDate;
                if (null == result[Field.RECORDING_DATE]) result[Field.RECORDING_DATE] = recYearOrDate;
            }
            else
            {
                if (result.ContainsKey(Field.RECORDING_YEAR)) result[Field.RECORDING_YEAR_OR_DATE] = result[Field.RECORDING_YEAR];
                else if (result.ContainsKey(Field.RECORDING_DATE)) result[Field.RECORDING_YEAR_OR_DATE] = result[Field.RECORDING_DATE];
            }


            if (Lyrics != null && Lyrics.UnsynchronizedLyrics != null) result[Field.LYRICS_UNSYNCH] = Lyrics.UnsynchronizedLyrics;

            return result;
        }

        /// <summary>
        /// Clear all values stored in TagData object
        /// </summary>
        public void Clear()
        {
            Pictures.Clear();
            Fields.Clear();
            AdditionalFields.Clear();
            if (Chapters != null) Chapters.Clear();
            if (Lyrics != null) Lyrics.Clear();

            TrackDigitsForLeadingZeroes = 0;
            DiscDigitsForLeadingZeroes = 0;

            PaddingSize = -1;
            DurationMs = 0;
        }

        /// <summary>
        /// Cleanup field values that need to be reformatted : track and disc numbers, chapter data
        /// </summary>
        public void Cleanup()
        {
            if (Fields.ContainsKey(Field.TRACK_NUMBER))
            {
                string trackNumber = Fields[Field.TRACK_NUMBER];
                string trackTotal = null;
                string trackNumberTotal;
                if (trackNumber.Contains("/"))
                {
                    trackNumberTotal = trackNumber;
                    string[] parts = trackNumber.Split('/');
                    trackNumber = parts[0];
                    trackTotal = parts[1];
                }
                else if (Utils.IsNumeric(trackNumber))
                {
                    trackNumberTotal = trackNumber;
                    if (Fields.ContainsKey(Field.TRACK_TOTAL))
                    {
                        trackTotal = Fields[Field.TRACK_TOTAL];
                        if (Utils.IsNumeric(trackTotal)) trackNumberTotal += "/" + trackTotal;
                    }
                }
                else
                {
                    trackNumberTotal = "";
                }
                Fields[Field.TRACK_NUMBER] = trackNumber;
                IntegrateValue(Field.TRACK_TOTAL, trackTotal);
                Fields[Field.TRACK_NUMBER_TOTAL] = trackNumberTotal;
            }

            if (Fields.ContainsKey(Field.DISC_NUMBER))
            {
                string discNumber = Fields[Field.DISC_NUMBER];
                string discTotal = null;
                string discNumberTotal;
                if (discNumber.Contains("/"))
                {
                    discNumberTotal = discNumber;
                    string[] parts = discNumber.Split('/');
                    discNumber = parts[0];
                    discTotal = parts[1];
                }
                else if (Utils.IsNumeric(discNumber))
                {
                    discNumberTotal = discNumber;
                    if (Fields.ContainsKey(Field.DISC_TOTAL))
                    {
                        discTotal = Fields[Field.DISC_TOTAL];
                        if (Utils.IsNumeric(discTotal)) discNumberTotal += "/" + discTotal;
                    }
                }
                else
                {
                    discNumberTotal = "";
                }
                Fields[Field.DISC_NUMBER] = discNumber;
                IntegrateValue(Field.DISC_TOTAL, discTotal);
                Fields[Field.DISC_NUMBER_TOTAL] = discNumberTotal;
            }

            if (Chapters != null && Chapters.Count > 0)
            {
                // Sort by start offset or time
                if (Chapters[0].UseOffset)
                    Chapters = Chapters.OrderBy(chapter => chapter.StartOffset).ToList();
                else
                    Chapters = Chapters.OrderBy(chapter => chapter.StartTime).ToList();

                // Auto-fill end offsets or times except for final chapter
                ChapterInfo previousChapter = null;
                foreach (ChapterInfo chapter in Chapters)
                {
                    if (previousChapter != null)
                    {
                        if (chapter.UseOffset && (0 == previousChapter.EndOffset || previousChapter.EndOffset != chapter.StartOffset)) previousChapter.EndOffset = chapter.StartOffset;
                        else if (0 == previousChapter.EndTime || previousChapter.EndTime != chapter.StartTime) previousChapter.EndTime = chapter.StartTime;
                    }
                    previousChapter = chapter;
                }
                // Calculate duration of final chapter with duration of audio
                if (previousChapter != null && 0 == previousChapter.EndTime) previousChapter.EndTime = (uint)Math.Round(DurationMs);
            }
        }

        /// <summary>
        /// Convert given value to empty string ("") if null or zero ("0")
        /// </summary>
        /// <param name="s">Value to convert</param>
        /// <returns>If null or zero ("0"), empty string (""); else initial value</returns>
        private string emptyIfZero(string s)
        {
            string result = s;

            if (!Settings.NullAbsentValues && s != null && s.Equals("0")) result = "";

            return result;
        }
    }
}
