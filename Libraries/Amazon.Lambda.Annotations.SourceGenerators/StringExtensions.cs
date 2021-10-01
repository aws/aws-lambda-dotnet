namespace Amazon.Lambda.Annotations.SourceGenerators
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
}