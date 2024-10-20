using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    /// <summary>
    /// This contains the functionality to manipulate a JSON blob
    /// </summary>
    internal class JsonWriter : ITemplateWriter
    {
        private JObject _rootNode;

        public JsonWriter()
        {
            _rootNode = new JObject();
        }

        /// <summary>
        /// Checks if the dot(.) seperated jsonPath exists in the json blob stored at the _rootNode
        /// </summary>
        /// <param name="jsonPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <returns>true if the path exist, else false</returns>
        /// <exception cref="InvalidDataException">Thrown if the jsonPath is invalid</exception>
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
                try
                {
                    currentNode = currentNode[property];
                }
                // If the currentNode is already a leaf value then we will encounter an InvalidOperationException
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            return currentNode != null;
        }

        /// <summary>
        /// This method converts the supplied token it into a <see cref="JToken"/> type and sets it at the dot(.) seperated jsonPath.
        /// Any non-existing nodes in the jsonPath are created on the fly.
        /// All non-terminal nodes in the jsonPath need to be of type <see cref="JObject"/>.
        /// </summary>
        /// <param name="jsonPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="token">The object to set at the specified jsonPath</param>
        /// <param name="tokenType"><see cref="TokenType"/>This does not play any role while setting a token for the JsonWriter</param>
        /// <exception cref="InvalidDataException">Thrown if the jsonPath is invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown if the terminal property in the jsonPath is null/empty or if any non-terminal nodes in the jsonPath cannot be converted to <see cref="JObject"/></exception>
        public void SetToken(string jsonPath, object token, TokenType tokenType = TokenType.Other)
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

            var terminalToken = GetDeserializedToken<JToken>(token);
            var currentNode = _rootNode;

            for (var i = 0; i < pathList.Length-1; i++)
            {
                if (currentNode == null)
                {
                    throw new InvalidOperationException($"Cannot set a token at '{jsonPath}' because one of the nodes in the path is null");
                }

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

            currentNode[lastProperty] = terminalToken;
        }

        /// <summary>
        /// Gets the object stored at the dot(.) seperated jsonPath. If the path does not exist then return the defaultToken.
        /// The defaultToken is only returned if it holds a non-null value.
        /// </summary>
        /// <param name="jsonPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="defaultToken">The object that is returned if jsonPath does not exist.</param>
        /// <exception cref="InvalidOperationException">Thrown if the jsonPath does not exist and the defaultToken is null</exception>
        public object GetToken(string jsonPath, object defaultToken = null)
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

        /// <summary>
        /// Gets the object stored at the dot(.) seperated jsonPath. If the path does not exist then return the defaultToken.
        /// The defaultToken is only returned if it holds a non-null value.
        /// The object is deserialized into type T before being returned.
        /// </summary>
        /// <param name="jsonPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="defaultToken">The object that is returned if jsonPath does not exist in the JSON blob. It will be convert to type T before being returned.</param>
        /// <exception cref="InvalidOperationException">Thrown if the jsonPath does not exist and the defaultToken is null</exception>
        public T GetToken<T>(string jsonPath, object defaultToken = null)
        {
            var token = GetToken(jsonPath, defaultToken);
            if (token == null)
            {
                throw new InvalidOperationException($"'{jsonPath}' points to a null token");
            }

            return GetDeserializedToken<T>(token);
        }

        /// <summary>
        /// Deletes the token found at the dot(.) separated jsonPath. It does not do anything if the jsonPath does not exist.
        /// </summary>
        /// <param name="jsonPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <exception cref="InvalidOperationException">Thrown if the terminal property in jsonPath is null or empty</exception>
        public void RemoveToken(string jsonPath)
        {
            if (!Exists(jsonPath))
            {
                return;
            }

            var pathList = jsonPath.Split('.');
            var lastProperty = pathList.LastOrDefault();
            if (string.IsNullOrEmpty(lastProperty))
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

        /// <inheritdoc/>
        public void RemoveTokenIfNullOrEmpty(string jsonPath)
        {
            if (!Exists(jsonPath))
            {
                return;
            }

            JToken currentNode = _rootNode;
            foreach (var property in jsonPath.Split('.'))
            {
                currentNode = currentNode[property];
            }

            if (currentNode.Type == JTokenType.Null || (currentNode.Type == JTokenType.Object && !currentNode.HasValues))
            {
                RemoveToken(jsonPath);
            }
        }

        /// <summary>
        /// Returns the template as a string
        /// </summary>
        public string GetContent()
        {
            return JsonConvert.SerializeObject(_rootNode, formatting: Formatting.Indented);
        }

        /// <summary>
        /// Converts the JSON string into a <see cref="JObject"/>
        /// </summary>
        /// <param name="content"></param>
        public void Parse(string content)
        {
            _rootNode = string.IsNullOrEmpty(content) ? new JObject() : JObject.Parse(content);
        }

        /// <summary>
        /// If the string does not start with '@', return it as is.
        /// If a string value starts with '@' then a reference node is created and returned.
        /// </summary>
        public object GetValueOrRef(string value)
        {
            if (!value.StartsWith("@"))
                return value;

            var jsonNode = new JObject();
            jsonNode["Ref"] = value.Substring(1);
            return jsonNode;
        }

        /// <summary>
        /// Validates that the jsonPath is not null or comprises only of white spaces. Also ensures that it does not have consecutive dots(.)
        /// </summary>
        /// <param name="jsonPath"></param>
        /// <returns>true if the path is valid, else fail</returns>
        private bool IsValidPath(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                return false;

            return !jsonPath.Split('.').Any(string.IsNullOrWhiteSpace);
        }

        private T GetDeserializedToken<T>(object token)
        {
            if (token is T deserializedToken)
            {
                return deserializedToken;
            }
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(token));
        }

        public IList<string> GetKeys(string path)
        {
            try
            {
                return GetToken<Dictionary<string, object>>(path).Keys.ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to retrieve keys for the specified JSON path '{path}'.", ex);
            }
        }
    }
}