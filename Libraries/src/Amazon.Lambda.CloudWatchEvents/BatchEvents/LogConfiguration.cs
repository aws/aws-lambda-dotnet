using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// Log configuration options to send to a custom log driver for the container.
    /// </summary>
    public class LogConfiguration
    {
        /// <summary>
        /// <para>The log driver to use for the container. The valid values listed for this parameter are log drivers that the Amazon ECS container agent can communicate with by default.</para>
        /// <para>The supported log drivers are <c>awslogs</c>, <c>fluentd</c>, <c>gelf</c>, <c>json-file</c>, <c>journald</c>, <c>logentries</c>, <c>syslog</c>, and <c>splunk</c>.</para>
        /// </summary>
        public string LogDriver { get; set; }

        /// <summary>
        /// The configuration options to send to the log driver. This parameter requires version 1.19 of the Docker Remote API or greater on your container instance. 
        /// To check the Docker Remote API version on your container instance, log into your container instance and run the following command: <c>sudo docker version | grep "Server API version"</c>
        /// </summary>
        public Dictionary<string, string> Options { get; set; }

        /// <summary>
        /// The secrets to pass to the log configuration. For more information, see <see href="https://docs.aws.amazon.com/batch/latest/userguide/specifying-sensitive-data.html">Specifying Sensitive Data</see> in the <i>AWS Batch User Guide</i>.
        /// </summary>
        public List<Secret> SecretOptions { get; set; }
    }
}
