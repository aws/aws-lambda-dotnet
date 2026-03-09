using System;
using System.Collections.Generic;
using System.IO;

#if NET6_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Implementation class for <see cref="IAuthorizerResult"/>. Use the static <see cref="Allow"/> and <see cref="Deny"/>
    /// factory methods to create instances, then optionally chain <see cref="WithContext"/> and <see cref="WithPrincipalId"/>
    /// to add context values and a principal ID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For HTTP API simple authorizers, the result is serialized to:
    /// <c>{ "isAuthorized": true/false, "context": { ... } }</c>
    /// </para>
    /// <para>
    /// For REST API and HTTP API IAM policy authorizers, the result is serialized to an IAM policy document
    /// with an Allow or Deny effect. The policy resource ARN is derived from the request's MethodArn.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple allow with context
    /// return AuthorizerResults.Allow()
    ///     .WithContext("userId", "user-123")
    ///     .WithContext("role", "admin");
    ///
    /// // Simple deny
    /// return AuthorizerResults.Deny();
    ///
    /// // REST API with principal ID
    /// return AuthorizerResults.Allow()
    ///     .WithPrincipalId("user-123")
    ///     .WithContext("tenantId", "42");
    /// </code>
    /// </example>
    public class AuthorizerResults : IAuthorizerResult
    {
        private AuthorizerResults(bool isAuthorized)
        {
            IsAuthorized = isAuthorized;
        }

        /// <inheritdoc/>
        public bool IsAuthorized { get; }

        /// <inheritdoc/>
        public string PrincipalId { get; private set; }

        /// <inheritdoc/>
        public IDictionary<string, object> Context { get; private set; }

        /// <summary>
        /// Creates an <see cref="IAuthorizerResult"/> that allows the request.
        /// </summary>
        /// <returns>An authorized result instance.</returns>
        public static IAuthorizerResult Allow()
        {
            return new AuthorizerResults(true);
        }

        /// <summary>
        /// Creates an <see cref="IAuthorizerResult"/> that denies the request.
        /// </summary>
        /// <returns>A denied result instance.</returns>
        public static IAuthorizerResult Deny()
        {
            return new AuthorizerResults(false);
        }

        /// <inheritdoc/>
        public IAuthorizerResult WithContext(string key, object value)
        {
            if (Context == null)
            {
                Context = new Dictionary<string, object>();
            }

            Context[key] = value;
            return this;
        }

        /// <inheritdoc/>
        public IAuthorizerResult WithPrincipalId(string principalId)
        {
            PrincipalId = principalId;
            return this;
        }

        /// <summary>
        /// Serializes the authorizer result into the correct API Gateway response format as a JSON stream.
        /// This method is called by the generated Lambda handler code.
        /// </summary>
        /// <param name="options">Serialization options that determine the output format.</param>
        /// <returns>The serialized response object appropriate for the authorizer format.</returns>
        public object Serialize(AuthorizerResultSerializationOptions options)
        {
#if NETSTANDARD2_0
            throw new NotImplementedException();
#else
            switch (options.Format)
            {
                case AuthorizerResultSerializationOptions.AuthorizerFormat.HttpApiSimple:
                    return SerializeSimpleResponse();

                case AuthorizerResultSerializationOptions.AuthorizerFormat.HttpApiIamPolicy:
                case AuthorizerResultSerializationOptions.AuthorizerFormat.RestApi:
                    return SerializeIamPolicyResponse(options.MethodArn);

                default:
                    throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported authorizer format: {options.Format}");
            }
#endif
        }

#if !NETSTANDARD2_0
        private Stream SerializeSimpleResponse()
        {
            var response = new SimpleAuthorizerResponse
            {
                IsAuthorized = IsAuthorized,
                Context = Context != null ? new Dictionary<string, object>(Context) : null
            };

            var stream = new MemoryStream();
            JsonSerializer.Serialize(stream, response, typeof(SimpleAuthorizerResponse), AuthorizerResponseJsonSerializerContext.Default);
            stream.Position = 0;
            return stream;
        }

        private Stream SerializeIamPolicyResponse(string methodArn)
        {
            var effect = IsAuthorized ? "Allow" : "Deny";
            var resource = methodArn ?? "*";

            var response = new IamPolicyAuthorizerResponse
            {
                PrincipalId = PrincipalId ?? "user",
                PolicyDocument = new PolicyDocument
                {
                    Version = "2012-10-17",
                    Statement = new List<PolicyStatement>
                    {
                        new PolicyStatement
                        {
                            Action = "execute-api:Invoke",
                            Effect = effect,
                            Resource = resource
                        }
                    }
                },
                Context = Context != null ? new Dictionary<string, object>(Context) : null
            };

            var stream = new MemoryStream();
            JsonSerializer.Serialize(stream, response, typeof(IamPolicyAuthorizerResponse), AuthorizerResponseJsonSerializerContext.Default);
            stream.Position = 0;
            return stream;
        }

        // Internal response types that mirror the API Gateway authorizer response structures.
        // The Annotations library cannot take a dependency on Amazon.Lambda.APIGatewayEvents,
        // so it defines its own serializable types (same pattern as HttpResults).

        internal class SimpleAuthorizerResponse
        {
            [JsonPropertyName("isAuthorized")]
            public bool IsAuthorized { get; set; }

            [JsonPropertyName("context")]
            public Dictionary<string, object> Context { get; set; }
        }

        internal class IamPolicyAuthorizerResponse
        {
            [JsonPropertyName("principalId")]
            public string PrincipalId { get; set; }

            [JsonPropertyName("policyDocument")]
            public PolicyDocument PolicyDocument { get; set; }

            [JsonPropertyName("context")]
            public Dictionary<string, object> Context { get; set; }
        }

        internal class PolicyDocument
        {
            [JsonPropertyName("Version")]
            public string Version { get; set; }

            [JsonPropertyName("Statement")]
            public List<PolicyStatement> Statement { get; set; }
        }

        internal class PolicyStatement
        {
            [JsonPropertyName("Action")]
            public string Action { get; set; }

            [JsonPropertyName("Effect")]
            public string Effect { get; set; }

            [JsonPropertyName("Resource")]
            public string Resource { get; set; }
        }
#endif
    }

#if !NETSTANDARD2_0
    [JsonSerializable(typeof(AuthorizerResults.SimpleAuthorizerResponse))]
    [JsonSerializable(typeof(AuthorizerResults.IamPolicyAuthorizerResponse))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    internal partial class AuthorizerResponseJsonSerializerContext : JsonSerializerContext
    {
    }
#endif
}
