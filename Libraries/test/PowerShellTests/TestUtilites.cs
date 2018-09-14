using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Amazon.Lambda.PowerShellTests
{
    public static class TestUtilites
    {
        public static string ConvertToString(Stream ms)
        {
            using (var reader = new StreamReader(ms))
            {
                return reader.ReadToEnd();
            }
        }

        public static Stream ConvertToStream(string str)
        {
            return new MemoryStream(UTF8Encoding.UTF8.GetBytes(str));
        }
    }
}
