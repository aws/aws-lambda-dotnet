// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Services
{
    public class LambdaClientTests : IDisposable
    {
        private readonly Mock<IOptions<RunCommandSettings>> _mockSettings;
        private readonly RunCommandSettings _settings;
        private readonly LambdaClient _client;

        public LambdaClientTests()
        {
            _settings = new RunCommandSettings
            {
                LambdaEmulatorHost = "localhost",
                LambdaEmulatorPort = 5050
            };
            _mockSettings = new Mock<IOptions<RunCommandSettings>>();
            _mockSettings.Setup(s => s.Value).Returns(_settings);
            _client = new LambdaClient(_mockSettings.Object);
        }

        [Fact]
        public void Constructor_InitializesCorrectly()
        {
            // Assert
            var expectedEndpoint = $"http://{_settings.LambdaEmulatorHost}:{_settings.LambdaEmulatorPort}";
            Assert.Single(_client.Clients);
            Assert.True(_client.Clients.ContainsKey(expectedEndpoint));
        }

        [Fact]
        public async Task InvokeAsync_UsesCurrentEndpoint()
        {
            // Arrange
            var request = new InvokeRequest();

            // Act & Assert
            await Assert.ThrowsAsync<AmazonLambdaException>(
                async () => await _client.InvokeAsync(request));
        }

        [Fact]
        public void SetEndpoint_CreatesNewClientForNewEndpoint()
        {
            // Arrange
            var newEndpoint = "http://newhost:1234";
            var initialCount = _client.Clients.Count;

            // Act
            _client.SetEndpoint(newEndpoint);

            // Assert
            Assert.True(_client.Clients.ContainsKey(newEndpoint));
            Assert.Equal(initialCount + 1, _client.Clients.Count);
        }

        [Fact]
        public void SetEndpoint_ReuseExistingClientForSameEndpoint()
        {
            // Arrange
            var initialEndpoint = $"http://{_settings.LambdaEmulatorHost}:{_settings.LambdaEmulatorPort}";
            var initialCount = _client.Clients.Count;

            // Act
            _client.SetEndpoint(initialEndpoint);

            // Assert
            Assert.Equal(initialCount, _client.Clients.Count);
            Assert.True(_client.Clients.ContainsKey(initialEndpoint));
        }

        [Fact]
        public void Dispose_ClearsAllClients()
        {
            // Act
            _client.Dispose();

            // Assert
            Assert.Empty(_client.Clients);
        }

        [Fact]
        public async Task Dispose_PreventsSubsequentOperations()
        {
            // Arrange
            _client.Dispose();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                async () => await _client.InvokeAsync(new InvokeRequest()));
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
