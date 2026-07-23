# SQSProcessor

An `SQSEvent` handler that logs each message body. Demonstrates the shape of an SQS-triggered Lambda and the [SQS event source](../../README.md#sqs-event-source). You can test it two ways.

## Option A: With the built-in sample event (no AWS needed)

**1. Start the Lambda emulator:**

```
dotnet lambda-test-tool start --lambda-emulator-port 5050
```

**2. Start the function** (separate terminal, this directory):

```
dotnet run --launch-profile SQSProcessor
```

**3. In the web UI** (`http://localhost:5050`), select `SQSProcessor`, choose the built-in **`sqs.json`** sample from the Example Requests dropdown, and click **Invoke**. The message body is logged in the function's console.

## Option B: With a real SQS queue (event source polling)

This polls an actual queue using your AWS credentials.

**1. Start the function** (separate terminal, this directory):

```
dotnet run --launch-profile SQSProcessor
```

**2. Start the test tool with the SQS event source** pointed at your queue:

```
dotnet lambda-test-tool start \
    --lambda-emulator-port 5050 \
    --sqs-eventsource-config "QueueUrl=https://sqs.<region>.amazonaws.com/<account-id>/<queue-name>,FunctionName=SQSProcessor,Region=<region>"
```

Send a message to the queue; the tool batches it into an `SQSEvent`, invokes `SQSProcessor`, and (on success) deletes the message. See the [main README](../../README.md#sqs-event-source) for all supported config keys.
