// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

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
}
