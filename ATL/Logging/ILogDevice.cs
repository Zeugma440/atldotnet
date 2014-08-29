using System;

namespace ATL.Logging
{
	/// <summary>
	/// This interface describes a logical device able to log messages
	/// </summary>
	public interface ILogDevice
	{
		/// <summary>
		/// Logs the message described by the provided LogItem object
		/// </summary>
		/// <param name="anItem">Data concerning the message to be logged</param>
		void DoLog(Log.LogItem anItem);
	}
}
