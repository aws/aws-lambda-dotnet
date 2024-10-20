using System;

namespace Amazon.Lambda.Annotations.SourceGenerator.Extensions
{
    public static class StringExtensions
    {
        public static string ToCamelCase(this string str)
        {
            if(!string.IsNullOrEmpty(str) && str.Length > 1)
            {
                return char.ToLowerInvariant(str[0]) + str.Substring(1);
            }
            return str;
        }
    }

    public static class ExceptionExtensions
    {
        public static string PrettyPrint(this Exception e)
        {
            if (null == e)
            {
                return string.Empty;
            }

            return $"{e.Message}{e.StackTrace}{PrettyPrint(e.InnerException)}";
        }
    }

    public static class EnvironmentExtensions
    {
        public static string ToEnvironmentLineEndings(this string str)
        {
            return str.Replace("\r\n", Environment.NewLine).
                Replace("\n", Environment.NewLine).
                Replace("\r\r\n", Environment.NewLine);
        }
    }
}