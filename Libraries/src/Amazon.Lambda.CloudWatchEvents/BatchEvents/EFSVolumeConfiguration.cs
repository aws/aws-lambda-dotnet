using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// This is used when you're using an Amazon Elastic File System file system for job storage. For more information, see <see href="https://docs.aws.amazon.com/batch/latest/userguide/efs-volumes.html">Amazon EFS Volumes</see> in the <i>AWS Batch User Guide</i>.
    /// </summary>
    public class EFSVolumeConfiguration
    {
        /// <summary>
        /// The authorization configuration details for the Amazon EFS file system.
        /// </summary>
        public EFSAuthorizationConfig AuthorizationConfig { get; set; }

        /// <summary>
        /// The Amazon EFS file system ID to use.
        /// </summary>
        public string FileSystemId { get; set; }

        /// <summary>
        /// The directory within the Amazon EFS file system to mount as the root directory inside the host. If this parameter is omitted, the root of the Amazon EFS volume is used instead. 
        /// Specifying <c>/</c> has the same effect as omitting this parameter. The maximum length is 4,096 characters.
        /// </summary>
        public string RootDirectory { get; set; }

        /// <summary>
        /// Determines whether to enable encryption for Amazon EFS data in transit between the Amazon ECS host and the Amazon EFS server. 
        /// Transit encryption must be enabled if Amazon EFS IAM authorization is used. If this parameter is omitted, the default value of <c>DISABLED</c> is used.
        /// </summary>
        public string TransitEncryption { get; set; }

        /// <summary>
        /// The port to use when sending encrypted data between the Amazon ECS host and the Amazon EFS server. 
        /// If you don't specify a transit encryption port, it uses the port selection strategy that the Amazon EFS mount helper uses. 
        /// The value must be between 0 and 65,535.
        /// </summary>
        public int TransitEncryptionPort { get; set; }
    }
}
