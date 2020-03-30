using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.Services;

using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// This class will continually poll a SQS queue for more messages from a dead letter queue. If a message was read then the Lambda function 
    /// will be invoked within the test tool.
    /// </summary>
    public class DlqMonitor
    {
        private readonly object LOG_LOCK = new object();
        private CancellationTokenSource _cancelSource;
        private IList<LogRecord> _records = new List<LogRecord>();
        
        private readonly ILocalLambdaRuntime _runtime;
        private readonly LambdaFunction _function;
        private readonly string _profile;
        private readonly string _region;
        private readonly string _queueUrl;

        public DlqMonitor(ILocalLambdaRuntime runtime, LambdaFunction function, string profile, string region, string queueUrl)
        {
            this._runtime = runtime;
            this._function = function;
            this._profile = profile;
            this._region = region;
            this._queueUrl = queueUrl;
        }

        public void Start()
        {
            this._cancelSource = new CancellationTokenSource();
            _ = Loop(this._cancelSource.Token);
        }

        public void Stop()
        {
            this._cancelSource.Cancel();
        }

        private async Task Loop(CancellationToken token)
        {
            var aws = this._runtime.AWSService;
            while (!token.IsCancellationRequested)
            {
                Message message = null;
                LogRecord logRecord = null;
                try
                {
                    // Read a message from the queue using the ExternalCommands console application.
                    message = await aws.ReadMessageAsync(this._profile, this._region, this._queueUrl);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    if (message == null)
                    {
                        // Since there are no messages, sleep a bit to wait for messages to come.
                        Thread.Sleep(1000);
                        continue;
                    }

                    // If a message was received execute the Lambda function within the test tool.
                    var request = new ExecutionRequest
                    {
                        AWSProfile = this._profile,
                        AWSRegion =  this._region,
                        Function = this._function,
                        Payload = message.Body
                    };

                    var response = await this._runtime.ExecuteLambdaFunctionAsync(request);
                    
                    // Capture the results to send back to the client application.
                    logRecord = new LogRecord
                    {
                        ProcessTime = DateTime.Now,
                        ReceiptHandle = message.ReceiptHandle,
                        Logs = response.Logs,
                        Error = response.Error
                    };
                }
                catch (Exception e)
                {
                    logRecord = new LogRecord
                    {
                        ProcessTime = DateTime.Now,
                        Error = e.Message
                    };
                    
                    Thread.Sleep(1000);
                }

                if(logRecord != null && message != null)
                {
                    logRecord.Event = message.Body;
                }

                lock (LOG_LOCK)
                {                    
                    this._records.Add(logRecord);
                }
            }
        }

        // Grabs the log messages since last requests and then resets the records collection.
        public IList<LogRecord> FetchNewLogs()
        {
            lock (LOG_LOCK)
            {
                var logsToSend = this._records;
                this._records = new List<LogRecord>();
                return logsToSend;
            }
        }
        

        public class LogRecord
        {
            public DateTime ProcessTime { get; set; }
            public string Event { get; set; }
            public string Logs { get; set; }
            public string Error { get; set; }
            public string ReceiptHandle { get; set; }
        }
    }
}