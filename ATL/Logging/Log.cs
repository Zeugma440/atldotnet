using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ATL.Logging
{
	/// <summary>
	/// This class handles the logging of the application's messages
	/// </summary>
	public class Log
	{
		// Definition of the four levels of logging
		public const int LV_DEBUG		= 0x00000008;
		public const int LV_INFO		= 0x00000004;
		public const int LV_WARNING		= 0x00000002;
		public const int LV_ERROR		= 0x00000001;

		
		// Definition of a message ("a line of the log") 
		public struct LogItem
		{
			public DateTime When;	// Date of the message
			public int Level;		// Logging level
            public string Location; // Location of the message (e.g. filename, line, module...)
			public string Message;	// Contents of the message
		}


		// Storage structure containing each LogItem logged since last reset 
        private IList<LogItem> masterLog;
		
		// Storage structure containing each LogDevice registered by this class
        private IList<ILogDevice> logDevices;

        // Storage structure containing current locations according to calling thread ID
        private IDictionary<int, string> locations;

        
        // ASYNCHRONOUS LOGGING

        // Indicates if logging is immediate or asynchronous (default : immediate)
        private bool asynchronous = false;

        // Queued LogItems waiting to be logged when asynchronous mode is on
        private IList<LogItem> asyncQueue = new List<LogItem>();


		// ---------------------------------------------------------------------------
		 
		/// <summary>
		/// Constructor
		/// </summary>
		public Log()
		{
            masterLog = new List<LogItem>();
			logDevices = new List<ILogDevice>();
            locations = new Dictionary<int, string>();

            // Define default location
            locations[Thread.CurrentThread.ManagedThreadId] = "";
		}

		/// <summary>
		/// Logs the provided message with the LV_DEBUG logging level
		/// </summary>
		/// <param name="msg">Contents of the message</param>
		public void Debug(String msg)
		{
			Write(LV_DEBUG,msg);
		}

		/// <summary>
		/// Logs the provided message with the LV_INFO logging level
		/// </summary>
		/// <param name="msg">Contents of the message</param>
		public void Info(String msg)
		{
			Write(LV_INFO,msg);
		}

		/// <summary>
		/// Logs the provided message with the LV_WARNING logging level
		/// </summary>
		/// <param name="msg">Contents of the message</param>
		public void Warning(String msg)
		{
			Write(LV_WARNING,msg);
		}

		/// <summary>
		/// Logs the provided message with the LV_ERROR logging level
		/// </summary>
		/// <param name="msg">Contents of the message</param>
		public void Error(String msg)
		{
			Write(LV_ERROR,msg);
		}

        /// <summary>
        /// Set current location
        /// 
        /// NB : Implementation is based on current thread ID, 
        /// so that Log object can be accessed by multiple threads,
        /// each setting its own location
        /// </summary>
        /// <param name="location">Location value</param>
        public void SetLocation(string location)
        {
            lock (locations)
            {
                locations[Thread.CurrentThread.ManagedThreadId] = location;
            }
        }


        /// <summary>
        /// Logs the provided message with the provided logging level
        /// </summary>
        /// <param name="level">Logging level of the new message</param>
        /// <param name="msg">Contents of the new message</param>
        /// <param name="forceDisplay">If true, forces all registered ILogDevices to immediately log the message, even if asynchoronous logging is enabled</param>
        public void Write(int level, String msg)
        {
            write(level, msg, false);
        }

		private void write(int level, string msg, bool forceDisplay)
		{
            // Creation and filling of the new LogItem
            LogItem theItem = new LogItem();

			theItem.When = DateTime.Now;
			theItem.Level = level;
			theItem.Message = msg;
            if (locations.ContainsKey(Thread.CurrentThread.ManagedThreadId)) theItem.Location = locations[Thread.CurrentThread.ManagedThreadId]; else theItem.Location = "";

            lock (masterLog)
            {
                // Adding to the list of logged items
                masterLog.Add(theItem);
            }

            doWrite(theItem, forceDisplay);
        }

        /// <summary>
        /// Logs the provided message with the provided logging level
        /// </summary>
        /// <param name="theItem">Message to log</param>
        /// <param name="forceDisplay">If true, forces all registered ILogDevices to immediately log the message, even if asynchoronous logging is enabled</param>
        private void doWrite(LogItem theItem, bool forceDisplay)
        {
            if (asynchronous && !forceDisplay)
            {
                lock (asyncQueue)
                {
                    asyncQueue.Add(theItem);
                }
            }
            else
            {
                // Asks each registered LogDevice to log the new LogItem
                foreach (ILogDevice aLogger in logDevices)
                {
                    aLogger.DoLog(theItem);
                }
            }
		}

		
		/// <summary>
		/// Clears the whole list of logged items
		/// </summary>
		public void ClearAll()
		{
            lock (masterLog)
            {
                masterLog.Clear();
            }
		}


		/// <summary>
		/// Gets all the logged items 
		/// </summary>
		/// <returns>List of all the logged items</returns>
		public IList<LogItem> GetAllItems()
		{
			return GetAllItems(0x0000000F);
		}

		 
		/// <summary>
		/// Gets the logged items whose logging level matches the provided mask 
		/// </summary>
		/// <param name="levelMask">Logging level mask</param>
		/// <returns>List of the matching logged items</returns>
		public IList<LogItem> GetAllItems(int levelMask)
		{
            IList<LogItem> result = new List<LogItem>();

            lock (masterLog)
            {
                foreach (LogItem anItem in masterLog)
                {
                    if ((levelMask & anItem.Level) > 0)
                    {
                        result.Add(anItem);
                    }
                }
            }
			return result;
		}


		/// <summary>
		/// Registers a LogDevice
		/// A registered LogDevice will be called each time a new LogItem is received
		/// (see Write method) 
		/// </summary>
		/// <param name="aLogger">Device to register</param>
		public void Register(ILogDevice aLogger)
		{
			logDevices.Add(aLogger);
		}

        /// <summary>
        /// Marks logging as asynchronous : no call will be made
        /// to LogDevice.DoLog until FlushQueue or Release are called
        /// </summary>
        public void SwitchAsync()
        {
            asynchronous = true;
        }

        /// <summary>
        /// Flushes all queued LogItems through call to LogDevice.DoLog
        /// </summary>
        public void FlushQueue()
        {
            lock (asyncQueue)
            {
                foreach (LogItem item in asyncQueue)
                {
                    doWrite(item, true);
                }
                asyncQueue.Clear();
            }
        }

        /// <summary>
        /// Makes logging synchronous again and flushes remaining LogItems in queue
        /// </summary>
        public void SwitchSync()
        {
            asynchronous = false;
            FlushQueue();
        }

	}
}
