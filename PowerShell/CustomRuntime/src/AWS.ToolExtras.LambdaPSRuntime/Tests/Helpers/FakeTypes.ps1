# Stand-in types the tests throw or plug in, compiled once per process. Each set lives in a
# test-owned namespace, so it can never collide with the real AWS SDK types on a runner that has
# AWS.Tools installed.

# Stand-ins for the AWS SDK service exceptions. Test-LambdaPSServiceError matches an exception by its
# type NAME or its ErrorCode, walking InnerException, so these carry the SDK's type names and ErrorCode
# values and can be thrown without a service call. ServiceException has a settable ErrorCode and an unrelated type name, for the
# ErrorCode-only match.
if (-not ('LambdaPSTests.Fakes.ResourceConflictException' -as [type])) {
    Add-Type -TypeDefinition @'
namespace LambdaPSTests.Fakes {
    public class ResourceConflictException : System.Exception {
        public string ErrorCode { get; set; }
        public ResourceConflictException(string message) : base(message) { }
    }
    public class AccessDeniedException : System.Exception {
        public string ErrorCode { get { return "AccessDeniedException"; } }
        public AccessDeniedException(string message) : base(message) { }
    }
    public class ResourceNotFoundException : System.Exception {
        public string ErrorCode { get { return "ResourceNotFoundException"; } }
        public ResourceNotFoundException(string message) : base(message) { }
    }
    public class RepositoryNotFoundException : System.Exception {
        public string ErrorCode { get { return "RepositoryNotFoundException"; } }
        public RepositoryNotFoundException(string message) : base(message) { }
    }
    public class ImageNotFoundException : System.Exception {
        public string ErrorCode { get { return "ImageNotFoundException"; } }
        public ImageNotFoundException(string message) : base(message) { }
    }
    public class RepositoryPolicyNotFoundException : System.Exception {
        public string ErrorCode { get { return "RepositoryPolicyNotFoundException"; } }
        public RepositoryPolicyNotFoundException(string message) : base(message) { }
    }
    public class RepositoryAlreadyExistsException : System.Exception {
        public string ErrorCode { get { return "RepositoryAlreadyExistsException"; } }
        public RepositoryAlreadyExistsException(string message) : base(message) { }
    }
    public class ServiceException : System.Exception {
        public string ErrorCode { get; set; }
        public ServiceException(string message, string errorCode) : base(message) { ErrorCode = errorCode; }
    }
}
'@
}

# Two HttpMessageHandlers, so the Runtime API POST path can be exercised without a Runtime API:
# FixedStatusHandler answers every request with a fixed status, and UnreachableHandler fails every
# request as a refused connection does.
if (-not ('LambdaPSTests.Fakes.UnreachableHandler' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace LambdaPSTests.Fakes {
    public class FixedStatusHandler : HttpMessageHandler {
        public int Status;
        public string Body = "";
        public int Calls;
        public string LastUri;
        public FixedStatusHandler(int status, string body) { Status = status; Body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            Calls++;
            LastUri = request.RequestUri.ToString();
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status) { Content = new StringContent(Body) });
        }
    }
    public class UnreachableHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            throw new HttpRequestException("Connection refused");
        }
    }
}
'@
}
