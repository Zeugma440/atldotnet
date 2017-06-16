using System;
using System.IO;

namespace ATL.test
{
    public class TestHelper
    {
        public static string REPO_NAME = "atldotnet";
        private static string locationRoot = null;

        public static string getResourceLocationRoot()
        {
            if (null == locationRoot)
            {
                locationRoot = Environment.CurrentDirectory;

                locationRoot = locationRoot.Substring(0, locationRoot.IndexOf(REPO_NAME) + REPO_NAME.Length);

                locationRoot += Path.DirectorySeparatorChar + "ATL.test" + Path.DirectorySeparatorChar + "Resources" + Path.DirectorySeparatorChar;
            }

            return locationRoot;
        }
    }
}
