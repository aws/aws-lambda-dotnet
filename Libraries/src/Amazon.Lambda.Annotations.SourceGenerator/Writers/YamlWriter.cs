using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    /// <summary>
    /// This contains the functionality to manipulate a YAML blob
    /// </summary>
    public class YamlWriter : ITemplateWriter
    {
        private YamlMappingNode _rootNode;
        private readonly Serializer _serializer  = new Serializer();
        private readonly Deserializer _deserializer = new Deserializer();
        private readonly SerializerBuilder _serializerBuilder = new SerializerBuilder();

        public YamlWriter()
        {
            _rootNode = new YamlMappingNode();
        }

        /// <summary>
        /// Checks if the dot(.) seperated yamlPath exists in the YAML blob stored at the _rootNode
        /// </summary>
        /// <param name="yamlPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <returns>true if the path exist, else false</returns>
        /// <exception cref="InvalidDataException">Thrown if the yamlPath is invalid</exception>
        public bool Exists(string yamlPath)
        {
            if (!IsValidPath(yamlPath))
            {
                throw new InvalidDataException($"'{yamlPath}' is not a valid {nameof(yamlPath)}");
            }

            YamlNode currentNode = _rootNode;
            foreach (var property in yamlPath.Split('.'))
            {
                if (currentNode == null)
                {
                    return false;
                }
                try
                {
                    currentNode = currentNode[property];
                }
                catch (KeyNotFoundException)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the object stored at the dot(.) seperated yamlPath. If the path does not exist then return the defaultToken.
        /// The defaultToken is only returned if it holds a non-null value.
        /// </summary>
        /// <param name="yamlPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="defaultToken">The object that is returned if yamlPath does not exist.</param>
        /// <exception cref="InvalidOperationException">Thrown if the yamlPath does not exist and the defaultToken is null</exception>
        public object GetToken(string yamlPath, object defaultToken = null)
        {
            if (!Exists(yamlPath))
            {
                if (defaultToken != null)
                {
                    return defaultToken;
                }
                throw new InvalidOperationException($"'{yamlPath}' does not exist in the JSON model");
            }

            YamlNode currentNode = _rootNode;
            foreach (var property in yamlPath.Split('.'))
            {
                currentNode = currentNode[property];
            }

            return currentNode;
        }

        /// <summary>
        /// Gets the object stored at the dot(.) seperated yamlPath. If the path does not exist then return the defaultToken.
        /// The defaultToken is only returned if it holds a non-null value.
        /// The object is deserialized into type T before being returned.
        /// </summary>
        /// <param name="yamlPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="defaultToken">The object that is returned if yamlPath does not exist in the YAML blob. It will be convert to type T before being returned.</param>
        /// <exception cref="InvalidOperationException">Thrown if the yamlPath does not exist and the defaultToken is null</exception>
        public T GetToken<T>(string yamlPath, object defaultToken = null)
        {
            var token = GetToken(yamlPath, defaultToken);
            if (token == null)
            {
                throw new InvalidOperationException($"'{yamlPath}' points to a null token");
            }

            return GetDeserializedToken<T>(token);
        }

        /// <summary>
        /// This method converts the supplied token it into a concrete <see cref="YamlNode"/> type and sets it at the dot(.) seperated yamlPath.
        /// Any non-existing nodes in the yamlPath are created on the fly.
        /// All non-terminal nodes in the yamlPath need to be of type <see cref="YamlNodeType.Mapping"/>.
        /// </summary>
        /// <param name="yamlPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <param name="token">The object to set at the specified yamlPath</param>
        /// <param name="tokenType"><see cref="TokenType"/></param>
        /// <exception cref="InvalidDataException">Thrown if the yamlPath is invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown if the terminal property in the yamlPath is null/empty or if any non-terminal nodes in the yamlPath cannot be converted to <see cref="YamlMappingNode"/></exception>
        public void SetToken(string yamlPath, object token, TokenType tokenType = TokenType.Other)
        {
            if (!IsValidPath(yamlPath))
            {
                throw new InvalidDataException($"'{yamlPath}' is not a valid '{nameof(yamlPath)}'");
            }
            if (token == null)
            {
                return;
            }

            var pathList = yamlPath.Split('.');
            var lastProperty = pathList.LastOrDefault();
            if (string.IsNullOrEmpty(lastProperty))
            {
                throw new InvalidOperationException($"Cannot set a token at '{yamlPath}' because the terminal property is null or empty");
            }

            YamlNode terminalToken;

            if (token is YamlNode yamlNode)
            {
                terminalToken = yamlNode;
            }
            else
            {
                switch (tokenType)
                {
                    case TokenType.List:
                        terminalToken = GetDeserializedToken<YamlSequenceNode>(token);
                        break;
                    case TokenType.KeyVal:
                    case TokenType.Object:
                        terminalToken = GetDeserializedToken<YamlMappingNode>(token);
                        break;
                    case TokenType.Other:
                        terminalToken = GetDeserializedToken<YamlScalarNode>(token);
                        break;
                    default:
                        throw new InvalidOperationException($"Failed to deserialize token because {nameof(tokenType)} is invalid");
                }
            }

            var currentNode = _rootNode;

            for (var i = 0; i < pathList.Length - 1; i++)
            {
                if (currentNode == null)
                {
                    throw new InvalidOperationException($"Cannot set a token at '{yamlPath}' because one of the nodes in the path is null");
                }

                var property = pathList[i];
                try
                {
                    currentNode = (YamlMappingNode)currentNode[property];
                }
                catch (KeyNotFoundException)
                {
                    currentNode.Children[property] = new YamlMappingNode();
                    currentNode = (YamlMappingNode)currentNode[property];
                }
                catch (InvalidCastException)
                {
                    throw new InvalidOperationException($"Cannot set a token at '{yamlPath}' because one of the nodes in the path cannot be converted to {nameof(YamlMappingNode)}");
                }
            }

            currentNode.Children[lastProperty] = terminalToken;
        }

        /// <summary>
        /// Deletes the token found at the dot(.) separated yamlPath. It does not do anything if the yamlPath does not exist.
        /// </summary>
        /// <param name="yamlPath">dot(.) seperated path. Example "Person.Name.FirstName"</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void RemoveToken(string yamlPath)
        {
            if (!Exists(yamlPath))
            {
                return;
            }

            var pathList = yamlPath.Split('.');
            var lastProperty = pathList.LastOrDefault();
            if (string.IsNullOrEmpty(lastProperty))
            {
                throw new InvalidOperationException(
                    $"Cannot remove the token at '{yamlPath}' because the terminal property is null or empty");
            }
            YamlNode currentNode = _rootNode;

            for (var i = 0; i < pathList.Length - 1; i++)
            {
                var property = pathList[i];
                currentNode = currentNode[property];
            }

            var terminalNode = (YamlMappingNode)currentNode;
            terminalNode.Children.Remove(lastProperty);
        }

        /// <summary>
        /// Parses the YAML string as a <see cref="YamlMappingNode"/>
        /// </summary>
        /// <param name="content"></param>
        public void Parse(string content)
        {
            _rootNode = string.IsNullOrEmpty(content)
                ? new YamlMappingNode()
                : _deserializer.Deserialize<YamlMappingNode>(content);
        }

        /// <summary>
        /// Converts the <see cref="YamlMappingNode"/> to a YAML string
        /// </summary>
        public string GetContent()
        {
            return _serializerBuilder
                .WithIndentedSequences()
                .Build()
                .Serialize(_rootNode);
        }

        /// <summary>
        /// If the string does not start with '@', return it as is.
        /// If a string value starts with '@' then a reference node is created and returned.
        /// </summary>
        public object GetValueOrRef(string value)
        {
            if (!value.StartsWith("@"))
                return value;

            var yamlNode = new YamlMappingNode();
            yamlNode.Children["Ref"] = value.Substring(1);
            return yamlNode;
        }

        /// <summary>
        /// Validates that the yamlPath is not null or comprises only of white spaces. Also ensures that it does not have consecutive dots(.)
        /// </summary>
        /// <param name="yamlPath"></param>
        /// <returns>true if the path is valid, else fail</returns>
        private bool IsValidPath(string yamlPath)
        {
            if (string.IsNullOrWhiteSpace(yamlPath))
                return false;

            return !yamlPath.Split('.').Any(string.IsNullOrWhiteSpace);
        }

        private T GetDeserializedToken<T>(object token)
        {
            if (token is T deserializedToken)
            {
                return deserializedToken;
            }

            return _deserializer.Deserialize<T>(_serializer.Serialize(token));
        }
    }
}