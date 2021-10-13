namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing the secret to expose to your container. Secrets can be exposed to a container in the following ways:
    /// <list type="bullet"><item><description>To inject sensitive data into your containers as environment variables, use the secrets container definition parameter.</description></item>
    /// <item><description>To reference sensitive information in the log configuration of a container, use the secretOptions container definition parameter.</description></item></list>
    /// For more information, see <see href="https://docs.aws.amazon.com/batch/latest/userguide/specifying-sensitive-data.html">Specifying sensitive data</see> in the <i>AWS Batch User Guide</i>.
    /// </summary>
    public class Secret
    {
        /// <summary>
        /// The name of the secret.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The secret to expose to the container. The supported values are either the full ARN of the AWS Secrets Manager secret or the full ARN of the parameter in the AWS Systems Manager Parameter Store.
        /// </summary>
        public string ValueFrom { get; set; }
    }
}
