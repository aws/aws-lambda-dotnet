# Amazon Lex getting started Order Flowers sample

This starter project consists of:
* Function.cs - The entry point for the Lambda function that chooses the IIntentProcessor to process and execute the processor
* OrderFlowersIntentProcessor - Intent processor for the OrderFlowers intent

* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

For instructions on how to set up and test this bot, as well as additional samples,
visit the Lex Getting Started documentation https://docs.aws.amazon.com/lex/latest/dg/getting-started.html.


## Here are some steps to follow to deploy:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your function you can use the following command lines to build, test and deploy your function to AWS Lambda from the command line:

Restore dependencies
```
    cd "BlueprintBaseName"
    dotnet restore
```

Execute unit tests
```
    cd "BlueprintBaseName/test/BlueprintBaseName.Tests"
    dotnet test
```

Deploy function to AWS Lambda
```
    cd "BlueprintBaseName/src/BlueprintBaseName"
    dotnet lambda deploy-function
```
