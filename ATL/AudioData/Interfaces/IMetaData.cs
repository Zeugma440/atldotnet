using System;
using System.Collections.Generic;

namespace ATL.AudioData
{
    /// <summary>
    /// This Interface defines an object aimed at giving audio metadata information
    /// </summary>
    public interface IMetaData
    {
        /// <summary>
        /// Available metadata formats
        /// </summary>
        IList<Format> MetadataFormats
        {
            get;
        }

        /// <summary>
        /// Title of the track
        /// </summary>
        string Title
        {
            get;
        }
        /// <summary>
        /// Artist
        /// </summary>
        string Artist
        {
            get;
        }
        /// <summary>
        /// Composer
        /// </summary>
        string Composer
        {
            get;
        }
        /// <summary>
        /// Comments
        /// </summary>
        string Comment
        {
            get;
        }
        /// <summary>
        /// Genre
        /// </summary>
        string Genre
        {
            get;
        }
        /// <summary>
        /// Track number
        /// </summary>
        ushort TrackNumber
        {
            get;
        }
        /// <summary>
		/// Total track number
		/// </summary>
		ushort TrackTotal
        {
            get;
        }
        /// <summary>
        /// Disc number
        /// </summary>
        ushort DiscNumber
        {
            get;
        }
        /// <summary>
        /// Total disc number
        /// </summary>
        ushort DiscTotal
        {
            get;
        }
        /// <summary>
        /// Recording date (DateTime.MinValue if field does not exist)
        /// </summary>
        DateTime Date
        {
            get;
        }
        /// <summary>
        /// True if the provided Date only has Year as a valid field
        /// </summary>
        bool IsDateYearOnly
        {
            get;
        }
        /// <summary>
        /// Title of the album
        /// </summary>
        string Album
        {
            get;
        }
        /// <summary>
        /// Rating of the track, from 0% to 100%
        /// </summary>
        float? Popularity
        {
            get;
        }
        /// <summary>
        /// Copyright
        /// </summary>
        string Copyright
        {
            get;
        }
        /// <summary>
        /// Original artist
        /// </summary>
        string OriginalArtist
        {
            get;
        }
        /// <summary>
        /// Title of the original album
        /// </summary>
        string OriginalAlbum
        {
            get;
        }
        /// <summary>
        /// General description
        /// </summary>
        string GeneralDescription
        {
            get;
        }
        /// <summary>
        /// Publisher
        /// </summary>
        string Publisher
        {
            get;
        }
        /// <summary>
        /// Publishing Date (DateTime.MinValue if field does not exist)
        /// </summary>
        DateTime PublishingDate
        {
            get;
        }
        /// <summary>
        /// Album Artist
        /// </summary>
        string AlbumArtist
        {
            get;
        }
        /// <summary>
        /// Conductor
        /// </summary>
        string Conductor
        {
            get;
        }
        /// <summary>
        /// Product ID
        /// </summary>
        string ProductId
        {
            get;
        }
        /// <summary>
        /// Album sort order
        /// A string which should be used instead of the album name for sorting purposes
        /// </summary>
        string SortAlbum
        {
            get;
        }
        /// <summary>
        /// Album artist sort order
        /// A string which should be used instead of the album artist for sorting purposes
        /// </summary>
        string SortAlbumArtist
        {
            get;
        }
        /// <summary>
        /// Artist sort order
        /// A string which should be used instead of the artist for sorting purposes
        /// </summary>
        string SortArtist
        {
            get;
        }
        /// <summary>
        /// Title sort order
        /// A string which should be used instead of the title for sorting purposes
        /// </summary>
        string SortTitle
        {
            get;
        }
        /// <summary>
        /// Content group description
        /// Used if the sound belongs to a larger category of sounds/music.
        /// For example, classical music is often sorted in different musical sections (e.g. "Piano Concerto").
        /// </summary>
        string Group
        {
            get;
        }
        /// <summary>
        /// Series title / Movement name
        /// </summary>
        string SeriesTitle
        {
            get;
        }
        /// <summary>
        /// Series part / Movement index
        /// </summary>
        string SeriesPart
        {
            get;
        }
        /// <summary>
        /// Long description (may also be called "Podcast description")
        /// </summary>
        string LongDescription
        {
            get;
        }
        /// <summary>
        /// Beats per minute
        /// </summary>
        int? BPM
        {
            get;
        }
        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// NB1 : Use MetaDataHolder.DATETIME_PREFIX + DateTime.ToFileTime() to set dates. ATL will format them properly.
        /// NB2 : when querying multi-stream files (e.g. MP4, ASF), this attribute will only return stream-independent properties of the whole file, in the first language available
        /// </summary>
        IDictionary<string, string> AdditionalFields
        {
            get;
        }
        /// <summary>
        /// Chapters table of contents description
        /// </summary>
        string ChaptersTableDescription
        {
            get;
        }
        /// <summary>
        /// Chapters
        /// </summary>
        IList<ChapterInfo> Chapters
        {
            get;
        }
        /// <summary>
        /// Lyrics
        /// </summary>
        LyricsInfo Lyrics
        {
            get;
        }

        /// <summary>
        /// List of pictures stored in the tag
        /// NB : PictureInfo.PictureData (raw binary picture data) is valued
        /// </summary>
        IList<PictureInfo> EmbeddedPictures
        {
            get;
        }
    }
}
