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
    public sealed class TagData : IEquatable<TagData>
    {
        /// <summary>
        /// Standardized metadata fields
        /// </summary>
        public enum Field
        {
            /// <summary>
            /// No field (fallback value when no match is found)
            /// </summary>
            NO_FIELD = -1,
            /// <summary>
            /// General description
            /// </summary>
            GENERAL_DESCRIPTION = 0,
            /// <summary>
            /// Title
            /// </summary>
            TITLE = 1,
            /// <summary>
            /// Artist
            /// </summary>
            ARTIST = 2,
            /// <summary>
            /// Composer
            /// </summary>
            COMPOSER = 3,
            /// <summary>
            /// Comment
            /// </summary>
            COMMENT = 4,
            /// <summary>
            /// Genre
            /// </summary>
            GENRE = 5,
            /// <summary>
            /// Album
            /// </summary>
            ALBUM = 6,
            /// <summary>
            /// Recording year (when target format only supports year)
            /// </summary>
            RECORDING_YEAR = 7,
            /// <summary>
            /// Recording date (when target format supports date)
            /// </summary>
            RECORDING_DATE = 8,
            /// <summary>
            /// Alternate to RECORDING_YEAR and RECORDING_DATE where the field may contain both
            /// </summary>
            RECORDING_DATE_OR_YEAR = 9,
            /// <summary>
            /// Recording day and month
            /// </summary>
            RECORDING_DAYMONTH = 10,
            /// <summary>
            /// Recoding time
            /// </summary>
            RECORDING_TIME = 11,
            /// <summary>
            /// Track number
            /// </summary>
            TRACK_NUMBER = 12,
            /// <summary>
            /// Disc number
            /// </summary>
            DISC_NUMBER = 13,
            /// <summary>
            /// Popularity (rating)
            /// </summary>
            RATING = 14,
            /// <summary>
            /// Original artist
            /// </summary>
            ORIGINAL_ARTIST = 15,
            /// <summary>
            /// Original album
            /// </summary>
            ORIGINAL_ALBUM = 16,
            /// <summary>
            /// Copyright
            /// </summary>
            COPYRIGHT = 17,
            /// <summary>
            /// Album artist
            /// </summary>
            ALBUM_ARTIST = 18,
            /// <summary>
            /// Publisher
            /// </summary>
            PUBLISHER = 19,
            /// <summary>
            /// Conductor
            /// </summary>
            CONDUCTOR = 20,
            /// <summary>
            /// Total number of tracks
            /// </summary>
            TRACK_TOTAL = 21,
            /// <summary>
            /// Alternate to TRACK_NUMBER and TRACK_TOTAL where both are in the same field
            /// </summary>
            TRACK_NUMBER_TOTAL = 22,
            /// <summary>
            /// Total number of discs
            /// </summary>
            DISC_TOTAL = 23,
            /// <summary>
            /// Alternate to DISC_NUMBER and DISC_TOTAL where both are in the same field
            /// </summary>
            DISC_NUMBER_TOTAL = 24,
            /// <summary>
            /// Chapters table of contents description
            /// </summary>
            CHAPTERS_TOC_DESCRIPTION = 25,
            /// <summary>
            /// Unsynchronized lyrics
            /// </summary>
            LYRICS_UNSYNCH = 26,
            /// <summary>
            /// Publishing date
            /// </summary>
            PUBLISHING_DATE = 27,
            /// <summary>
            /// Product ID
            /// </summary>
            PRODUCT_ID = 28,
            /// <summary>
            /// Album sort order
            /// </summary>
            SORT_ALBUM = 29,
            /// <summary>
            /// Album artist sort order
            /// </summary>
            SORT_ALBUM_ARTIST = 30,
            /// <summary>
            /// Artist sort order
            /// </summary>
            SORT_ARTIST = 31,
            /// <summary>
            /// Title sort order
            /// </summary>
            SORT_TITLE = 32,
            /// <summary>
            /// Content group description
            /// </summary>
            GROUP = 33,
            /// <summary>
            /// Series title / Movement name
            /// </summary>
            SERIES_TITLE = 34,
            /// <summary>
            /// Series part / Movement index
            /// </summary>
            SERIES_PART = 35,
            /// <summary>
            /// Long description
            /// </summary>
            LONG_DESCRIPTION = 36,
            /// <summary>
            /// Beats per minute
            /// </summary>
            BPM = 37,
            /// <summary>
            /// Person or organization that encoded the file
            /// </summary>
            ENCODED_BY = 38,
            /// <summary>
            /// Original release year (when target format only supports year)
            /// </summary>
            ORIG_RELEASE_YEAR = 39,
            /// <summary>
            /// Original release date (when target format supports date)
            /// </summary>
            ORIG_RELEASE_DATE = 40,
            /// <summary>
            /// Software that encoded the file, with relevant settings if any
            /// </summary>
            ENCODER = 41,
            /// <summary>
            /// Language
            /// </summary>
            LANGUAGE = 42,
            /// <summary>
            /// International Standard Recording Code (ISRC)
            /// </summary>
            ISRC = 43,
            /// <summary>
            /// Catalog number
            /// </summary>
            CATALOG_NUMBER = 44,
            /// <summary>
            /// Audio source URL
            /// </summary>
            AUDIO_SOURCE_URL = 45,
            /// <summary>
            /// Lyricist
            /// </summary>
            LYRICIST = 46,
            /// <summary>
            /// Mapping between functions (e.g. "producer") and names. Every odd field is a
            /// function and every even is an name or a comma delimited list of names
            /// </summary>
            INVOLVED_PEOPLE = 47
        }

        private static readonly ICollection<Field> numericFields = new HashSet<Field>
        {
            Field.RECORDING_YEAR, Field.RECORDING_DATE, Field.RECORDING_DAYMONTH, Field.RECORDING_TIME, Field.TRACK_NUMBER, Field.DISC_NUMBER, Field.RATING, Field.TRACK_TOTAL, Field.TRACK_NUMBER_TOTAL, Field.DISC_TOTAL, Field.DISC_NUMBER_TOTAL, Field.PUBLISHING_DATE, Field.BPM, Field.ORIG_RELEASE_DATE, Field.ORIG_RELEASE_YEAR
        };

        private static readonly ICollection<Field> nonTagFields = new HashSet<Field>
        {
            Field.CHAPTERS_TOC_DESCRIPTION
        };

        /// <summary>
        /// Chapters 
        /// NB : The whole chapter list is processed as a whole
        /// </summary>
        public IList<ChapterInfo> Chapters { get; set; }

        /// <summary>
        /// Lyrics
        /// </summary>
        public IList<LyricsInfo> Lyrics { get; set; }


        /// <summary>
        /// Embedded pictures
        /// NB : Each entry is processed as a metadata field on its own
        /// </summary>
        public IList<PictureInfo> Pictures { get; set; }

        /// <summary>
        /// All standard fields, stored according to their code
        /// </summary>
        private IDictionary<Field, string> Fields { get; }

        /// <summary>
        /// Additional fields = non-classic fields
        /// NB : Each entry is processed as a metadata field on its own
        /// </summary>
        public IList<MetaFieldInfo> AdditionalFields { get; set; }

        /// <summary>
        /// > 0 if Track field is formatted with leading zeroes over X digits
        /// </summary>
        public int TrackDigitsForLeadingZeroes { get; set; }
        /// <summary>
        /// > 0 if Disc field is formatted with leading zeroes over X digits
        /// </summary>
        public int DiscDigitsForLeadingZeroes { get; set; }

        /// <summary>
        /// Current difference between written data size vs. initial data size
        /// Used to calculate padding size variation when FileStructureHelper is unavailable
        /// TODO - this is ugly, remove that when FLAC has been redesigned to use a FileStructureHelper
        /// </summary>
        public long DataSizeDelta { get; set; } = 0;

        /// <summary>
        /// Size of padding area, if any (target size of padding area, if used as input)
        /// </summary>
        public long PaddingSize { get; set; } = -1;

        /// <summary>
        /// Duration of audio track, in milliseconds
        /// </summary>
        public double DurationMs { get; set; }


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
        /// <param name="supportsSynchronizedLyrics">True if the current tagging system supports synchronized lyrics</param>
        public TagData(TagData tagData, bool supportsSynchronizedLyrics = false)
        {
            Fields = new Dictionary<Field, string>();
            AdditionalFields = new List<MetaFieldInfo>();
            Pictures = new List<PictureInfo>();
            TrackDigitsForLeadingZeroes = tagData.TrackDigitsForLeadingZeroes;
            DiscDigitsForLeadingZeroes = tagData.DiscDigitsForLeadingZeroes;

            IntegrateValues(tagData, supportsSynchronizedLyrics: supportsSynchronizedLyrics);
        }

        /// <summary>
        /// Returns true if there's at least one metadata (usable or not) set; false if nothing is set
        /// </summary>
        public bool Exists()
        {
            if (Chapters is { Count: > 0 }) return true;
            if (Lyrics is { Count: > 0 }) return true;
            if (Pictures.Count > 0) return true;
            if (Fields.Count > 0) return true;
            return AdditionalFields.Count > 0;
        }

        /// <summary>
        /// Return true if any standard metadata field is set and usable
        /// </summary>
        public bool HasUsableField() => Fields.Any(f => f.Value.Length > 0 && !nonTagFields.Contains(f.Key));

        private static bool isNumeric(Field f) { return numericFields.Contains(f); }

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
            else
            {
                switch (key)
                {
                    case Field.LYRICS_UNSYNCH:
                        {
                            Lyrics ??= new List<LyricsInfo>();
                            LyricsInfo info;
                            if (0 == Lyrics.Count)
                            {
                                info = new LyricsInfo();
                                Lyrics.Add(info);
                            }
                            else
                            {
                                info = Lyrics[^1];
                            }
                            info.Parse(value);
                            break;
                        }
                    case Field.RECORDING_DATE_OR_YEAR when 0 == value.Length:
                        Fields[Field.RECORDING_YEAR] = "";
                        Fields[Field.RECORDING_DATE] = "";
                        break;
                    case Field.RECORDING_DATE_OR_YEAR when value.Length < 5:
                        Fields[Field.RECORDING_YEAR] = emptyIfZero(value);
                        break;
                    case Field.RECORDING_DATE_OR_YEAR:
                        {
                            if (!Fields.ContainsKey(Field.RECORDING_DATE))
                                Fields[Field.RECORDING_DATE] = emptyIfZero(value);
                            break;
                        }
                    default:
                        Fields[key] = isNumeric(key) ? emptyIfZero(value) : value;
                        break;
                }
            }
        }

        /// <summary>
        /// Merge given TagData object with current TagData object
        /// </summary>
        /// <param name="targetData">TagData object to merge</param>
        /// <param name="integratePictures">Set to true to merge picture information (default : true)</param>
        /// <param name="mergeAdditionalData">Set to true to merge additional (i.e. non-TagData) fields (default : true)</param>
        /// <param name="supportsSynchronizedLyrics">Indicate if the tagging system supports synchronized lyrics (default : false)</param>
        public void IntegrateValues(TagData targetData, bool integratePictures = true, bool mergeAdditionalData = true, bool supportsSynchronizedLyrics = false)
        {
            // String values
            IDictionary<Field, string> newData = targetData.ToMap(supportsSynchronizedLyrics);
            foreach (KeyValuePair<Field, string> kvp in newData) IntegrateValue(kvp.Key, kvp.Value);

            // Force to input value, if any
            if (targetData.PaddingSize > -1) PaddingSize = targetData.PaddingSize; else PaddingSize = -1;

            // Pictures
            // TODO merge with Track.toTagData ?
            if (integratePictures && targetData.Pictures != null) Pictures = integratePics(targetData.Pictures);

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
                            found = true;
                            if (newMetaInfo.MarkedForDeletion) metaInfo.MarkedForDeletion = true; // New MetaFieldInfo is a demand for deletion
                            else metaInfo.Value = newMetaInfo.Value;
                            break;
                        }
                    }

                    switch (newMetaInfo.MarkedForDeletion)
                    {
                        // New MetaFieldInfo type+streamNumber+language does not exist in current TagData
                        case false when !found:
                            AdditionalFields.Add(newMetaInfo);
                            break;
                        // Cannot delete a field that has not been found
                        case true when !found:
                            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Field code " + newMetaInfo.NativeFieldCode + " cannot be deleted because it has not been found on current TagData.");
                            break;
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
                if (Chapters != null) Chapters.Clear();
                else Chapters = new List<ChapterInfo>();

                foreach (ChapterInfo chapter in targetData.Chapters)
                {
                    Chapters.Add(new ChapterInfo(chapter));
                }
            }

            if (targetData.Lyrics != null)
            {
                // Sending an existing but empty lyrics list counts as a "marked for deletion"
                if (Lyrics != null) Lyrics.Clear();
                else Lyrics = new List<LyricsInfo>();

                foreach (LyricsInfo lyrics in targetData.Lyrics)
                {
                    Lyrics.Add(new LyricsInfo(lyrics));
                }
            }

            DurationMs = targetData.DurationMs;
        }

        private IList<PictureInfo> integratePics(IList<PictureInfo> targetPics)
        {
            IList<PictureInfo> resultPictures = new List<PictureInfo>();
            IList<PictureInfo> targetPictures = new List<PictureInfo>(targetPics.Where(p => !p.MarkedForDeletion));
            IList<KeyValuePair<string, int>> picturePositions = new List<KeyValuePair<string, int>>();

            // Remove contradictory target pictures (same ID with both keep and delete flags)
            var deleteOrders = targetPics.Where(p => p.MarkedForDeletion).ToHashSet();
            foreach (PictureInfo del in deleteOrders)
            {
                foreach (PictureInfo pic in targetPictures.ToHashSet())
                {
                    if (!pic.Equals(del)) continue;
                    targetPictures.Remove(pic);
                    break;
                }
            }

            // Index existing pictures and process remove orders
            foreach (PictureInfo existingPic in Pictures)
            {
                var addExisting = false;
                var deleteFound = false;

                foreach (PictureInfo del in deleteOrders.ToHashSet())
                {
                    if (!existingPic.EqualsProper(del)) continue;
                    deleteOrders.Remove(del); // Can only be used once
                    deleteFound = true;
                    break;
                }

                if (!deleteFound)
                {
                    registerPosition(existingPic, picturePositions);
                    existingPic.ComputePicHash();
                    addExisting = true;
                }

                // Keep existing one and update description if needed
                PictureInfo newPic = targetPictures.FirstOrDefault(tgt => existingPic.EqualsProper(tgt));

                if (newPic != null)
                {
                    newPic.ComputePicHash();
                    if (existingPic.PictureHash > 0 && existingPic.PictureHash == newPic.PictureHash)
                    {
                        PictureInfo addPic = existingPic;
                        if (newPic.Description != existingPic.Description)
                        {
                            addPic = new PictureInfo(existingPic, false)
                            {
                                Description = newPic.Description
                            };
                        }
                        resultPictures.Add(addPic);
                        // Target picture has been "consumed"
                        targetPictures.Remove(newPic);
                        addExisting = false; // Don't add existing as it has been already processed
                    }
                }
                if (addExisting) resultPictures.Add(new PictureInfo(existingPic, false));
            }

            // Add remaining target pictures
            foreach (PictureInfo target in targetPictures)
            {
                var targetPic = new PictureInfo(target, false)
                {
                    Position = 0
                };
                targetPic.Position = nextPosition(targetPic, picturePositions);
                resultPictures.Add(targetPic);
            }

            // Order according to input list
            IList<PictureInfo> orderedResultPictures = new List<PictureInfo>();
            foreach (PictureInfo target in targetPics)
            {
                foreach (PictureInfo tgt in resultPictures.ToHashSet())
                {
                    if (!tgt.EqualsProper(target)) continue;
                    orderedResultPictures.Add(tgt);
                    resultPictures.Remove(tgt);
                    break;
                }
            }
            foreach (PictureInfo target in resultPictures) orderedResultPictures.Add(target);

            return orderedResultPictures;
        }

        private static void registerPosition(PictureInfo picInfo, IList<KeyValuePair<string, int>> positions)
        {
            positions.Add(new KeyValuePair<string, int>(picInfo.ToString(), picInfo.Position));
        }

        private static int nextPosition(PictureInfo picInfo, IList<KeyValuePair<string, int>> positions)
        {
            string picId = picInfo.ToString();
            bool found = false;
            int picPosition = 1;

            for (int i = 0; i < positions.Count; i++)
            {
                if (!positions[i].Key.Equals(picId)) continue;

                picPosition = positions[i].Value + 1;
                positions[i] = new KeyValuePair<string, int>(picId, picPosition);
                found = true;
                break;
            }

            if (!found)
            {
                positions.Add(new KeyValuePair<string, int>(picId, 1));
            }

            return picPosition;
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
        public string this[Field index] => getField(index);

        /// <summary>
        /// Field accessor 
        /// </summary>
        /// <param name="index">ATL field code to search for</param>
        /// <param name="supportsSyncLyrics">true if synched lyrics are supported</param>
        /// <returns>Value associated with the given ATL field code</returns>
        private string getField(Field index, bool supportsSyncLyrics = false)
        {
            Fields.TryGetValue(index, out var result);

            if (null == result
                && (index == Field.RECORDING_YEAR || index == Field.RECORDING_DATE)
                && Fields.TryGetValue(Field.RECORDING_DATE_OR_YEAR, out var recYearOrDate))
                result = recYearOrDate;

            if (null == result
                && index == Field.RECORDING_DATE_OR_YEAR
                && Fields.TryGetValue(Field.RECORDING_DATE, out var recDate))
                result = recDate;

            if (null == result
                && index == Field.RECORDING_DATE_OR_YEAR
                && Fields.TryGetValue(Field.RECORDING_YEAR, out var recYear))
                result = recYear;

            if (null == result
                && index == Field.LYRICS_UNSYNCH
                && Lyrics != null)
            {
                var unsynch = Lyrics.FirstOrDefault(l => l.Format == LyricsInfo.LyricsFormat.UNSYNCHRONIZED && !string.IsNullOrEmpty(l.UnsynchronizedLyrics));
                if (unsynch != null)
                {
                    result = unsynch.UnsynchronizedLyrics;
                }
                else if (!supportsSyncLyrics)
                {
                    foreach (var l in Lyrics)
                    {
                        // Synch lyrics override unsynch lyrics when the target format cannot support both of them
                        if (l.SynchronizedLyrics.Count <= 0) continue;

                        result = l.FormatSynch();
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Convert non-null 'classic' fields values into a properties Map
        /// 
        /// NB : Additional fields, pictures and chapters won't be part of the Map
        /// </summary>
        /// <returns>Map containing all 'classic' metadata fields</returns>
        public IDictionary<Field, string> ToMap(bool supportsSyncLyrics = false)
        {
            IDictionary<Field, string> result = new Dictionary<Field, string>(Fields);

            if (result.ContainsKey(Field.RECORDING_DATE_OR_YEAR))
            {
                checkField(result, Field.RECORDING_YEAR, supportsSyncLyrics);
                checkField(result, Field.RECORDING_DATE, supportsSyncLyrics);
            }
            else checkField(result, Field.RECORDING_DATE_OR_YEAR, supportsSyncLyrics);

            checkField(result, Field.LYRICS_UNSYNCH, supportsSyncLyrics);

            return result;
        }

        private void checkField(IDictionary<Field, string> map, Field index, bool supportsSyncLyrics = false)
        {
            if (map.ContainsKey(index)) return;
            var value = getField(index, supportsSyncLyrics);
            if (value != null) map[index] = value;
        }

        /// <summary>
        /// Clear all values stored in TagData object
        /// </summary>
        public void Clear()
        {
            Pictures.Clear();
            Fields.Clear();
            AdditionalFields.Clear();
            Chapters?.Clear();
            Lyrics?.Clear();

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
                if (trackNumber.Contains('/'))
                {
                    trackNumberTotal = trackNumber;
                    string[] parts = trackNumber.Split('/');
                    trackNumber = parts[0];
                    trackTotal = parts[1];
                }
                else if (Utils.IsNumeric(trackNumber))
                {
                    trackNumberTotal = trackNumber;
                    if (Fields.TryGetValue(Field.TRACK_TOTAL, out var field))
                    {
                        trackTotal = field;
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
                if (discNumber.Contains('/'))
                {
                    discNumberTotal = discNumber;
                    string[] parts = discNumber.Split('/');
                    discNumber = parts[0];
                    discTotal = parts[1];
                }
                else if (Utils.IsNumeric(discNumber))
                {
                    discNumberTotal = discNumber;
                    if (Fields.TryGetValue(Field.DISC_TOTAL, out var field))
                    {
                        discTotal = field;
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

            if (Chapters is { Count: > 0 })
            {
                // Sort by start offset or time
                Chapters = Chapters[0].UseOffset ? Chapters.OrderBy(chapter => chapter.StartOffset).ToList() :
                    Chapters.OrderBy(chapter => chapter.StartTime).ToList();

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
                if (previousChapter is { EndTime: 0 }) previousChapter.EndTime = (uint)Math.Floor(DurationMs);
            }

            if (Lyrics is not { Count: > 0 }) return;

            foreach (LyricsInfo lyrics in Lyrics) lyrics.GuessFormat();
        }

        /// <summary>
        /// Convert given value to empty string ("") if null or zero ("0")
        /// </summary>
        /// <param name="s">Value to convert</param>
        /// <returns>If null or zero ("0"), empty string (""); else initial value</returns>
        private static string emptyIfZero(string s)
        {
            string result = s;

            if (!Settings.NullAbsentValues && s is "0") result = "";

            return result;
        }

        /// <inheritdoc />
        public bool Equals(TagData other)
        {
            // TODO do better than that and compare all fields
            if (null == other) return false;
            var map = ToMap();
            var otherMap = other.ToMap();
            if (map.Count != otherMap.Count) return false;
            foreach (var entry in map)
            {
                if (!otherMap.TryGetValue(entry.Key, out var value)) return false;
                if (value != entry.Value) return false;
            }
            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null) || obj.GetType() != this.GetType()) return false;
            return ((TagData)obj).Equals(this);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            // TODO do better than that and compare all fields
            return ToMap().GetHashCode();
        }
    }
}
