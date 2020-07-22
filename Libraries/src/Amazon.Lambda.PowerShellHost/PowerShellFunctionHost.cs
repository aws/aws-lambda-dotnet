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
                this._ps = PowerShell.Create(state);                
            }
            else
            {
                this._ps = PowerShell.Create();                                
            }

            this.SetupStreamHandlers();
            this.LoadModules();

            // Can be true if there was an exception importing modules packaged with the function.
            if(this._lastException != null)
            {
                Console.WriteLine(this._constructorLoggingBuffer.ToString());
                throw this._lastException;
            }

            this.PowerShellFunctionName = Environment.GetEnvironmentVariable(POWERSHELL_FUNCTION_ENV);
            if(!string.IsNullOrEmpty(this.PowerShellFunctionName))
            {
                this._constructorLoggingBuffer.AppendLine($"Configured to call function {this.PowerShellFunctionName} from the PowerShell script.");
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
            this._powerShellScriptFileName = powerShellScriptFileName;
        }

        /// <summary>
        /// AWS Lambda function handler that will execute the PowerShell script with the PowerShell Core runtime initiated during the construction of the class.
            /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Stream ExecuteFunction(Stream inputStream, ILambdaContext context)
        {
            this._lastException = null;

            if (this._runFirstTimeInitialization)
            {
                this._logger = context.Logger;

                if (this._constructorLoggingBuffer?.Length > 0)
                {
                    context.Logger.Log(this._constructorLoggingBuffer.ToString());
                    this._constructorLoggingBuffer = null;
                }

                this._runFirstTimeInitialization = false;
            }

            string inputString;
            using (var reader = new StreamReader(inputStream))
            {
                inputString = reader.ReadToEnd();
            }

            var result = this.BeginInvoke(inputString, context);
            this.WaitPowerShellExecution(result);

            if (this._lastException != null || this._ps.InvocationStateInfo.State == PSInvocationState.Failed)
            {
                var exception = this._exceptionManager.DetermineExceptionToThrow(this._lastException ?? this._ps.InvocationStateInfo.Reason);
                throw exception;
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(this.GetExecutionOutput()));
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
            this._ps.Commands?.Clear();
            this._ps.Streams.Verbose?.Clear();
            this._ps.Streams.Error?.Clear();
            this._ps.Runspace?.ResetRunspaceState();
            this._output.Clear();

            var providedScript = LoadScript(input, context);


            string executingScript = 
@"
Param(
   [string]$LambdaInputString,
   [Amazon.Lambda.Core.ILambdaContext]$LambdaContext
)
  
$LambdaInput = ConvertFrom-Json -InputObject $LambdaInputString

";

            var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT"));

            var tempFolder = isLambda ? "/tmp" : Path.GetTempPath();

            executingScript += $"{Environment.NewLine}$env:TEMP=\"{tempFolder}\"";
            executingScript += $"{Environment.NewLine}$env:TMP=\"{tempFolder}\"";
            executingScript += $"{Environment.NewLine}$env:TMPDIR=\"{tempFolder}\"{Environment.NewLine}";

            if(isLambda && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOME")))
            {
                // Make sure to set HOME directory to avoid issue with using the -Parallel PowerShell feature. This works around
                // a reported issue to the PowerShell team.
                // https://github.com/PowerShell/PowerShell/issues/13189
                Environment.SetEnvironmentVariable("HOME", $"{tempFolder}/home");
            }

            executingScript += providedScript;

            if (!string.IsNullOrEmpty(this.PowerShellFunctionName))
            {
                executingScript += $"{Environment.NewLine}{this.PowerShellFunctionName} $LambdaInput $LambdaContext{Environment.NewLine}";
            }


            this._ps.AddScript(executingScript);
            this._ps.AddParameter("LambdaInputString", input);
            this._ps.AddParameter("LambdaContext", context);


            return this._ps.BeginInvoke<PSObject, PSObject>(null, this._output);
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
            if(this._powerShellScriptFileContent != null)
            {
                return this._powerShellScriptFileContent;
            }

            if(string.IsNullOrEmpty(this._powerShellScriptFileName))
            {
                throw new LambdaPowerShellException("No PowerShell script specified to be executed. Either specify a script in the constructor or override the LoadScript method.");
            }
            if(!File.Exists(this._powerShellScriptFileName))
            {
                throw new LambdaPowerShellException($"Failed to find PowerShell script {this._powerShellScriptFileName}. Make sure the script is included with the package bundle.");
            }

            this._powerShellScriptFileContent = File.ReadAllText(this._powerShellScriptFileName);

            return this._powerShellScriptFileContent;
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
            var responseObject = this._output?.LastOrDefault();
            if (responseObject == null)
            {
                return string.Empty;
            }
            else if(responseObject.BaseObject is string baseObj)
            {
                return baseObj;
            }

            this._ps.Commands?.Clear();
            this._ps.Runspace?.ResetRunspaceState();

            string executingScript = @"
Param(
   [PSObject]$Response
)
  
ConvertTo-Json $Response

";
            this._ps.AddScript(executingScript);
            this._ps.AddParameter("Response", responseObject);
            var marshalled = this._ps.Invoke();

            return marshalled.FirstOrDefault()?.BaseObject as string;
        }

        private void SetupStreamHandlers()
        {
            this._output = new PSDataCollection<PSObject>();

            Func<string, EventHandler<DataAddingEventArgs>> _loggerFactory = (prefix) =>
            {
                EventHandler <DataAddingEventArgs> handler = (sender, e) =>
                {
                    var message = e?.ItemAdded?.ToString();

                    LogMessage(prefix, message);

                    var errorRecord = e?.ItemAdded as ErrorRecord;
                    if (errorRecord?.Exception != null)
                    {
                        this._lastException = errorRecord.Exception;
                    }
                };
                return handler;
            };

            this._ps.Streams.Verbose.DataAdding += _loggerFactory("Verbose");
            this._ps.Streams.Information.DataAdding += _loggerFactory("Information");
            this._ps.Streams.Warning.DataAdding += _loggerFactory("Warning");
            this._ps.Streams.Error.DataAdding += _loggerFactory("Error");
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

            if (this._logger != null)
            {
                this._logger.LogLine(message);
            }
            else
            {
                this._constructorLoggingBuffer.AppendLine(message);
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
                var result = this._ps.AddScript($"Import-Module \"{psd1Path}\"").BeginInvoke();
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
