using System;
using System.Collections.Generic;

namespace ATL.Logging
{
    /// <summary>
    /// This class handles the logging of the application's messages
    /// </summary>
    public class Log
    {
        // Definition of the four levels of logging
        /// <summary>
        /// Debug logging level
        /// </summary>
        public const int LV_DEBUG = 0x00000008;
        /// <summary>
        /// Info logging level
        /// </summary>
		public const int LV_INFO = 0x00000004;
        /// <summary>
        /// Warning logging level
        /// </summary>
		public const int LV_WARNING = 0x00000002;
        /// <summary>
        /// Error logging level
        /// </summary>
		public const int LV_ERROR = 0x00000001;


        /// <summary>
        /// Definition of a message ("a line of the log")  
        /// </summary>
        public struct LogItem
        {
            /// <summary>
            /// Date of the message
            /// </summary>
			public DateTime When { get; set; }
            /// <summary>
            /// Logging level
            /// </summary>
			public int Level { get; set; }
            /// <summary>
            /// Location of the message (e.g. filename, line, module...)
            /// </summary>
            public string Location { get; set; }
            /// <summary>
            /// Contents of the message
            /// </summary>
			public string Message { get; set; }
        }


        // Storage structure containing each LogItem logged since last reset 
        private readonly IList<LogItem> masterLog;

        // Storage structure containing each LogDevice registered by this class
        private readonly IList<ILogDevice> logDevices;

        // Storage structure containing current locations according to calling thread ID
        private readonly IDictionary<int, string> locations;


        // ASYNCHRONOUS LOGGING

        // Indicates if logging is immediate or asynchronous (default : immediate)
        private bool asynchronous;

        // Queued LogItems waiting to be logged when asynchronous mode is on
        private readonly IList<LogItem> asyncQueue = new List<LogItem>();


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
            locations[Environment.CurrentManagedThreadId] = "";
        }

        /// <summary>
        /// Log the provided message with the LV_DEBUG logging level
        /// </summary>
        /// <param name="msg">Contents of the message</param>
        public void Debug(string msg)
        {
            Write(LV_DEBUG, msg);
        }

        /// <summary>
        /// Log the provided message with the LV_INFO logging level
        /// </summary>
        /// <param name="msg">Contents of the message</param>
        public void Info(string msg)
        {
            Write(LV_INFO, msg);
        }

        /// <summary>
        /// Log the provided message with the LV_WARNING logging level
        /// </summary>
        /// <param name="msg">Contents of the message</param>
        public void Warning(string msg)
        {
            Write(LV_WARNING, msg);
        }

        /// <summary>
        /// Log the provided message with the LV_ERROR logging level
        /// </summary>
        /// <param name="msg">Contents of the message</param>
        public void Error(string msg)
        {
            Write(LV_ERROR, msg);
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
                locations[Environment.CurrentManagedThreadId] = location;
            }
        }


        /// <summary>
        /// Log the provided message with the provided logging level
        /// </summary>
        /// <param name="level">Logging level of the new message</param>
        /// <param name="msg">Contents of the new message</param>
        public void Write(int level, string msg)
        {
            write(level, msg, false);
        }

        private void write(int level, string msg, bool forceDisplay)
        {
            // Creation and filling of the new LogItem
            LogItem theItem = new LogItem
            {
                When = DateTime.Now,
                Level = level,
                Message = msg
            };

            lock (locations)
            {
                theItem.Location = locations.TryGetValue(Environment.CurrentManagedThreadId, out var location) ? location : "";
            }

            lock (masterLog)
            {
                // Adding to the list of logged items
                masterLog.Add(theItem);
            }

            doWrite(theItem, forceDisplay);
        }

        /// <summary>
        /// Log the provided message with the provided logging level
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
        /// Clear the whole list of logged items
        /// </summary>
        public void ClearAll()
        {
            lock (masterLog)
            {
                masterLog.Clear();
            }
        }


        /// <summary>
        /// Get all the logged items 
        /// </summary>
        /// <returns>List of all the logged items</returns>
        public IList<LogItem> GetAllItems()
        {
            return GetAllItems(0x0000000F);
        }


        /// <summary>
        /// Get the logged items whose logging level matches the provided mask 
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
        /// Register a LogDevice
        /// A registered LogDevice will be called each time a new LogItem is received
        /// (see Write method) 
        /// </summary>
        /// <param name="aLogger">Device to register</param>
        public void Register(ILogDevice aLogger)
        {
            logDevices.Add(aLogger);
        }

        /// <summary>
        /// Mark logging as asynchronous : no call will be made
        /// to LogDevice.DoLog until FlushQueue or Release are called
        /// </summary>
        public void SwitchAsync()
        {
            asynchronous = true;
        }

        /// <summary>
        /// Flush all queued LogItems through call to LogDevice.DoLog
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
        /// Make logging synchronous again and flushes remaining LogItems in queue
        /// </summary>
        public void SwitchSync()
        {
            asynchronous = false;
            FlushQueue();
        }

        /// <summary>
        /// Get the name of the given logging level in english
        /// </summary>
        /// <param name="level">Logging level</param>
        /// <returns>Name of the given logging level</returns>
        public static string getLevelName(int level)
        {
            switch (level)
            {
                case LV_DEBUG: return "DEBUG";
                case LV_INFO: return "INFO";
                case LV_WARNING: return "WARNING";
                case LV_ERROR: return "ERROR";
                default: return "";
            }
        }

    }
}
