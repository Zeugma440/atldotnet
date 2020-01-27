using ATL.Logging;

namespace ATL.test
{
    public class ConsoleLogger : ILogDevice
    {
        Log theLog = new Log();
        long previousTimestamp = System.DateTime.Now.Ticks;

        public ConsoleLogger()
        {
            LogDelegator.SetLog(ref theLog);
            theLog.Register(this);
        }

        public void DoLog(Log.LogItem anItem)
        {
            long delta_ms = (anItem.When.Ticks - previousTimestamp) / 10000; // Difference between last logging message, in ms
            System.Console.WriteLine("[" + Log.getLevelName(anItem.Level) + "] " + anItem.Location + " | " + anItem.Message + " [Δ=" + delta_ms + " ms]");

            previousTimestamp = anItem.When.Ticks;
        }
    }
}
