// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace Amazon.Lambda.TestTool.Tests.Common.Helpers;

public static class TestHelpers
{
    public static async Task<bool> WaitForApiToStartAsync(string url, int maxRetries = 5, int delayMilliseconds = 1000)
    {
        using (var client = new HttpClient())
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore exceptions, as the API might not yet be available
                }

                await Task.Delay(delayMilliseconds);
            }

            return false;
        }
    }

    public static async Task<HttpResponseMessage> SendRequest(string url)
    {
        using (var client = new HttpClient())
        {
            return await client.GetAsync(url);
        }
    }

    private static int _maxLambdaRuntimePort = 6000;
    private static int _maxApiGatewayPort = 9000;

    public static int GetNextLambdaRuntimePort()
    {
        return Interlocked.Increment(ref _maxLambdaRuntimePort);
    }

    public static int GetNextApiGatewayPort()
    {
        return Interlocked.Increment(ref _maxApiGatewayPort);
    }

    private static RandomNumberGenerator rng = RandomNumberGenerator.Create();
    public static int GetRandomIntegerInRange(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than maxValue");

        // Create a byte array to hold the random bytes
        byte[] randomBytes = new byte[4];

        // Fill the array with random bytes
        rng.GetBytes(randomBytes);

        // Convert the bytes to an integer
        int randomInteger = BitConverter.ToInt32(randomBytes, 0);

        // Make sure the random integer is within the desired range
        // Apply modulus to get a number in the range [0, maxValue - minValue]
        int range = maxValue - minValue;
        return (Math.Abs(randomInteger) % range) + minValue;
    }
}
