# Contributing Guidelines

Thank you for your interest in contributing to our project. Whether it's a bug report, new feature, correction, or additional 
documentation, we greatly value feedback and contributions from our community.

Please read through this document before submitting any issues or pull requests to ensure we have all the necessary 
information to effectively respond to your bug report or contribution.


## Reporting Bugs/Feature Requests

We welcome you to use the GitHub issue tracker to report bugs or suggest features.

When filing an issue, please check [existing open](https://github.com/aws/aws-lambda-dotnet/issues), or [recently closed](https://github.com/aws/aws-lambda-dotnet/issues?utf8=%E2%9C%93&q=is%3Aissue%20is%3Aclosed%20), issues to make sure somebody else hasn't already 
reported the issue. Please try to include as much information as you can. Details like these are incredibly useful:

* A reproducible test case or series of steps
* The version of our code being used
* Any modifications you've made relevant to the bug
* Anything unusual about your environment or deployment


## Contributing via Pull Requests
Contributions via pull requests are much appreciated. Before sending us a pull request, please ensure that:

1. You are working against the latest source on the *master* branch.
2. You check existing open, and recently merged, pull requests to make sure someone else hasn't addressed the problem already.
3. You open an issue to discuss any significant work - we would hate for your time to be wasted.

To send us a pull request, please:

1. Fork the repository.
2. Modify the source; please focus on the specific change you are contributing. If you also reformat all the code, it will be hard for us to focus on your change.
3. Ensure local tests pass.
4. Commit to your fork using clear commit messages.
5. Send us a pull request, answering any default questions in the pull request interface.
6. Pay attention to any automated CI failures reported in the pull request, and stay involved in the conversation.

GitHub provides additional document on [forking a repository](https://help.github.com/articles/fork-a-repo/) and 
[creating a pull request](https://help.github.com/articles/creating-a-pull-request/).

## Adding a `change file` to your contribution branch

Each contribution branch should include a `change file` that contains a changelog message for each project that has been updated, as well as the type of increment to perform for those changes when versioning the project.

A `change file` looks like the following example:
```json
{
  "Projects": [
    {
      "Name": "Amazon.Lambda.Annotations",
      "Type": "Patch",
      "ChangelogMessages": [
        "Fixed an issue causing a failure somewhere"
      ]
    }
  ]
}
```
The `change file` lists all the modified projects, the changelog message for each project as well as the increment type. 

These files are located in the repo at .autover/changes/

You can use the `AutoVer` tool to create the change file. You can install it using the following command:
```
dotnet tool install -g AutoVer
```

You can create the `change file` using the following command:
```
autover change --project-name "Amazon.Lambda.Annotations" -m "Fixed an issue causing a failure somewhere
```
Note: Make sure to run the command from the root of the repository.

You can update the command to specify which project you are updating.
The available projects are:
* Amazon.Lambda.Annotations
* Amazon.Lambda.APIGatewayEvents
* Amazon.Lambda.ApplicationLoadBalancerEvents
* Amazon.Lambda.AspNetCoreServer
* Amazon.Lambda.AspNetCoreServer.Hosting
* Amazon.Lambda.CloudWatchEvents
* Amazon.Lambda.CloudWatchLogsEvents
* Amazon.Lambda.CognitoEvents
* Amazon.Lambda.ConfigEvents
* Amazon.Lambda.ConnectEvents
* Amazon.Lambda.Core
* Amazon.Lambda.DynamoDBEvents
* Amazon.Lambda.KafkaEvents
* Amazon.Lambda.KinesisAnalyticsEvents
* Amazon.Lambda.KinesisEvents
* Amazon.Lambda.KinesisFirehoseEvents
* Amazon.Lambda.LexEvents
* Amazon.Lambda.LexV2Events
* Amazon.Lambda.Logging.AspNetCore
* Amazon.Lambda.MQEvents
* Amazon.Lambda.PowerShellHost
* Amazon.Lambda.RuntimeSupport
* Amazon.Lambda.S3Events
* Amazon.Lambda.Serialization.Json
* Amazon.Lambda.Serialization.SystemTextJson
* Amazon.Lambda.SimpleEmailEvents
* Amazon.Lambda.SNSEvents
* Amazon.Lambda.SQSEvents
* Amazon.Lambda.TestUtilities
* Amazon.Lambda.TestTool.BlazorTester

The possible increment types are:
* Patch
* Minor
* Major

Note: You do not need to create a new `change file` for every changelog message or project within your branch. You can create one `change file` that contains all the modified projects and the changelog messages.

## Finding contributions to work on
Looking at the existing issues is a great way to find something to contribute on. As our projects, by default, use the default GitHub issue labels ((enhancement/bug/duplicate/help wanted/invalid/question/wontfix), looking at any ['help wanted'](https://github.com/aws/aws-lambda-dotnet/labels/help%20wanted) issues is a great place to start. 


## Code of Conduct
This project has adopted the [Amazon Open Source Code of Conduct](https://aws.github.io/code-of-conduct). 
For more information see the [Code of Conduct FAQ](https://aws.github.io/code-of-conduct-faq) or contact 
opensource-codeofconduct@amazon.com with any additional questions or comments.


## Security issue notifications
If you discover a potential security issue in this project we ask that you notify AWS/Amazon Security via our [vulnerability reporting page](http://aws.amazon.com/security/vulnerability-reporting/). Please do **not** create a public github issue.


## Licensing

See the [LICENSE](https://github.com/aws/aws-lambda-dotnet/blob/master/LICENSE) file for our project's licensing. We will ask you to confirm the licensing of your contribution.

We may ask you to sign a [Contributor License Agreement (CLA)](http://en.wikipedia.org/wiki/Contributor_License_Agreement) for larger changes.
