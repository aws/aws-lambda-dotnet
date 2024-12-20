using System.Collections.Concurrent;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// The runtime api data store manager is used to partition the Lambda events per Lambda function using the Lambda runtime api.
/// </summary>
public interface IRuntimeApiDataStoreManager
{
    /// <summary>
    /// Gets the IRuntimeApiDataStore for the Lambda function
    /// </summary>
    /// <param name="functionName"></param>
    /// <returns></returns>
    IRuntimeApiDataStore GetLambdaRuntimeDataStore(string functionName);

    /// <summary>
    /// Gets the list of lambda functions. For each Lambda function that calls into Lambda runtime api the GetLambdaRuntimeDataStore is
    /// called creating a data store. This method returns that list of functions that have had a data store created.
    /// </summary>
    /// <returns></returns>
    string[] GetListOfFunctionNames();
}

/// <inheritdoc/>
internal class RuntimeApiDataStoreManager : IRuntimeApiDataStoreManager
{
    private readonly ConcurrentDictionary<string, IRuntimeApiDataStore> _dataStores = new ConcurrentDictionary<string, IRuntimeApiDataStore>();

    /// <inheritdoc/>
    public IRuntimeApiDataStore GetLambdaRuntimeDataStore(string functionName)
    {
        return _dataStores.GetOrAdd(functionName, name => new RuntimeApiDataStore());
    }

    /// <inheritdoc/>
    public string[] GetListOfFunctionNames()
    {
        return _dataStores.Keys.ToArray();
    }
}
