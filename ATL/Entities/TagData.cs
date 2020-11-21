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
        // Identifiers for 'classic' fields
#pragma warning disable CS1591 // Missing XML comment for publicly visible members
        public const byte TAG_FIELD_GENERAL_DESCRIPTION = 0;
        public const byte TAG_FIELD_TITLE = 1;
        public const byte TAG_FIELD_ARTIST = 2;
        public const byte TAG_FIELD_COMPOSER = 3;
        public const byte TAG_FIELD_COMMENT = 4;
        public const byte TAG_FIELD_GENRE = 5;
        public const byte TAG_FIELD_ALBUM = 6;
        public const byte TAG_FIELD_RECORDING_YEAR = 7;
        public const byte TAG_FIELD_RECORDING_DATE = 8;
        public const byte TAG_FIELD_RECORDING_YEAR_OR_DATE = 9;
        public const byte TAG_FIELD_RECORDING_TIME = 10;
        public const byte TAG_FIELD_RECORDING_DAYMONTH = 11;
        public const byte TAG_FIELD_TRACK_NUMBER = 12;
        public const byte TAG_FIELD_DISC_NUMBER = 13;
        public const byte TAG_FIELD_RATING = 14;
        public const byte TAG_FIELD_ORIGINAL_ARTIST = 15;
        public const byte TAG_FIELD_ORIGINAL_ALBUM = 16;
        public const byte TAG_FIELD_COPYRIGHT = 17;
        public const byte TAG_FIELD_ALBUM_ARTIST = 18;
        public const byte TAG_FIELD_PUBLISHER = 19;
        public const byte TAG_FIELD_CONDUCTOR = 20;
        public const byte TAG_FIELD_TRACK_TOTAL = 21;
        public const byte TAG_FIELD_TRACK_NUMBER_TOTAL = 22;
        public const byte TAG_FIELD_DISC_TOTAL = 23;
        public const byte TAG_FIELD_DISC_NUMBER_TOTAL = 24;
        public const byte TAG_FIELD_CHAPTERS_TOC_DESCRIPTION = 25;
        public const byte TAG_FIELD_LYRICS_UNSYNCH = 26;
        public const byte TAG_FIELD_LYRICS_SYNCH = 27;
        public const byte TAG_FIELD_PUBLISHING_DATE = 28;
#pragma warning disable S1104 // Fields should not have public accessibility
        // Values for 'classic' fields
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
        public string RecordingTime = null;
        public string RecordingDate = null;
        public string TrackNumber = null;
        public string DiscNumber = null;
        public string Rating = null;
        public string Copyright = null;
        public string AlbumArtist = null;
        public string Publisher = null;
        public string Conductor = null;
        public string TrackTotal = null;
        public string TrackNumberTotal = null;
        public string DiscTotal = null;
        public string DiscNumberTotal = null;
        public string ChaptersTableDescription = null;
        public string PublishingDate = null;
#pragma warning restore CS1591 // Missing XML comment for publicly visible members

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
            Pictures = new List<PictureInfo>();
            AdditionalFields = new List<MetaFieldInfo>();
        }

        /// <summary>
        /// Construct a TagData by copying the properties of the given TagData
        /// </summary>
        /// <param name="tagData">TagData to copy properties from</param>
        public TagData(TagData tagData)
        {
            Pictures = new List<PictureInfo>();
            AdditionalFields = new List<MetaFieldInfo>();

            IntegrateValues(tagData);
        }

        /// <summary>
        /// Stores a 'classic' metadata value into current TagData object according to its key
        /// 
        /// NB : This method cannot be used to store non-classic fields; use tagData.AdditionalFields instead
        /// </summary>
        /// <param name="key">Identifier describing the metadata to store (see TagData public consts)</param>
        /// <param name="value">Value of the metadata to store</param>
        public void IntegrateValue(byte key, string value)
        {
            switch (key)
            {
                // Textual fields
                case TAG_FIELD_GENERAL_DESCRIPTION: GeneralDescription = value; break;
                case TAG_FIELD_TITLE: Title = value; break;
                case TAG_FIELD_ARTIST: Artist = value; break;
                case TAG_FIELD_COMPOSER: Composer = value; break;
                case TAG_FIELD_COMMENT: Comment = value; break;
                case TAG_FIELD_GENRE: Genre = value; break;
                case TAG_FIELD_ALBUM: Album = value; break;
                case TAG_FIELD_ORIGINAL_ARTIST: OriginalArtist = value; break;
                case TAG_FIELD_ORIGINAL_ALBUM: OriginalAlbum = value; break;
                case TAG_FIELD_COPYRIGHT: Copyright = value; break;
                case TAG_FIELD_ALBUM_ARTIST: AlbumArtist = value; break;
                case TAG_FIELD_PUBLISHER: Publisher = value; break;
                case TAG_FIELD_CONDUCTOR: Conductor = value; break;
                // Numeric fields (a value at zero mean nothing has been valued -> field should be empty)
                case TAG_FIELD_RECORDING_DATE: RecordingDate = emptyIfZero(value); break;
                case TAG_FIELD_RECORDING_YEAR: RecordingYear = emptyIfZero(value); break;
                case TAG_FIELD_RECORDING_YEAR_OR_DATE:
                    if (value != null)
                    {
                        if (value.Length < 5) RecordingYear = emptyIfZero(value);
                        else RecordingDate = emptyIfZero(value);
                    }
                    break;
                case TAG_FIELD_RECORDING_DAYMONTH: RecordingDayMonth = emptyIfZero(value); break;
                case TAG_FIELD_RECORDING_TIME: RecordingTime = emptyIfZero(value); break;
                case TAG_FIELD_TRACK_NUMBER: TrackNumber = emptyIfZero(value); break;
                case TAG_FIELD_DISC_NUMBER: DiscNumber = emptyIfZero(value); break;
                case TAG_FIELD_RATING: Rating = emptyIfZero(value); break;
                case TAG_FIELD_TRACK_TOTAL: TrackTotal = emptyIfZero(value); break;
                case TAG_FIELD_TRACK_NUMBER_TOTAL: TrackNumberTotal = emptyIfZero(value); break;
                case TAG_FIELD_DISC_TOTAL: DiscTotal = emptyIfZero(value); break;
                case TAG_FIELD_DISC_NUMBER_TOTAL: DiscNumberTotal = emptyIfZero(value); break;
                case TAG_FIELD_CHAPTERS_TOC_DESCRIPTION: ChaptersTableDescription = emptyIfZero(value); break;
                case TAG_FIELD_LYRICS_UNSYNCH:
                    if (null == Lyrics) Lyrics = new LyricsInfo();
                    Lyrics.UnsynchronizedLyrics = value;
                    break;
                case TAG_FIELD_PUBLISHING_DATE: PublishingDate = emptyIfZero(value); break;
            }
        }

        /// <summary>
        /// Merge given TagData object with current TagData object
        /// </summary>
        /// <param name="data">TagData object to merge</param>
        /// <param name="integratePictures">Set to true to merge picture information (default : true)</param>
        /// <param name="mergeAdditionalData">Set to true to merge additional (i.e. non-TagData) fields (default : true)</param>
        public void IntegrateValues(TagData data, bool integratePictures = true, bool mergeAdditionalData = true)
        {
            // String values
            IDictionary<byte, String> newData = data.ToMap();
            foreach (byte key in newData.Keys)
            {
                IntegrateValue(key, newData[key]);
            }

            // Force to input value, if any
            if (data.PaddingSize > -1) PaddingSize = data.PaddingSize; else PaddingSize = -1;

            // Pictures
            if (integratePictures && data.Pictures != null)
            {
                IDictionary<PictureInfo, int> picturePositions = generatePicturePositions();

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
            if (mergeAdditionalData)
            {
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
                    else if (newMetaInfo.MarkedForDeletion && !found) // Cannot delete a field that has not been found
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Field code " + newMetaInfo.NativeFieldCode + " cannot be deleted because it has not been found on current TagData.");
                    }
                }
            }
            else
            {
                AdditionalFields = new List<MetaFieldInfo>(data.AdditionalFields);
            }

            // Chapters, processed as a whole
            if (data.Chapters != null)
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

                foreach (ChapterInfo chapter in data.Chapters)
                {
                    Chapters.Add(new ChapterInfo(chapter));
                }
            }

            if (data.Lyrics != null)
                Lyrics = new LyricsInfo(data.Lyrics);

            DurationMs = data.DurationMs;
        }

        /// <summary>
        /// Indicate whether the current TagData stores a value for the given ATL field code
        /// </summary>
        /// <param name="id">Field code to search for</param>
        /// <returns>True if the current TagData stores a value for the given ATL field code; false if not</returns>
        public bool hasKey(byte id)
        {
            return ToMap().ContainsKey(id);
        }

        /// <summary>
        /// Convert non-null 'classic' fields values into a properties Map
        /// 
        /// NB : Additional fields, pictures and chapters won't be part of the Map
        /// </summary>
        /// <returns>Map containing all 'classic' metadata fields</returns>
        public IDictionary<byte, String> ToMap()
        {
            IDictionary<byte, String> result = new Dictionary<byte, String>();

            // Supported fields only
            // NB : The following block of code determines the order of appearance of fields within written files
            addIfConsistent(Artist, TAG_FIELD_ARTIST, result);
            addIfConsistent(Title, TAG_FIELD_TITLE, result);
            addIfConsistent(Album, TAG_FIELD_ALBUM, result);
            addIfConsistent(RecordingDate, TAG_FIELD_RECORDING_DATE, result);
            addIfConsistent(RecordingYear, TAG_FIELD_RECORDING_YEAR, result);
            addIfConsistent(RecordingDate, TAG_FIELD_RECORDING_YEAR_OR_DATE, result);
            addIfConsistent(RecordingYear, TAG_FIELD_RECORDING_YEAR_OR_DATE, result);
            addIfConsistent(RecordingDayMonth, TAG_FIELD_RECORDING_DAYMONTH, result);
            addIfConsistent(RecordingTime, TAG_FIELD_RECORDING_TIME, result);
            addIfConsistent(Genre, TAG_FIELD_GENRE, result);
            addIfConsistent(Composer, TAG_FIELD_COMPOSER, result);
            addIfConsistent(AlbumArtist, TAG_FIELD_ALBUM_ARTIST, result);
            addIfConsistent(TrackNumber, TAG_FIELD_TRACK_NUMBER, result);
            addIfConsistent(TrackNumberTotal, TAG_FIELD_TRACK_NUMBER_TOTAL, result);
            addIfConsistent(TrackTotal, TAG_FIELD_TRACK_TOTAL, result);
            addIfConsistent(DiscNumber, TAG_FIELD_DISC_NUMBER, result);
            addIfConsistent(DiscNumberTotal, TAG_FIELD_DISC_NUMBER_TOTAL, result);
            addIfConsistent(DiscTotal, TAG_FIELD_DISC_TOTAL, result);
            addIfConsistent(Comment, TAG_FIELD_COMMENT, result);
            addIfConsistent(Rating, TAG_FIELD_RATING, result);
            addIfConsistent(OriginalArtist, TAG_FIELD_ORIGINAL_ARTIST, result);
            addIfConsistent(OriginalAlbum, TAG_FIELD_ORIGINAL_ALBUM, result);
            addIfConsistent(Copyright, TAG_FIELD_COPYRIGHT, result);
            addIfConsistent(Publisher, TAG_FIELD_PUBLISHER, result);
            addIfConsistent(Conductor, TAG_FIELD_CONDUCTOR, result);
            addIfConsistent(GeneralDescription, TAG_FIELD_GENERAL_DESCRIPTION, result);
            addIfConsistent(ChaptersTableDescription, TAG_FIELD_CHAPTERS_TOC_DESCRIPTION, result);
            if (Lyrics != null)
                addIfConsistent(Lyrics.UnsynchronizedLyrics, TAG_FIELD_LYRICS_UNSYNCH, result);
            addIfConsistent(PublishingDate, TAG_FIELD_PUBLISHING_DATE, result);

            return result;
        }

        /// <summary>
        /// Clear all values stored in TagData object
        /// </summary>
        public void Clear()
        {
            Pictures.Clear();
            AdditionalFields.Clear();
            if (Chapters != null) Chapters.Clear();

            GeneralDescription = null;
            Title = null;
            Artist = null;
            OriginalArtist = null;
            Composer = null;
            Comment = null;
            Genre = null;
            Album = null;
            OriginalAlbum = null;
            RecordingYear = null;
            RecordingDayMonth = null;
            RecordingTime = null;
            RecordingDate = null;
            TrackNumber = null;
            DiscNumber = null;
            Rating = null;
            Copyright = null;
            AlbumArtist = null;
            Publisher = null;
            Conductor = null;
            TrackTotal = null;
            TrackNumberTotal = null;
            DiscTotal = null;
            DiscNumberTotal = null;
            ChaptersTableDescription = null;
            Lyrics = null;
            PublishingDate = null;

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
            if (TrackNumber != null && TrackNumber.Contains("/"))
            {
                TrackNumberTotal = TrackNumber;
                string[] parts = TrackNumber.Split('/');
                TrackNumber = parts[0];
                TrackTotal = parts[1];
            }
            else if (Utils.IsNumeric(TrackNumber))
            {
                TrackNumberTotal = TrackNumber;
                if (Utils.IsNumeric(TrackTotal)) TrackNumberTotal += "/" + TrackTotal;
            }

            if (DiscNumber != null && DiscNumber.Contains("/"))
            {
                DiscNumberTotal = DiscNumber;
                string[] parts = DiscNumber.Split('/');
                DiscNumber = parts[0];
                DiscTotal = parts[1];
            }
            else if (Utils.IsNumeric(DiscNumber))
            {
                DiscNumberTotal = DiscNumber;
                if (Utils.IsNumeric(DiscTotal)) DiscNumberTotal += "/" + DiscTotal;
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
        /// Add given value to given map if value is not null
        /// </summary>
        /// <param name="data">Value to add to the map</param>
        /// <param name="id">Key to add to the map</param>
        /// <param name="map">Target map to host given values</param>
        private void addIfConsistent(string data, byte id, IDictionary<byte, string> map)
        {
            if (data != null) map[id] = data;
        }

        /// <summary>
        /// Convert given value to empty string ("") if null or zero ("0")
        /// </summary>
        /// <param name="s">Value to convert</param>
        /// <returns>If null or zero ("0"), empty string (""); else initial value</returns>
        private string emptyIfZero(string s)
        {
            string result = s;

            if (s != null && s.Equals("0")) result = "";

            return result;
        }

        /// <summary>
        /// Build a map containing the position of each picture in the Pictures field, based on the PictureInfo.Position fields
        /// 
        /// NB : This method does not calculate any position; it just generates the map
        /// </summary>
        /// <returns>Map containing the position for each picture</returns>
        private IDictionary<PictureInfo, int> generatePicturePositions()
        {
            IDictionary<PictureInfo, int> result = new Dictionary<PictureInfo, int>();

            foreach (PictureInfo picInfo in Pictures)
            {
                if (result.ContainsKey(picInfo)) result[picInfo] = Math.Max(result[picInfo], picInfo.Position);
                else result.Add(picInfo, picInfo.Position);
            }

            return result;
        }
    }
}
