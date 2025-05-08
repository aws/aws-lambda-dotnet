// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0


using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Services;
using Moq;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Services
{
     public class LambdaClientTests : IDisposable
    {
        private readonly LambdaClient _lambdaClient;
        private readonly InvokeRequest _validRequest;

        public LambdaClientTests()
        {
            _lambdaClient = new LambdaClient();
            _validRequest = new InvokeRequest
            {
                FunctionName = "TestFunction",
                Payload = "{}"
            };
        }

        [Fact]
        public void InvokeAsync_CreatesNewClientForNewEndpoint()
        {
            // Arrange
            var endpoint = "invalid://example.com";

            // Act
            _lambdaClient.InvokeAsync(_validRequest, endpoint);

            // Assert
            Assert.Single(_lambdaClient.Clients);
            Assert.True(_lambdaClient.Clients.ContainsKey(endpoint));
        }

        [Fact]
        public void InvokeAsync_ReuseExistingClientForSameEndpoint()
        {
            // Arrange
            var endpoint = "invalid://example.com";

            // Act
            _lambdaClient.InvokeAsync(_validRequest, endpoint);
            _lambdaClient.InvokeAsync(_validRequest, endpoint);

            // Assert
            Assert.Single(_lambdaClient.Clients);
            Assert.True(_lambdaClient.Clients.ContainsKey(endpoint));
        }

        [Fact]
        public void InvokeAsync_CreatesSeparateClientsForDifferentEndpoints()
        {
            // Arrange
            var endpoint1 = "invalid://example1.com";
            var endpoint2 = "invalid://example2.com";

            // Act
            _lambdaClient.InvokeAsync(_validRequest, endpoint1);
            _lambdaClient.InvokeAsync(_validRequest, endpoint2);

            // Assert
            Assert.Equal(2, _lambdaClient.Clients.Count);
            Assert.True(_lambdaClient.Clients.ContainsKey(endpoint1));
            Assert.True(_lambdaClient.Clients.ContainsKey(endpoint2));
        }

        [Fact]
        public void Dispose_ClearsClientDictionary()
        {
            // Arrange
            var endpoint = "invalid://example.com";
            _lambdaClient.InvokeAsync(_validRequest, endpoint);
            Assert.Single(_lambdaClient.Clients);

            // Act
            _lambdaClient.Dispose();

            // Assert
            Assert.Empty(_lambdaClient.Clients);
        }

        [Fact]
        public void MultipleEndpoints_CreateCorrectNumberOfClients()
        {
            // Arrange
            var endpoints = new[]
            {
                "invalid://example1.com",
                "invalid://example2.com",
                "invalid://example3.com",
                "invalid://example1.com" // Duplicate to test reuse
            };

            // Act
            foreach (var endpoint in endpoints)
            {
                _lambdaClient.InvokeAsync(_validRequest, endpoint);
            }

            // Assert
            Assert.Equal(3, _lambdaClient.Clients.Count);
            Assert.True(_lambdaClient.Clients.ContainsKey("invalid://example1.com"));
            Assert.True(_lambdaClient.Clients.ContainsKey("invalid://example2.com"));
            Assert.True(_lambdaClient.Clients.ContainsKey("invalid://example3.com"));
        }

        public void Dispose()
        {
            _lambdaClient.Dispose();
        }
    }
}
