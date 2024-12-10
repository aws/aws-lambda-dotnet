namespace Amazon.Lambda.TestTool.UnitTests.Helpers;

internal static class TestHelpers
{
    internal static async Task<bool> WaitForApiToStartAsync(string url, int maxRetries = 5, int delayMilliseconds = 1000)
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

    internal static async Task CancelAndWaitAsync(Task executeTask)
    {
        await Task.Delay(1000);
    }
}
