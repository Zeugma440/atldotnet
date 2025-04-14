using System.Collections.Generic;
using ATL.AudioData.IO;
using System.IO;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData
{
	/// <summary>
	/// This Interface defines an object aimed at giving audio "physical" data information
	/// </summary>
	public interface IAudioDataIO
	{
        /// <summary>
        /// Full access path of the underlying file
        /// </summary>
        string FileName
        {
            get;
        }
        /// <summary>
        /// Bitrate (kilobytes per second)
        /// </summary>
        double BitRate
		{
			get;
		}
		/// <summary>
		/// Duration (milliseconds)
		/// </summary>
		double Duration
		{
			get;
		}
        /// <summary>
		/// Sample rate (Hz)
		/// </summary>
		int SampleRate
        {
            get;
        }
        /// <summary>
		/// Bit depth (bits per sample)
        /// -1 if bit depth is not relevant to that audio format (e.g. lossy audio)
		/// </summary>
		int BitDepth
        {
            get;
        }
        /// <summary>
        /// Returns true if the bitrate is variable; false if not
        /// </summary>
        bool IsVBR
		{
			get;
		}
        /// <summary>
        /// Format of the audio data
        /// </summary>
        AudioFormat AudioFormat
        {
            get;
        }
        /// <summary>
        /// Family of the audio codec (see AudioDataIOFactory for the list of codec families)
        /// </summary>
        int CodecFamily
		{
			get;
		}
        /// <summary>
        /// Channels arrangement
        /// </summary>
        ChannelsArrangement ChannelsArrangement
        {
            get;
        }
        /// <summary>
        /// Offset of the audio data chunk (bytes)
        /// </summary>
        long AudioDataOffset
        {
            get;
        }
        /// <summary>
        /// Size of the audio data chunk (bytes)
        /// </summary>
        long AudioDataSize
        {
            get;
        }

        /// <summary>
        /// Indicate which metadata types are supported by the present audio format, ordered by most recommended
        /// </summary>
        /// <returns>Metadata type supported by the present audio format</returns>
        List<MetaDataIOFactory.TagType> GetSupportedMetas();

        /// <summary>
        /// True if the native tagging system contains a wide array of fields
        /// </summary>
        bool IsNativeMetadataRich
        {
            get;
        }

        /// <summary>
        /// Read audio data from the given stream.
        /// NB1 : Standard metadata (i.e. ID3v2, ID3v1 and APE) have to be read _before_ calling this method, and their size stored in sizeInfo
        /// NB2 : Stream is _not_ closed after reading; resource deallocation has to be done by the caller
        /// </summary>
        /// <param name="source">Stream to read</param>
        /// <param name="sizeNfo">Description of the size of the undelying stream and associated metadata</param>
        /// <param name="readTagParams">Reading parameters and options</param>
        /// <returns>True if the stream has been successfuly read; false if not</returns>
        bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams);
    }
}
