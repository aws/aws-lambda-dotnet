using System;
using Amazon.Lambda.CloudWatchEvents;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
  
    /// <summary>
    /// AWS ECS event
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules.html
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules_example-events.html
    /// https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs_cwe_events.html
    /// </summary>
    public class ECSContainerInstanceStateChangeEvent : CloudWatchEvent<ContainerInstance>
    {

    }
}
