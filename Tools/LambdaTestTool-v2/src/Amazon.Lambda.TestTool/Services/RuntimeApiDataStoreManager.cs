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

    /// <summary>
    /// An event that some event or event collection has changed. This is used by the UI to
    /// know when it should refresh.
    /// </summary>
    event EventHandler? StateChange;
}

/// <inheritdoc/>
internal class RuntimeApiDataStoreManager : IRuntimeApiDataStoreManager
{
    private readonly ConcurrentDictionary<string, IRuntimeApiDataStore> _dataStores = new ConcurrentDictionary<string, IRuntimeApiDataStore>();

    /// <inheritdoc/>
    public event EventHandler? StateChange;

    /// <inheritdoc/>
    public IRuntimeApiDataStore GetLambdaRuntimeDataStore(string functionName)
    {
        if (_dataStores.ContainsKey(functionName))
        {
            return _dataStores[functionName];
        }
        else
        {
            _dataStores[functionName] = new RuntimeApiDataStore();
            RaiseStateChanged();
            return _dataStores[functionName];
        }
    }

    /// <inheritdoc/>
    public string[] GetListOfFunctionNames()
    {
        return _dataStores.Keys.ToArray();
    }

    private void RaiseStateChanged()
    {
        var handler = StateChange;
        handler?.Invoke(this, EventArgs.Empty);
    }
}
