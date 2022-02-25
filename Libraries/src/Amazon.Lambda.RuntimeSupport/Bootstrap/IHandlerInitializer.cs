using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    /// <summary>
    /// This interface provides a way for classes implementing handlers for C# Lambda
    /// implementations to initialize an instance of the handler class asynchronously
    /// before the handler method starts being invoked by the Lambda runtime.
    /// </summary>
    public interface IHandlerInitializer
    {
        /// <summary>
        /// Initializes the Lambda handler as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation to
        /// initialize the Lambda handler which return <see langword="true"/> if the
        /// handler was successfully initialized, otherwise <see langword="false"/>.
        /// </returns>
        Task<bool> InitializeAsync();
    }
}
