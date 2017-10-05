using ATL.Logging;
using System.Collections.Generic;

namespace ATL.test
{
    public class ArrayLogger : ILogDevice
    {
        Log theLog = new Log();
        public IList<Log.LogItem> items;

        public ArrayLogger()
        {
            LogDelegator.SetLog(ref theLog);
            items = new List<Log.LogItem>();
            theLog.Register(this);
        }

        public void DoLog(Log.LogItem anItem)
        {
            items.Add(anItem);
        }
    }
}
