using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.Testing
{
    public class LambdaRouteConfig
    {
        private static readonly Regex _pathVariableRegex = new(@"\{(?<name>.+)\}");

        private Regex _pathRegex;

        public PayloadFormat PayloadFormat { get; set; }
        public string PathTemplate { get; set; }
        public string HttpMethod { get; set; }
        public string AssemblyName { get; set; }
        public string TypeName { get; set; }
        public string MethodName { get; set; }

        public bool Match(HttpMethod method, Uri requestUri, out Dictionary<string, string> pathParameters)
        {
            _pathRegex ??= BuildPathRegex(PathTemplate);

            pathParameters = null;

            if (!string.Equals(HttpMethod, method.Method, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(HttpMethod, "Any", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var regexMatch = _pathRegex.Match(requestUri.AbsolutePath);

            if (!regexMatch.Success)
                return false;

            pathParameters = _pathRegex.GetGroupNames().ToDictionary(name => name, name => regexMatch.Groups[name].Value);
            return true;
        }

        private static Regex BuildPathRegex(string pathTemplate)
        {
            var regexSegments = pathTemplate.Split('/').Select(segment => _pathVariableRegex.IsMatch(segment)
                    ? _pathVariableRegex.Replace(segment, match => $@"(?<{match.Groups["name"]}>[^\/]+)")
                    : Regex.Escape(segment));

            var regexString = "^" + string.Join(@"\/", regexSegments) + "$";

            return new Regex(regexString);
        }
    }
}
