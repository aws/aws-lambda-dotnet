using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    public interface IJsonWriter
    {
        bool Exists(string jsonPath);
        void SetToken(string jsonPath, JToken token);
        JToken GetToken(string jsonPath, JToken defaultToken = null);
        void RemoveToken(string jsonPath);
        string GetPrettyJson();
        void Parse(string content);
    }
}