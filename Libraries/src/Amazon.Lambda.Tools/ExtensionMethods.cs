using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ThirdParty.Json.LitJson;

namespace Amazon.Lambda.Tools
{
    internal static class ExtensionMethods
    {
        public static void SetIfNotNull(this JsonData data, string key, string value)
        {
            if (value == null)
                return;

            data[key] = value;
        }
        public static void SetIfNotNull(this JsonData data, string key, bool? value)
        {
            if (!value.HasValue)
                return;

            data[key] = value.Value;
        }
        public static void SetIfNotNull(this JsonData data, string key, int? value)
        {
            if (!value.HasValue)
                return;

            data[key] = value.Value;
        }
    }
}
