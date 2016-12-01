using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Logging.AspNetCore.Tests
{
    public class TestLoggerFactory : ILoggerFactory
    {
        private ILoggerProvider _provider;

        public void AddProvider(ILoggerProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            if (_provider != null)
            {
                throw new InvalidOperationException("Provider is already set, cannot add another.");
            }

            _provider = provider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _provider.CreateLogger(categoryName);
        }

        public void Dispose()
        {
            var provider = _provider;
            _provider = null;
            if (provider != null)
            {
                provider.Dispose();
            }
        }
    }
}
