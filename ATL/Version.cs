using System.Reflection;

namespace ATL
{
    /// <summary>
    /// Represents the current version of Audio Tools Library
    /// </summary>
    public static class Version
    {
        /// <summary>
        /// Get the current version of Audio Tools Library
        /// </summary>
        /// <returns>Current version of Audio Tools Library</returns>
        public static string getVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
    }
}
