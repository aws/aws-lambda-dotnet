using System;
using System.Reflection;

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
        
        public static string ApplyReplacements(this string str)
        {
            var generatorType = typeof(Generator);
            var generatorAssembly = generatorType.Assembly;
            var assemblyName = generatorAssembly.GetName();
            var assemblyVersion = assemblyName.Version;
            return str.Replace("{ANNOTATIONS_ASSEMBLY_VERSION}", assemblyVersion?.ToString());
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