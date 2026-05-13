# Contributing Guidelines

Please follow our main contribution [guide](https://github.com/aws/aws-lambda-dotnet/blob/master/CONTRIBUTING.md) for high level details on how to contribute.

## Lambda Test Tool Specific Contribution Details

###  Running the Test Tool Project via an IDE

Download this repository and create/modify the launch settings `Properties/launchSettings.json` to startup the test tool

```json
{
    "profiles": {
        "Lambda Test Tool": {
          "commandName": "Project",
            "commandLineArgs": "start --lambda-emulator-port 5050 --api-gateway-emulator-port 5051 --api-gateway-emulator-mode Rest",
            "environmentVariables": {
                "APIGATEWAY_EMULATOR_ROUTE_CONFIG": {
                    "LambdaResourceName": "AddLambdaFunction",
                    "HttpMethod": "Get",
                    "Path": "/add/{x}/{y}"
                }
            }
        }
    }
}

```

Follow the [README](README.md#example-lambda-function-setup) for setting up a local lambda function to pair with the emulator.

