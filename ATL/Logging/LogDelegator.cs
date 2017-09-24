using System;

namespace ATL.Logging
{
	/// <summary>
    /// This class is the static entry point for all logging operations
	/// </summary>
	public class LogDelegator
	{
		// Declaration of the delegate method signature for logging messages
		public delegate void LogWriteDelegate( int level, string msg );
        // Declaration of the delegate method signature for setting location
        public delegate void LogLocateDelegate(string msg);

        private static LogWriteDelegate theLogWriteDelegate;	// Logging delegate object
        private static LogLocateDelegate theLogLocateDelegate; // Logging delegate object

        static LogDelegator()
		{
            // Initialized with a dummy method to avoid returning null
            // when no call to SetLog has been made
			theLogWriteDelegate = new LogWriteDelegate(writeDummyMethod);
            theLogLocateDelegate = new LogLocateDelegate(locateDummyMethod);
        }

		private static void writeDummyMethod(int a, string b) {}
        private static void locateDummyMethod(string a) { }

        /// <summary>
        /// Sets the delegate to the Write method of the Log object 
        /// used for logging messages
        /// </summary>
        /// <param name="theLog">Log to be used</param> 
        public static void SetLog(ref Log theLog)
		{
			theLogWriteDelegate = new LogWriteDelegate(theLog.Write);
            theLogLocateDelegate = new LogLocateDelegate(theLog.SetLocation);
        }


		/// <summary>
		/// Gets the delegate routine to use for logging messages
		/// </summary>
		/// <returns>Delegate routine object to be used</returns>
		public static LogWriteDelegate GetLogDelegate()
		{
			return theLogWriteDelegate;
		}

        /// <summary>
        /// Gets the delegate routine to use for setting location
        /// </summary>
        /// <returns>Delegate routine object to be used</returns>
        public static LogLocateDelegate GetLocateDelegate()
        {
            return theLogLocateDelegate;
        }

    }
}
