using System;
using System.Collections.Generic;

namespace Amazon.Lambda.BedrockAgentEvents
{
    /// <summary>
    /// This class represents the input event from Amazon Bedrock Agent API.
    /// </summary>
    public class BedrockAgentApiRequest
    {
        /// <summary>
        /// The version of the message format.
        /// </summary>
        public string MessageVersion { get; set; }

        /// <summary>
        /// Information about the agent.
        /// </summary>
        public AgentInfo Agent { get; set; }

        /// <summary>
        /// The input text from the user.
        /// </summary>
        public string InputText { get; set; }

        /// <summary>
        /// The session ID for the conversation.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The action group being invoked.
        /// </summary>
        public string ActionGroup { get; set; }

        /// <summary>
        /// The API path being invoked.
        /// </summary>
        public string ApiPath { get; set; }

        /// <summary>
        /// The HTTP method being used.
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// The parameters for the request.
        /// </summary>
        public List<Parameter> Parameters { get; set; }

        /// <summary>
        /// The request body.
        /// </summary>
        public RequestBody RequestBody { get; set; }

        /// <summary>
        /// Session attributes that persist for the entire session.
        /// </summary>
        public Dictionary<string, string> SessionAttributes { get; set; }

        /// <summary>
        /// Prompt session attributes.
        /// </summary>
        public Dictionary<string, string> PromptSessionAttributes { get; set; }
    }

    /// <summary>
    /// Information about the agent.
    /// </summary>
    public class AgentInfo
    {
        /// <summary>
        /// The name of the agent.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The ID of the agent.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The alias of the agent.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// The version of the agent.
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// A parameter for the request.
    /// </summary>
    public class Parameter
    {
        /// <summary>
        /// The name of the parameter.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the parameter.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The value of the parameter.
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// The request body.
    /// </summary>
    public class RequestBody
    {
        /// <summary>
        /// The content of the request body. Only one content type is supported.
        /// </summary>
        public Dictionary<string, ContentTypeProperties> Content { get; set; }
    }

    /// <summary>
    /// Properties for a specific content type.
    /// </summary>
    public class ContentTypeProperties
    {
        /// <summary>
        /// The properties for this content type.
        /// </summary>
        public List<Property> Properties { get; set; }
    }

    /// <summary>
    /// A property in the request body.
    /// </summary>
    public class Property
    {
        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the property.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The value of the property.
        /// </summary>
        public string Value { get; set; }
    }
} 
