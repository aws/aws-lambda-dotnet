using Amazon.Lambda.Core;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.PowerShellHost
{
    /// <summary>
    /// Base class for Lambda functions hosting PowerShell Core runtime
    /// </summary>
    public abstract class PowerShellFunctionHost
    {
        readonly ExceptionManager _exceptionManager = new ExceptionManager();

        /// <summary>
        /// When using a PowerShell function handler the function is identified in this environment variable.
        /// </summary>
        public const string POWERSHELL_FUNCTION_ENV = "AWS_POWERSHELL_FUNCTION_HANDLER";

        /// <summary>
        /// An optional property that identifies the PowerShell function to execute once the script is loaded.
        /// Otherwise just the script will be executed.
        /// </summary>
        public virtual string PowerShellFunctionName {get;set;}

        // Resource file included with the Lambda package bundle that will be executed.
        private readonly string _powerShellScriptFileName;
        private string _powerShellScriptFileContent;

        // Cached result of detecting whether a subclass has overridden LoadScript().
        // When overridden, the host must execute the override's returned text instead of
        // invoking the file directly, to preserve the pre-5.x contract that override return
        // values are always executed. Lazy + short-circuit: only evaluated when a script
        // file is present, then cached.
        private bool? _isLoadScriptOverriddenCache;

        // The PowerShell Object for executing PowerShell code
        private readonly PowerShell _ps;

        // Holds the PSObject Standard Output from the PowerShell execution
        private PSDataCollection<PSObject> _output;

        // Holds the exception captured from the stream capture of the PowerShell execution. This is the exception returned to Lambda
        // when the script execution stops.
        private Exception _lastException;

        private bool _runFirstTimeInitialization = true;

        // Logging messages that happen during the constructor are saved in a buffer to be written out once we have an instance of
        // ILambdaLogger. The preference for using ILambdaLogger over Console.WriteLine is so that tools like the console or the VS toolkit
        // that invoke the functions and request the tail will also get these logging messages.
        private StringBuilder _constructorLoggingBuffer = new StringBuilder();
        private ILambdaLogger _logger;

        /// <summary>
        /// Creates an instances of the class. As part of creation it will initiate the PowerShell Core runtime and load any required PowerShell modules.
        /// </summary>
        protected PowerShellFunctionHost()
        {
            // This will only be true for local testing.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var state = InitialSessionState.CreateDefault();
                state.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
                _ps = PowerShell.Create(state);                
            }
            else
            {
                _ps = PowerShell.Create();                                
            }

            SetupStreamHandlers();
            LoadModules();

            // Can be true if there was an exception importing modules packaged with the function.
            if(_lastException != null)
            {
                Console.WriteLine(_constructorLoggingBuffer.ToString());
                throw _lastException;
            }

            PowerShellFunctionName = Environment.GetEnvironmentVariable(POWERSHELL_FUNCTION_ENV);
            if(!string.IsNullOrEmpty(PowerShellFunctionName))
            {
                _constructorLoggingBuffer.AppendLine($"Configured to call function {PowerShellFunctionName} from the PowerShell script.");
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates an instances of the class. As part of creation it will initiated the PowerShell Core runtime and load any required PowerShell modules.
        /// </summary>
        /// <param name="powerShellScriptFileName">The PowerShell script that will run as part of every Lambda invocation</param>
        protected PowerShellFunctionHost(string powerShellScriptFileName)
            : this()
        {
            _powerShellScriptFileName = powerShellScriptFileName;
        }

        /// <summary>
        /// AWS Lambda function handler that will execute the PowerShell script with the PowerShell Core runtime initiated during the construction of the class.
            /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Stream ExecuteFunction(Stream inputStream, ILambdaContext context)
        {
            _lastException = null;

            if (_runFirstTimeInitialization)
            {
                _logger = context.Logger;

                if (_constructorLoggingBuffer?.Length > 0)
                {
                    context.Logger.Log(_constructorLoggingBuffer.ToString());
                    _constructorLoggingBuffer = null;
                }

                _runFirstTimeInitialization = false;
            }

            string inputString;
            using (var reader = new StreamReader(inputStream))
            {
                inputString = reader.ReadToEnd();
            }

            var result = BeginInvoke(inputString, context);
            WaitPowerShellExecution(result);

            if (_lastException != null || _ps.InvocationStateInfo.State == PSInvocationState.Failed)
            {
                var exception = _exceptionManager.DetermineExceptionToThrow(_lastException ?? _ps.InvocationStateInfo.Reason);
                throw exception;
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(GetExecutionOutput()));
        }

        /// <summary>
        /// Begin the async PowerShell execution
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private IAsyncResult BeginInvoke(string input, ILambdaContext context)
        {
            // Clear all previous PowerShell executions, variables and outputs
            _ps.Commands?.Clear();
            _ps.Streams.Verbose?.Clear();
            _ps.Streams.Debug?.Clear();
            _ps.Streams.Information?.Clear();
            _ps.Streams.Warning?.Clear();
            _ps.Streams.Error?.Clear();
            _ps.Runspace?.ResetRunspaceState();
            _output.Clear();

            var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT"));
            var tempFolder = isLambda ? "/tmp" : Path.GetTempPath();

            if(isLambda && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOME")))
            {
                // Make sure to set HOME directory to avoid issue with using the -Parallel PowerShell feature. This works around
                // a reported issue to the PowerShell team.
                // https://github.com/PowerShell/PowerShell/issues/13189
                Environment.SetEnvironmentVariable("HOME", $"{tempFolder}/home");
            }

            // Set environment variables and Lambda input/context as global variables.
            // Using $global: makes them visible in all scopes including the user's script.
            string setupScript = $@"
$env:TEMP = '{tempFolder}'
$env:TMP = '{tempFolder}'
$env:TMPDIR = '{tempFolder}'
$global:LambdaInputString = $args[0]
$global:LambdaInput = ConvertFrom-Json -InputObject $args[0]
$global:LambdaContext = $args[1]
";

            _ps.AddScript(setupScript, useLocalScope: false);
            _ps.AddArgument(input);
            _ps.AddArgument(context);
            var setupResult = _ps.BeginInvoke();
            WaitPowerShellExecution(setupResult);
            _ps.Commands.Clear();

            // Execute the user's script. Use the new AddCommand path only when a file exists
            // AND no subclass has overridden LoadScript(). Reflection is short-circuited away
            // entirely when no file path was provided (the common S3-fetched-script case).
            if (!string.IsNullOrEmpty(_powerShellScriptFileName)
                && File.Exists(_powerShellScriptFileName)
                && !IsLoadScriptOverridden())
            {
                var scriptFullPath = Path.GetFullPath(_powerShellScriptFileName);

                if (!string.IsNullOrEmpty(PowerShellFunctionName))
                {
                    // Dot-source the script so functions it defines are visible in the current scope,
                    // then call the named function.
                    _ps.AddScript(
                        $". '{scriptFullPath}'{Environment.NewLine}{PowerShellFunctionName} $global:LambdaInput $global:LambdaContext",
                        useLocalScope: false);
                }
                else
                {
                    // Invoke the script file directly. PowerShell resolves it as ExternalScript,
                    // populating $PSScriptRoot, $PSCommandPath, $MyInvocation, etc.
                    var command = new Command(scriptFullPath, isScript: true, useLocalScope: false);
                    _ps.Commands.AddCommand(command);
                }
            }
            else
            {
                // Either no file on disk, or a subclass has overridden LoadScript() and we
                // must execute its returned text rather than invoking the file directly.
                // Automatic variables will be empty in both cases since there is no backing
                // file from PowerShell's perspective.
                var providedScript = LoadScript(input, context);

                if (!string.IsNullOrEmpty(PowerShellFunctionName))
                {
                    providedScript += $"{Environment.NewLine}{PowerShellFunctionName} $global:LambdaInput $global:LambdaContext{Environment.NewLine}";
                }

                _ps.AddScript(providedScript, useLocalScope: false);
            }

            return _ps.BeginInvoke<PSObject, PSObject>(null, _output);
        }

        /// <summary>
        /// Reads the script from disk
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual string LoadScript(string input, ILambdaContext context)
        {
            // Check to see if the file contents have already been read.
            if(_powerShellScriptFileContent != null)
            {
                return _powerShellScriptFileContent;
            }

            if(string.IsNullOrEmpty(_powerShellScriptFileName))
            {
                throw new LambdaPowerShellException("No PowerShell script specified to be executed. Either specify a script in the constructor or override the LoadScript method.");
            }
            if(!File.Exists(_powerShellScriptFileName))
            {
                throw new LambdaPowerShellException($"Failed to find PowerShell script {_powerShellScriptFileName}. Make sure the script is included with the package bundle.");
            }

            _powerShellScriptFileContent = File.ReadAllText(_powerShellScriptFileName);

            return _powerShellScriptFileContent;
        }

        // Detects (and caches) whether a subclass has overridden LoadScript(). The override
        // is an officially advertised extension point; preserving its pre-5.x semantics is
        // required for backward compatibility with subclassers who transform script content.
        // Parameter types are specified explicitly so a future overload of LoadScript would
        // not throw AmbiguousMatchException, and a null result is handled explicitly so any
        // future signature/visibility change does not silently regress every caller onto the
        // legacy path.
        private bool IsLoadScriptOverridden()
        {
            if (!_isLoadScriptOverriddenCache.HasValue)
            {
                var method = GetType().GetMethod(
                    nameof(LoadScript),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(string), typeof(ILambdaContext) },
                    modifiers: null);
                _isLoadScriptOverriddenCache = method != null
                    && method.DeclaringType != typeof(PowerShellFunctionHost);
            }
            return _isLoadScriptOverriddenCache.Value;
        }

        /// <summary>
        /// Waits for the PowerShell execution to be completed
        /// </summary>
        private void WaitPowerShellExecution(IAsyncResult result)
        {
            while (!result.IsCompleted)
            {
                result.AsyncWaitHandle.WaitOne(500);
            }
        }

         /// <summary>
        /// Returns the string output from the PowerShell execution, or an empty string
        /// </summary>
        private string GetExecutionOutput()
        {
            var responseObject = _output?.LastOrDefault();
            if (responseObject == null)
            {
                return string.Empty;
            }
            else if(responseObject.BaseObject is string baseObj)
            {
                return baseObj;
            }

            _ps.Commands?.Clear();
            _ps.Runspace?.ResetRunspaceState();

            string executingScript = @"
Param(
   [PSObject]$Response
)
  
ConvertTo-Json $Response

";
            _ps.AddScript(executingScript);
            _ps.AddParameter("Response", responseObject);
            var marshalled = _ps.Invoke();

            return marshalled.FirstOrDefault()?.BaseObject as string;
        }

        private void SetupStreamHandlers()
        {
            _output = new PSDataCollection<PSObject>();

            Func<string, EventHandler<DataAddingEventArgs>> _loggerFactory = (prefix) =>
            {
                EventHandler <DataAddingEventArgs> handler = (sender, e) =>
                {
                    var message = e?.ItemAdded?.ToString();

                    LogMessage(prefix, message);

                    var errorRecord = e?.ItemAdded as ErrorRecord;
                    if (errorRecord?.Exception != null)
                    {
                        _lastException = errorRecord.Exception;
                    }
                };
                return handler;
            };

            _ps.Streams.Verbose.DataAdding += _loggerFactory("Verbose");
            _ps.Streams.Debug.DataAdding += _loggerFactory("Debug");
            _ps.Streams.Information.DataAdding += _loggerFactory("Information");
            _ps.Streams.Warning.DataAdding += _loggerFactory("Warning");
            _ps.Streams.Error.DataAdding += _loggerFactory("Error");
        }

        private void LogMessage(string prefix, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if(!string.IsNullOrEmpty(prefix))
            {
                message = $"[{prefix}] - {message}";
            }

            if (_logger != null)
            {
                _logger.LogLine(message);
            }
            else
            {
                _constructorLoggingBuffer.AppendLine(message);
            }
        }

        /// <summary>
        /// Import all the bundled PowerShell modules. Bundle modules are stored under the Modules folder with the structure Module-Name/Version-Number/Module-Name.psd1.
        /// It is possible that developer accidently includes multiple versions of the same module. If so we default to loading the latest version.
        /// </summary>
        private void LoadModules()
        {
            if (!Directory.Exists("./Modules"))
                return;

            foreach (var moduleDir in Directory.GetDirectories("./Modules"))
            {
                var versionDir = Directory.GetDirectories(moduleDir).OrderByDescending(x => Version.TryParse(new DirectoryInfo(x).Name, out var version) ? version : new Version("0.0.0")).FirstOrDefault();
                if (string.IsNullOrEmpty(versionDir))
                    continue;
                
                var module = new DirectoryInfo(moduleDir).Name;

                var psd1Path = Path.Combine(versionDir, $"{module}.psd1");
                if (!File.Exists(psd1Path))
                {
                    var files = Directory.GetFiles(versionDir, "*.psd1", SearchOption.TopDirectoryOnly);
                    if (files.Length == 1)
                    {
                        psd1Path = files[0];
                    }
                }

                if (!File.Exists(psd1Path))
                {
                    _constructorLoggingBuffer.AppendLine($"Unable to determine psd1 file for {module} ({new DirectoryInfo(versionDir).Name})");
                    continue;
                }

                _constructorLoggingBuffer.AppendLine($"Importing module {psd1Path}");
                var result = _ps.AddScript($"Import-Module \"{psd1Path}\"").BeginInvoke();
                WaitPowerShellExecution(result);
            }
        }

        static PowerShellFunctionHost()
        {
            const string envName = "AWS_EXECUTION_ENV";
            const string powershellEnv = "powershell";

            var envValue = Environment.GetEnvironmentVariable(envName);
            if(!string.IsNullOrEmpty(envValue) && !envValue.Contains(powershellEnv))
            {
                var assemblyVersion = typeof(PowerShellFunctionHost).Assembly
                    .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                    .FirstOrDefault()
                    as AssemblyInformationalVersionAttribute;

                Environment.SetEnvironmentVariable(envName, $"{envValue}_{powershellEnv}_{assemblyVersion?.InformationalVersion}");
            }
        }
    }
}
