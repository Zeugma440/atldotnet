namespace ATL.test
{
#pragma warning disable S2223 // Non-constant static fields should not be visible
#pragma warning disable S1104 // Fields should not have public accessibility

    /// <summary>
    /// Global settings for the behaviour of the test suite
    /// </summary>
    public static class Settings
    {
        public static bool DeleteAfterSuccess = true;
    }

#pragma warning restore S1104 // Fields should not have public accessibility
#pragma warning restore S2223 // Non-constant static fields should not be visible
}
