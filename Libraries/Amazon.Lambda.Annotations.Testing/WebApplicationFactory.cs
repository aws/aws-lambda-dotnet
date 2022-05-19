using System.Reflection;

namespace Amazon.Lambda.Annotations.Testing
{
    public class WebApplicationFactory
    {
        private readonly Assembly _assembly;
        private string _templateFilePath;

        public WebApplicationFactory(Assembly assembly)
        {
            _assembly = assembly;
        }

        public WebApplicationFactory WithTemplateFilePath(string templateFilePath)
        {
            _templateFilePath = templateFilePath;
            return this;
        }

        public HttpClient CreateHttpClient(params DelegatingHandler[] delegatingHandlers)
        {
            var handler = CreateHttpMessageHandler();

            foreach (var delegatingHandler in delegatingHandlers.Reverse())
            {
                delegatingHandler.InnerHandler = handler;
                handler = delegatingHandler;
            }

            return new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            };
        }

        private HttpMessageHandler CreateHttpMessageHandler()
        {
            var templateFile = _templateFilePath
                ?? DetermineTemplatePath(_assembly)
                ?? throw new InvalidOperationException("Template file is not found.");

            var routeConfigs = ServerlessTemplateParser.GetConfigsFromTemplate(templateFile);

            return new LambdaApplicationHttpClientHandler(routeConfigs, new[] { _assembly });
        }

        private static string DetermineTemplatePath(Assembly assembly)
        {
            // Just find any *.template file. It needs to be copied to output directory.
            return Directory.GetParent(assembly.Location)
                .EnumerateFiles()
                .FirstOrDefault(file => file.Name.EndsWith(".template"))
                ?.FullName;
        }
    }
}
