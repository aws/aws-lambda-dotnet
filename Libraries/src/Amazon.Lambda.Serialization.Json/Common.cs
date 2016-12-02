using System;
using System.IO;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Common logic.
    /// </summary>
    internal static class Common
    {
        public static MemoryStream Base64ToMemoryStream(string dataBase64)
        {
            var dataBytes = Convert.FromBase64String(dataBase64);
            MemoryStream stream = new MemoryStream(dataBytes);
            return stream;
        }
    }
}
