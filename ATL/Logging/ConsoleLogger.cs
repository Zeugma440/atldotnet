namespace ATL.Logging
{
    /// <summary>
    /// Utility class to display ATL logging on the Console by simply calling `new ConsoleLogger();`
    /// </summary>
    public class ConsoleLogger : ILogDevice
    {
        private readonly Log theLog = new();
        private long previousTimestamp = System.DateTime.Now.Ticks;

        /// <summary>
        /// Constructor
        /// </summary>
        public ConsoleLogger()
        {
            LogDelegator.SetLog(ref theLog);
            theLog.Register(this);
        }

        void ILogDevice.DoLog(Log.LogItem anItem)
        {
            long delta_ms = (anItem.When.Ticks - previousTimestamp) / 10000; // Difference between last logging message, in ms
            System.Console.WriteLine("[" + Log.getLevelName(anItem.Level) + "] " + anItem.Location + " | " + anItem.Message + " [Δ=" + delta_ms + " ms]");

            previousTimestamp = anItem.When.Ticks;
        }
    }
}
