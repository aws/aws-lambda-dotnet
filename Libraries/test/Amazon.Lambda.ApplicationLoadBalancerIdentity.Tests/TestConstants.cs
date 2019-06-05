namespace Amazon.Lambda.ApplicationLoadBalancerIdentity.Tests
{
    using System;
    using System.IO;
    using System.Threading;

    public static class TestConstants
    {
        public static readonly string TokenData = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "token.txt"));

        public static readonly string PublicKey = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "public_key.txt"));

        public static readonly SemaphoreSlim TestLock = new SemaphoreSlim(1, 1);
    }
}
