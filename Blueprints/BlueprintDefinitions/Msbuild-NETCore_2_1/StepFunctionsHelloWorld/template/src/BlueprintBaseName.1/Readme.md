# Step Functions Hello World

This starter project consists of:

* serverless.template - An AWS CloudFormation template file for declaring your Serverless functions and other AWS resources
* state-machine.json -The definition of the Step Function state machine.
* StepFunctionTasks.cs - This class contains the Lambda functions that the Step Function state machine will call.
* State.cs - This class represent the state of the step function executions between Lambda function calls.
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated project is a simple hello world Step Functions example. It generates 2 Lambda functions that are called as tasks in a state machine. In the state-machine.json file the Step Function state machine is defined that tells the Step Function service in what order to call the Lambda functions. The Step Function execution's state is maintained in the State object which the Lambda functions read from, populate and return. In this example the first Lambda function also returns back a wait time to show how to configure a wait in the state machine.

### Defining a State Machine

The state machine is defined in the state-machine.json file. When the project is deployed the contents of state-machine.json are copied into the serverless.template. The insertion location is controlled by the --template-substitutions parameter. The project template presets the --template-substitutions parameter in aws-lambda-tools-defaults.json. The format of the value for --template-substitutions is <json-path>=<file-name>.

For example this project template sets the value to be:

--template-substitutions $.Resources.StateMachine.Properties.DefinitionString.Fn::Sub=state-machine.json

### Test State Machine

Once the project is deployed you can test it with the Step Functions in the web console https://console.aws.amazon.com/states/home. Select the newly created state machine and then click the "New Execution" button. Enter the initial JSON document for the input to the execution which will be serialized in to the State object. This project will look for a "Name" property to use in its execution. Here is an example input JSON.

{
    "Name" : "MyStepFunctions"
}

## Here are some steps to follow from Visual Studio:

To deploy your Serverless application, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed application open the Stack View window by double-clicking the stack name shown beneath the AWS CloudFormation node in the AWS Explorer tree. The Stack View also displays the root URL to your published application.

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

Deploy application
```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-serverless
```
