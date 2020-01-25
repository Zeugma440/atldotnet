using System.Reflection;

namespace ATL
{
    public static class Version
    {
        public static string getVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
    }
}
