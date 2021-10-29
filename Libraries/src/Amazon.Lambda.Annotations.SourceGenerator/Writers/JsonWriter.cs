using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    public class JsonWriter : IJsonWriter
    {
        private JObject _rootNode;

        public JsonWriter()
        {
            _rootNode = new JObject();
        }

        public JsonWriter(JObject rootNode)
        {
            _rootNode = rootNode;
        }

        public bool Exists(string jsonPath)
        {
            if (!IsValidPath(jsonPath))
            {
                throw new InvalidDataException($"'{jsonPath}' is not a valid '{nameof(jsonPath)}'");
            }
            
            JToken currentNode = _rootNode;
            foreach (var property in jsonPath.Split('.'))
            {
                if (currentNode == null)
                {
                    return false;
                }
                currentNode = currentNode[property];
            }

            return currentNode != null;
        }

        public void SetToken(string jsonPath, JToken token)
        {
            if (!IsValidPath(jsonPath))
            {
                throw new InvalidDataException($"'{jsonPath}' is not a valid '{nameof(jsonPath)}'");
            }
            if (token == null)
            {
                return;
            }
                
            var pathList = jsonPath.Split('.');
            var lastProperty = pathList.LastOrDefault();
            if (string.IsNullOrEmpty((lastProperty)))
            {
                throw new InvalidOperationException($"Cannot set a token at '{jsonPath}' because the terminal property is null or empty");
            }
            var currentNode = _rootNode;

            for (var i = 0; i < pathList.Length-1; i++)
            {
                var property = pathList[i];
                if (!currentNode.ContainsKey(property))
                {
                    currentNode[property] = new JObject();
                }
                currentNode = currentNode[property] as JObject;
                if (currentNode == null)
                {
                    throw new InvalidOperationException($"Cannot set a value at '{jsonPath}' because the token at {property} does not represent a {typeof(JObject)}");
                }
            }

            currentNode[lastProperty] = token;
        }

        public JToken GetToken(string jsonPath, JToken defaultToken = null)
        {
            if (!Exists(jsonPath))
            {
                if (defaultToken != null)
                {
                    return defaultToken;
                }
                throw new InvalidOperationException($"'{jsonPath}' does not exist in the JSON model");
            }

            JToken currentNode = _rootNode;
            foreach (var property in jsonPath.Split('.'))
            {
                currentNode = currentNode[property];
            }

            return currentNode;
        }

        public void RemoveToken(string jsonPath)
        {
            if (!Exists(jsonPath))
            {
                return;
            }

            var pathList = jsonPath.Split('.');
            var lastProperty = pathList.LastOrDefault();
            if (string.IsNullOrEmpty((lastProperty)))
            {
                throw new InvalidOperationException(
                    $"Cannot remove the token at '{jsonPath}' because the terminal property is null or empty");
            }
            var currentNode = _rootNode;

            for (var i = 0; i < pathList.Length-1; i++)
            {
                var property = pathList[i];
                currentNode = currentNode[property] as JObject;
            }

            currentNode.Remove(lastProperty);
        }

        public string GetPrettyJson()
        {
            return JsonConvert.SerializeObject(_rootNode, formatting: Formatting.Indented);
        }

        public void Parse(string content)
        {
            _rootNode = string.IsNullOrEmpty(content) ? new JObject() : JObject.Parse(content);
        }
        
        private bool IsValidPath(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                return false;
            
            return !jsonPath.Split('.').Any(x => string.IsNullOrWhiteSpace(x));
        }
    }
}