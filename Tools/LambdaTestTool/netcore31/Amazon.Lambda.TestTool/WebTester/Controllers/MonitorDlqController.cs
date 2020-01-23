using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.WebTester.Models;
using Microsoft.AspNetCore.Mvc;

namespace Amazon.Lambda.TestTool.WebTester.Controllers
{
    [Route("webtester-api/[controller]")]
    public class MonitorDlqController : Controller
    {
        static readonly object LOCK_OBJECT = new object();
        static DlqMonitor GlobalDlqMonitor { get; set; }
        
        LocalLambdaOptions LambdaOptions { get; set; }

        public MonitorDlqController(LocalLambdaOptions lambdaOptions)
        {
            this.LambdaOptions = lambdaOptions;
        }
        
        [HttpGet("queues/{awsProfile}/{awsRegion}")]
        public async Task<IList<QueueItem>> ListAvailableQueuesAsync(string awsProfile, string awsRegion)
        {
            var queueUrls = await this.LambdaOptions.LambdaRuntime.AWSService.ListQueuesAsync(awsProfile, awsRegion);

            var items = new List<QueueItem>();

            foreach (var queueUrl in queueUrls)
            {
                items.Add(new QueueItem{QueueUrl = queueUrl});
            }

            return items;
        }

        [HttpPost("start")]
        public void StartDlqMonitor([FromBody] StartDlqMonitorModel model)
        {
            lock (LOCK_OBJECT)
            {
                var function = this.LambdaOptions.LoadLambdaFuntion(model.ConfigFile, model.Function);

                StopDlqMonitor();
            
                GlobalDlqMonitor = new DlqMonitor(this.LambdaOptions.LambdaRuntime, function, model.Profile, model.Region, model.QueueUrl);
                GlobalDlqMonitor.Start();
                
            }
        }
        
        [HttpPost("stop")]
        public void StopDlqMonitor()
        {
            lock (LOCK_OBJECT)
            {
                GlobalDlqMonitor?.Stop();
                GlobalDlqMonitor = null;                
            }
        }

        [HttpPost("purge")]
        public async Task PurgeDlq([FromBody] PurgeDlqModel model)
        {
            await this.LambdaOptions.LambdaRuntime.AWSService.PurgeQueueAsync(model.Profile, model.Region, model.QueueUrl);            
        }

        [HttpGet("is-running")]
        public bool IsRunning()
        {
            lock (LOCK_OBJECT)
            {
                return GlobalDlqMonitor != null;
            }
        }

        [HttpGet("logs")]
        public IList<DlqMonitor.LogRecord> FetchNewMonitorLogs()
        {
            lock (LOCK_OBJECT)
            {
                if (GlobalDlqMonitor == null)
                    return new List<DlqMonitor.LogRecord>();

                return GlobalDlqMonitor.FetchNewLogs();
            }
        }
    }
}