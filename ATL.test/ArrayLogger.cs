using ATL.Logging;
using System.Collections.Generic;
using static ATL.Logging.Log;

namespace ATL.test
{
    public class ArrayLogger : ILogDevice
    {
        Log theLog = new Log();
        public IList<Log.LogItem> Items;

        public ArrayLogger()
        {
            LogDelegator.SetLog(ref theLog);
            Items = new List<Log.LogItem>();
            theLog.Register(this);
        }

        public void DoLog(Log.LogItem anItem)
        {
            Items.Add(anItem);
        }

        public IList<LogItem> GetAllItems(int levelMask)
        {
            return theLog.GetAllItems(levelMask);
        }
    }
}
