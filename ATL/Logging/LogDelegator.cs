using System;

namespace ATL.Logging
{
	/// <summary>
    /// This class is the static entry point for all logging operations
	/// </summary>
	public class LogDelegator
	{
		// Declaration of the delegate method signature for logging messages
		public delegate void LogWriteDelegate( int level, String msg );

		private static LogWriteDelegate theLogDelegate;	// Logging delegate object

		static LogDelegator()
		{
            // Initialized with a dummy method to avoid returning null
            // when no call to SetLog has been made
			theLogDelegate = new LogWriteDelegate( dummyMethod );
		}

		private static void dummyMethod(int a, String b) {}

		/// <summary>
		/// Sets the delegate to the Write method of the Log object 
		/// used for logging messages
		/// </summary>
		/// <param name="theLog">Log to be used</param> 
		public static void SetLog(ref Log theLog)
		{
			theLogDelegate = new LogWriteDelegate( theLog.Write );
		}


		/// <summary>
		/// Gets the delegate routine to use for logging messages
		/// </summary>
		/// <returns>Delegate routine object to be used</returns>
		public static LogWriteDelegate GetLogDelegate()
		{
			return theLogDelegate;
		}
	}
}
