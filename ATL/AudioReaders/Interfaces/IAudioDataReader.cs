using System;

namespace ATL.AudioReaders
{
	/// <summary>
	/// This Interface defines an object aimed at giving audio "physical" data information
	/// </summary>
	public interface IAudioDataReader
	{
		/// <summary>
		/// Bitrate of the file
		/// </summary>
		double BitRate
		{
			get;
		}
		/// <summary>
		/// Duration of the file (seconds)
		/// </summary>
		double Duration
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
		/// Returns the family of the coded used for that file (see MetaDataManager for codec families)
		/// </summary>
		int CodecFamily
		{
			get;
		}
        /// <summary>
        /// Indicates if file format allows parsable metadata to be present (e.g. not MIDI files)
        /// </summary>
        bool AllowsParsableMetadata
        {
            get;
        }
		/// <summary>
		/// Gets the ID3v1 metadata contained in this file
		/// </summary>
		BinaryLogic.TID3v1 ID3v1
		{
			get;
		}
		/// <summary>
		/// Gets the ID3v2 metadata contained in this file
		/// </summary>
		BinaryLogic.TID3v2 ID3v2
		{
			get; 
		}
		/// <summary>
		/// Gets the APEtag metadata contained in this file
		/// </summary>
        BinaryLogic.TAPEtag APEtag
        {
            get;
        }

		/// <summary>
		/// Parses the file's binary contents
		/// </summary>
		/// <param name="fileName">Path of the file</param>
        /// <param name="pictureStreamHandler">Delegate for reading picture data stream</param>
		/// <returns>True if the parsing is successful; false if not</returns>
        bool ReadFromFile(String fileName, StreamUtils.StreamHandlerDelegate pictureStreamHandler = null);
	}
}
