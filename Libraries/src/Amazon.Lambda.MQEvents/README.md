# Amazon.Lambda.MQEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon ActiveMQ and RabbitMQ events.

# Serialization

If you are using this package with Amazon Lambda but are not also using `Amazon.Lambda.Serialization.Json`, be aware that one property requires custom serialization.

This property is `Data` on the types `Amazon.Lambda.MQEvents.ActiveMQEvent+ActiveMQMessage` and `Amazon.Lambda.MQEvents.RabbitMQEvent+RabbitMQMessage` respectively. This is a `MemoryStream` object that should be populated by converting the JSON string from base64 to an array of bytes, then constructing a `MemoryStream` object from these bytes. Here is a code sample showing this deserialization logic.
```csharp
string dataBase64 = GetJsonString();
byte[] dataBytes = Convert.FromBase64String(dataBase64);
MemoryStream stream = new MemoryStream(dataBytes);
```

A Newtonsoft.Json `IContractResolver` implementation which handles this custom serialization is located in [Amazon.Lambda.Serialization.Json\AwsResolver.cs](../Amazon.Lambda.Serialization.Json/AwsResolver.cs), consult this source for more information.

# Sample Function

The following are the sample classes along with the Lambda functions that receive ActiveMQ and RabbitMQ event record data respectively as an input, and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

**ActiveMQ event**
```csharp
public class Function
{
    public object Handler(ActiveMQEvent activemqEvent)
    {
        Console.WriteLine($"Total messages received from event source: {activemqEvent.Messages.Count}");
        foreach (var message in activemqEvent.Messages)
        {
            Console.WriteLine(message.Data);
        }

        return new
        {
            StatusCode = 200,
            Body = "Hello from Lambda!"
        };
    }
}
```
**RabbitMQ event**
```csharp
public class Function
{
    public object Handler(RabbitMQEvent rabbitmqEvent)
    {
        Console.WriteLine("Target Lambda function invoked");

        if (rabbitmqEvent.RmqMessagesByQueue == null || rabbitmqEvent.RmqMessagesByQueue.Count == 0)
        {
            Console.WriteLine("Invalid event data");
            return new
            {
                StatusCode = 404
            };
        }

        Console.WriteLine("Data received from event source: ");
        foreach (var queue in rabbitmqEvent.RmqMessagesByQueue)
        {
            Console.WriteLine($"Total messages received from event source queue {queue.Key}: {queue.Value.Count}");
            foreach (var message in queue.Value)
            {
                Console.WriteLine(message.Data);
            }
        }

        return new
        {
            StatusCode = 200,
            Body = "Hello from Lambda!"
        };
    }
}
```
