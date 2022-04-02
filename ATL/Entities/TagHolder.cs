using System;
using System.Collections.Generic;
using static ATL.TagData;

namespace ATL
{
    public class TagHolder
    {
        public TagHolder()
        {
            tagData = new TagData();
        }

        public TagHolder(TagData data)
        {
            tagData = new TagData(data);
        }

        public TagData tagData { get; set; }

        public string Title
        {
            get => tagData[Field.TITLE];
            set => tagData.IntegrateValue(Field.TITLE, value);
        }
        public string Artist
        {
            get => tagData[Field.ARTIST];
            set => tagData.IntegrateValue(Field.ARTIST, value);
        }
        public string Composer
        {
            get => tagData[Field.COMPOSER];
            set => tagData.IntegrateValue(Field.COMPOSER, value);
        }
        public string Comment
        {
            get => tagData[Field.COMMENT];
            set => tagData.IntegrateValue(Field.COMMENT, value);
        }
        public string Genre
        {
            get => tagData[Field.GENRE];
            set => tagData.IntegrateValue(Field.GENRE, value);
        }
        public string TrackNumber
        {
            get => tagData[Field.TRACK_NUMBER];
            set => tagData.IntegrateValue(Field.TRACK_NUMBER, value);
        }
        public string TrackTotal
        {
            get => tagData[Field.TRACK_TOTAL];
            set => tagData.IntegrateValue(Field.TRACK_TOTAL, value);
        }
        public string DiscNumber
        {
            get => tagData[Field.DISC_NUMBER];
            set => tagData.IntegrateValue(Field.DISC_NUMBER, value);
        }
        public string DiscTotal
        {
            get => tagData[Field.DISC_TOTAL];
            set => tagData.IntegrateValue(Field.DISC_TOTAL, value);
        }
        public string RecordingDate
        {
            get => tagData[Field.RECORDING_DATE];
            set
            {
                tagData.IntegrateValue(Field.RECORDING_DATE, value);
                tagData.IntegrateValue(Field.RECORDING_YEAR_OR_DATE, value);
            }
        }
        public string RecordingYear
        {
            get => tagData[Field.RECORDING_YEAR];
            set
            {
                tagData.IntegrateValue(Field.RECORDING_YEAR, value);
                tagData.IntegrateValue(Field.RECORDING_YEAR_OR_DATE, value);
            }
        }
        public string Album
        {
            get => tagData[Field.ALBUM];
            set => tagData.IntegrateValue(Field.ALBUM, value);
        }
        public string Rating
        {
            get => tagData[Field.RATING];
            set => tagData.IntegrateValue(Field.RATING, value);
        }
        public string Copyright
        {
            get => tagData[Field.COPYRIGHT];
            set => tagData.IntegrateValue(Field.COPYRIGHT, value);
        }
        public string OriginalArtist
        {
            get => tagData[Field.ORIGINAL_ARTIST];
            set => tagData.IntegrateValue(Field.ORIGINAL_ARTIST, value);
        }
        public string OriginalAlbum
        {
            get => tagData[Field.ORIGINAL_ALBUM];
            set => tagData.IntegrateValue(Field.ORIGINAL_ALBUM, value);
        }
        public string GeneralDescription
        {
            get => tagData[Field.GENERAL_DESCRIPTION];
            set => tagData.IntegrateValue(Field.GENERAL_DESCRIPTION, value);
        }
        public string Publisher
        {
            get => tagData[Field.PUBLISHER];
            set => tagData.IntegrateValue(Field.PUBLISHER, value);
        }
        public string PublishingDate
        {
            get => tagData[Field.PUBLISHING_DATE];
            set => tagData.IntegrateValue(Field.PUBLISHING_DATE, value);
        }
        public string AlbumArtist
        {
            get => tagData[Field.ALBUM_ARTIST];
            set => tagData.IntegrateValue(Field.ALBUM_ARTIST, value);
        }
        public string Conductor
        {
            get => tagData[Field.CONDUCTOR];
            set => tagData.IntegrateValue(Field.CONDUCTOR, value);
        }
        public string ProductId
        {
            get => tagData[Field.PRODUCT_ID];
            set => tagData.IntegrateValue(Field.PRODUCT_ID, value);
        }
        public IList<MetaFieldInfo> AdditionalFields
        {
            get => tagData.AdditionalFields;
            set => tagData.AdditionalFields = value;
        }
        public IList<PictureInfo> Pictures
        {
            get => tagData.Pictures;
            set => tagData.Pictures = value;
        }
        public IList<ChapterInfo> Chapters
        {
            get => tagData.Chapters;
            set => tagData.Chapters = value;
        }
        public string ChaptersTableDescription
        {
            get => tagData[Field.CHAPTERS_TOC_DESCRIPTION];
            set => tagData.IntegrateValue(Field.CHAPTERS_TOC_DESCRIPTION, value);
        }
    }
}
