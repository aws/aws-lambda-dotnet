using System;

namespace TestServerlessApp.IntegrationTests.Helpers
{
    public static class StaticHelpers
    {
        public static TimeSpan GetWaitTime(int attemptCount)
        {
            var waitTime = Math.Pow(2, attemptCount) * 5;
            return TimeSpan.FromSeconds(waitTime);
        }
    }
}