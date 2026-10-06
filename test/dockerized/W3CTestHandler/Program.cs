// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace W3CTestHandler
{
    public static class Program
    {
        private static readonly DefaultLambdaJsonSerializer JsonSerializer = new DefaultLambdaJsonSerializer();

        public static async Task Main(string[] args)
        {
            // The RIE and the Lambda runtime API both pass the configured
            // handler through the _HANDLER environment variable.
            var handler = Environment.GetEnvironmentVariable("_HANDLER") ?? string.Empty;

            Func<JsonNode, ILambdaContext, object> dispatch = handler switch
            {
                "w3c.get_w3c" => GetW3c,
                "w3c.get_w3c_and_source" => GetW3cAndSource,
                "w3c.echo_client_context" => EchoClientContext,
                "w3c.w3c_is_callable" => W3CIsCallable,
                _ => throw new Exception($"Handler '{handler}' is not supported."),
            };

            using var wrapper = HandlerWrapper.GetHandlerWrapper(
                (Func<JsonNode, ILambdaContext, object>)dispatch,
                JsonSerializer);
            using var bootstrap = new LambdaBootstrap(wrapper);
            await bootstrap.RunAsync();
        }

        /// <summary>Returns the raw W3C dictionary — the primary assertion target.</summary>
        public static object GetW3c(JsonNode input, ILambdaContext context)
        {
            // Returning the IReadOnlyDictionary directly serializes as a JSON object.
            return context.W3C;
        }

        public static object GetW3cAndSource(JsonNode input, ILambdaContext context)
        {
            var clientContext = context.ClientContext;
            return new Dictionary<string, object>
            {
                ["w3c"] = context.W3C,
                ["clientContextIsDefined"] = clientContext != null,
                // IClientContext has no "w3c" accessor at all on .NET — the
                // strip-the-source step is naturally met by the type system.
                ["clientContextHasW3c"] = false,
                ["clientContext"] = SerializeClientContext(clientContext),
            };
        }

        public static object EchoClientContext(JsonNode input, ILambdaContext context)
        {
            return SerializeClientContext(context.ClientContext);
        }

        public static object W3CIsCallable(JsonNode input, ILambdaContext context)
        {
            var property = context.GetType().GetProperty(nameof(ILambdaContext.W3C));
            return new Dictionary<string, object>
            {
                ["isCallable"] = property != null,
            };
        }

        private static object SerializeClientContext(IClientContext clientContext)
        {
            if (clientContext == null)
            {
                return null;
            }
            var result = new Dictionary<string, object>();
            if (clientContext.Custom != null && clientContext.Custom.Count > 0)
            {
                result["custom"] = clientContext.Custom.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            }
            if (clientContext.Environment != null && clientContext.Environment.Count > 0)
            {
                result["env"] = clientContext.Environment.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            }
            return result.Count == 0 ? null : result;
        }
    }
}
