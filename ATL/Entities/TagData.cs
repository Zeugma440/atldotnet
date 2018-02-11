using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL
{
	/// <summary>
	/// Basic metadata fields container
    /// 
    /// TagData aims at staying a basic, universal container, without any Property accessor layer nor any field interpretation logic
	/// </summary>
	public class TagData
	{
        [Obsolete("Access picture data directly through the Pictures attribute, after reading a file with ReadTagParams.ReadPictures=true")]
        public delegate void PictureStreamHandlerDelegate(ref MemoryStream stream, PictureInfo.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object nativePicCode, int position);

        // Identifiers for 'classic' fields
        public const byte TAG_FIELD_GENERAL_DESCRIPTION     = 0;
        public const byte TAG_FIELD_TITLE                   = 1;
        public const byte TAG_FIELD_ARTIST                  = 2;
        public const byte TAG_FIELD_COMPOSER                = 3;
        public const byte TAG_FIELD_COMMENT                 = 4;
        public const byte TAG_FIELD_GENRE                   = 5;
        public const byte TAG_FIELD_ALBUM                   = 6;
        public const byte TAG_FIELD_RECORDING_YEAR          = 7;
        public const byte TAG_FIELD_RECORDING_DATE          = 8;
        public const byte TAG_FIELD_RECORDING_TIME          = 9; // Used internally for ID3v2 for now
        public const byte TAG_FIELD_RECORDING_DAYMONTH      = 10;
        public const byte TAG_FIELD_TRACK_NUMBER            = 11;
        public const byte TAG_FIELD_DISC_NUMBER             = 12;
        public const byte TAG_FIELD_RATING                  = 13;
        public const byte TAG_FIELD_ORIGINAL_ARTIST         = 14;
        public const byte TAG_FIELD_ORIGINAL_ALBUM          = 15;
        public const byte TAG_FIELD_COPYRIGHT               = 16;
        public const byte TAG_FIELD_ALBUM_ARTIST            = 17;
        public const byte TAG_FIELD_PUBLISHER               = 18;
        public const byte TAG_FIELD_CONDUCTOR               = 19;

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
        public string RecordingDate = null;
        public string TrackNumber = null;
        public string DiscNumber = null;
        public string Rating = null;
        public string Copyright = null;
        public string AlbumArtist = null;
        public string Publisher = null;
        public string Conductor = null;

        /// <summary>
        /// Chapters 
        /// NB : The whole chapter list is processed as a whole
        /// </summary>
        public IList<ChapterInfo> Chapters = null;


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



        public TagData()
        {
            Pictures = new List<PictureInfo>();
            AdditionalFields = new List<MetaFieldInfo>();
        }

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

        /// <summary>
        /// Merge given TagData object with current TagData object
        /// </summary>
        /// <param name="data">TagData object to merge</param>
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
                else if (newMetaInfo.MarkedForDeletion && !found) // Cannot delete a field that has not been found
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Field code "+newMetaInfo.NativeFieldCode+" cannot be deleted because it has not been found on current TagData.");
                }
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

                foreach(ChapterInfo chapter in data.Chapters)
                {
                    Chapters.Add(new ChapterInfo(chapter));
                }
            }
        }

        /// <summary>
        /// Converts non-null 'classic' fields values into a properties Map
        /// 
        /// NB : Additional fields, pictures and chapters won't be part of the Map
        /// </summary>
        /// <returns>Map containing all 'classic' metadata fields</returns>
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

        /// <summary>
        /// Clears all values stored in TagData object
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
            RecordingDate = null;
            TrackNumber = null;
            DiscNumber = null;
            Rating = null;
            Copyright = null;
            AlbumArtist = null;
            Publisher = null;
            Conductor = null;
        }

        /// <summary>
        /// Adds given value to given map if value is not null
        /// </summary>
        /// <param name="data">Value to add to the map</param>
        /// <param name="id">Key to add to the map</param>
        /// <param name="map">Target map to host given values</param>
        private void addIfConsistent(String data, byte id, IDictionary<byte,String> map)
        {
            if (data != null) map[id] = data;
        }

        /// <summary>
        /// Converts given value to empty string ("") if null or zero ("0")
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
        /// Builds a map containing the position of each picture in the Pictures field, based on the PictureInfo.Position fields
        /// 
        /// NB : This method does not calculate any position; it just generates the map
        /// </summary>
        /// <returns>Map containing the position for each picture</returns>
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
