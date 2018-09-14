# Amazon Lex Getting Started Book Trip Bot Code Hooks

This starter project consists of:
* Function.cs - The entry point for the Lambda function that chooses the IIntentProcessor to process and execute the processor
* BookCarIntentProcessor - Intent processor for the BookCar intent
* BookHotelIntentProcessor - Intent processor for the BookHotel intent 
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

This sample demonstrates an implementation of the Lex Code Hook Interface
in order to serve a sample bot which manages reservations for hotel rooms and car rentals.
Bot, Intent, and Slot models which are compatible with this sample can be found in the Lex Console
as part of the 'BookTrip' template.

For instructions on how to set up and test this bot, as well as additional samples,
visit the Lex Getting Started documentation https://docs.aws.amazon.com/lex/latest/dg/getting-started.html.


## Here are some steps to follow to deploy:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can deploy your application using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.

Install Amazon.Lambda.Tools Global Tools if not already installed.
```
    dotnet tool install -g Amazon.Lambda.Tools
```

If already installed check if new version is available.
```
    dotnet tool update -g Amazon.Lambda.Tools
```

Execute unit tests
```
    cd "BlueprintBaseName.1/test/BlueprintBaseName.1.Tests"
    dotnet test
```

Deploy function to AWS Lambda
```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-function
```
