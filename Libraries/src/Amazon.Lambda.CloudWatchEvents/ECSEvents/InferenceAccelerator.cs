using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// Details on a Elastic Inference accelerator. 
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_InferenceAccelerator.html
    /// </summary>
    public class InferenceAccelerator
    {
        /// <summary>
        /// The Elastic Inference accelerator device name.
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// The Elastic Inference accelerator type to use.
        /// </summary>
        public string DeviceType { get; set; }
    }
}
